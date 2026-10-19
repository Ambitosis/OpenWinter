"""Validate that immunity probes cast spells and retain the native-observed effect subsets."""
from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results")
for spell in (4, 8, 82, 83, 166):
    for engine in ("nwn", "openwinter"):
        path = root / f"spell_immune_{spell:03}" / f"{engine}.txt"
        data = dict(line.split("|", 1) for line in path.read_text(encoding="utf-8").splitlines() if "|" in line)
        if data.get("__done__") != "1":
            raise ValueError(f"{path}: incomplete")
        for immunity in (0, 1, 6, 7, 8, 9, 17, 19):
            key = f"immune{immunity}"
            if data[key + ".consumed"] != "5" or data[key + ".inconsistent"] != "0":
                raise ValueError(f"{path}: missing casts or inconsistent recipients ({key})")
            expected = ({33} if spell == 4 else {13, 67, 74} if spell == 8
                        else {27, 74} if spell in (82, 83) else {37, 74})
            if (spell == 4 and immunity == 17 or spell in (82, 83) and immunity in (1, 6)
                    or spell == 166 and immunity == 9):
                expected = set()
            if spell == 8 and immunity in (7, 8):
                expected -= {67 if immunity == 7 else 13}
            value = data[key + ".effects"]
            observed = set() if value == "-" else {int(part.split(":", 1)[0]) for part in value.split(";")[:-1]}
            if observed != expected or data[key + ".anyAffected"] != str(int(bool(expected))):
                raise ValueError(f"{path}: wrong effect subset ({key})")
            if spell == 4 and immunity == 19 and data[key + ".stats"] != "-":
                raise ValueError(f"{path}: curse bypassed ability-decrease immunity")
        print(f"{spell} {engine}: 40 actual casts, all immunity subsets pass")
