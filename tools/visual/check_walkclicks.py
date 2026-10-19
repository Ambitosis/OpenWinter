"""Reject missing Shift input; compare ordinary/Shift/released rapid-click gaits."""
import argparse
import json
import math
from pathlib import Path


def read(folder):
    values = {}
    for line in (folder / "notes.txt").read_text(encoding="utf-8").splitlines():
        parts = line.split("|")
        if len(parts) == 6 and parts[:3] == ["VISUAL", "note", "walkclick"]:
            if parts[3] in values:
                raise ValueError("duplicate sample " + parts[3])
            values[parts[3]] = tuple(map(float, parts[4:]))
    expected = {name + suffix for name in ["initial_run", "shift_clicks", "released_run"] for suffix in ["_a", "_b"]}
    if set(values) != expected:
        raise ValueError("incomplete walk-click samples")
    result = {}
    for name in ["initial_run", "shift_clicks", "released_run"]:
        speed = math.dist(values[name + "_a"], values[name + "_b"]) / 0.5
        low, high = (1.8, 2.2) if name == "shift_clicks" else (3.75, 4.25)
        if not low <= speed <= high:
            raise ValueError(f"{folder.name} {name}: {speed:.4f} m/s outside positive-control range {low}..{high}")
        result[name] = round(speed, 4)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native", type=Path)
    parser.add_argument("client", type=Path)
    args = parser.parse_args()
    print(json.dumps({"native": read(args.native), "client": read(args.client)}, indent=2))


if __name__ == "__main__":
    main()
