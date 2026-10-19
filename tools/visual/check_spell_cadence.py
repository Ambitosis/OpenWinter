"""Check the actual-PC/copy cadence fixture; 50ms script clock, no wall-log timing."""
import argparse
import hashlib
import json
from pathlib import Path


def analyze(path):
    rows = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if "VISUAL|note|" not in line:
            continue
        key, value = line.split("VISUAL|note|", 1)[1].split("|", 1)
        if key in rows:
            raise ValueError("duplicate marker: " + key)
        rows[key] = value
    checks = []
    def require(label, actual, expected, tolerance=0):
        passed = abs(actual - expected) <= tolerance if isinstance(expected, (float, int)) else actual == expected
        checks.append(dict(label=label, actual=actual, expected=expected, tolerance=tolerance, passed=passed))
    def clock(key):
        return int(rows[key]) / 20
    for actor, pc in [("pc", 1), ("copy", 0)]:
        require(actor + ".identity", rows[actor + ".identity"], f"Aluvian Darkstar.pc.{pc}.wizard.1.known.1")
        for phase, release, interval in [("normal", 2.0, 6.0), ("haste", 1.25, 3.0)]:
            prefix = actor + "." + phase
            start = clock(prefix + ".start")
            require(prefix + ".slot", int(rows[prefix + ".slot"]), 102)
            for i in range(3):
                impact = clock(prefix + ".impact" + str(i))
                after = clock(prefix + ".after" + str(i))
                require(prefix + f".impact{i}.elapsed", impact - start, release + interval * i, .2)
                require(prefix + f".recovery{i}", after - impact, 1.0, .15)
                require(prefix + f".spent{i}", int(rows[prefix + f".spent{i}"]), 1)
                require(prefix + f".meta{i}", int(rows[prefix + f".meta{i}"]), 0)
        for apr, shortened in [(1, 5.0), (2, 4.5), (6, 3.65)]:
            prefix = actor + ".attack" + str(apr)
            require(actor + f".apr{apr}", int(rows[actor + f".apr{apr}"]), apr)
            require(prefix + ".swung", int(rows[prefix + ".swung"]), 1)
            require(prefix + ".spent", int(rows[prefix + ".spent0"]), 1)
            impact = clock(prefix + ".impact0")
            require(prefix + ".impact.elapsed", impact - clock(prefix + ".start"), shortened + pc * .5 + 2.0, .2)
            require(prefix + ".recovery", clock(prefix + ".after0") - impact, 1.0, .15)
    return {"input": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "scope": "actual Aluvian PC and copied NPC; ordinary/Haste and cleared passive-melee handoffs; no PC Quicken or visual parity claim",
            "passed": all(c["passed"] for c in checks), "checks": checks}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("notes", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    report = analyze(args.notes)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="")
    print(f"{sum(c['passed'] for c in report['checks'])}/{len(report['checks'])} checks pass")
    if not report["passed"]:
        for check in report["checks"]:
            if not check["passed"]:
                print(check)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
