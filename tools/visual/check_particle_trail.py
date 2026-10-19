"""Presence/cleanup gate for authored white particle motion controls, not pixel parity."""
from pathlib import Path
import argparse
import json
from PIL import Image

ACTIVE = ("turn_050", "turn_100", "turn_200", "turn_300", "turn_400", "draining")
NAMES = ("baseline",) + ACTIVE + ("clear",)


def check(folder):
    folder = Path(folder)
    logs = [p for p in (folder / "native.log", folder / "godot.log") if p.exists()]
    if len(logs) != 1:
        raise ValueError("Expected exactly one engine log")
    log = logs[0].read_text(encoding="utf-8", errors="replace")
    if "VISUAL|done|" not in log:
        raise ValueError("Scene did not complete")
    if "Shader compilation failed" in log or "SHADER ERROR" in log:
        raise ValueError("Shader failed")
    counts = {}
    for name in NAMES:
        if "VISUAL|shot|" + name + "|" not in log:
            raise ValueError("Missing shot gate: " + name)
        im = Image.open(folder / (name + ".png")).convert("RGB")
        if im.size != (1280, 720):
            raise ValueError("Unexpected frame dimensions")
        raw = folder / (name + ".tga")
        if logs[0].name == "native.log":
            if not raw.exists() or Image.open(raw).convert("RGB").tobytes() != im.tobytes():
                raise ValueError("Native raw frame does not match PNG: " + name)
        # Fixed-camera ROI excludes HUD/cursor. White authored particles are
        # separated from dark floor and the player's colored clothing.
        pixels = im.crop((490, 180, 800, 440)).getdata()
        counts[name] = sum(min(rgb) > 210 and max(rgb) - min(rgb) < 20 for rgb in pixels)
    failures = [name for name in ACTIVE if counts[name] < 100]
    failures += [name for name in ("baseline", "clear") if counts[name] > 5]
    return {"passed": not failures, "failures": failures, "whitePixels": counts,
            "scope": "Visible authored trail at every active angle and empty baseline/cleanup; not exact geometry or timing parity."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder")
    args = parser.parse_args()
    result = check(args.folder)
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result["passed"] else 1)
