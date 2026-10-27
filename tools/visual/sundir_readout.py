"""The area light read back from the sundir scene's shots (its ow_sundir.shd draws them as flat colour columns).

    python tools/visual/sundir_readout.py [variant ...]         (default: every built variant)
    python tools/visual/sundir_readout.py --tag=_x ext

For each shot and engine: the direction towards the sun or moon in world space (x east, y north, z up) with its
elevation and compass bearing, the same as given (view space), the diffuse and ambient colours and the hour.
"""
import math
import sys
from pathlib import Path
import numpy as np
from PIL import Image

SHOTS = Path(__file__).resolve().parent.parent.parent / "out" / "visual" / "shots"


def cells(path):
    a = np.asarray(Image.open(path).convert("RGB"), dtype=np.int64)
    h, w = a.shape[:2]
    out = []
    for i in range(8):
        # The commonest colour in the middle of each 160-pixel column (of a 1280-pixel view): the tile's plants
        # cover parts of it.
        x = int((i + 0.5) * w / 8)
        patch = a[h // 4:3 * h // 4, x - 40:x + 40].reshape(-1, 3)
        keys, counts = np.unique((patch[:, 0] << 16) | (patch[:, 1] << 8) | patch[:, 2], return_counts=True)
        k = int(keys[counts.argmax()])
        out.append(np.array([k >> 16, (k >> 8) & 255, k & 255], dtype=np.float64))
    return out


def vector(coarse, fine):
    v = (np.round(coarse) + np.round(fine) / 255.0) / 255.0
    return 2.0 * v - 1.0


def describe(d):
    n = np.linalg.norm(d)
    if n < 1e-6:
        return "(0)"
    u = d / n
    elevation = math.degrees(math.asin(max(-1.0, min(1.0, u[2]))))
    bearing = math.degrees(math.atan2(u[0], u[1])) % 360  # compass: 0 north, 90 east
    return f"({u[0]:+.3f} {u[1]:+.3f} {u[2]:+.3f}) up {elevation:5.1f} bearing {bearing:5.1f}"


def read(path):
    c = cells(path)
    world = vector(c[0], c[1])
    view = vector(c[2], c[3])
    return {
        "world": describe(world), "view": describe(view),
        "diffuse": " ".join(f"{x / 255:.3f}" for x in c[4]), "ambient": " ".join(f"{x / 255:.3f}" for x in c[5]),
        "hour": int(round(c[6][0])), "check": " ".join(f"{int(round(x))}" for x in c[7]),
    }


def main():
    args = sys.argv[1:]
    tag = ""
    if args and args[0].startswith("--tag="):
        tag, args = args[0][6:], args[1:]
    folders = [SHOTS / f"vis_sundir_{v}{tag}" for v in args] if args else sorted(p for p in SHOTS.glob("vis_sundir_*") if p.is_dir() and p.name.endswith(tag))
    for folder in folders:
        print(f"==== {folder.name}")
        names = sorted({p.stem for s in ("nwn", "openwinter") for p in (folder / s).glob("*.png")}, key=lambda n: (not n.startswith("start"), n))
        for shot in names:
            for side in ("nwn", "openwinter"):
                p = folder / side / f"{shot}.png"
                if not p.exists():
                    continue
                r = read(p)
                print(f"{shot:<11} {side:<10} world {r['world']}  view {r['view']}  dif {r['diffuse']}  amb {r['ambient']}  h {r['hour']:>2}  chk {r['check']}")


if __name__ == "__main__":
    main()
