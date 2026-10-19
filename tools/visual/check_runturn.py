"""Check runturn's --visual-movement-trace for restarts during uninterrupted movement.

Usage: python tools/visual/check_runturn.py out/visual/shots/vis_runturn/openwinter/movement.csv
"""
import csv
import itertools
import sys
from pathlib import Path


def check(path):
    with path.open(encoding="utf-8", newline="") as stream:
        rows = list(csv.DictReader(stream))
    trials = {"Run": 0, "Walk": 0}
    failures = []
    for mode, group in itertools.groupby(rows, lambda r: "Run" if float(r["speed"]) > 3.9
                                       else "Walk" if 1.9 < float(r["speed"]) < 2.1 else ""):
        segment = list(group)
        if not mode or float(segment[-1]["seconds"]) - float(segment[0]["seconds"]) < 1:
            continue
        trials[mode] += 1
        # Allow the first frame's update/animation ordering at movement onset.
        start = float(segment[0]["seconds"]) + 0.25
        steady = [r for r in segment if float(r["seconds"]) >= start]
        transitions = int(steady[-1]["transitions"]) - int(steady[0]["transitions"])
        states = {r["animation_state"] for r in steady}
        print(f"{mode} {trials[mode]}: {len(steady)} samples, {transitions} transitions, {sorted(states)}")
        if states != {mode} or transitions:
            failures.append(f"{mode} restarted or left its movement clip")
    if trials != {"Run": 2, "Walk": 2}:
        failures.append(f"Expected two runs and two walks; got {trials}")
    if failures:
        raise SystemExit("FAIL: " + "; ".join(failures))
    print("PASS: both turn directions keep continuous running and walking clips")


if __name__ == "__main__":
    check(Path(sys.argv[1]))
