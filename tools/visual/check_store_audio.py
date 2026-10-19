"""Check process-only native and client store cues and the intentional Shift-click upgrade.

Run after spellvoice_audio.py storesound effects --real-mouse and the client
storesound capture with --ui classic --visual-delay 0 --visual-audio.
Identity normalizes gain/latency; GUI button overlap is removed using that same
engine's isolated Cancel recording, not by editing either recorded artifact.
"""
import hashlib
import json
from pathlib import Path
import wave
import numpy as np
from scipy.signal import correlate

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "out"
clips, files, observations = {}, [], {}
for engine, path in [("native", OUT / "vis_storesound_effects"),
                     ("openwinter", OUT / "visual/shots/vis_storesound/openwinter/audio")]:
    metadata, recording = path.with_suffix(".json"), path.with_suffix(".wav")
    files += [metadata, recording]
    data = json.loads(metadata.read_text(encoding="utf-8"))
    if engine == "native":
        assert data["recording"]["session"]["restored"]
    with wave.open(str(recording), "rb") as wav:
        assert wav.getsampwidth() == 2 and wav.getframerate() == 44100
        rate = wav.getframerate()
        pcm = np.frombuffer(wav.readframes(wav.getnframes()), "<i2").reshape(-1, wav.getnchannels()).mean(axis=1)
    observations[engine] = [line[line.index("VISUAL|note|"):] for _, line in data["notes"] if "VISUAL|note|" in line]
    clips[engine] = {}
    for timestamp, line in data["notes"]:
        if "|store_audio|" not in line:
            continue
        at = timestamp - data["recording"]["started"]
        clips[engine][line.split("|")[-1]] = pcm[int((at - .1) * rate):int((at + 2) * rate)]


def score(a, b):
    return float(correlate(a, b, mode="full", method="fft").max() / np.sqrt(np.dot(a, a) * np.dot(b, b)))


def remove_cue(samples, cue):
    nonzero = np.where(abs(cue) > 2)[0]
    cue = cue[nonzero[0]:nonzero[-1] + 1]
    correlation = correlate(samples, cue, mode="valid", method="fft")
    at = correlation.argmax()
    gain = correlation[at] / np.dot(cue, cue)
    residual = samples.copy()
    residual[at:at + len(cue)] -= gain * cue
    return residual


identity = {key: score(clips["native"][key], clips["openwinter"][key])
            for key in ("cancel_prompt", "armor_prompt", "dagger_prompt", "drag_pickup", "cancel", "drag_buy")}
for key in ("armor_buy", "dagger_buy"):
    identity[key + "_without_gui_button"] = score(
        remove_cue(clips["native"][key], clips["native"]["cancel"]),
        remove_cue(clips["openwinter"][key], clips["openwinter"]["cancel"]))
identity["shift_buy_placement_without_pickup"] = score(clips["native"]["drag_buy"],
    remove_cue(clips["openwinter"]["shift_buy"], clips["openwinter"]["cancel_prompt"]))
assert all(value > .995 for value in identity.values()), identity

# Single placement: no doubled drag cue amplitude relative to confirmed placement.
placement = remove_cue(clips["openwinter"]["dagger_buy"], clips["openwinter"]["cancel"])
energy_ratio = float(np.dot(clips["openwinter"]["drag_buy"], clips["openwinter"]["drag_buy"]) / np.dot(placement, placement))
assert .98 < energy_ratio < 1.02, energy_ratio

native_dir = OUT / "visual/shots/vis_storesound_effects/nwn"
client_dir = OUT / "visual/shots/vis_storesound/openwinter"
notes = {}
for engine, directory in [("native", native_dir), ("openwinter", client_dir)]:
    # The audio metadata belongs to this recording; the wrapper's notes.txt can append earlier runs.
    notes[engine] = observations[engine]
    files += [directory / "notes.txt", *directory.glob("*.png")]
assert [x for x in notes["native"] if "shift_bought" not in x] == [x for x in notes["openwinter"] if "shift_bought" not in x]
assert "VISUAL|note|shift_bought|gold=10048" in notes["native"]
assert "VISUAL|note|shift_bought|gold=10042" in notes["openwinter"]
files += list((ROOT / "parity/visual/scenes/storesound").iterdir())
report = {"normalized_waveform_identity": identity, "single_drag_placement_energy_ratio": energy_ratio,
          "notes": notes, "intentional_qol": "Native Shift-click asks; OpenWinter buys once without confirmation.",
          "limits": "Gain and latency normalized. GUI button/pickup removed for overlapping cue identity. No exact mix-timing claim.",
          "sha256": {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.is_file()}}
(OUT / "store-sound-check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="")
print(json.dumps({"identity": identity, "single_drag_placement_energy_ratio": energy_ratio}, indent=2))
