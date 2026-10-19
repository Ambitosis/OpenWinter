"""Summarize independent single-speaker synthetic grunt trials.
Usage: python tools/visual/summarize_voice_trials.py out/trial1.json out/trial2.json
The Wilson interval assumes independent attack attempts; it does not identify RNG
internals. Overlapping speakers are rejected instead of pooled into this model.
"""
import json
import math
from pathlib import Path
import subprocess
import sys
from scipy.stats import binomtest


def wilson(successes, count):
    z = 1.959963984540054
    p = successes / count
    center = (p + z*z/(2*count)) / (1 + z*z/count)
    half = z*math.sqrt(p*(1-p)/count + z*z/(4*count*count)) / (1 + z*z/count)
    return [center-half, center+half]


def summarize(paths):
    trials = []
    for path in paths:
        document = json.loads(path.read_text(encoding='utf-8'))
        speakers, swings, seen = set(), [], []
        ammo_initial, ammo_remaining = [], []
        started = document['recording']['started']
        for when, line in document['notes']:
            if 'VISUAL|voice_actor|' in line:
                speakers.add(int(line.split('VISUAL|voice_actor|')[1].split('|')[0]))
            if 'VISUAL|voice_swing|' in line: swings.append(when-started)
            if 'VISUAL|voice_seen|' in line: seen.append(line.split('|')[-1].strip())
            if 'VISUAL|voice_equipped|' in line: ammo_initial.append(int(line.split('|')[-1]))
            if 'VISUAL|voice_ammo_remaining|' in line: ammo_remaining.append(int(line.split('|')[-1]))
        if len(speakers) != 1 or seen != ['1'] or not swings:
            raise ValueError(f'{path}: requires one perceived, attacking speaker')
        if not any('VISUAL|done|' in line for _, line in document['notes']):
            raise ValueError(f'{path}: missing scene completion')
        if not document['recording'].get('session', {}).get('restored'):
            raise ValueError(f'{path}: own-session restoration unproven')
        analyzer = Path(__file__).with_name('check_voice_actors.py')
        rows = json.loads(subprocess.check_output([sys.executable,str(analyzer),str(path)],encoding='utf-8'))
        onsets = sorted(t for row in rows if row['actor'] in speakers and row['family'].startswith('grunt') for t in row['onsets'])
        attempts = len(swings)
        denominator = 'target attack events'
        if ammo_initial or ammo_remaining:
            if len(ammo_initial)!=1 or len(ammo_remaining)!=1 or not 0<=ammo_remaining[0]<ammo_initial[0]:
                raise ValueError(f'{path}: requires one valid initial/final ammunition count')
            attempts = ammo_initial[0]-ammo_remaining[0]
            denominator = 'ammunition consumed'
        trial = dict(source=str(path), attempts=attempts, denominator=denominator, targetAttackEvents=len(swings), grunts=len(onsets),
            rate=len(onsets)/attempts, wilson95=wilson(len(onsets),attempts),
            minimumInterval=min((b-a for a,b in zip(onsets,onsets[1:])),default=None),
            perSlot={r['family']:len(r['onsets']) for r in rows if r['actor'] in speakers and r['family'].startswith('grunt')})
        if len(onsets)>attempts: raise ValueError(f'{path}: more cues than attacks')
        trials.append(trial)
    n=sum(t['attempts'] for t in trials); k=sum(t['grunts'] for t in trials)
    return dict(trials=trials, combined=dict(attempts=n,grunts=k,rate=k/n,wilson95=wilson(k,n),
        candidateTwoSidedPValues={str(p):binomtest(k,n,p).pvalue for p in [1/3,.4,.5,2/3]}),
        limitations='Wilson/binomial tests assume independent attempts. Native log delivery is buffered, so receipt times do not establish voice-vs-swing latency. Synthetic short clips remove long-sample overlap; results do not prove exact RNG, long-clip arbitration, all attack kinds or visual identity.')


if __name__ == '__main__':
    print(json.dumps(summarize([Path(p) for p in sys.argv[1:]]),indent=2))
