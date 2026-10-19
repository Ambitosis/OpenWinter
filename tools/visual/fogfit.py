"""Fog calibration from the fogcal scene: the fraction of fog on the ground at each camera distance.

    python tools/visual/fogfit.py [nwn|openwinter]

The ground is seen from straight above, so its distance from the camera is the camera distance plus the
camera's focus height. Fog is pure red, so the fog fraction is 1 - green(fogged) / green(clear).
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent.parent.parent / "out" / "visual" / "shots"
side = sys.argv[1] if len(sys.argv) > 1 else "nwn"


def green(variant, shot):
    a = np.asarray(Image.open(OUT / f"vis_fogcal_{variant}" / side / f"{shot}.png").convert("RGB"), dtype=np.float64)
    h, w = a.shape[:2]
    # A central patch of ground, away from the tree tops.
    patch = a[h // 2 - 40:h // 2 + 40, w // 2 - 40:w // 2 + 40]
    return patch[..., 1].mean()


print(f"{side}: fog fraction on the ground (rows: fog amount, columns: camera distance)")
print("amount " + " ".join(f"{d:>6}" for d in ("d05", "d10", "d20", "d35")))
for v in ("f5", "f8", "f10", "f12", "f15"):
    print(f"{v:<6} " + " ".join(f"{1 - green(v, d) / green('none', d):6.3f}" for d in ("d05", "d10", "d20", "d35")))
