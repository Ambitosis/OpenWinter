"""Measure heard-only opacity from the fixed-camera creaturevisalpha native control.

The two arm interiors avoid silhouette coverage, the moving door, shadows and the
head (whose pixels are not stable between opaque and translucent rendering).
No images are modified. Coefficients describe encoded framebuffer RGB, not a claim
that another renderer's linear-light blend produces identical output.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
parser.add_argument("--out", type=Path)
parser.add_argument("--client", action="store_true", help="Use the inspected OpenWinter arm ROIs; still compare against native encoded 0.5")
args = parser.parse_args()
names = ["a_closed", "b_open", "c_closed_repeat", "d_background_closed", "e_background_open"]
paths = [args.directory / (name + ".png") for name in names]
frames = [np.asarray(Image.open(path).convert("RGB"), dtype=float) for path in paths]
if any(frame.shape != (720, 1280, 3) for frame in frames):
    raise SystemExit("Expected the inspected 1280x720 native framebuffer captures")

notes = (args.directory / "notes.txt").read_text(encoding="utf-8")
gates = {}
for name, seen, los in [("a_closed", 0, 0), ("b_open", 1, 1), ("c_closed_repeat", 0, 0)]:
    lines = [line for line in notes.splitlines() if f"|{name}|" in line]
    gates[name] = (any("door_valid=1|actor_position_valid=1" in line for line in lines)
                   and any("|valid=1|" in line and f"|seen={seen}|heard=1|los={los}|" in line for line in lines))

patches = []
rois = [("left_arm", (618, 108, 10, 17)), ("right_arm", (646, 108, 10, 17))] if args.client else [
    ("left_arm", (618, 118, 10, 17)), ("right_arm", (647, 118, 10, 17))]
for label, roi in rois:
    x, y, width, height = roi
    heard, opaque, repeat, bg_closed, bg_open = [v[y:y+height, x:x+width] for v in frames]
    # Select skin by opaque-frame color only, then erode one pixel to exclude edges.
    skin = (opaque[:, :, 0] > 45) & (opaque[:, :, 0] > opaque[:, :, 1] * 1.2) & (opaque[:, :, 1] > 25)
    mask = np.zeros_like(skin)
    mask[1:-1, 1:-1] = np.logical_and.reduce([
        skin[i:i+height-2, j:j+width-2] for i in range(3) for j in range(3)])
    mask &= (np.abs(bg_closed-bg_open).max(axis=2) == 0)
    mask &= (np.abs(heard-repeat).max(axis=2) <= 1)
    source = (opaque-bg_open)[mask]
    observed = ((heard+repeat)/2-bg_closed)[mask]
    if len(source) < 10:
        raise SystemExit(f"Insufficient stable foreground pixels in {label}: {len(source)}")
    alpha = float((source*observed).sum()/(source*source).sum())
    ratios = (source*observed).sum(axis=1)/(source*source).sum(axis=1)
    residual = np.abs(observed-0.5*source)
    patches.append(dict(name=label, roi=list(roi), stable_pixels=len(source), fitted_alpha=alpha,
                        ratio_median=float(np.median(ratios)), ratio_p5_p95=np.percentile(ratios, [5, 95]).tolist(),
                        half_alpha_mean_absolute_error=float(residual.mean()),
                        half_alpha_max_absolute_error=float(residual.max())))

passed = all(gates.values()) and all(abs(p["fitted_alpha"]-0.5) < 0.02
                                   and p["half_alpha_max_absolute_error"] <= 2 for p in patches)
report = dict(passed=passed, source=str(args.directory).replace("\\", "/"), state_gates=gates,
              framebuffer_sha256={name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in zip(names, paths)},
              blend_space="encoded framebuffer RGB", patches=patches,
              conclusion=("Heard-only arm interiors match 0.5 foreground + 0.5 background within byte quantization."
                          if passed else "Does not match native encoded-space 0.5; retain this failed comparison as a rendering limitation."),
              limitations="This establishes native interior opacity, not alpha composition across overlapping surfaces, every effect, or linear-light renderer output.")
result = json.dumps(report, indent=2) + "\n"
if args.out:
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8", newline="") as handle:
        handle.write(result)
print(result)
raise SystemExit(0 if passed else 1)
