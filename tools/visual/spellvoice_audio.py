"""Meter only the harness-owned native process to compare spell chant lifetimes.

Optional dependencies: pip install --target out/audio-probe pycaw comtypes
Run: python tools/visual/spellvoice_audio.py spellvoice_interrupt isolated_nofail
Modes: voice (voice volume only), effects, all, isolated (mute conjure/cast SFX),
       isolated_nofail (also mute sff_spellfail to expose the underlying chant).
Synthetic silent overrides exist only in out/visual/user/override during the run.
Only the harness-owned process is recorded, never microphone or desktop audio.
Its session mute is temporarily lifted and restored before harness termination.
"""
import sys, time, threading, json, wave, argparse
from contextlib import contextmanager
import shutil
from pathlib import Path
sys.path[:0] = [str(Path('out/audio-probe').resolve()), str(Path('tools/visual').resolve())]
import visual
from pycaw.pycaw import AudioUtilities, IAudioMeterInformation
import comtypes
from process_audio import ProcessRecording

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('scene', nargs='?', default='spellvoice')
parser.add_argument('mode', nargs='?', default='all', choices=('voice', 'effects', 'all', 'isolated', 'isolated_nofail'))
parser.add_argument('variant', nargs='?')
parser.add_argument('--native-framebuffer', action='store_true', help='Retain native PrintScreen frames and raw provenance alongside audio')
parser.add_argument('--real-mouse', action='store_true', help='Use explicitly authorized temporary pointer control for native UI audio checks')
args = parser.parse_args()
scene, tag = args.scene, args.mode
cfg = visual.config()
mods = visual.build(scene)
variant = args.variant
if variant: mods = [m for m in mods if m.stem == 'vis_'+scene+'_'+variant]
if len(mods) != 1: raise SystemExit('Choose exactly one scene variant as the third argument')
mod = mods[0]
sj = visual.scene_json(scene)
character = sj.get('character','aluviandarkstar')
user = visual.prepare_user(cfg, character, (1280,720), 'plain', text_bubble_mode=sj.get('textBubbleMode'), large_font=sj.get('largeFont'), ui_scale=sj.get('uiScale'))
if mod.with_suffix('.bic').exists(): shutil.copy(mod.with_suffix('.bic'),user/'localvault'/(character+'.bic'))
if 'difficulty' in sj:
    difficulty = int(sj['difficulty'])
    if difficulty not in (1, 2, 3, 4): raise ValueError('difficulty must be 1..4')
    with (user/'settings.tml').open('a', encoding='utf-8', newline='') as f:
        f.write(f'\n[ruleset]\ndifficulty = {difficulty}\n')
run_name = mod.stem+'_'+tag
@contextmanager
def silent_overrides():
    muted = []
    try:
        if tag.startswith('isolated'):
            (user/'override').mkdir(exist_ok=True)
            for sound in ['sco_mehancold03', 'sca_outsonic'] + (['sff_spellfail'] if tag == 'isolated_nofail' else []):
                p = user/'override'/(sound+'.wav')
                if p.exists(): raise RuntimeError(f'Refusing to replace existing override: {p}')
                # Exclusive creation protects other experiments in this sandbox.
                with p.open('xb') as output:
                    muted.append(p)
                    with wave.open(output, 'wb') as f:
                        f.setparams((1,2,44100,4410,'NONE','not compressed'))
                        f.writeframes(bytes(8820))
        yield
    finally:
        for p in muted: p.unlink()
with (user/'settings.tml').open('a', encoding='utf-8', newline='') as f:
    f.write('\n[sound.music]\nvolume=0.0\n[sound.effects]\nvolume=' + ('0.0' if tag == 'voice' else '1.0') + '\n[sound.voices]\nvolume=' + ('0.0' if tag == 'effects' else '1.0') + '\n')
samples, notes = [], []
stop = threading.Event()
target = [None]
recording = [None]
record_info = None
original = visual.subprocess.Popen
def launch(*a, **kw):
    proc = original(*a, **kw)
    target[0] = proc.pid
    recording[0] = ProcessRecording(proc,visual.REPO/'out'/(run_name+'.wav'),audible=True)
    original_kill = proc.kill
    def kill_owned():
        try: recording[0].close()
        finally: original_kill()
    proc.kill = kill_owned
    return proc
visual.subprocess.Popen = launch
def meter():
    comtypes.CoInitialize()
    m, line_count = None, 0
    while not stop.is_set():
        if m is None and target[0]:
            session = AudioUtilities.GetProcessSession(target[0])
            if session: m = session._ctl.QueryInterface(IAudioMeterInformation)
        if m: samples.append([time.time(), m.GetPeakValue()])
        log = user/'logs'/'nwclientLog1.txt'
        if target[0] and log.exists():
            lines = log.read_text(encoding='utf-8', errors='replace').splitlines()
            if len(lines) < line_count: line_count = 0
            for line in lines[line_count:]:
                if 'VISUAL|' in line:
                    notes.append([time.time(), line])
            line_count = len(lines)
        time.sleep(.01 if m else .2)
    comtypes.CoUninitialize()
t = threading.Thread(target=meter)
with silent_overrides():
    t.start()
    try:
        visual.run_nwn(cfg, mod, user, visual.OUT/'shots'/run_name/'nwn', int(sj.get('seconds',60))+60,
                       native_framebuffer=args.native_framebuffer, real_mouse=args.real_mouse)
    finally:
        stop.set(); t.join(); visual.subprocess.Popen = original
        if recording[0]: record_info = recording[0].close()
with (Path('out')/(run_name+'.json')).open('w',encoding='utf-8',newline='') as f:
    json.dump(dict(pid=target[0],scene=scene,variant=variant,mode=tag,nativeFramebuffer=args.native_framebuffer,
                   recording=record_info,samples=samples,notes=notes), f)
print('peak', max((s[1] for s in samples), default=0), flush=True)
