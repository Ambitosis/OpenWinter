"""Radial light profile of the torchlight scene: the floor's linear brightness around the torch-bearer seen from
above, original against OpenWinter (same floor, same view, so the ratio is what the creature light has to change).

    python tools/visual/torchprofile.py [shot directory name, default vis_torchlight]
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

SHOTS = Path(__file__).resolve().parent.parent.parent / "out" / "visual" / "shots"
scene = sys.argv[1] if len(sys.argv) > 1 else "vis_torchlight"


def lin(side):
    a = np.asarray(Image.open(SHOTS / scene / side / "top.png").convert("RGB"), dtype=np.float64) / 255.0
    return (a ** 2.2).mean(axis=2)


nwn, ely = lin("nwn"), lin("openwinter")
h, w = nwn.shape
px_per_m = (h / 2) / (19.0 * np.tan(np.radians(22.5)))
cx, cy = w / 2 - 5.0 * px_per_m, h / 2          # the torch-bearer, 5 m west of the camera's anchor
yy, xx = np.mgrid[0:h, 0:w]
d = np.hypot(xx - cx, yy - cy) / px_per_m
# Plain floor: leave out the creatures (within 1 m) and the benches and tables (rows around the middle band
# are mostly furniture); take the median of each ring so the remaining furniture matters little.
print(f"{'r (m)':>6} {'nwn':>8} {'openwinter':>8} {'ratio':>6}")
for r in (1.5, 2, 3, 4, 5, 6, 8, 10, 12, 14):
    m = (np.abs(d - r) < 0.35) & (np.abs(yy - cy) > 2.2 * px_per_m)
    if not m.any():
        continue
    a, b = np.median(nwn[m]), np.median(ely[m])
    print(f"{r:>6} {a:8.4f} {b:8.4f} {b / max(a, 1e-5):6.2f}")
