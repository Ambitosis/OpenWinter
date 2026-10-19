"""Validate self-buff readings and write an explicitly scoped statistics evidence ledger.

Run all buffscope01..08 suites first, then: python parity/check_buffscope.py
Matching metadata is not proof of damage, save distributions, targeting or audiovisual parity.
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parent.parent
NO_STATS = {22, 103, 106, 136, 140, 162}  # Stock scripts only show VFX or placeholder speech.
RANDOM = {1, 9, 13, 49, 73, 99, 169, *range(354, 363), 441}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    cases, errors = [], []
    helper = ROOT / "parity" / "include" / "parity_buffscope.nss"
    for batch in range(1, 9):
        suite = f"buffscope{batch:02}"
        folder = ROOT / "parity" / "suites" / suite
        script = folder / "parity_main.nss"
        inputs = [script, folder / "suite.json", helper]
        readings = {}
        for engine in ["nwn", "openwinter"]:
            path = ROOT / "out" / "parity" / "results" / suite / f"{engine}.txt"
            if not path.exists():
                errors.append(f"{suite}/{engine}: missing results")
                continue
            if path.stat().st_mtime < max(p.stat().st_mtime for p in inputs):
                errors.append(f"{suite}/{engine}: results older than fixture inputs; rerun")
            lines = path.read_text(encoding="utf-8-sig").splitlines()
            readings[engine] = dict(line.split("|", 1) for line in lines)
            if readings[engine].get("__done__") != "1" or any(k.startswith("error.") for k in readings[engine]):
                errors.append(f"{suite}/{engine}: invalid completion or script error")
        if len(readings) != 2:
            continue
        for number, blueprint, label in re.findall(r'BCase\(\d+,(\d+),"([^"]+)"\); // (.+)', script.read_text(encoding="utf-8")):
            spell = int(number)
            prefix = f"spell.{spell}."
            native = {k[len(prefix):]: v for k, v in readings["nwn"].items() if k.startswith(prefix)}
            client = {k[len(prefix):]: v for k, v in readings["openwinter"].items() if k.startswith(prefix)}
            expected = {"effects", "has", "changed" if spell in RANDOM else "delta"}
            if spell in RANDOM:
                expected.add("roll.valid.expected1")
            if set(native) != expected or set(client) != expected or native != client:
                errors.append(f"{suite}/{spell}: missing or different readings")
            if native.get("has") != ("0" if spell in NO_STATS else "1"):
                errors.append(f"{suite}/{spell}: unexpected application state")
            if spell in RANDOM and native.get("roll.valid.expected1") != "1":
                errors.append(f"{suite}/{spell}: rolled bonus outside authored range or not applied to stats")
            cases.append({"id": spell, "label": label.strip(), "status": "verified-context",
                          "suite": suite, "caster": blueprint, "target": "self", "cheatInstant": True,
                          "difficulty": 3, "randomAmount": spell in RANDOM, "originalStatNoOp": spell in NO_STATS,
                          "readings": native,
                          "inputHashes": {str(p.relative_to(ROOT)): digest(p) for p in inputs}})
    if len(cases) != 93:
        errors.append(f"Expected 93 unique cases, found {len(cases)}")
    if len({c["id"] for c in cases}) != len(cases):
        errors.append("Duplicate spell cases")
    if errors:
        raise SystemExit("\n".join(errors))
    report = {"revision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
              "scope": "One stock caster and legal self-target context per listed spell; lasting effects and stat deltas",
              "notVerified": ["rolled amount distributions", "all caster levels", "metamagic and slot legality",
                              "other targets, saves and resistances", "damage-shield retaliation and absorption consumption",
                              "expiry, dispel and stacking for every spell", "visuals, animation and audio"],
              "rows": cases}
    out = ROOT / "out" / "spell-audit" / "statistics-ledger.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open("w", encoding="utf-8", newline="") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    print(f"PASS: {len(cases)} contexts, {sum(len(c['readings']) for c in cases)} readings; {len(RANDOM)} rolled-bonus checks")
    print(out)


if __name__ == "__main__":
    main()
