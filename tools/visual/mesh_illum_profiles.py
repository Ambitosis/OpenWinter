"""Read the authored meshillum grayscale ramps, requiring 15 visible dark controls."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def measure(folder):
    names = ["dark", "ambient128", "dark_restored"]
    frames = {name: np.asarray(Image.open(folder / f"{name}.png").convert("RGB")) for name in names}
    dark = frames["dark"]
    mask = np.max(dark, axis=2) > 3
    unseen = mask.copy()
    components = []
    for y, x in zip(*np.where(mask)):
        if not unseen[y, x]:
            continue
        queue = deque([(int(y), int(x))])
        unseen[y, x] = False
        pixels = []
        while queue:
            cy, cx = queue.popleft()
            pixels.append((cy, cx))
            for yy, xx in [(cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)]:
                if 0 <= yy < mask.shape[0] and 0 <= xx < mask.shape[1] and unseen[yy, xx]:
                    unseen[yy, xx] = False
                    queue.append((yy, xx))
        if len(pixels) < 100:
            continue
        yy, xx = np.array(pixels).T
        center = [int(np.median(xx)), int(np.median(yy))]
        components.append({"center": center,
                           "rgb": {name: np.median(frame[yy, xx], axis=0).tolist() for name, frame in frames.items()}})
    if len(components) != 15:
        raise ValueError(f"Expected 15 nonzero controls and one invisible zero control, got {len(components)}")
    ordered = sorted(components, key=lambda c: c["center"][1])
    rows = []
    for component in ordered:
        if not rows or component["center"][1] - rows[-1][-1]["center"][1] > 35:
            rows.append([])
        rows[-1].append(component)
    if [len(row) for row in rows] != [3, 4, 4, 4]:
        raise ValueError(f"Unexpected projected ramp layout: {[len(row) for row in rows]}")
    report = {}
    for label, row in zip(["additive", "gray_texture", "diffuse_025", "untextured"], rows):
        report[label] = sorted(row, key=lambda c: c["center"][0])
    # Source nodes are equally spaced: the omitted additive zero sample is one
    # column to the right. A small center patch stays clear of the sloping edges.
    top = report["additive"]
    x = int(round(2 * top[2]["center"][0] - top[1]["center"][0]))
    y = int(round(2 * top[2]["center"][1] - top[1]["center"][1]))
    report["additive_zero"] = {"center": [x, y], "rgb": {
        name: np.median(frame[y-3:y+4, x-3:x+4], axis=(0, 1)).tolist() for name, frame in frames.items()}}
    report["sha256"] = {name: hashlib.sha256((folder / f"{name}.png").read_bytes()).hexdigest() for name in names}
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    result = measure(args.folder)
    with args.output.open("w", encoding="utf-8", newline="") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    for name, row in result.items():
        if isinstance(row, list):
            print(name, {shot: [c["rgb"][shot] for c in row] for shot in ["dark", "ambient128"]})
    print("additive_zero", result["additive_zero"])
