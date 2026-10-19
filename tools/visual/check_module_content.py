"""Check authored red-to-blue module-only replacement, without claiming material parity."""
import hashlib
import json
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[2]
shots = repo / "out/visual/shots/vis_modcontenta"
checks = []
for engine in ("nwn", "openwinter"):
    for shot, channel in (("red_source", 0), ("blue_destination", 2)):
        path = shots / engine / (shot + ".png")
        with Image.open(path) as frame:
            # Fully authored marker; discard GUI and unrelated low-saturation floor pixels.
            pixels = frame.convert("RGB").crop((700, 180, 1050, 560)).getdata()
            dominant = sum(p[channel] > max(p[(channel + 1) % 3], p[(channel + 2) % 3]) + 20 for p in pixels)
        assert dominant > 5000, (engine, shot, dominant)
        checks.append({"engine": engine, "shot": shot, "dominantColorPixels": dominant,
                       "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
with (repo / "parity/visual/scenes/modcontenta/evidence.json").open("w", encoding="utf-8", newline="") as stream:
    json.dump({"checks": checks, "scope": "module resource replacement; material brightness differs"}, stream, indent=2)
    stream.write("\n")
print("All four inspected frames contain the expected module-only marker color.")
