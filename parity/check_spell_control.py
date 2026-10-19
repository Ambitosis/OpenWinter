"""Require real casts, stable effect payloads and independently plausible save rates."""
from pathlib import Path
import math
import re
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results")
primary = {4: 33, 8: 67, 82: 27, 83: 27, 166: 37}
for spell, kind in primary.items():
    for engine in ("nwn", "openwinter"):
        path = root / f"spell_control_{spell:03}" / f"{engine}.txt"
        data = dict(line.split("|", 1) for line in path.read_text(encoding="utf-8").splitlines() if "|" in line)
        if data.get("__done__") != "1":
            raise ValueError(f"{path}: incomplete")
        for arm, expected in (("lowSave", .95), ("highSave", .05)):
            if data[arm + ".consumed"] != "20" or data[arm + ".inconsistent"] != "0":
                raise ValueError(f"{path}: missing casts or inconsistent effects")
            successes = round(float(data[arm + ".fraction"]) * 20)
            probabilities = [math.comb(20, k) * expected**k * (1 - expected)**(20 - k) for k in range(21)]
            pvalue = sum(p for p in probabilities if p <= probabilities[successes] + 1e-12)
            if pvalue < .001:
                raise ValueError(f"{path}: {arm} rejects expected save behavior (p={pvalue})")
        parts = data["effectData"].split(";")[:-1]
        types = []
        for part in parts:
            match = re.fullmatch(r"(\d+):(\d+):(\d+):(\d+)\[[^]]+\]:duration(\d+):cl20:creator1", part)
            if not match or int(match[4]) != spell or int(match[5]) != (0 if spell == 4 else 120):
                raise ValueError(f"{path}: unexpected creator/level/duration/payload")
            types.append(int(match[1]))
        if kind not in types or not data["statChanges"] or data["statChanges"] == "-":
            raise ValueError(f"{path}: no primary effect or actual stat change")
        print(f"{spell} {engine}:40 real casts; payload/state and independent save checks pass")

# Race-only deltas isolate stock script eligibility from creature blueprint feats.
races = [r for r in range(30) if r not in (21, 22, 26, 27, 28)]
for spell in (82, 83):
    for engine in ("nwn", "openwinter"):
        path = root / f"spell_race_{spell:03}" / f"{engine}.txt"
        data = dict(line.split("|", 1) for line in path.read_text(encoding="utf-8").splitlines() if "|" in line)
        if data.get("__done__") != "1":
            raise ValueError(f"{path}: incomplete")
        for race in races:
            key = f"race{race}"
            expected = int(spell == 82 or race <= 6 or 12 <= race <= 15)
            if (data[key + ".race"] != str(race) or data[key + ".consumed"] != "5"
                    or data[key + ".anyAffected"] != str(expected)):
                raise ValueError(f"{path}: failed race eligibility/actual-cast control {race}")
        print(f"{spell} {engine}: all 25 race-only eligibility contexts pass")
