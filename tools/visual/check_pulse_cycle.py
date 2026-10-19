"""Measure a native pulsecal run; calibration pixels are chosen without pulse data.

This establishes one programmed-effect color curve, not overall visual parity.
The frozen actor's constant-color controls remove display transfer and animated
halo pixels from the measurement. Failed table-loading runs must be rejected.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("shots", type=Path)
parser.add_argument("--out", type=Path, required=True)
args = parser.parse_args()
root = args.shots
notes = (root / "notes.txt").read_text()
if "VISUAL|note|tables.valid.1" not in notes or "tables.valid.0" in notes:
    raise SystemExit("REJECTED: authored table positive control missing or failed")
times = dict((n, float(t)) for n, t in (s.split("|") for s in (root / "frames.txt").read_text().splitlines()))


def pixels(name):
    with Image.open(root / (name + ".png")) as im:
        if im.size != (1280, 720):
            raise SystemExit("REJECTED: measurement requires the authored 1280x720 framing")
        a = np.asarray(im, dtype=float)
    if a.max() - a.min() < 20:
        raise SystemExit("REJECTED: blank capture " + name)
    return a[250:445, 590:677, 0]


levels = np.linspace(0, 1, 5)
calibration = np.array([[pixels(f"cal_{pct}_{i:03d}") for i in range(8)] for pct in (0, 25, 50, 75, 100)])
means = calibration.mean(axis=1)
# Defined from constant controls only. Exclude the animated surrounding halo,
# low-signal pixels and changing actor geometry; do not select for curve fit.
mask = (means[-1] - means[0] > 20) & (calibration.std(axis=1).max(axis=0) < .1)
if mask.sum() < 100:
    raise SystemExit("REJECTED: insufficient stable, responsive calibration pixels")
response = means[:, mask].mean(axis=1)
if np.any(np.diff(response) <= 0):
    raise SystemExit("REJECTED: constant-color controls are not monotonic")
names = [f"red_black_{i:03d}" for i in range(4, 36)]
t = np.array([times[n] for n in names])
raw = np.array([pixels(n)[mask].mean() for n in names])
fraction = np.interp(raw, response, levels)
# Coarse calibration is least precise near black; fit the calibrated midrange.
valid = (fraction >= .28) & (fraction <= .90)
if valid.sum() < 12:
    raise SystemExit("REJECTED: too few midrange pulse samples")
half_periods = np.linspace(.95, 1.05, 101)
origins = np.linspace(.35, .60, 251)
phase = (t[valid, None, None] - origins[None, None, :]) / half_periods[None, :, None]
triangle = np.abs(1 - np.mod(phase, 2))
cosine = .5 + .5 * np.cos(np.pi * phase)


def fit(predicted):
    errors = np.sqrt(((predicted - fraction[valid, None, None]) ** 2).mean(axis=0))
    i, j = np.unravel_index(np.argmin(errors), errors.shape)
    return dict(rmse=float(errors[i, j]), halfPeriodSeconds=float(half_periods[i]), captureOriginSeconds=float(origins[j]))


tri, cos = fit(triangle), fit(cosine)
report = dict(
    fixture="pulsecal", shots=str(root), calibratedPixels=int(mask.sum()),
    channel="red", crop=[590, 250, 677, 445], constantColorMeans=response.tolist(),
    samples=[dict(name=n, seconds=float(x), raw=float(r), fraction=float(q), fitted=bool(v)) for n, x, r, q, v in zip(names, t, raw, fraction, valid)],
    triangle=tri, cosine=cos,
    passThresholds=dict(triangleRmse=.03, triangleRelativeToCosine=.4, halfPeriodError=.02),
    limitation="Native Type6 midrange color interpolation only; halo shape, opacity, stacking, pause and every spell remain separate.",
    sha256={p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.glob("*.png"))},
)
passed = tri["rmse"] < .03 and tri["rmse"] < cos["rmse"] * .4 and abs(tri["halfPeriodSeconds"] - 1) < .02
report["passed"] = passed
args.out.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({k: report[k] for k in ("calibratedPixels", "triangle", "cosine", "passed")}, indent=2))
raise SystemExit(0 if passed else 1)
