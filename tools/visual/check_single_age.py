"""Check singlefade/singlelong positive controls and sustained final particle state.

This checks visibility over time, not exact color, size, timing or general VFX parity.
Usage: python tools/visual/check_single_age.py <shot-folder> --mode fade|solid
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

SHOTS = ["baseline", "age050", "age100", "age170", "age230", "age290",
         "age350", "age450", "age550", "age700", "age850"]
ROI = (300, 200, 600, 420)


def check(folder, mode):
    failures, frames = [], {}
    logs = [p for p in (folder / "native.log", folder / "godot.log") if p.exists()]
    log = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in logs)
    if "VISUAL|done|1" not in log:
        failures.append("Missing completion gate")
    if "SHADER ERROR:" in log or "Shader compilation failed" in log:
        failures.append("Shader compilation failed")
    notes = (folder / "notes.txt").read_text(encoding="utf-8")
    if "VISUAL|note|models|vim_bonac|vff_summund" not in notes:
        failures.append("Missing expected authored/stock model gate")
    shot_log = (folder / "shots.txt").read_text(encoding="utf-8")
    expected_names = [line.split("|")[2] for line in shot_log.splitlines()
                      if line.startswith("VISUAL|shot|")]
    if expected_names != SHOTS:
        failures.append("Unexpected shot sequence")
    for name in SHOTS:
        path = folder / (name + ".png")
        if not path.exists():
            failures.append(f"Missing image {name}")
            continue
        with Image.open(path) as im:
            if im.size != (1280, 720):
                failures.append(f"Wrong resolution for {name}: {im.size}")
            pixels = np.asarray(im.convert("RGB").crop(ROI), dtype=np.int16)
        r, g, b = (pixels[:, :, i] for i in range(3))
        counts = {
            "red": int(((r > 40) & (r > g + 20) & (r > b + 20)).sum()),
            "green": int(((g > 80) & (g > r + 50) & (g > b + 50)).sum()),
            "blue": int(((b > 100) & (b > r + 80) & (b > g + 80)).sum()),
        }
        frames[name] = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest(), **counts}
        # Require large, unmistakable positive controls; absent particles cannot pass.
        expected = {"red": name != "baseline" and (mode == "solid" or name == "age050"),
                    "green": name != "baseline",
                    "blue": name in ("age050", "age100", "age170")}
        for color, visible in expected.items():
            if (counts[color] < 100 if visible else counts[color] > 10):
                failures.append(f"{name} {color}: expected {'visible' if visible else 'absent'}, got {counts[color]} pixels")

    # Validate retained native raw files when present. Window captures on this host
    # are black and deliberately excluded; decoded native framebuffer pixels matter.
    native_path = folder / "native-framebuffer.jsonl"
    latencies = []
    if native_path.exists():
        records = [json.loads(line) for line in native_path.read_text(encoding="utf-8").splitlines()]
        if [r["shot"] for r in records] != SHOTS:
            failures.append("Unexpected native framebuffer sequence")
        for record in records:
            name = record["shot"]
            raw = folder / (name + ".tga")
            png = folder / (name + ".png")
            if hashlib.sha256(raw.read_bytes()).hexdigest() != record["rawSha256"]:
                failures.append(f"Raw hash mismatch: {name}")
            with Image.open(raw) as a, Image.open(png) as b:
                if not np.array_equal(np.asarray(a.convert("RGB")), np.asarray(b.convert("RGB"))):
                    failures.append(f"Raw/PNG pixel mismatch: {name}")
            latencies.append(record["latencySeconds"])
    return {"folder": str(folder.resolve()), "mode": mode, "roi": ROI,
            "scope": "Visibility/final-state controls only; not exact timing, color, size or full spell parity",
            "nativeLatencySeconds": latencies, "frames": frames,
            "passed": not failures, "failures": failures}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    parser.add_argument("--mode", choices=("fade", "solid"), required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = check(args.folder, args.mode)
    output = json.dumps(result, indent=2) + "\n"
    if args.output:
        with args.output.open("w", encoding="utf-8", newline="") as stream:
            stream.write(output)
    print(output)
    raise SystemExit(0 if result["passed"] else 1)
