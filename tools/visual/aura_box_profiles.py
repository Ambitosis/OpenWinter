"""Measure authored aura-box silhouettes and outward halo profiles from captures.

Pixels stay in captured display RGB. This reports observations; it does not assert
that different renderers or camera projections match.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def load(path):
    return np.asarray(Image.open(path).convert("RGB"))


def measure(folder, distance, effect):
    baseline_path = folder / f"baseline_{distance}.png"
    effect_path = folder / f"{effect}_{distance}.png"
    baseline, frame = load(baseline_path), load(effect_path)
    occupied = np.max(baseline, axis=2) > 5
    columns = np.any(occupied, axis=0)
    starts = np.flatnonzero(columns & ~np.r_[False, columns[:-1]])
    ends = np.flatnonzero(columns & ~np.r_[columns[1:], False])
    boxes = []
    if len(starts) != 3:
        raise ValueError(f"Expected three visible boxes in {baseline_path}, got {len(starts)}")
    for index, (left, right) in enumerate(zip(starts, ends)):
        if index == 1:
            continue  # Both sides contain overlapping halos from the neighboring boxes.
        rows = np.flatnonzero(np.any(occupied[:, left:right + 1], axis=1))
        top, bottom = int(rows[0]), int(rows[-1])
        middle = int((top + bottom) / 2)
        # Follow the left silhouette at the central row, with a five-row median
        # reducing rasterization noise without smearing the whole sloping edge.
        # Other boxes' halos overlap in the interior gaps. Only the outside
        # edges of the first/last boxes are independent halo controls.
        direction = 1 if index == 2 else -1
        profiles = []
        edges = []
        for row in range(middle - 2, middle + 3):
            silhouette = np.flatnonzero(occupied[row, left:right + 1])
            edge = int(left + silhouette[-1 if direction == 1 else 0])
            edges.append(edge)
            profiles.append([frame[row, min(frame.shape[1] - 1, max(0, edge + direction * d))].tolist()
                             for d in range(1, 161)])
        profile = np.median(profiles, axis=0)
        boxes.append({
            "bbox": [int(left), top, int(right), bottom],
            "sample_row": middle,
            "direction": "right" if direction == 1 else "left",
            "independent_outer_edge": index != 1,
            "edges": edges,
            "outward_rgb": profile.tolist(),
            "last_distance_at_red_threshold": {
                str(threshold): int(np.max(np.flatnonzero(profile[:, 0] >= threshold)) + 1)
                if np.any(profile[:, 0] >= threshold) else 0
                for threshold in [1, 2, 4, 8, 16, 32, 64]
            },
        })
    return {
        "baseline": str(baseline_path),
        "baseline_sha256": hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
        "effect": str(effect_path),
        "effect_sha256": hashlib.sha256(effect_path.read_bytes()).hexdigest(),
        "boxes": boxes,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    report = {f"{effect}_{distance}": measure(args.folder, distance, effect)
              for distance, effect in [("far", "red"), ("far", "white"), ("near", "red")]}
    with args.output.open("w", encoding="utf-8", newline="") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    for name, observation in report.items():
        for box in observation["boxes"]:
            print(name, box["bbox"], box["last_distance_at_red_threshold"])
