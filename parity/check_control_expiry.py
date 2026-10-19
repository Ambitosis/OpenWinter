"""Validate natural expiry bounds of normal and Extended prepared control spells."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
for engine in ("nwn", "openwinter"):
    path = root / "out/parity/results/spell_control_expiry" / (engine + ".txt")
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if "|" not in line:
            continue
        key, value = line.split("|", 1)
        assert key not in rows, (engine, "duplicate", key)
        rows[key] = value
    assert rows.get("__done__") == "1", (engine, "incomplete")
    for case in range(9):
        def expect(field, value):
            assert rows.get(f"{case}.{field}") == str(value), (engine, case, field, rows.get(f"{case}.{field}"))
        expect("consumed", 5)
        expect("found", 1)
        assert rows[f"{case}.statChanges"] not in ("", "-"), (engine, case, "no initial stat change")
        if case == 8:
            expect("duration", 0)
            expect("durationType", 2)
            expect("after.present", 1)
            expect("after.restored", 0)
        else:
            expect("duration", 120 if case < 4 else 240)
            expect("durationType", 1)
            expect("remaining.valid", 1)
            expect("before.present", 1)
            expect("after.present", 0)
            expect("after.restored", 1)
    print(f"{engine}: 45 prepared casts; 8 natural expiry/stat restoration bounds and 1 permanent control passed")
