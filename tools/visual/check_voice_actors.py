"""Identify synthetic per-actor voice probes; no installed sound is fingerprinted.
Usage: python tools/visual/check_voice_actors.py out/voice_slots_voiceactors_11,12,13.json
Requires numpy (analysis only). Emits each actor's waveform onsets relative to its
logged ActionAttack request; five-second phase is a measurement, not an assertion.
"""
import json,sys,wave
from pathlib import Path
import numpy as np
p=Path(sys.argv[1]);data=json.loads(p.read_text(encoding='utf-8'))
record=data['recording']
if record.get('pcm_peak',0)<20:raise SystemExit('No usable PCM waveform')
with wave.open(record['path']) as w:
 rate=w.getframerate();a=np.frombuffer(w.readframes(w.getnframes()),np.int16).reshape(-1,w.getnchannels()).astype(float).mean(axis=1)
size=rate//50;hop=rate//200
starts=np.arange(0,len(a)-size,hop);window=np.hanning(size)
frames=np.lib.stride_tricks.sliding_window_view(a,size)[::hop]
spectrum=abs(np.fft.rfft(frames*window,axis=1))*2/window.sum()
notes={}
for stamp,line in data['notes']:
 if 'VISUAL|voice_actor|' in line:
  parts=line.split('VISUAL|voice_actor|',1)[1].split('|');notes[int(parts[0])]=stamp-record['started']
rows=[]
for tone in data.get('actor_tones',[dict(actor=i,family='grunt',hz=400+100*i) for i in range(20)]):
 actor=tone['actor'];family=tone['family'];hz=tone['hz']
 amp=spectrum[:,hz//50];threshold=max(5,float(amp.max())*.25)
 active=np.flatnonzero(amp>threshold);groups=np.split(active,np.where(np.diff(active)>3)[0]+1)
 onsets=[];durations=[]
 for group in groups:
  if len(group)==0 or (group[-1]-group[0]+1)*hop/rate<.05:continue
  onsets.append(round((group[0]*hop+size/2)/rate,4))
  durations.append(round((group[-1]-group[0]+1)*hop/rate,4))
 origin=notes.get(actor)
 rows.append(dict(actor=actor,family=family,hz=hz,peak=round(float(amp.max()),2),origin=origin,
   onsets=onsets,durations=durations,relative=[round(t-origin,3) for t in onsets] if origin is not None else [],
   intervals=[round(b-a,3) for a,b in zip(onsets,onsets[1:])]))
print(json.dumps(rows,indent=2))
