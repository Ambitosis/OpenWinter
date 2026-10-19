"""Analyze the sequential lethal-melee pain/death sound-slot discriminator.
Uses the unchanged synthetic tone detector. No waveform implies no claim about
an unobservable engine event shorter than the detector's 50ms minimum.
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def summarize(path):
    doc=json.loads(path.read_text(encoding='utf-8'))
    if not doc.get('validated') or not doc['recording'].get('session',{}).get('restored'):
        raise ValueError('Native completion/session restoration not validated')
    origin=doc['recording']['started']
    notes=[(t-origin,line.split('VISUAL|note|',1)[1].strip()) for t,line in doc['notes'] if 'VISUAL|note|' in line]
    start=next(t for t,n in notes if n=='combat.begin')
    end=next(t for t,n in notes if n=='fatal.completed.20')
    contexts=[n for _,n in notes if n.startswith('fatal.context.')]
    deaths=[(t,int(n.split('.')[-1])) for t,n in notes if n.startswith('fatal.death.')]
    attacks=[n for _,n in notes if n.startswith('fatal.attack.')]
    if (sorted(int(n.split(".")[2]) for n in contexts)!=list(range(20))
        or sorted(int(n.split(".")[2]) for n in attacks)!=list(range(20))
        or sorted(i for _,i in deaths)!=list(range(20))):
        raise ValueError('Twenty distinct ordered fatal contexts did not complete')
    if any('.valid.1.dead.0.seen.1.enemy.1.' not in n for n in contexts):
        raise ValueError('Fatal context identity/perception/hostility failure')
    rows=json.loads(subprocess.check_output([sys.executable,str(Path(__file__).with_name('check_voice_actors.py')),str(path)],encoding='utf-8'))
    cues=[dict(family=r['family'],time=t,duration=d) for r in rows for t,d in zip(r['onsets'],r['durations']) if start<=t<end]
    positives=[]
    for t,n in notes:
        if n.startswith('positive.slot.'):
            slot=int(n.split('.')[-1]);family='death18' if slot==18 else f'pain{slot}'
            positives.append(dict(slot=slot,time=t,identified=any(r['family']==family and any(abs(c-t)<.5 for c in r['onsets']) for r in rows)))
    if len(positives)!=8 or not all(p['identified'] for p in positives):
        raise ValueError('All eight explicit sound-slot positive controls must identify')
    return dict(source=str(path),sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
        fixtureSha256=doc['fixtureSha256'],nativeEffectsMuted=doc.get('nativeEffectsMuted'),
        contexts=contexts,attacks=attacks,deaths=deaths,damage=[(t,n) for t,n in notes if n.startswith('fatal.damage.')],
        positiveControls=positives,painCues=sum(c['family'].startswith('pain') for c in cues),
        deathCues=sum(c['family']=='death18' for c in cues),cues=cues,
        limitations='Sequential ordinary NPC melee deaths only; no PC death, ranged, every creature or exact engine-call ordering claim. Buffered log receipt is not exact cue timing. The detector resolves tones lasting at least50ms; absence does not exclude a shorter immediately-preempted internal voice event. Native pixels require separate validation.')


if __name__=='__main__':
    print(json.dumps(summarize(Path(sys.argv[1])),indent=2))
