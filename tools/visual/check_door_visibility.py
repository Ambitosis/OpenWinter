"""Validate black-box room/door controls and retain small evidence, never game assets."""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SHOTS = ROOT / "out/visual/shots"
checks = []
artifacts = {}

def folder(scene, engine):
    p = SHOTS / ("vis_" + scene) / engine
    for f in sorted(p.glob("*.png")):
        if f.stem.endswith("_window"): continue
        artifacts[str(f.relative_to(ROOT))] = hashlib.sha256(f.read_bytes()).hexdigest()
    return p

def notes(scene, engine, kind):
    p = folder(scene, engine) / "notes.txt"
    artifacts[str(p.relative_to(ROOT))] = hashlib.sha256(p.read_bytes()).hexdigest()
    result = {}
    for line in p.read_text(encoding="utf-8").splitlines():
        fields = line.split("|")
        if fields[:3] != ["VISUAL", "note", kind]: continue
        result[fields[3]] = fields[4:]
    return result

def check(ok, label):
    if not ok: raise AssertionError(label)
    checks.append(label)

n = notes("doorreveal_settled", "nwn", "map")
e = notes("doorreveal_settled", "openwinter", "map")
check(len(n) == len(e) == 9, "nine room discovery observations")
for phase in n: check(n[phase] == e[phase], phase + ": all 100 discovery bits agree")
readings = {"native": n, "openwinter": e}
n = notes("doorreveal_settled", "nwn", "reveal")
e = notes("doorreveal_settled", "openwinter", "reveal")
for phase in n:
    a = dict(f.split("=", 1) for f in n[phase]); b = dict(f.split("=", 1) for f in e[phase])
    for key in a:
        if key in ("pc", "npc"):
            check(np.allclose(list(map(float, a[key].split(","))), list(map(float, b[key].split(","))), atol=.02), phase + ": " + key + " position agrees")
        else: check(a[key] == b[key], phase + ": " + key + " agrees")
for scene, kind, native_tag, client_tag in [("roombars", "bars", "reference", "final"), ("roomrange", "map", "reference", "final")]:
    a = notes(scene + "_" + native_tag, "nwn", kind); b = notes(scene + "_" + client_tag, "openwinter", kind)
    check(len(a) == len(b) == 3, scene + ": three observations")
    for phase in a: check(a[phase] == b[phase], scene + ": " + phase + " all readings agree")
check(notes("roomrange45_reference", "nwn", "map") == notes("roomrange_reference", "nwn", "map"), "fog distances 45 and 100 give the same discovery masks")
clicks = {}
for scene, client_tag in [("doorpickside", "harness"), ("doorpickreverse", "final")]:
    clicks[scene] = {}
    for engine, tag in [("nwn", "reference"), ("openwinter", client_tag)]:
        states = notes(scene + "_" + tag, engine, "pick")
        values = {phase: dict(f.split("=", 1) for f in row) for phase, row in states.items()}
        clicks[scene][engine] = values
        for phase, opened in [("a_faded_hover", "0"), ("b_faded_click", "0"), ("c_solid_hover", "0"), ("d_solid_click", "1")]:
            check(values[phase]["open"] == opened, scene + "/" + engine + ": " + phase)
        start = np.array(list(map(float, values["a_faded_hover"]["pc"].split(","))))
        end = np.array(list(map(float, values["b_faded_click"]["pc"].split(","))))
        check(np.linalg.norm(end-start) > .25, scene + "/" + engine + ": faded click reaches ground")
# Human-inspected camera frames are hashed, including posed leaves and lateral offsets.
folder("doorcamera_reference", "nwn"); folder("doorcamera_fixed", "openwinter")
# Constant red panel, no area light or shadow: no guessed texture/color correspondence.
p = folder("dooralpha_gated", "nwn")
opaque = np.asarray(Image.open(p / "a_opaque.png").convert("RGB"))[200:260, 600:680]
faded = np.asarray(Image.open(p / "b_faded.png").convert("RGB"))[400:500, 600:680]
background = np.asarray(Image.open(p / "c_background.png").convert("RGB"))[400:500, 600:680]
check(np.all(opaque == [255,0,0]), "authored native panel opaque positive is constant red")
check(np.all(background == 0), "authored native background is black")
check(np.all(faded == [64,0,0]), "native door fade is 25 percent within one-byte quantization")
p = folder("dooralpha_final", "openwinter")
client_fade = np.asarray(Image.open(p / "b_faded.png").convert("RGB"))[400:500, 600:680]
blend = {"nativeOpaque": [255,0,0], "nativeFaded": [64,0,0], "nativeBackground": [0,0,0],
         "semanticAlpha": .25, "openwinterFadedMedian": np.median(client_fade, axis=(0,1)).tolist(),
         "framebufferMatch": bool(np.all(client_fade == [64,0,0])),
         "limitation": "Godot blends in linear light; native blends encoded RGB. Semantic alpha matches, framebuffer blend does not."}
result = {"checks": checks, "discovery": readings, "clicks": clicks, "fade": blend, "sha256": artifacts,
          "rejected": ["dooralpha_reference and dooralpha_white: positive panel absent (JSON table patch unsupported by visual builder)",
                       "doorpickside_final client: old harness emitted duplicate release/motion state; repeated with corrected harness",
                       "doorreveal_final/gated early NPC samples: explicit AI and more than one NPC perception interval required",
                       "doorpickside_reference trailing reverse phases: open/position preconditions failed; replaced by fresh doorpickreverse scene"]}
out = ROOT / "docs/evidence/door-visibility.json"
out.parent.mkdir(parents=True, exist_ok=True)
with out.open("w", encoding="utf-8", newline="") as f: json.dump(result, f, indent=2); f.write("\n")
print(f"{len(checks)} behavior/measurement checks passed; native framebuffer fade match={blend['framebufferMatch']}; {out}")
