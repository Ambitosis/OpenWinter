"""Identify native automatic sound-set slots using a synthetic sandbox-only SSF.
Every slot has a recognizable stock TLK caption (4710 + slot); WAV is synthetic.
No installed asset is modified or committed. Captures are under out/visual/shots/voice_slots.
Optional fifth argument client/client-modern records the client Master bus instead of native process PCM.
Optional sixth argument effects leaves the native Effects volume enabled; metadata records it.
Example: voice_slots.py 11,12,13 voiceactors_pc_slots .12 trial1 client
"""
import math, struct, sys, wave, threading, time, json, hashlib, shutil, re
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import visual
sys.path.insert(0,str(visual.REPO/"out"/"audio-probe"))
from pycaw.pycaw import AudioUtilities, IAudioMeterInformation
import comtypes
from process_audio import ProcessRecording
engine=sys.argv[5] if len(sys.argv)>5 else "native"
if engine not in ("native","client","client-modern"):raise SystemExit("Fifth argument must be native, client or client-modern")
ui_style="modern" if engine=="client-modern" else "classic"
if engine=="client-modern":engine="client"
effects_enabled=len(sys.argv)>6 and sys.argv[6]=="effects"
if len(sys.argv)>6 and not effects_enabled:raise SystemExit("Optional sixth argument must be effects")
cfg=visual.config()
scene=sys.argv[2] if len(sys.argv)>2 else "voicecombat"
mod=visual.build(scene)[0]
user=visual.prepare_user(cfg,"aluviandarkstar",(1280,720),"plain")
# This installed stock character has vs_fshaldrf; the scene gives NPC the same set.
override=user/"override"
override.mkdir(exist_ok=True)
created=[]
mode=sys.argv[1] if len(sys.argv)>1 else "all"
run_name=(scene+"_" if len(sys.argv)>2 else "")+mode
if len(sys.argv)>4:
    variant=sys.argv[4]
    if not variant or any(not(c.isalnum() or c in "-_") for c in variant):
        raise SystemExit("Variant must contain only letters, digits, hyphen or underscore")
    run_name += "_"+variant
probe_seconds=float(sys.argv[3]) if len(sys.argv)>3 else .2
selected = {int(s) for s in mode.split(",")} if mode[0].isdigit() else None
if selected is None and mode not in ("all","voice","grunts","grunts_effects"):raise SystemExit("Use all, voice, grunts, grunts_effects or comma-separated slots")
mute_effects=not effects_enabled and (selected is not None or mode in ("voice","grunts"))
with (user/"settings.tml").open("a",encoding="utf-8",newline="") as f:
    f.write("\n[sound.music]\nvolume=0.0\n[sound.effects]\nvolume="+("0.0" if mute_effects else "1.0")+"\n[sound.voices]\nvolume=1.0\n")
stop=threading.Event()
samples=[];notes=[];target=[None];recording=[None];record_info=None;session_info={};script_log=None;validated=False
probe_family='pain' if scene.startswith('voiceactors_pain') else 'grunt'
actor_tones=[dict(actor=i,family=family,hz=400+200*i+(100 if family=='battlecry' else 0)) for i in range(20) for family in (probe_family,'battlecry')] if scene.startswith('voiceactors') else []
per_slot=scene.endswith("_slots")
probe_slots=(14,15,16) if scene.startswith("voiceactors_pain") else (11,12,13)
if scene=="voiceactors_painfatal_slots":probe_slots=(14,15,16,18)
encumbrance_probe=scene.startswith("voiceactors_encumbered")
if encumbrance_probe:probe_slots=(32,)
near_probe=scene in ("voiceactors_neardeath_slots", "voiceactors_nearmortal_slots", "voiceactors_nearhench_slots")
if near_probe:probe_slots=(17,)
entry_probe=scene.startswith("voiceactors_entry")
if entry_probe:probe_slots=(0,1,2,3)
quickchat_probe=scene.startswith("voiceactors_quickchat")
if quickchat_probe:probe_slots=tuple(range(49))
# Native quickchat consumes PrintScreen while open. Its audio/input control uses
# noninteractive GDI diagnostics; black frames are excluded from visual evidence.
native_framebuffer=entry_probe or encumbrance_probe or near_probe
if per_slot:
    actor_tones=[dict(actor=0,family=("death18" if slot==18 else f"{probe_family}{slot}"),hz=400+200*i) for i,slot in enumerate(probe_slots)]
arbitration=scene in ("voiceactors_arbitration_slots", "voiceactors_painspacing_slots")
if entry_probe:actor_tones=[dict(actor=0,family=("attack0" if slot==0 else f"battlecry{slot}"),hz=400+200*i) for i,slot in enumerate(probe_slots)]
if encumbrance_probe:actor_tones=[dict(actor=0,family="encumbered32",hz=400)]
if near_probe:actor_tones=[dict(actor=0,family="neardeath17",hz=400)]
if quickchat_probe:actor_tones=[dict(actor=0,family=f"slot{slot}",hz=400+100*i) for i,slot in enumerate(probe_slots)]
many_entries=scene=="voiceactors_entrynpcmany8_slots"
if many_entries:actor_tones=[dict(actor=actor,family=("attack0" if slot==0 else f"battlecry{slot}"),hz=400+400*actor+100*i) for actor in range(20) for i,slot in enumerate(probe_slots)]
if arbitration: actor_tones.append(dict(actor=0,family="hello",hz=1200))
original=visual.subprocess.Popen
def launch(*args,**kwargs):
    proc=original(*args,**kwargs);target[0]=proc.pid
    recording[0]=ProcessRecording(proc,visual.REPO/"out"/("voice_slots_"+run_name+".wav"),audible=True)
    original_kill=proc.kill
    def kill_owned():
        try: recording[0].close() # restore this process session before its persisted state is saved
        finally: original_kill()
    proc.kill=kill_owned
    return proc
if engine=="native":visual.subprocess.Popen=launch
def meter():
    comtypes.CoInitialize();m=None;line_count=0
    try:
        while not stop.is_set():
            if m is None and target[0]:
                session=AudioUtilities.GetProcessSession(target[0])
                if session:
                    m=session._ctl.QueryInterface(IAudioMeterInformation)
                    session_info.update(pid=session.ProcessId,mute=session.SimpleAudioVolume.GetMute(),volume=session.SimpleAudioVolume.GetMasterVolume(),identifier=session.Identifier,instance=session.InstanceIdentifier)
            if m:samples.append([time.time(),m.GetPeakValue()])
            log=user/"logs"/"nwclientLog1.txt"
            if target[0] and log.exists():
                lines=log.read_text(encoding="utf-8",errors="replace").splitlines()
                if len(lines)<line_count:line_count=0
                for line in lines[line_count:]:
                    if "VISUAL|" in line:notes.append([time.time(),line])
                line_count=len(lines)
            time.sleep(.01)
    finally:comtypes.CoUninitialize()
thread=threading.Thread(target=meter)
if engine=="native":thread.start()
try:
    # Per-speaker probes use an entirely synthetic table, including blank rows for
    # the PC. No installed table or sample is copied into the repository/override.
    actors=20 if scene.startswith("voiceactors") else 1
    assets={}
    if actors>1:
        rows=["2DA V2.0", "", "LABEL RESREF STRREF GENDER TYPE"]
        rows += [f"{i} Blank **** **** 0 4" for i in range(5100)]
        rows += [f"{5100+i} Probe{i} ely_vset{i:02d} **** 0 0" for i in range(actors)]
        assets['soundset.2da']=("\n".join(rows)+"\n").encode('ascii')
    for actor in range(actors):
        sample=f"ely_vtone{actor:02d}" if actors>1 else "ely_voiceprobe"
        ssf=f"ely_vset{actor:02d}.ssf" if actors>1 else "vs_fshaldrf.ssf"
        data=bytearray(40+49*24);data[:8]=b"SSF V1.0"
        struct.pack_into("<II",data,8,49,40)
        for slot in range(49):
            offset=40+49*4+slot*20
            struct.pack_into("<I",data,40+slot*4,offset)
            if (slot in selected if selected is not None else not mode.startswith("grunts") or slot in (11,12,13,18)):
                name=f"ely_btone{actor:02d}" if actors>1 and slot in (1,2,3) else sample
                if per_slot and slot in probe_slots: name=f"ely_g{slot}_{actor:02d}"
                if arbitration and slot==34: name="ely_hello"
                data[offset:offset+len(name)]=name.encode('ascii')
            struct.pack_into("<I",data,offset+16,4710+slot)
        assets[ssf]=data
        import io
        raw=io.BytesIO()
        frequency=400+200*actor if actors>1 else 440
        with wave.open(raw,"wb") as f:
            f.setparams((1,2,22050,0,"NONE","not compressed"))
            f.writeframes(b"".join(struct.pack("<h",int(5000*math.sin(i*2*math.pi*frequency/22050))) for i in range(int(22050*probe_seconds))))
        assets[sample+'.wav']=raw.getvalue()
        if per_slot:
            for tone_index,slot in enumerate(probe_slots):
                raw=io.BytesIO()
                with wave.open(raw,'wb') as f:
                    f.setparams((1,2,22050,0,'NONE','not compressed'))
                    frequency=400+400*actor+100*tone_index if many_entries else 400+100*tone_index if quickchat_probe else 400+200*tone_index
                    f.writeframes(b''.join(struct.pack('<h',int(5000*math.sin(i*2*math.pi*frequency/22050))) for i in range(int(22050*probe_seconds))))
                assets[f'ely_g{slot}_{actor:02d}.wav']=raw.getvalue()
        if arbitration:
            raw=io.BytesIO()
            with wave.open(raw,'wb') as f:
                f.setparams((1,2,22050,0,'NONE','not compressed'))
                f.writeframes(b''.join(struct.pack('<h',int(5000*math.sin(i*2*math.pi*1200/22050))) for i in range(22050*8)))
            assets['ely_hello.wav']=raw.getvalue()
        if actors>1 and (selected is None or any(slot in selected for slot in (1,2,3))):
            raw=io.BytesIO()
            with wave.open(raw,'wb') as f:
                f.setparams((1,2,22050,0,'NONE','not compressed'))
                f.writeframes(b''.join(struct.pack('<h',int(5000*math.sin(i*2*math.pi*(frequency+100)/22050))) for i in range(int(22050*probe_seconds))))
            assets[f'ely_btone{actor:02d}.wav']=raw.getvalue()
    for name in assets:
        if (override/name).exists(): raise RuntimeError(f"Refusing to replace {override/name}")
    for name,data in assets.items():
        p=override/name
        with p.open('xb') as f: created.append(p);f.write(data)
    output=visual.OUT/"shots"/("voice_slots_"+run_name)/("nwn" if engine=="native" else "openwinter")
    seconds=visual.scene_json(scene).get("seconds",60)
    if engine=="native":visual.run_nwn(cfg,mod,user,output,seconds,native_framebuffer=native_framebuffer)
    else:
        extra=["--visual-audio","--visual-voices","--visual-delay","0","--ui",ui_style]
        if quickchat_probe or encumbrance_probe or near_probe:extra.append("--visual-gui")
        visual.run_openwinter(cfg,mod,user,output,seconds,"aluviandarkstar",(1280,720),extra)
        client_audio=json.loads((output/"audio.json").read_text(encoding="utf-8"))
        record_info=client_audio['recording'];notes=client_audio['notes']
    script_log=output/("native.log" if engine=="native" else "godot.log")
    if engine=="native":shutil.copyfile(user/"logs"/"nwclientLog1.txt",script_log)
    script_text=script_log.read_text(encoding="utf-8",errors="replace")
    if "VISUAL|done|1" not in script_text or re.search(r"TOO MANY INSTRUCTIONS|SCRIPT ERROR|\[script error\]|INVALID_PC_CONTEXT",script_text,re.IGNORECASE):
        raise RuntimeError("Voice fixture did not complete successfully")
    if scene=="voiceactors_pc_slots":
        for marker in ("pc.identity.1.level.1.soundset.5100","pc.ready.weapon.22.enemy.1.seen.1"):
            if marker not in script_text:raise RuntimeError("Actual-PC precondition missing: "+marker)
    if scene in ("voiceactors_painpc_slots", "voiceactors_painbow_slots"):
        for marker in ("target.pc.1.name.", ".soundset.5100.maxhp.", "combat.seen.1.enemy.1", "combat.done.hp.", ".dead.0."):
            if marker not in script_text:raise RuntimeError("Actual-PC pain precondition missing: "+marker)
    if scene.startswith("voiceactors_painbow") and "bow.ready.weapon.8.ammo.99" not in script_text:
        raise RuntimeError("Equipped bow/ammunition positive control missing")
    if scene=="voiceactors_painbownpc_slots" and "target.pc.0.name.Bandit.soundset.5100.maxhp.12" not in script_text:
        raise RuntimeError("NPC victim positive control missing")
    if scene=="voiceactors_painfatal_slots" and ("fatal.completed.20" not in script_text or "INVALID_FATAL_CONTEXT" in script_text):
        raise RuntimeError("Twenty valid lethal melee contexts did not complete")
    if entry_probe and "INVALID_ENTRY_ATTACK" in script_text:
        raise RuntimeError("Posted/scripted attack positive control failed")
    if scene=="voiceactors_entrypc_slots" and "entry.completed.10" not in script_text:
        raise RuntimeError("Ten scripted actual-PC entries did not finish")
    if scene=="voiceactors_entryui_slots" and "entry.ui.completed.attacks." not in script_text:
        raise RuntimeError("Posted actual-PC Attack did not finish")
    encumbrance_done="encumberedbag.completed" if scene in ("voiceactors_encumberedbag_slots", "voiceactors_encumbereddrop_slots") else "encumbered.completed"
    if encumbrance_probe and (encumbrance_done not in script_text or "INVALID_" in script_text):
        raise RuntimeError("Encumbrance threshold/acquisition context failed")
    near_done="nearhench.completed.100" if scene=="voiceactors_nearhench_slots" else "near.completed"
    if near_probe and (near_done not in script_text or "INVALID_" in script_text):
        raise RuntimeError("Near-death HP/combat context failed")
    validated=True
finally:
    stop.set()
    if engine=="native":thread.join()
    visual.subprocess.Popen=original
    if recording[0]:
        try: record_info=recording[0].close()
        except Exception as error: record_info={"error":str(error)};print("[audio]",error)
    for p in created:p.unlink()
    with (visual.REPO/"out"/("voice_slots_"+run_name+("_client" if engine=="client" else "")+".json")).open("w",encoding="utf-8",newline="") as f:
        json.dump(dict(engine=engine,uiStyle=ui_style if engine=="client" else "native",validated=validated,nativeFramebuffer=native_framebuffer and engine=="native",helperSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),nativeEffectsMuted=engine=="native" and mute_effects,scriptLog=str(script_log) if script_log else None,fixtureSha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (visual.REPO/"parity"/"visual"/"scenes"/scene).iterdir() if p.suffix in (".nss",".json")},actor_tones=actor_tones,session=session_info,recording=record_info,scene=scene,probe_seconds=probe_seconds,slots=sorted(selected) if selected is not None else mode,pid=target[0],samples=samples,notes=notes),f)
