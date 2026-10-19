"""Check the measured familiar-arrival cue in validated real-player spell batches.

Requires the existing match_spell_audio helper and locally extracted sff_summund.wav.
Sample identity and fixed-origin windows do not establish every cue or visual parity.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import numpy as np
import match_spell_audio as matcher

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('metadata',type=Path)
parser.add_argument('capture',type=Path,help='spell_evidence capture manifest')
parser.add_argument('reference',type=Path,help='clean installed sff_summund WAV')
parser.add_argument('--output',type=Path,required=True)
a=parser.parse_args()
d=json.loads(a.metadata.read_text(encoding='utf-8'));c=json.loads(a.capture.read_text(encoding='utf-8'))
if not c.get('sceneDone') or c.get('contextFailures'):raise ValueError('Incomplete or invalid capture context')
o=c['observations']['318']
if o.get('triggerStatus')!='execution-observed':raise ValueError('Familiar execution was not observed')
text='\n'.join(line for _,line in d['notes'])
if re.search(r'TOO MANY INSTRUCTIONS|SCRIPT ERROR|\[script error\]',text,re.I):raise ValueError('Script error')
for tag in ('state.318.before.pc.1','state.318.before.feat_uses.1','state.318.late.feat_uses.0','state.318.late.associate.3.1','VISUAL|done|1'):
 if tag not in text:raise ValueError('Missing context '+tag)
r=d['recording']
if c['engine']=='nwn' and not r.get('session',{}).get('restored'):raise ValueError('Owned native session not restored')
starts=[t-r['started'] for t,line in d['notes'] if line.endswith('VISUAL|note|start.318')]
if len(starts)!=1:raise ValueError('Expected one familiar start')
matcher.RATE=44100
wave=Path(r['path'])
audio=c.get('audio',{})
if not audio.get('valid'):raise ValueError('Capture manifest does not validate audio')
for path,key in ((a.metadata,'metadataSha256'),(wave,'waveformSha256')):
 if hashlib.sha256(path.read_bytes()).hexdigest()!=audio.get(key):raise ValueError('Capture/audio hash mismatch: '+key)
pcm=matcher.decode(wave);ref=matcher.decode(a.reference)
start=max(0,starts[0]-.15);end=min(starts[0]+8,len(pcm)/matcher.RATE)
result=matcher.identify_candidates(pcm[int(start*matcher.RATE):int(end*matcher.RATE)],ref,())
windows=[]
if result['status']=='identified':
 origin=start+result['offset']
 for t in np.arange(0,len(ref)/matcher.RATE-.3,.3):
  x=ref[round(t*matcher.RATE):round((t+.3)*matcher.RATE)];j=round((origin+t)*matcher.RATE);y=pcm[j:j+len(x)]
  if len(x)!=len(y):raise ValueError('Incomplete fixed-origin window')
  xx=x-x.mean();yy=y-y.mean();den=np.sqrt(np.dot(xx,xx)*np.dot(yy,yy))
  windows.append(dict(start=round(float(t),2),score=round(float(np.dot(xx,yy)/den),5) if den else 0,referenceRms=round(float(np.sqrt(np.mean(x*x))),6)))
report=dict(scene=c['scene'],engine=c['engine'],revision=c['revision'],fixtureSha256=c['fixtureSha256'],resref='sff_summund',source='Measured SummonFamiliar arrival -> visualeffects36 SoundImpact; spell318 sound fields blank',identity=result,fixedOriginWindows=windows,configuration=dict(rate=44100,multipleAnchorThreshold=.65,singleAnchorThreshold=.85,originToleranceSeconds=.025),sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (a.metadata,a.capture,a.reference,wave)},limitations='Only arrival cue identity. Quiet tail can be masked; original recording timing uses buffered note receipt. No chant absence, all-cue completeness, volume, spatialization, visual parity or exact onset claim.')
with a.output.open('w',encoding='utf-8',newline='') as f:json.dump(report,f,indent=2)
print(c['scene'],c['engine'],result['status'],result.get('anchorScores'))
