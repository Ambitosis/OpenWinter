"""Report synthetic footstep marker intervals; no automatic animation parity approval.

Run after cast_animation_marker.py. The thresholds are unchanged between controls
and candidate clips. Overlapping sound can mask a marker; silence proves no absence.
"""
import json,wave,sys,hashlib
from pathlib import Path
import numpy as np
scene=sys.argv[1]
path=Path('out/vis_'+scene+'_all.json');m=json.loads(path.read_text());record=m['recording']
with wave.open(record['path'],'rb') as w:
 rate=w.getframerate();channels=w.getnchannels();x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').reshape(-1,channels).astype(float).mean(axis=1)
size=int(rate*.04);step=int(rate*.02);t=np.arange(size)/rate;basis=np.exp(-2j*np.pi*880*t);windows=[]
for i in range(0,len(x)-size,step):
 y=x[i:i+size];amp=2*abs(np.sum(y*basis))/size;energy=float(np.mean(y*y));fraction=min(1,amp*amp/2/max(energy,1))
 if amp>=300 and fraction>=.65:windows.append(dict(time=round(i/rate,4),amplitude=round(float(amp),2),toneEnergyFraction=round(fraction,4)))
notes=[dict(time=round(stamp-record['started'],4),note=line.split('VISUAL|',1)[1]) for stamp,line in m['notes']]
report=dict(scene=scene,rate=rate,frequency=880,windowSeconds=.04,hopSeconds=.02,minimumAmplitude=300,minimumToneEnergyFraction=.65,qualifyingWindows=windows,notes=notes,sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [path,Path(record['path']),Path('parity/visual/scenes/'+scene+'/vis_main.nss')]},limits='Synthetic marker detector only. Original animation timing can change under replacement. No pixel parity, negative inference from masked audio, or whole-spell axis approval.')
Path('out/'+scene+'-analysis.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('notes',notes)
intervals=[]
for win in windows:
 if intervals and win['time']-intervals[-1][1]<.061:intervals[-1][1]=win['time']+.04
 else:intervals.append([win['time'],win['time']+.04])
print('qualifying tone intervals',intervals)
