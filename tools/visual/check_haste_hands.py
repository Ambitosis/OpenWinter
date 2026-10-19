"""Validate prepared-player long-cast controls; images require independent inspection."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def check(path, cap=False):
    data = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.search(r"VISUAL\|note\|([^|]+)\|(.+)", line)
        if match:
            data.setdefault(match[1], []).append(match[2])
    checks = []

    def one(key):
        values = data.get(key, [])
        if len(values) != 1:
            raise ValueError(f"Expected one {key}: {values}")
        return values[0]

    def add(key, expected, tolerance=0):
        actual = one(key)
        if isinstance(expected, (int, float)):
            actual = float(actual)
        passed = abs(actual - expected) <= tolerance if tolerance else actual == expected
        checks.append(dict(key=key, actual=actual, expected=expected, tolerance=tolerance, passed=passed))

    add("identity", "Aluvian Darkstar.pc.1.wizard.1.known.1")
    add("table_conjure", "4000")
    add("table_recovery", "5000")
    cases = {"fast_cap": (4.5, 9.5)} if cap else {
        "normal_flips": (4.5, 9.5), "fast_flips": (2.5, 7.5), "late_expiry": (4.5, 8.2)}
    for case, (impact, recovery) in cases.items():
        add(case + ".slot", 102)
        add(case + ".ready", 1)
        add(case + ".spent", 1)
        start = int(one(case + ".start"))
        for event, expected in (("impact", impact), ("after", recovery)):
            actual = (int(one(case + "." + event)) - start) / 20
            checks.append(dict(key=case + "." + event + "_seconds", actual=actual, expected=expected,
                               tolerance=.15, passed=abs(actual-expected) <= .15))
    return dict(input=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(), checks=checks,
                passed=sum(c["passed"] for c in checks), total=len(checks),
                scope="Actual prepared player mechanics and authored table gates only; no visual parity inferred.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("notes", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--cap", action="store_true")
    args = parser.parse_args()
    result = check(args.notes, args.cap)
    with args.output.open("w", encoding="utf-8", newline="") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    print(f"{result['passed']}/{result['total']} gates passed")
    raise SystemExit(0 if result["passed"] == result["total"] else 1)
