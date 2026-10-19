"""Check positive action/balance gates in authored goldpoint native observations.

Usage: python tools/visual/check_goldpoint.py <near|far|cancel> <notes.txt>
Pixels/dialog readiness must also be inspected; this does not certify visual parity.
"""
import json
import math
import sys
from pathlib import Path

mode, path = sys.argv[1], Path(sys.argv[2])
rows = []
piles = []
for line in path.read_text(encoding="utf-8").splitlines():
    fields = line.split("|")
    if len(fields) < 5 or fields[:2] != ["VISUAL", "note"]:
        continue
    data = dict(part.split("=", 1) for part in fields[4:])
    data = {key: float(value) for key, value in data.items()}
    data["phase"] = fields[3]
    (rows if fields[2] == "gold" else piles).append(data)
accepted = next(r for r in rows if r["phase"] == "accepted")
final = next(r for r in rows if r["phase"] == "final")
assert accepted["balance"] == 198 and accepted["piles"] == 0
assert math.dist((accepted["x"], accepted["y"]), (20, 15)) < 0.01, "Earlier UI input moved PC; rejected"
actions = [r for r in rows if r["action"] == 2]
assert actions, "No native gold-drop action: reject input conclusions"
result = {"mode": mode, "actionStart": float(actions[0]["phase"]), "final": final}
if mode == "cancel":
    assert final["balance"] == 198 and final["piles"] == 0 and not piles
    assert final["action"] == 65535
    assert math.dist((final["x"], final["y"]), (20, 15)) > 0.1
else:
    assert final["balance"] == 0 and final["piles"] == 1
    assert all(p["count"] == 198 for p in piles)
    spent = next(r for r in rows if r["balance"] == 0)
    result["spentAt"] = float(spent["phase"])
    result["distance"] = piles[-1]["pcdistance"]
    assert all(r["balance"] == 198 for r in rows if r["phase"] not in ("accepted", "final") and float(r["phase"]) < result["spentAt"])
    if mode == "near":
        assert all(math.dist((r["x"], r["y"]), (20, 15)) < 0.01 for r in rows)
        assert result["distance"] < 1
        result["dropDelay"] = result["spentAt"] - result["actionStart"]
        assert 1.35 <= result["dropDelay"] <= 1.65
    else:
        assert abs(result["distance"] - 1) < 0.01
        assert piles[-1]["facing"] == 0
        stopped = next(r for r in rows if r["phase"] not in ("accepted", "final") and math.dist((r["x"], r["y"]), (final["x"], final["y"])) < 0.01)
        result["stoppedAt"] = float(stopped["phase"])
        result["stopToSpend"] = result["spentAt"] - result["stoppedAt"]
print(json.dumps(result, indent=2))
