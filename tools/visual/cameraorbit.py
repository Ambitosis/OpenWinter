"""Check the rendered camera response, including every frame during turns and after release.

    python tools/visual/visual.py run camorbit --openwinter-args "--visual-delay 0 --visual-camera-trace 1"
    python tools/visual/cameraorbit.py [out/visual/shots/vis_camorbit/openwinter/camera.csv]

The original client must actually turn in its captures before drawing conclusions about its timing.
This check verifies OpenWinter's response, not the original's angular speed or mouse sensitivity.
"""
import csv
import math
import sys
from pathlib import Path


def check(path):
    with path.open(encoding="utf-8", newline="") as trace:
        rows = [{k: float(v) for k, v in row.items()} for row in csv.DictReader(trace)]
    if len(rows) < 30:
        raise SystemExit("FAIL: missing frame trace")
    steps = [math.remainder(b["yaw"] - a["yaw"], math.tau) for a, b in zip(rows, rows[1:])]
    # Exclude script camera snaps. Require substantial input in both directions.
    left = sum(v for v in steps if 0 < v < 0.2)
    right = -sum(v for v in steps if -0.2 < v < 0)
    if min(left, right) < 0.5:
        raise SystemExit("FAIL: the camera did not orbit in both directions")
    errors = {angle: max(abs(math.remainder(r[angle] - r["actual_" + angle], math.tau))
                        for r in rows) for angle in ("yaw", "pitch")}
    print(f"{len(rows)} frames; left {math.degrees(left):.2f} deg, right {math.degrees(right):.2f} deg")
    for angle, error in errors.items():
        print(f"Maximum rendered {angle} error: {math.degrees(error):.6f} deg")
    if max(errors.values()) > math.radians(0.01):
        raise SystemExit("FAIL: rendered orbit lags behind the requested angle")
    print("PASS: rendered orbit follows input without angular easing")


if __name__ == "__main__":
    check(Path(sys.argv[1]) if len(sys.argv) > 1 else
          Path(__file__).resolve().parents[2] / "out/visual/shots/vis_camorbit/openwinter/camera.csv")
