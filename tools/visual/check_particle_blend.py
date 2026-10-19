"""Read the authored particleblend grid; report native/client values without approving parity."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image

p = argparse.ArgumentParser()
p.add_argument("shots", type=Path)
p.add_argument("--out", type=Path, required=True)
p.add_argument("--expect-native", action="store_true", help="Fail unless every center equals the native reference")
a = p.parse_args()
path = a.shots / "blend_grid.png"
im = np.asarray(Image.open(path).convert("RGB"))
if im.shape != (720, 1280, 3):
    raise SystemExit("REJECTED: fixture requires 1280x720")

def runs(mask):
    edges = np.diff(np.r_[False, mask, False].astype(int))
    return list(zip(np.flatnonzero(edges == 1), np.flatnonzero(edges == -1)))

# Wholly black room; controls are the only broad nonblack regions.
rows = [(lo, hi) for lo, hi in runs((im.max(axis=2) > 0).sum(axis=1) > 100) if hi-lo > 20]
if len(rows) != 3:
    raise SystemExit(f"REJECTED: expected three control rows, found {rows}")
expected = [[0, 2, 8, 32, 128, 255], [0, 2, 8, 32, 128, 255], [32, 64, 128, 128, 255, 255]]
result = []
for row, (lo, hi) in enumerate(rows):
    y = (lo+hi)//2
    cols = [(x0, x1) for x0, x1 in runs(im[y].max(axis=1) > 0) if x1-x0 > 15]
    if (row < 2 and not 2 <= len(cols) <= 5) or (row == 2 and len(cols) != 6):
        raise SystemExit(f"REJECTED: row{row} has an invalid visible-cell layout: {cols}")
    xs = [(x0+x1)//2 for x0, x1 in cols]
    if row < 2:
        # The first two rows are monotonic. A deliberately bad transfer can
        # round dark controls to zero; infer their positions from the visible
        # tail instead of silently excluding this important negative result.
        spacing = float(np.median(np.diff(xs)))
        if any(abs(v-spacing) > 2 for v in np.diff(xs)):
            raise SystemExit("REJECTED: nonuniform control spacing")
        xs = [round(xs[0]-spacing*i) for i in range(6-len(xs), 0, -1)] + xs
    cells = []
    for i, x in enumerate(xs):
        crop = im[y-3:y+4, x-3:x+4]
        if crop.shape != (7, 7, 3) or int(crop.max()-crop.min()) != 0:
            raise SystemExit(f"REJECTED: row{row} cell{i} is not a flat grayscale control")
        rgb = np.median(crop, axis=(0,1)).astype(int).tolist()
        cells.append(dict(center=[int(x),int(y)], rgb=rgb, spread=int(crop.max()-crop.min()), nativeExpected=expected[row][i]))
    result.append(dict(kind=["texture","vertexColor","overlap"][row], cells=cells))
    if row < 2 and (cells[0]["rgb"] != [0,0,0] or cells[5]["rgb"] != [255,255,255]):
        raise SystemExit("REJECTED: black/white anchors are missing")
removed = np.asarray(Image.open(a.shots/"removed.png").convert("RGB"))
if removed.max() != 0:
    raise SystemExit("REJECTED: expected black removed-effect control")
matched = sum(c["rgb"] == [c["nativeExpected"]]*3 for r in result for c in r["cells"])
report = dict(shots=str(a.shots), rows=result, controlsValid=True, nativeMatches=matched, totalCells=18,
              limitation="Three isolated additive rows over black only; colored backgrounds, alpha and real spells unverified.",
              sha256={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in [path,a.shots/"removed.png"]})
with a.out.open("w",encoding="utf-8",newline="") as f:
    json.dump(report,f,indent=2)
for r in result:
    print(r["kind"], [c["rgb"] for c in r["cells"]])
print(f"Native reference: {matched}/18 cells")
if a.expect_native and matched != 18:
    raise SystemExit(1)
