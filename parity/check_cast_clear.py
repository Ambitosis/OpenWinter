"""Reject uncommitted casts and missing queue/recovery controls."""
from pathlib import Path
import sys
root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results/cast_clear")
for engine in ("nwn", "openwinter"):
    text = (root / (engine + ".txt")).read_text(encoding="utf-8")
    assert "error." not in text.lower(), engine + ": script error"
    rows = dict(line.split("|", 1) for line in text.splitlines() if "|" in line)
    assert rows.get("__done__") == "1", engine + ": incomplete"
    for i in range(7):
        values = {"class": 1, "prepared": 102, "preparedReady": 1, "committed": 1,
                  "during.impacts": 1, "during.old": 0, "during.next": 0,
                  "during.action": 4, "during.ready": 0, "during.moved": 0,
                  "done.impacts": 1, "done.old": int(i == 0), "done.next": int(1 <= i <= 5),
                  "done.action": 65535, "done.ready": 0}
        if i:
            values["readyAtClear"] = 0
        for key, value in values.items():
            assert rows.get(f"{i}.{key}") == str(value), (engine, i, key, rows.get(f"{i}.{key}"), value)
        impact = float(rows[f"{i}.done.impactAfterCommit"].split("|")[0])
        assert .35 <= impact <= .6, (engine, i, "invalid commit anchor", impact)
        if 1 <= i <= 5:
            next_time = float(rows[f"{i}.done.nextAfterCommit"].split("|")[0])
            assert next_time >= 1.3, (engine, i, "premature continuation", next_time)
print("cast clear: all seven observed-commit, queued-command, wait, movement and recovery controls pass")
