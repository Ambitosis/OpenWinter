"""Retain load transitions and slot32 identity separately; never infer a cooldown."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def summarize(path, tones=None):
    doc = json.loads(path.read_text(encoding='utf-8'))
    bag = doc.get('scene') in ('voiceactors_encumberedbag_slots', 'voiceactors_encumbereddrop_slots')
    prefix = 'encumberedbag' if bag else 'encumbered'
    if doc.get('scene') not in ('voiceactors_encumbered_slots', 'voiceactors_encumberedbag_slots', 'voiceactors_encumbereddrop_slots') or not doc.get('validated'):
        raise ValueError('Completed encumbrance capture required')
    if doc['engine'] == 'native' and not doc['recording'].get('session', {}).get('restored'):
        raise ValueError('Native owned audio session must be restored')
    origin = doc['recording']['started']
    notes = [(t-origin, line.split('VISUAL|note|', 1)[1].strip())
             for t, line in doc['notes'] if 'VISUAL|note|' in line]
    if any('INVALID_' in n for _, n in notes):
        raise ValueError('Native context/weight/acquisition gate failed')
    ready = [n for _, n in notes if n.startswith(prefix+'.ready.')]
    if len(ready) != 1 or not ready[0].startswith(prefix+'.ready.1.pc.1.name.Aluvian Darkstar.soundset.5100.'):
        raise ValueError('Actual player, soundset and installed threshold gates required')
    if not any(n == prefix+'.completed' for _, n in notes):
        raise ValueError('Completion missing')
    states = []
    for t, n in notes:
        pattern = re.escape(prefix)+r'\.state\.(\w+)\.weight\.(\d+)\.normal\.(\d+)\.heavy\.(\d+)\.derived\.(\d+)\.expected\.(-?\d+)'
        match = re.fullmatch(pattern if bag else pattern+r'\.str\.(\d+)', n)
        if match:
            phase, weight, normal, heavy, derived, expected = match.groups()[:6]
            row = dict(phase=phase, time=t, weight=int(weight), normal=int(normal), heavy=int(heavy),
                       derived=int(derived), expected=int(expected))
            if not bag:
                row['strength'] = int(match.groups()[6])
            measured = row['derived'] if bag else row['weight']
            if row['expected'] >= 0 and row['expected'] != measured:
                raise ValueError('Exact weight gate failed')
            derived = 2 if row['weight'] > row['heavy'] else 1 if row['weight'] > row['normal'] else 0
            if derived != row['derived']:
                raise ValueError('Weight/threshold derivation inconsistent')
            states.append(row)
    phases = (['directoverload', 'directreset', 'encumbered', 'carriedoverload', 'carriedencumbered', 'reset'] if bag else
              ['normal', 'crossnormal', 'remain', 'reset1', 'crossheavy', 'reset2', 'prepickup', 'pickup', 'reset3', 'recross'])
    if [s['phase'] for s in states] != phases:
        raise ValueError('All indexed threshold/reset states required')
    if bag:
        pickup = [n for _, n in notes if n.startswith('encumberedbag.owned.')]
        if pickup != ['encumberedbag.owned.1']*2:
            raise ValueError('Both bag pickup ownership gates required')
        if sum(n == 'encumberedbag.shield.valid.1.owned.1.weight.450' for _, n in notes) != 1:
            raise ValueError('Direct shield ownership/weight positive required')
    else:
        pickup = [n for _, n in notes if n.startswith('encumbered.pickup.complete.')]
        if len(pickup) != 1 or '.valid.1.owned.1.' not in pickup[0] or not pickup[0].endswith('.acquire.1'):
            raise ValueError('Ground pickup ownership/acquisition positive required')
    if doc['scene'] == 'voiceactors_encumbereddrop_slots' and not any(n == 'encumberedbag.shield.ground.1' for _, n in notes):
        raise ValueError('Completed ground drop gate required')
    if tones is None:
        tones = json.loads(subprocess.check_output([sys.executable,
            str(Path(__file__).with_name('check_voice_actors.py')), str(path)], encoding='utf-8'))
    if len(tones) != 1 or tones[0]['family'] != 'encumbered32':
        raise ValueError('Expected exactly the independently coded slot32 detector')
    cues = [dict(time=t, duration=d) for t, d in zip(tones[0]['onsets'], tones[0]['durations'])]
    positive_times = [t for t, n in notes if n == 'encumbered.positive.slot.32']
    positives = [dict(time=t, identified=sum(abs(c['time']-t) < .5 for c in cues) == 1) for t in positive_times]
    if len(positives) != 2 or not all(p['identified'] for p in positives):
        raise ValueError('Before/after slot32 positive controls must both identify')
    automatic = [c for c in cues if not any(abs(c['time']-t) < .5 for t in positive_times)]
    markers = (('encumberedbag.pickup.', 'encumberedbag.remove.', 'encumberedbag.drop.', 'encumberedbag.add.') if bag else
               ('encumbered.load.', 'encumbered.unload.', 'encumbered.pickup.item.'))
    transitions = [(t, n) for t, n in notes if n.startswith(markers)]
    intervals = []
    for i, (start, marker) in enumerate(transitions):
        end = transitions[i+1][0] if i+1 < len(transitions) else positive_times[-1]
        intervals.append(dict(marker=marker, start=start, end=end,
                              cues=[c for c in automatic if start <= c['time'] < end]))
    return dict(source=str(path), sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
                fixtureSha256=doc['fixtureSha256'], readiness=ready[0], states=states,
                pickup=pickup, positiveControls=positives, automaticCues=automatic,
                intervals=intervals,
                limitations='Synthetic slot identity and explicit carried-weight/threshold/acquisition contexts. Derived encumbrance is computed from public weight/table readings, not an engine state getter. No exact cooldown/chance, caption, movement-attempt or installed-waveform claim.')


if __name__ == '__main__':
    print(json.dumps(summarize(Path(sys.argv[1])), indent=2))
