"""Radial light profile for the placelight scene: the floor's light relative to the evenly lit tilelight_white
floor (same tiles, same top-down view), in linear units, against distance from the brazier and the lamp post.

    python tools/visual/lightprofile.py [nwn|openwinter]
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

SHOTS = Path(__file__).resolve().parent.parent.parent / "out" / "visual" / "shots"
side = sys.argv[1] if len(sys.argv) > 1 else "nwn"


def lin(path):
    a = np.asarray(Image.open(path).convert("RGB"), dtype=np.float64) / 255.0
    return (a ** 2.2).mean(axis=2)


lit = lin(SHOTS / "vis_placelight" / side / "top.png")
ref = lin(SHOTS / "vis_tilelight_white" / side / "top.png")
h, w = lit.shape
px_per_m = (h / 2) / (19.0 * np.tan(np.radians(22.5)))
ratio = lit / np.maximum(ref, 1e-4)
yy, xx = np.mgrid[0:h, 0:w]
# Floor away from the furniture rows: the bands above and below them.
floor = (np.abs(yy - h / 2) > 160) & (ref > 0.01)
for name, dx in (("brazier", -5.0), ("lamppost", 6.0)):
    cx = w / 2 + dx * px_per_m
    d = np.hypot(xx - cx, yy - h / 2) / px_per_m
    other = np.hypot(xx - (w / 2 + (6.0 if dx < 0 else -5.0) * px_per_m), yy - h / 2) / px_per_m
    rows = []
    for r in (3.5, 4.5, 6, 8, 10, 12, 15):
        m = floor & (np.abs(d - r) < 0.5) & (other > d + 3)
        rows.append(f"{r:>5}:{np.median(ratio[m]) if m.any() else float('nan'):6.3f}")
    print(f"{side} {name:<9} " + " ".join(rows))
