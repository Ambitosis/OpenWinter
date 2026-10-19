"""Validate actual-player live-Haste fixture gates, without inferring visual parity."""
import argparse
import hashlib
import json
import re
from pathlib import Path

CASES = {
    "normal": (0, 2, 3), "fast": (1, 1.25, 2.25),
    "add_conjure": (0, 1.25, 2.25), "add_release": (0, 1.7, 2.25),
    "remove_conjure": (1, 2, 3), "remove_recover": (1, 1.25, 3),
}
FLIPS = {"add_remove": (0, 2, 3), "remove_add": (1, 1.25, 2.25), "add_late": (0, 2, 2.6)}


def check(path, spell, flips=False):
    data = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.search(r"VISUAL\|note\|([^|]+)\|(.+)", line)
        if match:
            data.setdefault(match[1], []).append(match[2])
    checks = []

    def one(key):
        values = data.get(key, [])
        if len(values) != 1:
            raise ValueError(f"Expected one {key!r} observation, got {values!r}")
        return values[0]

    def add(key, actual, expected, tolerance=0):
        passed = abs(actual - expected) <= tolerance if isinstance(expected, (int, float)) else actual == expected
        checks.append(dict(key=key, actual=actual, expected=expected, tolerance=tolerance, passed=passed))

    add("identity", one("identity"), "Aluvian Darkstar.pc.1.wizard.1.known.1")
    for label, (haste, impact, after) in (FLIPS if flips else CASES).items():
        start = int(one(label + ".start"))
        observed = one(label + ".impact").split(".spell.")
        add(label + ".impact_spell", int(observed[1]), spell)
        add(label + ".slot", int(one(label + ".slot")), spell)
        add(label + ".ready", int(one(label + ".ready")), 1)
        add(label + ".spent", int(one(label + ".spent")), 1)
        add(label + ".initial_haste", int(one(label + ".initial_haste")), haste)
        add(label + ".impact_seconds", (int(observed[0]) - start) / 20, impact, .15)
        add(label + ".recovery_seconds", (int(one(label + ".after")) - start) / 20, after, .15)
    return dict(input=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(), checks=checks,
                passed=sum(c["passed"] for c in checks), total=len(checks),
                scope="Actual prepared player spell mechanics gates only; no visual/audio parity inferred.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("notes", type=Path)
    parser.add_argument("spell", type=int)
    parser.add_argument("output", type=Path)
    parser.add_argument("--flips", action="store_true")
    args = parser.parse_args()
    report = check(args.notes, args.spell, args.flips)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    print(f"{report['passed']}/{report['total']} gates passed")
    raise SystemExit(0 if report["passed"] == report["total"] else 1)
