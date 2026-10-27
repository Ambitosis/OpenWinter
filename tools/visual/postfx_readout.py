"""Engine values read back from the postfx scene's readout shots (its fsfbpostpr, mode 3), or the depth over the
water in the waterfx scene's depth shots.

    python tools/visual/postfx_readout.py [shot ...]
    python tools/visual/postfx_readout.py waterfx [shot ...]
    python tools/visual/postfx_readout.py --tag=_ui15 [shot ...]     (a run with visual.py --tag)

The scene's post-process pass writes 32 values as exact 8-bit colours in cells along the top tenth of the
screen; each is decoded from its cell's centre for both engines, side by side. Cells 0 and 1 are constants
that check the colours arrive unchanged.
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

SHOTS = Path(__file__).resolve().parent.parent.parent / "out" / "visual" / "shots"
OFFSET = 1 << 23

# name, how it is stored: "int" (as is), "s" (offset by 2^23), or the fixed-point scale (offset by 2^23)
CELLS = [
    ("check 0x336699", "int"), ("check 0xcc0055", "int"), ("worldtimerTimeOfDay low", "int"), ("worldtimerTimeOfDay high", "int"),
    ("areaGlobalWind.x", 1000), ("areaGlobalWind.y", 1000), ("areaGlobalWind.z", 1000), ("areaWeatherType", "s"),
    ("areaWeatherDensity", 1000), ("moduleHour", "int"), ("moduleMinutesPerHour", "int"), ("sceneCurrentFrame", "int"),
    ("windPointSourcesCount", "s"), ("cameraViewAngle", 1000), ("nearClip", 1000), ("farClip", 100),
    ("playerPosition.x", 100), ("playerPosition.y", 100), ("playerPosition.z", 100), ("areaFlags", "int"),
    ("targetShape", "s"), ("depth at (0.5, 0.25)", 100), ("world x there", 100), ("world y there", 100),
    ("world z there", 100), ("camera z", 100), ("aspect", 1000), ("screen width", 1),
    ("moduleDawnHour", "int"), ("moduleDuskHour", "int"), ("cameraYaw", 100), ("cameraPitch", 100),
]


WATER_CELLS = [("check 0x336699", "int"), ("check 0xcc0055", "int")] + [
    (f"depth at ({0.5 if i < 8 else 0.3}, {0.15 + 0.1 * (i % 8):.2f})", 100) for i in range(16)]


def decode(path, cells):
    a = np.asarray(Image.open(path).convert("RGB"), dtype=np.int64)
    h, w = a.shape[:2]
    values = []
    for i, (_, kind) in enumerate(cells):
        r, g, b = a[int(h * 0.05), int((i + 0.5) * w / 32)]
        n = (r << 16) | (g << 8) | b
        if kind == "int":
            values.append(n)
        elif kind == "s":
            values.append(n - OFFSET)
        else:
            values.append((n - OFFSET) / kind)
    return values


def main():
    args = sys.argv[1:]
    water = "waterfx" in args
    args = [a for a in args if a != "waterfx"]
    tag = ""
    if args and args[0].startswith("--tag="):
        tag, args = args[0][6:], args[1:]
    out = SHOTS / (("vis_waterfx" if water else "vis_postfx") + tag)
    cells = WATER_CELLS if water else CELLS
    if water:
        shots = args or ["f_depth_high", "g_depth_low"]
    else:
        shots = args or sorted({p.stem for p in (out / "nwn").glob("*.png") if "readout" in p.stem or p.stem[:2] in ("g_", "h_", "i_", "j_", "k_", "l_")})
    for shot in shots:
        print(f"== {shot}")
        sides = {s: decode(out / s / f"{shot}.png", cells) for s in ("nwn", "openwinter") if (out / s / f"{shot}.png").exists()}
        print(f"{'':28}" + "".join(f"{s:>16}" for s in sides))
        for i, (name, _) in enumerate(cells):
            print(f"{name:28}" + "".join(f"{v[i]:>16}" for v in sides.values()))


if __name__ == "__main__":
    main()
