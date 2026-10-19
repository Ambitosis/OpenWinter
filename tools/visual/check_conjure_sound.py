"""Report synthetic 700 Hz cue durations; logs establish success/cancellation separately."""
import json, sys, wave
import numpy as np
from scipy.signal import stft
from pathlib import Path
p=Path(sys.argv[1])
with wave.open(str(p),'rb') as w:
    rate=w.getframerate();channels=w.getnchannels()
    samples=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').reshape(-1,channels).mean(axis=1)
f,t,z=stft(samples,rate,nperseg=round(rate*.02),noverlap=round(rate*.015))
power=np.abs(z[np.argmin(abs(f-700))])
threshold=max(5,float(power.max())*.25)
active=power>threshold
indices=np.flatnonzero(active)
groups=np.split(indices,np.where(np.diff(indices)>3)[0]+1)
segments=[dict(start=float(t[g[0]]),end=float(t[g[-1]]),duration=float(t[g[-1]]-t[g[0]])) for g in groups if len(g)>10]
print(json.dumps(dict(path=str(p),rate=rate,frequency=700,windowSeconds=.02,hopSeconds=.005,threshold=threshold,segments=segments),indent=2))
