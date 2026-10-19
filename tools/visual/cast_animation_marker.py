"""Owned-process native animation identity probe using synthetic footstep markers.

Run from the repository root with a DISPLAY2 lease:
  python tools/visual/cast_animation_marker.py caststepisolated
  python tools/visual/cast_animation_marker.py caststepquiet
The latter silences known Mage Armor cues solely in the sandbox to expose markers.
No installed resource is modified. Requires the native process-audio helper deps.
"""
import math,sys,wave,struct,runpy,json,hashlib
from pathlib import Path
scene=sys.argv[1] if len(sys.argv)>1 else "caststepquiet"
if scene not in ("caststepisolated", "caststepquiet"): raise SystemExit("Expected caststepisolated or caststepquiet")
root=Path.cwd();directory=root/'out/visual/user/override';directory.mkdir(parents=True,exist_ok=True)
surfaces=['dirt','grass','stone','wood','water','carpt','metal','pudle','leaf','x0snd','x0snw']
sounds=[f'fs_{surface}_hard{i}' for surface in surfaces for i in (1,2,3)]
# Synthetic identity marker; never installed sounds or global audio.
rate=44100;data=b''.join(struct.pack('<h',int(14000*math.sin(2*math.pi*880*i/rate))) for i in range(int(.18*rate)))
muted=["sco_mehanelec01","vs_chant_conj_lm","vs_chant_conj_lf","sim_bonac","sce_positive"] if scene=="caststepquiet" else []
created=[]
try:
 for sound in sounds:
  path=directory/(sound+'.wav')
  with path.open('xb') as output:
   created.append(path)
   with wave.open(output,'wb') as f:f.setparams((1,2,rate,0,'NONE','not compressed'));f.writeframes(data)
 for sound in muted:
  path=directory/(sound+'.wav')
  with path.open('xb') as output:
   created.append(path)
   with wave.open(output,'wb') as f:f.setparams((1,2,rate,0,'NONE','not compressed'));f.writeframes(bytes(8820))
 sys.argv=['spellvoice_audio.py',scene,'all']
 runpy.run_path(str(root/'tools/visual/spellvoice_audio.py'),run_name='__main__')
finally:
 for path in created:path.unlink()
 (root/('out/'+scene+'-marker-overrides.json')).write_text(json.dumps(dict(sounds=sounds,muted=muted,frequency=880,seconds=.18,pcmSha256=hashlib.sha256(data).hexdigest(),removed=all(not p.exists() for p in created)),indent=2),encoding='utf-8')
