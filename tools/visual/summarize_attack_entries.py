"""Inspect synthetic Attack/battlecry slots without conflating scripted and posted commands."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def summarize(path):
    doc=json.loads(path.read_text(encoding='utf-8'))
    if not doc.get('validated') or not doc['recording'].get('session',{}).get('restored'):
        raise ValueError('Native fixture/session did not validate')
    origin=doc['recording']['started']
    notes=[(t-origin,line.split('VISUAL|note|',1)[1].strip()) for t,line in doc['notes'] if 'VISUAL|note|' in line]
    posted=doc['scene'].startswith('voiceactors_entryui')
    if posted:
        if doc['scene'].startswith('voiceactors_entryui4'):
            starts=[(t,n) for t,n in notes if n.startswith('entry.ui.ready.')]
            stops=[(t,n) for t,n in notes if n.startswith('entry.ui.completed.')]
            if (sorted(int(n.split('.')[3]) for _,n in starts)!=list(range(4))
                or any(n.split('.')[4]!='1' for _,n in starts)
                or sorted(int(n.split('.')[3]) for _,n in stops)!=list(range(4))
                or any(int(n.split('.')[5])<1 for _,n in stops)):
                raise ValueError('Four legal posted attacks must complete')
            start=starts[0][0];end=stops[-1][0]
            ends={int(n.split('.')[3]):t for t,n in stops}
            windows=[(int(n.split('.')[3]),t,ends[int(n.split('.')[3])]) for t,n in starts]
        else:
            start=next(t for t,n in notes if n.startswith('entry.ui.ready.1.'))
            end=next(t for t,n in notes if n.startswith('entry.ui.completed.attacks.'))
            windows=[(0,start,end)]
        attacks=[n for _,n in notes if n.startswith('entry.ui.attack.') and n.endswith('.attackerPC.1')]
        if not attacks:raise ValueError('No posted Attack positive event')
    else:
        expected=1 if doc['scene'] in ('voiceactors_entrynpcsingle_slots','voiceactors_entrynpcstock_slots') else 5 if doc['scene'] in ('voiceactors_entrypc5_slots','voiceactors_entrynpc5_slots') else 10
        actor_pc=0 if doc['scene'].startswith('voiceactors_entrynpc') else 1
        starts=[(t,n) for t,n in notes if n.startswith('entry.start.')]
        stops=[n for _,n in notes if n.startswith('entry.stop.')]
        if sorted(int(n.split('.')[2]) for _,n in starts)!=list(range(expected)) or any('.ready.1.' not in n for _,n in starts):
            raise ValueError(f'{expected} legal entry contexts required')
        if sorted(int(n.split('.')[2]) for n in stops)!=list(range(expected)) or any(int(n.split('.')[4])<1 for n in stops):
            raise ValueError('Every entry must attack')
        start=starts[0][0];end=next(t for t,n in notes if n==f'entry.completed.{expected}')
        ends={int(n.split('.')[2]):t for t,n in notes if n.startswith('entry.stop.')}
        windows=[(int(n.split('.')[2]),t,ends[int(n.split('.')[2])]) for t,n in starts]
        attacks=[n for _,n in notes if n.startswith('entry.attack.') and n.endswith(f'.attackerPC.{actor_pc}')]
    if not attacks:raise ValueError('No matching actor attack events')
    rows=json.loads(subprocess.check_output([sys.executable,str(Path(__file__).with_name('check_voice_actors.py')),str(path)],encoding='utf-8'))
    positives=[]
    for t,n in notes:
        if n.startswith('positive.slot.'):
            slot=int(n.split('.')[-1]);family='attack0' if slot==0 else f'battlecry{slot}'
            positives.append(dict(slot=slot,identified=any(r['family']==family and any(abs(c-t)<.5 for c in r['onsets']) for r in rows)))
    if len(positives)!=8 or not all(p['identified'] for p in positives):raise ValueError('All eight positive sound cues must identify')
    cues=[dict(family=r['family'],time=t,duration=d) for r in rows for t,d in zip(r['onsets'],r['durations']) if start<=t<end]
    return dict(source=str(path),sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
        fixtureSha256=doc['fixtureSha256'],entryWindows=[dict(index=i,start=a,end=b,cues=[c for c in cues if a<=c['time']<b]) for i,a,b in windows],context='posted radial Attack' if posted else f'scripted ActionAttack (actor PC={actor_pc})',
        attacks=attacks,positiveControls=positives,cues=cues,
        entries=[dict(time=t,note=n) for t,n in notes if n.startswith(('entry.start.','entry.stop.','entry.ui.ready.','entry.ui.completed.','entry.ui.state.'))],
        limitations='Only the sampled actor, equipment and passive-target contexts. Scripted attacks do not establish GUI acknowledgement behavior. Radial target/selection requires separate native frame inspection. Fixed detector minimum50ms; buffered note times are not exact playback latency. No inferred probability from absent cues.')

if __name__=='__main__':
    print(json.dumps(summarize(Path(sys.argv[1])),indent=2))
