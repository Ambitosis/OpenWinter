"""Measure four separated authored particle rows; counts are observations, not parity acceptance."""
import argparse
import json
from pathlib import Path
from PIL import Image
import numpy as np

NAMES = ['baseline', 'moving050', 'moving150', 'moving250', 'stopped025', 'stopped075', 'draining', 'clear']

def rows(path):
    image = Image.open(path).convert('RGB')
    if image.size != (1280, 720):
        raise ValueError('Unexpected image dimensions')
    a = np.asarray(image)[120:490, 300:1050].astype(int)
    mask = (a.min(axis=2) > 210) & (a.max(axis=2) - a.min(axis=2) < 20)
    ys = np.flatnonzero(mask.any(axis=1))
    groups = []
    for y in ys:
        if not groups or y - groups[-1][-1] > 5:
            groups.append([])
        groups[-1].append(int(y))
    result = []
    for group in groups:
        xs = np.flatnonzero(mask[group].any(axis=0))
        runs = []
        for x in xs:
            if not runs or x - runs[-1][-1] > 1:
                runs.append([])
            runs[-1].append(int(x))
        result.append(dict(y=[group[0]+120, group[-1]+120], x=[int(xs[0])+300,int(xs[-1])+300],
                           horizontalComponents=len(runs), whitePixels=int(mask[group].sum())))
    return result

def inspect(folder):
    folder = Path(folder)
    logs = [p for p in (folder/'native.log',folder/'godot.log') if p.exists()]
    if len(logs) != 1:
        raise ValueError('Expected exactly one engine log')
    log = logs[0].read_text(encoding='utf-8', errors='replace')
    if 'VISUAL|done|' not in log or 'SHADER ERROR' in log or 'Shader compilation failed' in log:
        raise ValueError('Incomplete scene or failed shader')
    samples = {}
    for name in NAMES:
        if 'VISUAL|shot|'+name+'|' not in log:
            raise ValueError('Missing shot '+name)
        image = folder/(name+'.png')
        if logs[0].name == 'native.log':
            raw = folder/(name+'.tga')
            if Image.open(raw).convert('RGB').tobytes() != Image.open(image).convert('RGB').tobytes():
                raise ValueError('Raw native framebuffer mismatch')
        samples[name] = rows(image)
    failures = [name for name in ('moving050','moving150','moving250') if len(samples[name]) != 4]
    failures += [name for name in ('baseline','clear') if samples[name]]
    return dict(contextPassed=not failures, failures=failures, samples=samples,
                scope='Four independent visible rows and cleanup only; components may merge. No rate formula or exact timing inferred.')

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('folder')
    result = inspect(p.parse_args().folder)
    print(json.dumps(result,indent=2))
    raise SystemExit(0 if result['contextPassed'] else 1)
