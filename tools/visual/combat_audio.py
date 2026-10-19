"""Which combat sounds the original plays, and when: every sound weaponsounds.2da names is replaced, in the sandbox
only, by its own pure tone; the harness-owned nwmain is recorded and each tone found in the recording.

Optional dependencies: out/audio-probe (pycaw, comtypes), numpy.
Run: python tools/visual/combat_audio.py [scene]        (default combatsound)
     python tools/visual/combat_audio.py levelup --sounds gui_   (every installed gui_* sound instead)
     python tools/visual/combat_audio.py --analyse out/vis_combatsound_tones.json

The tones live in out/visual/user/override only during the run (created exclusively, removed afterwards), so no
game asset is copied or changed. Writes out/vis_<scene>_tones.wav/.json and prints each sound found with the
scene's phase and the seconds since that phase began.
"""
import argparse
import json
import math
import struct
import subprocess
import sys
import threading
import time
import wave
from contextlib import contextmanager
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
sys.path[:0] = [str(REPO / "out" / "audio-probe"), str(HERE)]

RATE = 44100
FIRST, STEP = 400.0, 60.0


def sound_names():
    """Every cb_* sound named in weaponsounds.2da, in a stable order."""
    out = subprocess.run(["dotnet", "run", "--project", str(REPO / "src" / "OpenWinter.Tools"), "-c", "Release", "--no-build", "--", "2da", "weaponsounds", "200"],
                         cwd=REPO, capture_output=True, text=True).stdout
    names = set()
    for line in out.splitlines()[1:]:
        for cell in line.split("\t")[2:]:
            if cell.lower().startswith("cb_"):
                names.add(cell.lower())
    return sorted(names)


def prefixed_sounds(prefix):
    """Every installed sound whose resref starts with prefix (the game's own resources, no module)."""
    out = subprocess.run(["dotnet", "run", "--project", str(REPO / "src" / "OpenWinter.Tools"), "-c", "Release", "--no-build", "--", "list", prefix, "wav", "--base"],
                         cwd=REPO, capture_output=True, text=True).stdout
    return sorted({n.lower() for n in out.split() if n.lower().startswith(prefix.lower())})


def tone_table(names):
    return {n: FIRST + STEP * i for i, n in enumerate(names)}


def write_tone(path, freq, seconds=0.35):
    frames = int(RATE * seconds)
    with path.open("xb") as f, wave.open(f, "wb") as w:
        w.setparams((1, 2, RATE, frames, "NONE", "not compressed"))
        data = bytearray()
        for i in range(frames):
            env = min(1.0, i / (0.01 * RATE), (frames - i) / (0.01 * RATE))
            data += struct.pack("<h", int(16000 * env * math.sin(2 * math.pi * freq * i / RATE)))
        w.writeframes(bytes(data))


def analyse(wav_path, tones, notes, started):
    import numpy as np
    with wave.open(str(wav_path), "rb") as w:
        rate, channels = w.getframerate(), w.getnchannels()
        pcm = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32)
    mono = pcm.reshape(-1, channels).mean(axis=1)
    n, hop = 4096, 1024
    window = np.hanning(n)
    freqs = np.fft.rfftfreq(n, 1 / rate)
    names = list(tones)
    want = np.array([tones[k] for k in names])
    bins = np.searchsorted(freqs, want)
    events, active = [], {}
    for start in range(0, len(mono) - n, hop):
        spec = np.abs(np.fft.rfft(mono[start:start + n] * window))
        floor = np.median(spec) + 1e-6
        t = (start + n / 2) / rate
        for i, b in enumerate(bins):
            peak = spec[max(0, b - 2):b + 3].max()
            # A tone towers over the frame's spectrum; real effects are broadband.
            on = peak > 60 * floor and peak > 2e5
            if on and names[i] not in active:
                active[names[i]] = t
            elif not on and names[i] in active:
                begun = active.pop(names[i])
                if t - begun >= 0.1:
                    events.append((begun, names[i]))
    events.sort()
    # Phase markers from the scene's notes ("VISUAL|note|<scene>|phase|<name>"), on the recording's clock.
    phases = [(stamp - started, line.split("phase|", 1)[1].strip()) for stamp, line in notes if "|phase|" in line]
    rows = []
    for t, name in events:
        phase = max((p for p in phases if p[0] <= t), default=(0, "-"))
        rows.append((phase[1], round(t - phase[0], 2), name))
    return rows


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("scene", nargs="?", default="combatsound")
    ap.add_argument("variant", nargs="?", help="one of the scene's variants")
    ap.add_argument("--analyse", help="re-analyse an earlier run's .json")
    ap.add_argument("--sounds", help="a resref prefix (gui_, as_, ...): tone every installed sound it starts instead of weaponsounds.2da's")
    ap.add_argument("--real-mouse", action="store_true", help="the real pointer on the development monitor for clicks the original only takes from it (visual.py --real-mouse)")
    a = ap.parse_args()
    if a.analyse:
        info = json.loads(Path(a.analyse).read_text(encoding="utf-8"))
        for row in analyse(Path(info["recording"]["path"]), info["tones"], info["notes"], info["recording"]["started"]):
            print(*row, sep="\t")
        return
    import visual
    from process_audio import ProcessRecording
    cfg = visual.config()
    if a.real_mouse and (cfg.get("devDisplay") != "DISPLAY2" or not visual.dev_display(cfg)):
        ap.error("--real-mouse requires the configured DISPLAY2 monitor")
    mods = visual.build(a.scene)
    mod = next(m for m in mods if not a.variant or m.stem == f"vis_{a.scene}_{a.variant}")
    sj = visual.scene_json(a.scene)
    user = visual.prepare_user(cfg, sj.get("character", "aluviandarkstar"), (1280, 720), "plain", chat_log=sj.get("chatLog", False))
    with (user / "settings.tml").open("a", encoding="utf-8", newline="") as f:
        # Voice-set tones (--sounds vs_...) need the voices channel; otherwise it stays quiet.
        voices = "1.0" if (a.sounds or "").lower().startswith("vs_") else "0.0"
        f.write(f"\n[sound.music]\nvolume=0.0\n[sound.voices]\nvolume={voices}\n[sound.effects]\nvolume=1.0\n")
    tones = tone_table(prefixed_sounds(a.sounds) if a.sounds else sound_names())
    run = f"{mod.stem}_tones"
    wav_path = REPO / "out" / f"{run}.wav"
    notes, recording = [], [None]

    @contextmanager
    def overrides():
        made = []
        try:
            (user / "override").mkdir(exist_ok=True)
            for name, freq in tones.items():
                p = user / "override" / f"{name}.wav"
                if p.exists():
                    raise RuntimeError(f"refusing to replace an existing override: {p}")
                write_tone(p, freq)
                made.append(p)
            yield
        finally:
            for p in made:
                p.unlink(missing_ok=True)

    original = visual.subprocess.Popen

    def launch(*args, **kw):
        proc = original(*args, **kw)
        recording[0] = ProcessRecording(proc, wav_path, audible=True)
        kill = proc.kill

        def kill_owned():
            try:
                recording[0].close()
            finally:
                kill()
        proc.kill = kill_owned
        return proc

    stop = threading.Event()

    def follow_log():
        log, seen = user / "logs" / "nwclientLog1.txt", 0
        while not stop.is_set():
            if log.exists():
                lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
                for line in lines[seen:]:
                    if "VISUAL|" in line:
                        notes.append([time.time(), line])
                seen = len(lines)
            time.sleep(0.02)

    visual.subprocess.Popen = launch
    # A previous run's log would be read as this run's notes.
    (user / "logs" / "nwclientLog1.txt").unlink(missing_ok=True)
    reader = threading.Thread(target=follow_log)
    with overrides():
        reader.start()
        try:
            visual.run_nwn(cfg, mod, user, visual.OUT / "shots" / run / "nwn", int(sj.get("seconds", 60)) + 60, real_mouse=a.real_mouse, native_framebuffer=True)
        finally:
            stop.set()
            reader.join()
            visual.subprocess.Popen = original
    info = recording[0].close() if recording[0] else None
    (REPO / "out" / f"{run}.json").write_text(json.dumps(dict(scene=a.scene, tones=tones, recording=info, notes=notes)), encoding="utf-8")
    for row in analyse(wav_path, tones, notes, info["started"]):
        print(*row, sep="\t")


if __name__ == "__main__":
    main()
