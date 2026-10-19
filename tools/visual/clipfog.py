"""The original's distance ("clip") fog from fogcal: the 'low' view of the ground with red fog, fog fraction by
distance from the camera, read row by row (ground rows near the bottom are the unfogged reference).

    python tools/visual/clipfog.py [nwn|openwinter] [scene=fogcal_none]
"""
import math
import sys
from pathlib import Path
import numpy as np
from PIL import Image

side = sys.argv[1] if len(sys.argv) > 1 else "nwn"
scene = sys.argv[2] if len(sys.argv) > 2 else "fogcal_none"
a = np.asarray(Image.open(Path(__file__).resolve().parent.parent.parent / "out" / "visual" / "shots" / f"vis_{scene}" / side / "low.png").convert("RGB"), dtype=np.float64)
h, w = a.shape[:2]
K = (h / 2) / math.tan(math.radians(22.5))
cam_h = 1 + 10 * math.cos(math.radians(80))
theta0 = math.atan((cam_h - 1) / (10 * math.sin(math.radians(80))))
# Middle third of each row (trees stand at the sides less often there); green channel against the bottom rows.
g = np.median(a[:, w // 3: 2 * w // 3, 1], axis=1)
ref = np.median(g[h - 60:])
for y in range(h - 1, 0, -6):
    ang = theta0 + math.atan((y - h / 2) / K)
    if ang <= 0.005:
        continue
    d = cam_h / math.sin(ang)
    print(f"{d:6.1f} m  fog {1 - g[y] / ref:5.2f}")
