"""Validate spell images separately from native raw-frame provenance and audio."""
import hashlib
import json
import math
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect_frames(folder, expected, native_framebuffer=False):
    from PIL import Image
    folder = Path(folder)
    expected = set(expected)
    paths = sorted(folder.glob('*.png'))
    if native_framebuffer:
        comparisons = {Path(name).stem + '_window.png' for name in expected}
        paths = [p for p in paths if p.name not in comparisons]
    shots, blank, errors = {}, [], []
    for path in paths:
        try:
            shots[path.name] = digest(path)
            with Image.open(path) as frame:
                if frame.convert('RGB').getbbox() is None:
                    blank.append(path.name)
        except (OSError, ValueError) as exc:
            errors.append(f'{path.name}: {exc}')
    native = None
    if native_framebuffer:
        native = inspect_native_frames(folder, expected)
    complete = (set(shots) == expected and not blank and not errors
                and (native is None or native['valid']))
    return dict(shots=shots, blankShots=blank, imageErrors=errors,
                nativeFramebuffer=native, complete=complete)


def inspect_native_frames(folder, expected):
    from PIL import Image, ImageChops
    folder = Path(folder)
    metadata = folder / 'native-framebuffer.jsonl'
    frames, errors = {}, []
    result = dict(valid=False, metadataFile=metadata.name, metadataSha256=None,
                  frames=frames, errors=errors)
    try:
        raw_metadata = metadata.read_bytes()
        result['metadataSha256'] = hashlib.sha256(raw_metadata).hexdigest()
        records = [json.loads(line) for line in raw_metadata.decode('utf-8').splitlines() if line.strip()]
        names = [record['shot'] + '.png' for record in records]
        if len(set(names)) != len(names) or set(names) != set(expected):
            raise ValueError('Native records must name every expected phase exactly once.')
        for record, name in zip(records, names):
            if Path(name).name != name or '/' in name or '\\' in name:
                raise ValueError('Invalid native phase name.')
            raw = folder / Path(name).with_suffix('.tga')
            comparison = folder / (Path(name).stem + '_window.png')
            if digest(raw) != record['rawSha256'] or digest(comparison) != record['windowSha256']:
                raise ValueError(f'{name}: native raw/window hash mismatch.')
            for field in ('latencySeconds', 'requestedUnixSeconds'):
                value = record[field]
                if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
                    raise ValueError(f'{name}: invalid {field}.')
            with Image.open(raw) as source, Image.open(folder / name) as decoded:
                if source.format != 'TGA' or list(source.size) != record['size'] or source.size != decoded.size:
                    raise ValueError(f'{name}: native dimensions or format mismatch.')
                source_rgb = source.convert('RGB')
                if ImageChops.difference(source_rgb, decoded.convert('RGB')).getbbox() is not None:
                    raise ValueError(f'{name}: PNG does not decode from retained native bytes.')
                if (source_rgb.getbbox() is not None) != record['nonblack']:
                    raise ValueError(f'{name}: native nonblack flag mismatch.')
            frames[name] = dict(rawFile=raw.name, rawSha256=record['rawSha256'],
                                source=record['source'], requestedUnixSeconds=record['requestedUnixSeconds'],
                                latencySeconds=record['latencySeconds'])
        result['valid'] = True
    except (OSError, ValueError, KeyError, TypeError, UnicodeError) as exc:
        errors.append(str(exc))
    return result


def trusted_native_frame(run, name):
    """Old Windows/client captures retain their existing PNG-hash validation."""
    if run.get('imageSource') != 'native-framebuffer':
        return True
    native = run.get('nativeFramebuffer') or {}
    if not native.get('valid') or name not in native.get('frames', {}):
        return False
    folder = Path(run['folder'])
    frame = native['frames'][name]
    try:
        # Names are derived from the phase, never from a path stored in JSON.
        if Path(name).name != name or '/' in name or '\\' in name:
            return False
        return (digest(folder / 'native-framebuffer.jsonl') == native['metadataSha256']
                and digest(folder / Path(name).with_suffix('.tga')) == frame['rawSha256'])
    except (OSError, KeyError, TypeError):
        return False
