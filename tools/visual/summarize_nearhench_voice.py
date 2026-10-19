"""Validate explicit stock AI calls before associating independently coded slot17 PCM."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from scipy.stats import binomtest


def summarize(path,tones=None):
    doc=json.loads(path.read_text(encoding='utf-8'))
    if doc.get('scene')!='voiceactors_nearhench_slots' or not doc.get('validated'):
        raise ValueError('Completed stock henchman fixture required')
    if doc['engine']=='native' and not doc['recording'].get('session',{}).get('restored'):
        raise ValueError('Native session restoration required')
    origin=doc['recording']['started']
    notes=[(t-origin,line.split('VISUAL|note|',1)[1].strip()) for t,line in doc['notes'] if 'VISUAL|note|' in line]
    if any('INVALID_' in n for _,n in notes):raise ValueError('Explicit stock AI context failure')
    if not any(n=='nearhench.completed.100' for _,n in notes):raise ValueError('All100 calls must finish')
    ready=[n for _,n in notes if n.startswith('nearhench.ready.')]
    if len(ready)!=1 or not re.fullmatch(r'nearhench\.ready\.1\.type\.(\d+)\.hp\.1\.max\.12\.masterPC\.1',ready[0]):
        raise ValueError('Genuine henchman/master/HP readiness required')
    associate_type=int(ready[0].split('.')[4])
    attempts=[]
    for t,n in notes:
        m=re.fullmatch(r'nearhench\.attempt\.(\d+)\.hp\.(\d+)\.max\.(\d+)\.type\.(\d+)\.masterPC\.(\d+)\.bindings\.(\d+)\.result\.(\d+)\.afterHP\.(\d+)',n)
        if m:
            number,hp,maximum,kind,master,bindings,result,after=map(int,m.groups())
            if (hp,maximum,kind,master,bindings,result,after)!=(1,12,associate_type,1,1,0,1):raise ValueError('Every stock AI call must preserve eligibility and remain unhealed')
            attempts.append(dict(time=t,number=number,hp=hp,maximum=maximum,associateType=kind,masterPC=master,blankBindings=bindings,result=result,afterHP=after))
    if [a['number'] for a in attempts]!=list(range(1,101)):raise ValueError('100 contiguous eligible calls required')
    if tones is None:tones=json.loads(subprocess.check_output([sys.executable,str(Path(__file__).with_name('check_voice_actors.py')),str(path)],encoding='utf-8'))
    if len(tones)!=1 or tones[0]['family']!='neardeath17':raise ValueError('Independent slot17 detector required')
    cues=[dict(time=t,duration=d) for t,d in zip(tones[0]['onsets'],tones[0]['durations'])]
    times=[t for t,n in notes if n=='nearhench.positive.slot.17']
    positives=[dict(time=t,identified=sum(abs(c['time']-t)<.5 for c in cues)==1) for t in times]
    if len(positives)!=2 or not all(p['identified'] for p in positives):raise ValueError('Before/after slot17 positives required')
    automatic=[c for c in cues if not any(abs(c['time']-t)<.5 for t in times)]
    if any(not attempts[0]['time']-.5<=c['time']<=attempts[-1]['time']+.5 for c in automatic):raise ValueError('Unexpected cue outside authored stock AI call window')
    k=len(automatic);n=len(attempts)
    if k>n:raise ValueError('More cues than eligible calls')
    interval=binomtest(k,n).proportion_ci()
    return dict(source=str(path),sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),fixtureSha256=doc['fixtureSha256'],attempts=attempts,positiveControls=positives,scriptRequestedCues=automatic,sourceModel=dict(probability=.05,expected=n*.05,zeroProbabilityIfIndependent=.95**n,twoSidedP=float(binomtest(k,n,.05).pvalue)),observed=dict(cues=k,eligibleCalls=n,binomial95=[float(interval.low),float(interval.high)]),limitations='Explicit calls to unmodified shipped bkCombatAttemptHeal on a genuine henchman; not complete ordinary AI scheduling. Source-derived two-stage probability assumes independent public random draws and does not establish engine RNG internals or timing. Synthetic slot identity, not every installed waveform.')


if __name__=='__main__':print(json.dumps(summarize(Path(sys.argv[1])),indent=2))
