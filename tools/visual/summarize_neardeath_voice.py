"""Keep actual-player HP routes and slot17 identity independent; no probability fit."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def summarize(path, tones=None):
    doc = json.loads(path.read_text(encoding='utf-8'))
    mortal = doc.get('scene') == 'voiceactors_nearmortal_slots'
    if doc.get('scene') not in ('voiceactors_neardeath_slots','voiceactors_nearmortal_slots') or not doc.get('validated'):
        raise ValueError('Completed near-death fixture required')
    if doc['engine'] == 'native' and not doc['recording'].get('session', {}).get('restored'):
        raise ValueError('Native owned audio session restoration required')
    origin = doc['recording']['started']
    notes = [(t-origin, line.split('VISUAL|note|', 1)[1].strip())
             for t, line in doc['notes'] if 'VISUAL|note|' in line]
    # Master capture can start after OnEnter. Read static startup gates from
    # the original log; never invent an audio-relative timestamp for them.
    script_log = Path(doc['scriptLog']) if doc.get('scriptLog') else None
    full_log = script_log.read_text(encoding='utf-8', errors='replace') if script_log else ''
    if any('INVALID_' in n for _, n in notes) or 'INVALID_' in full_log:
        raise ValueError('Explicit native context failure')
    ready = 'near.ready.1.pc.1.name.Aluvian Darkstar.soundset.5100.max.11'
    if sum(n == ready for _, n in notes) != 1 or not any(n == 'near.completed' for _, n in notes):
        raise ValueError('Actual-player identity and completion required')
    expected = [('set9',9),('set6',6),('set2',2),('set1',1),('reset1',11),
                ('scriptlow',1),('reset2',11),('combatlow',1)]
    if mortal:
        expected=expected[:6]
        if not any(n=='near.immortal.0' for _,n in notes) and not re.search(r'VISUAL\|note\|near\.immortal\.0\s*$', full_log, re.MULTILINE):
            raise ValueError('Nonimmortal player positive required')
    states=[]
    for t,n in notes:
        m=re.fullmatch(r'near\.state\.(\w+)\.hp\.(\d+)\.max\.(\d+)\.dead\.(\d+)\.damage\.(\d+)',n)
        if m:
            phase,hp,maximum,dead,damage=m.groups()
            states.append(dict(time=t,phase=phase,hp=int(hp),maximum=int(maximum),dead=int(dead),damage=int(damage)))
    if [(s['phase'],s['hp']) for s in states] != expected or any(s['maximum'] != 11 or s['dead'] for s in states):
        raise ValueError('Every exact nonfatal HP state required')
    if not mortal and sum(n == 'near.combat.ready.1' for _, n in notes) != 1:
        raise ValueError('Perception/hostility combat positive required')
    damage=[]
    for t,n in notes:
        m=re.fullmatch(r'near\.damage\.event\.(\d+)\.phase\.(\w+)\.amount\.(\d+)\.hp\.(\d+)',n)
        if m:
            number,phase,amount,hp=m.groups()
            damage.append(dict(time=t,event=int(number),phase=phase,amount=int(amount),hp=int(hp)))
    if [e['event'] for e in damage] != list(range(1,len(damage)+1)):
        raise ValueError('Contiguous damage callbacks required')
    scripted=[e for e in damage if e['phase']=='script']
    combat=[e for e in damage if e['phase']=='combat' and e['amount']>0]
    if len(scripted)!=1 or scripted[0]['hp']!=1 or scripted[0]['amount']!=10 or (not mortal and not combat) or (mortal and combat):
        raise ValueError('Independent scripted and ordinary combat damage required')
    if tones is None:
        tones=json.loads(subprocess.check_output([sys.executable,str(Path(__file__).with_name('check_voice_actors.py')),str(path)],encoding='utf-8'))
    if len(tones)!=1 or tones[0]['family']!='neardeath17':
        raise ValueError('Independently coded slot17 detector required')
    cues=[dict(time=t,duration=d) for t,d in zip(tones[0]['onsets'],tones[0]['durations'])]
    positive_times=[t for t,n in notes if n=='near.positive.slot.17']
    positives=[dict(time=t,identified=sum(abs(c['time']-t)<.5 for c in cues)==1) for t in positive_times]
    if len(positives)!=2 or not all(p['identified'] for p in positives):
        raise ValueError('Before/after sound identity positives required')
    automatic=[c for c in cues if not any(abs(c['time']-t)<.5 for t in positive_times)]
    transitions=[(t,n) for t,n in notes if n.startswith(('near.set.','near.damage.script.','near.combat.ready.'))]
    intervals=[]
    for i,(start,n) in enumerate(transitions):
        end=transitions[i+1][0] if i+1<len(transitions) else positive_times[-1]
        intervals.append(dict(marker=n,start=start,end=end,cues=[c for c in automatic if start<=c['time']<end]))
    return dict(source=str(path),sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
                scriptLogSha256=hashlib.sha256(script_log.read_bytes()).hexdigest() if script_log else None,
                fixtureSha256=doc['fixtureSha256'],nonimmortal=mortal,states=states,damage=damage,
                positiveControls=positives,automaticCues=automatic,intervals=intervals,
                limitations='Single actual-player Constitution-buffed eleven-HP context; creature scripts cleared. No exact threshold, probability, cooldown, stock AI, NPC, fatal or installed-waveform generalization.')


if __name__=='__main__':
    print(json.dumps(summarize(Path(sys.argv[1])),indent=2))
