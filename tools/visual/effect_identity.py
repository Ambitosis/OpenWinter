"""Identify native built-in VFX by replacing installed model names with authored bit markers.

Writes synthetic models only, under the effectidentity scene. No installed model content is copied.
Run this generator, then `visual.py run effectidentity --only nwn`. The marker row encodes the
one-based index in manifest.json: left red anchor, then low-to-high bits (green=1, blue=0).
"""
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCENE = ROOT / "parity/visual/scenes/effectidentity"
names = set()
for prefix in ("v", "fx"):
    result = subprocess.run(["dotnet", "run", "--project", "src/OpenWinter.Tools", "-c", "Release", "--no-build",
                             "--", "list", prefix, "mdl", "--base"], cwd=ROOT, capture_output=True, text=True, check=True)
    names.update(n for n in result.stdout.split() if re.fullmatch(prefix + r"[a-z0-9_]{0,15}", n))
names = sorted(names)
bits = len(names).bit_length()
SCENE.mkdir(parents=True, exist_ok=True)
for index, name in enumerate(names, 1):
    lines = [f"newmodel {name}", f"setsupermodel {name} NULL", "classification Effects", "setanimationscale 1",
             f"beginmodelgeom {name}", f"node dummy {name}", "parent NULL", "endnode"]
    for bit in range(-1, bits):
        color = "1 0 0" if bit == -1 else "0 1 0" if index & (1 << bit) else "0 0 1"
        lines += [f"node emitter bit{bit+1}", f"parent {name}", f"position {(bit - bits/2) * .65} 0 2.5",
                  "orientation 0 0 0 0", "update Single", "render Normal", "blend Lighten", "texture fxpa_white",
                  "xgrid 1", "ygrid 1", "birthrate 1", "lifeExp -1", "sizeStart .45", "sizeEnd .45",
                  f"colorStart {color}", f"colorEnd {color}", "alphaStart 1", "alphaEnd 1", "inherit 1", "endnode"]
    lines += [f"endmodelgeom {name}", f"donemodel {name}", ""]
    with (SCENE / (name + ".mdl")).open("w", encoding="utf-8", newline="") as f:
        f.write("\n".join(lines))
with (SCENE / "manifest.json").open("w", encoding="utf-8", newline="") as f:
    json.dump({str(i): n for i, n in enumerate(names, 1)}, f, indent=2)
    f.write("\n")
print(f"{len(names)} synthetic models, {bits} bits; no installed model content copied.")
