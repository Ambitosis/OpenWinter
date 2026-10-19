"""Check recorded output for each door action, using the scene's UTC operation markers.

This checks audible routing and normalized waveform identity, not identical gain or timing.
Run after spellvoice_audio.py dooraudio effects and visual.py's --visual-audio capture.
"""
import array
import json
from pathlib import Path
import wave
import numpy as np
from scipy.signal import correlate

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "out"
results = {}
clips = {}
for engine, metadata, recording in [
    ("native", OUT / "vis_dooraudio_effects.json", OUT / "vis_dooraudio_effects.wav"),
    ("openwinter", OUT / "visual/shots/vis_dooraudio/openwinter/audio.json", OUT / "visual/shots/vis_dooraudio/openwinter/audio.wav"),
]:
    info = json.loads(metadata.read_text(encoding="utf-8"))
    with wave.open(str(recording), "rb") as wav:
        assert wav.getsampwidth() == 2
        rate, channels = wav.getframerate(), wav.getnchannels()
        pcm = array.array("h", wav.readframes(wav.getnframes()))
    markers = {line.split("door_audio|", 1)[1]: timestamp - info["recording"]["started"]
               for timestamp, line in info["notes"] if "VISUAL|note|door_audio|" in line}
    readings = {}
    clips[engine] = {}
    for state in ("Opened", "Closed", "Locked", "Destroyed"):
        start = markers[state]
        samples = pcm[max(0, int((start - .1) * rate)) * channels:int((start + 3.5) * rate) * channels]
        peak = max(map(abs, samples), default=0)
        rms = (sum(x*x for x in samples) / max(1, len(samples))) ** .5
        assert peak > 100 and rms > 10, (engine, state, peak, rms)
        clips[engine][state] = np.asarray(samples, dtype=float).reshape(-1, channels).mean(axis=1)
        readings[state] = dict(marker_seconds=round(start, 3), peak=peak, rms=round(rms, 2))
    results[engine] = readings
identity = {}
for state in ("Opened", "Closed", "Locked", "Destroyed"):
    native, client = clips["native"][state], clips["openwinter"][state]
    score = float(correlate(native, client, mode="full", method="fft").max()
                  / np.sqrt(np.dot(native, native) * np.dot(client, client)))
    assert score > .95, (state, "waveforms differ", score)
    identity[state] = round(score, 6)
results["normalized_waveform_correlation"] = identity
path = OUT / "door-audio-check.json"
path.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8", newline="")
print(json.dumps(results, indent=2))
