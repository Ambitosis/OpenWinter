"""Report frequency-coded projectile sample restarts without inferring visual lifetime."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import wave

import numpy as np
from scipy.signal import stft

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('metadata', type=Path)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
data = json.loads(args.metadata.read_text(encoding='utf-8'))
probe, record = data['probe'], data['recording']
if probe['scene'] not in ('fireballpattern','fireballbare') or probe['original']:
    raise ValueError('Requires synthetic fireballpattern capture')
if 'nativeLog' in data and not record.get('session', {}).get('restored'):
    raise ValueError('Owned native audio session was not restored')
log = Path(data.get('nativeLog', args.metadata.with_name('godot.log')))
text = log.read_text(encoding='utf-8', errors='replace')
if re.search(r'TOO MANY INSTRUCTIONS|SCRIPT ERROR|\[script error\]', text, re.I):
    raise ValueError('Script error invalidates capture')
for marker in ['VISUAL|done|1', *probe['requiredMarkers']]:
    if marker not in text:
        raise ValueError('Missing fixture marker: ' + marker)
wav = Path(record['path'])
with wave.open(str(wav), 'rb') as w:
    rate, channels = w.getframerate(), w.getnchannels()
    if w.getsampwidth() != 2:
        raise ValueError('Expected 16-bit PCM')
    pcm = np.frombuffer(w.readframes(w.getnframes()), dtype='<i2').reshape(-1, channels).T
freq, times, spectrum = stft(pcm, rate, nperseg=round(rate*.02), noverlap=round(rate*.015), axis=-1)
pattern = probe['patterns']['spr_fireball']
reports = []
for fraction in (.025, .05, .10):
    bands = []
    for hz in pattern['frequencies']:
        power = np.abs(spectrum[:, abs(freq-hz) <= 50, :]).max(axis=1).mean(axis=0)
        threshold = max(5, float(power.max())*fraction)
        indices = np.flatnonzero(power > threshold)
        groups = np.split(indices, np.where(np.diff(indices) > 2)[0]+1)
        segments = [dict(start=float(times[g[0]]), end=float(times[g[-1]])) for g in groups if len(g) >= 5]
        bands.append(dict(hz=hz, threshold=threshold, segments=segments))
    phases = []
    for name, marker, span in [('near', 'cast.0.valid.', 7), ('far', 'cast.1.valid.', 8), ('direct', 'play.valid.', 5)]:
        marks = [stamp-record['started'] for stamp, line in data['notes'] if 'VISUAL|note|'+marker in line]
        if len(marks) != 1:
            raise ValueError('Expected exactly one phase marker: '+name)
        start, end = marks[0]-.25, marks[0]+span
        selected = [dict(hz=b['hz'], segments=[s for s in b['segments'] if start <= s['start'] < end]) for b in bands]
        origins = selected[0]['segments']
        if not origins:
            raise ValueError('No positive sample-origin control: '+name)
        final_origin = origins[-1]['start']
        sequence = []
        for i, b in enumerate(selected):
            expected = final_origin+i*pattern['stepSeconds']
            candidates = [s for s in b['segments'] if abs(s['start']-expected) < .06]
            sequence.append(dict(hz=b['hz'], expectedStart=expected, segments=candidates))
        full_tail = all(len(b['segments']) == 1 and abs(b['segments'][0]['end']-b['segments'][0]['start']-pattern['stepSeconds']) < .06 for b in sequence)
        if name == 'direct' and (len(origins) != 1 or not full_tail):
            raise ValueError('Direct playback positive control is incomplete or ambiguous')
        phases.append(dict(phase=name, originSegments=origins, restartIntervals=[s['start']-origins[0]['start'] for s in origins[1:]], completeTailAfterFinalOrigin=full_tail, sequence=sequence, allBands=selected))
    reports.append(dict(relativeThreshold=fraction, absoluteFloor=5, phases=phases))
result = dict(metadata=str(args.metadata), probe=probe, rate=rate, windowSeconds=.02, hopSeconds=.005, bandHalfWidthHz=50, reports=reports,
    sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (args.metadata, log, wav)},
    limitations='Synthetic sound phase/routing only. Script receipt is buffered; restart intervals use PCM origins. No visual, gain, spatial, universal projectile or exact server-flight formula claim.')
with args.output.open('w', encoding='utf-8', newline='') as f:
    json.dump(result, f, indent=2)
print(json.dumps([dict(threshold=r['relativeThreshold'], phases=[{k:v for k,v in p.items() if k not in ('sequence','allBands')} for p in r['phases']]) for r in reports], indent=2))
