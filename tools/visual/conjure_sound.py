"""Measure ConjSoundVFX lifetime with an authored 8-second 700 Hz cue.
The override is exclusive, sandbox-only, and removed even if capture fails.
Native records only the harness-owned process; client records its Master bus.
"""
import json, math, runpy, struct, sys, wave
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import visual
created=[]
original=visual.prepare_user
def prepare(*a,**kw):
    user=original(*a,**kw)
    p=user/'override'/'sco_mehanholy01.wav'
    p.parent.mkdir(exist_ok=True)
    with p.open('xb') as raw:
        created.append(p)
        with wave.open(raw,'wb') as f:
            f.setparams((1,2,44100,0,'NONE','not compressed'))
            f.writeframes(b''.join(struct.pack('<h',int(6000*math.sin(i*2*math.pi*700/44100))) for i in range(44100*8)))
    return user
visual.prepare_user=prepare
try:
    client = len(sys.argv)>1 and sys.argv[1]=='client'
    if client:
        cfg=visual.config();mod=visual.build('conjuresound')[0]
        user=prepare(cfg,'aluviandarkstar',(1280,720),'plain')
        visual.run_openwinter(cfg,mod,user,visual.OUT/'shots'/'conjuresound'/'openwinter',58,'aluviandarkstar',(1280,720),['--visual-audio','--visual-delay','0','--fx-verbose','--ui','classic'])
    else:
        sys.argv=['spellvoice_audio.py','conjuresound','all']
        runpy.run_path(str(Path(__file__).with_name('spellvoice_audio.py')),run_name='__main__')
    metadata = visual.OUT/'shots'/'conjuresound'/'openwinter'/'audio.json' if client else visual.REPO/'out'/'vis_conjuresound_all.json'
    data=json.loads(metadata.read_text(encoding='utf-8'))
    data['syntheticOverride'] = dict(resource='sco_mehanholy01',frequency=700,durationSeconds=8,rate=44100,amplitude=6000)
    with metadata.open('w',encoding='utf-8',newline='') as f: json.dump(data,f,indent=2)
finally:
    visual.prepare_user=original
    for p in created:p.unlink()
