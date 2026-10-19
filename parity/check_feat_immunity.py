"""Require consumed spell slots and the measured immunity branches in both engines."""
from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results")
for spell in (54, 155, 165):
    for engine in ("nwn", "openwinter"):
        path = root / f"feat_spell_{spell:03}" / f"{engine}.txt"
        data = dict(line.split("|", 1) for line in path.read_text(encoding="utf-8").splitlines() if "|" in line)
        if data.get("__done__") != "1":
            raise ValueError(f"{path}: incomplete")
        for mode in (0, 1):
            key = str(mode)
            if (data[key + ".consumed"] != "5" or data[key + ".feat"] != key
                    or data[key + ".immune"] != key or data[key + ".hd"] != "1"):
                raise ValueError(f"{path}: invalid actual-cast/immunity fixture ({key})")
            types = {int(x) for x in data[key + ".types"].split(",") if x and x != "-"}
            blocked = 30 if spell == 165 else 25
            if (blocked in types) != (mode == 0):
                raise ValueError(f"{path}: wrong sleep/fear branch ({key})")
            if mode == 1 and spell == 155 and not {41, 43, 51}.issubset(types):
                raise ValueError(f"{path}: Scare must retain its other penalties")
            if mode == 1 and spell != 155 and types:
                raise ValueError(f"{path}: blocked spell retained effects")
        print(f"{spell} {engine}: ten prepared casts, immunity branches pass")
