"""Validate captured audio artifacts independently of screenshot completeness."""
import array
import hashlib
import json
import math
from pathlib import Path
import sys
import wave


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as source:
        while data := source.read(1024 * 1024):
            digest.update(data)
    return digest.hexdigest()


def inspect_audio(waveform, metadata):
    waveform, metadata = Path(waveform), Path(metadata)
    result = dict(valid=False, waveform=str(waveform), metadata=str(metadata))
    try:
        document = json.loads(metadata.read_text(encoding='utf-8'))
        recording = document['recording']
        if Path(recording['path']).resolve() != waveform.resolve():
            raise ValueError('Metadata names a different waveform')
        started = float(recording['started'])
        if not math.isfinite(started):
            raise ValueError('Nonfinite recording start')
        notes = document['notes']
        if not isinstance(notes, list) or any(not isinstance(n, list) or len(n) != 2
                or not isinstance(n[1], str) or not isinstance(n[0], (int, float))
                or not math.isfinite(n[0]) for n in notes):
            raise ValueError('Invalid timed marker schema')
        peak = 0
        with wave.open(str(waveform), 'rb') as source:
            rate, channels, frames = source.getframerate(), source.getnchannels(), source.getnframes()
            if source.getsampwidth() != 2 or source.getcomptype() != 'NONE':
                raise ValueError('Expected uncompressed 16-bit PCM')
            if rate <= 0 or channels not in (1, 2) or frames <= 0:
                raise ValueError('Empty or invalid recording')
            actual_frames = 0
            while data := source.readframes(65536):
                samples = array.array('h', data)
                if sys.byteorder != 'little': samples.byteswap()
                actual_frames += len(samples) // channels
                peak = max(peak, max(abs(sample) for sample in samples))
            if actual_frames != frames:
                raise ValueError('Truncated waveform')
        for key, actual in [('rate', rate), ('channels', channels), ('frames', frames), ('pcm_peak', peak)]:
            if recording.get(key) != actual:
                raise ValueError(f'Metadata {key} differs from waveform')
        result.update(valid=True, waveformSha256=sha256(waveform), metadataSha256=sha256(metadata),
                      started=started, rate=rate, channels=channels, frames=frames, pcmPeak=peak)
        return result, notes
    except (OSError, ValueError, KeyError, TypeError, wave.Error, EOFError) as error:
        result['error'] = str(error)
        return result, []


def trusted_audio(run):
    """Revalidate both hashes before using previously captured marker timestamps."""
    audio = run.get('audio', {})
    if not audio.get('valid') or run.get('exitCode') != 0:
        return None
    try:
        if (sha256(audio['waveform']) != audio['waveformSha256']
                or sha256(audio['metadata']) != audio['metadataSha256']):
            return None
        document = json.loads(Path(audio['metadata']).read_text(encoding='utf-8'))
        return audio, document['notes']
    except (OSError, ValueError, KeyError, TypeError):
        return None


def interval_evidence(trusted, spell, span):
    if trusted is None: return None
    audio, notes = trusted
    starts = [when for when, line in notes if 'VISUAL|note|' in line
              and line.split('VISUAL|note|', 1)[1].strip() == f'start.{spell}']
    if len(starts) != 1: return None
    start = starts[0] - audio['started']
    duration = audio['frames'] / audio['rate']
    if start < 0 or start >= duration: return None
    return dict(audio=audio['waveform'], metadata=audio['metadata'],
                waveformSha256=audio['waveformSha256'], metadataSha256=audio['metadataSha256'],
                startSeconds=start, endSeconds=min(start + span, duration),
                status='captured-unreviewed',
                note='Timed waveform interval only; silence and cue identity require waveform/manual review. Screenshot validity is independent.')
