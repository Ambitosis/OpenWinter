"""Record a visual scene's sound on either engine and measure it: tone events, levels, and which music plays when.

Optional dependencies: out/audio-probe (pycaw, comtypes), numpy; ffmpeg on PATH for music references.

  python tools/visual/audio_scene.py run musicbattle --engine nwn --music 1 --effects 0 --voices 0
  python tools/visual/audio_scene.py run soundobj --engine openwinter --effects 1
  python tools/visual/audio_scene.py tones out/vis_soundobj_nwn.json
  python tools/visual/audio_scene.py levels out/vis_soundobj_nwn.json --freq 500
  python tools/visual/audio_scene.py music out/vis_musicbattle_nwn.json --refs mus_ruralday1,mus_bat_rural1

A scene may ship tones.json: {"resref": {"freq": 500, "seconds": 2.0, "amp": 0.5}}. Each becomes a pure-tone WAV
in the sandbox user folder's override (out/visual/user/override) for the run only: created exclusively, removed
afterwards, so no game asset is copied or changed. Sound objects in the scene can name resrefs that exist only
there, and installed WAV resrefs (ambient sounds, footsteps) are shadowed only in the sandbox.

Both engines play through the machine's speakers while they are recorded (the recorders capture the process's own
mix, which needs a live output device), so `run` refuses unless --audible is given. Analyses of
earlier recordings (tones, levels, phases, music, envelope) are silent.

The original runs under the nwmain lock (visual.nwmain_lock_path()) with native framebuffer
shots; only the harness-owned process is recorded (process_audio.py). OpenWinter runs through visual.py with
--visual-audio. Both write a JSON (recording, notes, tones) the analysers read: out/vis_<scene>_<engine>.json.
"""
import argparse
import json
import math
import os
import shutil
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


# ---------------------------------------------------------------- tones

def write_tone(path, freq, seconds, amp=0.5, channels=1):
    """A pure tone with 10 ms ramps, 16-bit 44.1 kHz (stereo when channels=2: the original plays ambient
    sound files as stereo, so a mono one runs at double speed and an octave up)."""
    frames = int(RATE * seconds)
    ramp = 0.01 * RATE
    data = bytearray()
    for i in range(frames):
        env = min(1.0, i / ramp, (frames - i) / ramp)
        sample = struct.pack("<h", int(32767 * amp * env * math.sin(2 * math.pi * freq * i / RATE)))
        data += sample * channels
    with path.open("xb") as f, wave.open(f, "wb") as w:
        w.setparams((channels, 2, RATE, frames, "NONE", "not compressed"))
        w.writeframes(bytes(data))


@contextmanager
def tone_overrides(user, tones):
    made = []
    try:
        (user / "override").mkdir(exist_ok=True)
        for name, spec in tones.items():
            p = user / "override" / f"{name}.wav"
            if p.exists():
                raise RuntimeError(f"refusing to replace an existing override: {p}")
            write_tone(p, spec["freq"], spec.get("seconds", 1.0), spec.get("amp", 0.5), spec.get("channels", 1))
            made.append(p)
        yield
    finally:
        for p in made:
            p.unlink(missing_ok=True)


@contextmanager
def nwmain_lock(branch):
    import visual
    lock = visual.nwmain_lock_path()
    while lock.exists():
        print("[audio] waiting for", lock)
        time.sleep(10)
    lock.write_text(branch, encoding="utf-8")
    try:
        yield
    finally:
        lock.unlink(missing_ok=True)


def current_branch():
    r = subprocess.run(["git", "-C", str(REPO), "branch", "--show-current"], capture_output=True, text=True)
    return r.stdout.strip() or "audio-scene"


def volume_settings(music, effects, voices):
    return f"\n[sound.music]\nvolume={music}\n[sound.voices]\nvolume={voices}\n[sound.effects]\nvolume={effects}\n"


# ---------------------------------------------------------------- running

def run_nwn(scene, a, tones):
    # Everything that touches the shared sandbox (settings.tml, vault, override) happens under the lock.
    with nwmain_lock(a.branch or current_branch()):
        return _run_nwn(scene, a, tones)


def _run_nwn(scene, a, tones):
    import visual
    from process_audio import ProcessRecording
    cfg = visual.config()
    mods = visual.build(scene)
    mod = next(m for m in mods if not a.variant or m.stem == f"vis_{scene}_{a.variant}")
    sj = visual.scene_json(scene)
    character = sj.get("character", "aluviandarkstar")
    user = visual.prepare_user(cfg, character, (1280, 720), "plain", chat_log=sj.get("chatLog", False))
    if mod.with_suffix(".bic").exists():
        shutil.copy(mod.with_suffix(".bic"), user / "localvault" / (character + ".bic"))
    with (user / "settings.tml").open("a", encoding="utf-8", newline="") as f:
        f.write(volume_settings(a.music, a.effects, a.voices))
    run = f"{mod.stem}_nwn"
    wav_path = REPO / "out" / f"{run}.wav"
    notes, recording = [], [None]
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

    (user / "logs" / "nwclientLog1.txt").unlink(missing_ok=True)
    reader = threading.Thread(target=follow_log)
    with tone_overrides(user, tones):
        visual.subprocess.Popen = launch
        reader.start()
        try:
            visual.run_nwn(cfg, mod, user, visual.OUT / "shots" / run / "nwn", int(sj.get("seconds", 60)) + 60, native_framebuffer=True)
        finally:
            stop.set()
            reader.join()
            visual.subprocess.Popen = original
    info = recording[0].close() if recording[0] else None
    out = REPO / "out" / f"{run}.json"
    out.write_text(json.dumps(dict(engine="nwn", scene=scene, tones=tones, recording=info, notes=notes,
                                   volumes=dict(music=a.music, effects=a.effects, voices=a.voices))), encoding="utf-8")
    print("[audio] wrote", out)
    return out


def run_openwinter(scene, a, tones):
    import visual
    cfg = visual.config()
    user = visual.OUT / "user"
    stem = f"vis_{scene}" + (f"_{a.variant}" if a.variant else "")
    args = f"--visual-audio --visual-volumes music={a.music},effects={a.effects},voices={a.voices},ambient={a.effects}"
    if a.openwinter_args:
        args += " " + a.openwinter_args
    user.mkdir(parents=True, exist_ok=True)
    with tone_overrides(user, tones):
        subprocess.run([sys.executable, str(HERE / "visual.py"), "run", scene, "--only", "openwinter", f"--openwinter-args={args}"],
                       cwd=REPO, check=False)
    shots = visual.OUT / "shots" / stem / "openwinter"
    info = json.loads((shots / "audio.json").read_text(encoding="utf-8"))
    out = REPO / "out" / f"{stem}_openwinter.json"
    out.write_text(json.dumps(dict(engine="openwinter", scene=scene, tones=tones, recording=info["recording"], notes=info["notes"],
                                   volumes=dict(music=a.music, effects=a.effects, voices=a.voices))), encoding="utf-8")
    print("[audio] wrote", out)
    return out


# ---------------------------------------------------------------- analysis

def load(json_path):
    import numpy as np
    info = json.loads(Path(json_path).read_text(encoding="utf-8"))
    rec = info["recording"]
    with wave.open(rec["path"], "rb") as w:
        rate, channels = w.getframerate(), w.getnchannels()
        pcm = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
    pcm = pcm.reshape(-1, channels)
    phases = []
    for stamp, line in info.get("notes", []):
        if "|phase|" in line:
            phases.append((stamp - rec["started"], line.split("|phase|", 1)[1].strip()))
    return info, rate, pcm, phases


def phase_at(phases, t):
    p = max((p for p in phases if p[0] <= t), default=(0.0, "-"))
    return p[1], t - p[0]


NFFT = 4096


def stft(mono, rate, n=None, hop=1024):
    import numpy as np
    n = n or NFFT
    window = np.hanning(n)
    frames = (len(mono) - n) // hop
    idx = np.arange(n)[None, :] + hop * np.arange(frames)[:, None]
    spec = np.abs(np.fft.rfft(mono[idx] * window, axis=1)) ** 2
    times = (np.arange(frames) * hop + n / 2) / rate
    return spec, times, np.fft.rfftfreq(n, 1 / rate), float((window ** 2).sum()), n


def band_amp(spec, freqs, wsum, n, lo, hi):
    """Amplitude (full scale = 1) of a sinusoid whose power lies in [lo, hi] Hz."""
    import numpy as np
    sel = (freqs >= lo) & (freqs <= hi)
    return np.sqrt(4 * spec[:, sel].sum(axis=1) / (n * wsum))


def db(x):
    import numpy as np
    return 20 * np.log10(np.maximum(x, 1e-7))


def tone_events(json_path, min_db=-60.0):
    """Every tone's sounding intervals: start, end, peak level per channel, measured frequency."""
    import numpy as np
    info, rate, pcm, phases = load(json_path)
    mono = pcm.mean(axis=1)
    spec, times, freqs, wsum, n = stft(mono, rate)
    specs_lr = [stft(pcm[:, c], rate)[0] for c in range(pcm.shape[1])]
    rows = []
    for name, t in info["tones"].items():
        f0 = t["freq"]
        spread = t.get("search", 0.04)
        lo, hi = f0 * (1 - spread), f0 * (1 + spread)
        amp = band_amp(spec, freqs, wsum, n, lo, hi)
        on = db(amp) > min_db
        i = 0
        while i < len(on):
            if not on[i]:
                i += 1
                continue
            j = i
            while j < len(on) and on[j]:
                j += 1
            seg = slice(i, j)
            k = i + int(np.argmax(amp[seg]))
            sel = (freqs >= lo) & (freqs <= hi)
            band = spec[k, sel]
            b = int(np.argmax(band))
            # Parabolic interpolation of the peak bin on log power.
            if 0 < b < len(band) - 1:
                y0, y1, y2 = np.log(band[b - 1] + 1e-20), np.log(band[b] + 1e-20), np.log(band[b + 1] + 1e-20)
                d = 0.5 * (y0 - y2) / (y0 - 2 * y1 + y2)
            else:
                d = 0.0
            freq = freqs[sel][b] + d * (freqs[1] - freqs[0])
            lr = [float(db(band_amp(s, freqs, wsum, n, lo, hi)[k])) for s in specs_lr]
            start, end = times[i], times[j - 1]
            ph, since = phase_at(phases, start)
            rows.append(dict(tone=name, start=round(float(start), 3), end=round(float(end), 3), dur=round(float(end - start), 3),
                             peak_db=round(float(db(amp[k])), 2), median_db=round(float(np.median(db(amp[seg]))), 2),
                             lr_db=[round(x, 2) for x in lr], freq=round(float(freq), 1), phase=ph, since=round(float(since), 3)))
            i = j
    rows.sort(key=lambda r: r["start"])
    return rows


def tone_levels(json_path, freq, step=0.25, spread=0.04):
    """A tone's level over time (median dB per step), labelled with the scene phase."""
    import numpy as np
    info, rate, pcm, phases = load(json_path)
    spec, times, freqs, wsum, n = stft(pcm.mean(axis=1), rate)
    lr = [band_amp(stft(pcm[:, c], rate)[0], freqs, wsum, n, freq * (1 - spread), freq * (1 + spread)) for c in range(pcm.shape[1])]
    amp = band_amp(spec, freqs, wsum, n, freq * (1 - spread), freq * (1 + spread))
    out = []
    t = 0.0
    while t < times[-1]:
        sel = (times >= t) & (times < t + step)
        if sel.any():
            ph, since = phase_at(phases, t)
            out.append((round(t, 2), ph, round(since, 2), round(float(np.median(db(amp[sel]))), 2),
                        [round(float(np.median(db(c[sel]))), 2) for c in lr]))
        t += step
    return out


def phase_levels(json_path, freq, spread=0.04, skip=0.3):
    """Median level of a tone over each scene phase (the first `skip` seconds of a phase left out)."""
    import numpy as np
    info, rate, pcm, phases = load(json_path)
    spec, times, freqs, wsum, n = stft(pcm.mean(axis=1), rate)
    amp = db(band_amp(spec, freqs, wsum, n, freq * (1 - spread), freq * (1 + spread)))
    lr = [db(band_amp(stft(pcm[:, c], rate)[0], freqs, wsum, n, freq * (1 - spread), freq * (1 + spread))) for c in range(pcm.shape[1])]
    out = []
    for k, (t0, name) in enumerate(phases):
        t1 = phases[k + 1][0] if k + 1 < len(phases) else times[-1]
        sel = (times >= t0 + skip) & (times < t1)
        if sel.any():
            out.append((name, round(t0, 2), round(t1 - t0, 2), round(float(np.median(amp[sel])), 2),
                        [round(float(np.median(c[sel])), 2) for c in lr]))
    return out


def decode(resref, rate, ext):
    """An installed music/sound resource decoded to mono float at `rate` (temporary files under out/, removed)."""
    import numpy as np
    tmp = REPO / "out" / "audio-refs"
    tmp.mkdir(parents=True, exist_ok=True)
    raw = tmp / f"{resref}.{ext}"
    pcm_path = tmp / f"{resref}_{rate}.f32"
    if not pcm_path.exists():
        subprocess.run(["dotnet", "run", "--project", str(REPO / "src" / "OpenWinter.Tools"), "-c", "Release", "--no-build", "--",
                        "resource", resref, ext, str(raw)], cwd=REPO, check=True, capture_output=True)
        data = raw.read_bytes()
        if data[:8] == b"BMU V1.0":
            data = data[8:]
        raw.write_bytes(data)
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(raw), "-ac", "1", "-ar", str(rate), "-f", "f32le", str(pcm_path)], check=True)
        raw.unlink(missing_ok=True)
    return np.fromfile(pcm_path, dtype=np.float32)


def music_segments(json_path, refs, rate=4000, window=2.0, hop=1.0, max_ref=180.0, threshold=0.5):
    """For each window of the recording: the best-matching reference, the offset into it, and the gain."""
    import numpy as np
    info, rec_rate, pcm, phases = load(json_path)
    mono = pcm.mean(axis=1)
    # The scene's tones are cut out (and everything above the analysis band) so they cannot pass for music,
    # then the recording is decimated to the analysis rate.
    spec = np.fft.rfft(mono)
    freqs = np.fft.rfftfreq(len(mono), 1 / rec_rate)
    spec[freqs > rate * 0.45] = 0
    for t in info.get("tones", {}).values():
        for f0 in (t["freq"], 2 * t["freq"]):
            half = f0 * t.get("search", 0.0) + 25
            spec[(freqs > f0 - half) & (freqs < f0 + half)] = 0
    smooth = np.fft.irfft(spec, len(mono))
    factor = rec_rate / rate
    x = np.interp(np.arange(0, len(smooth) / factor) * factor, np.arange(len(smooth)), smooth).astype(np.float32)
    references = {}
    for name in refs:
        ext = "wav" if name.startswith("mus_sbat") else "bmu"
        r = decode(name, rate, ext)[: int(max_ref * rate)]
        references[name] = r
    w = int(window * rate)
    rows = []
    for name, r in references.items():
        size = 1 << int(math.ceil(math.log2(len(r) + w)))
        R = np.fft.rfft(r, size)
        csum = np.concatenate([[0.0], np.cumsum(r.astype(np.float64) ** 2)])
        references[name] = (r, R, csum, size)
    t = 0.0
    while (t + window) * rate < len(x):
        seg = x[int(t * rate): int(t * rate) + w].astype(np.float64)
        seg = seg - seg.mean()
        energy = np.sqrt((seg ** 2).sum())
        rms = energy / math.sqrt(w)
        best = ("-", 0.0, 0.0, 0.0)
        if rms > 1e-4:
            for name, (r, R, csum, size) in references.items():
                corr = np.fft.irfft(np.conj(np.fft.rfft(seg, size)) * R, size)[: len(r) - w]
                norms = np.sqrt(csum[w: len(r)] - csum[: len(r) - w])
                ncc = corr / (energy * np.maximum(norms, 1e-9))
                i = int(np.argmax(ncc))
                if ncc[i] > best[1]:
                    gain = rms / max(norms[i] / math.sqrt(w), 1e-9)
                    best = (name, float(ncc[i]), i / rate, float(gain))
        ph, since = phase_at(phases, t)
        name = best[0] if best[1] >= threshold else "?"
        rows.append(dict(t=round(t, 2), phase=ph, since=round(since, 2), rms_db=round(float(db(rms)), 1), ref=name,
                         ncc=round(best[1], 3), offset=round(best[2], 2), gain_db=round(float(db(best[3])), 1) if best[1] >= threshold else None))
        t += hop
    return rows


def envelope(json_path, step=0.1):
    """Overall RMS level (dB) per step with phase labels."""
    import numpy as np
    info, rate, pcm, phases = load(json_path)
    mono = pcm.mean(axis=1)
    n = int(step * rate)
    out = []
    for i in range(0, len(mono) - n, n):
        t = i / rate
        ph, since = phase_at(phases, t)
        out.append((round(t, 2), ph, round(since, 2), round(float(db(np.sqrt((mono[i:i + n] ** 2).mean()))), 1)))
    return out


# ---------------------------------------------------------------- command line

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("scene")
    r.add_argument("--variant")
    r.add_argument("--engine", choices=["nwn", "openwinter"], required=True)
    r.add_argument("--music", default="0.0")
    r.add_argument("--effects", default="0.0")
    r.add_argument("--voices", default="0.0")
    r.add_argument("--branch", help="name written into the nwmain lock (default: the current git branch)")
    r.add_argument("--openwinter-args", default="")
    r.add_argument("--audible", action="store_true", help="confirm the run may play through the speakers")
    for name in ("tones", "levels", "phases", "music", "envelope"):
        p = sub.add_parser(name)
        p.add_argument("json")
        p.add_argument("--nfft", type=int, default=4096, help="FFT size (16384 separates tones a few hertz apart)")
        if name in ("levels", "phases"):
            p.add_argument("--freq", type=float, required=True)
            p.add_argument("--step", type=float, default=0.25)
            p.add_argument("--spread", type=float, default=0.04)
        if name == "music":
            p.add_argument("--refs", required=True)
            p.add_argument("--threshold", type=float, default=0.5)
        if name == "tones":
            p.add_argument("--min-db", type=float, default=-60.0)
        if name == "envelope":
            p.add_argument("--step", type=float, default=0.5)
    a = ap.parse_args()
    global NFFT
    NFFT = getattr(a, "nfft", NFFT)
    if a.cmd == "run" and not a.audible:
        ap.error("recording plays through the speakers; pass --audible to confirm")
    if a.cmd == "run":
        tones_file = REPO / "parity" / "visual" / "scenes" / a.scene / "tones.json"
        tones = json.loads(tones_file.read_text(encoding="utf-8")) if tones_file.exists() else {}
        (run_nwn if a.engine == "nwn" else run_openwinter)(a.scene, a, tones)
    elif a.cmd == "tones":
        for row in tone_events(a.json, a.min_db):
            print(json.dumps(row))
    elif a.cmd == "levels":
        for row in tone_levels(a.json, a.freq, a.step, a.spread):
            print(*row, sep="\t")
    elif a.cmd == "phases":
        for row in phase_levels(a.json, a.freq, a.spread):
            print(*row, sep="\t")
    elif a.cmd == "music":
        for row in music_segments(a.json, a.refs.split(","), threshold=a.threshold):
            print(json.dumps(row))
    elif a.cmd == "envelope":
        for row in envelope(a.json, a.step):
            print(*row, sep="\t")


if __name__ == "__main__":
    main()
