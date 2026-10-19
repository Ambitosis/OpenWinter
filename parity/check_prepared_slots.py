"""Check independently specified native prepared-slot acceptance, not only engine agreement."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
accepted = {0, 2, 5, 8, 14}
expected_levels = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, 1, 1]
for engine in ("nwn", "openwinter"):
    path = root / "out/parity/results/prepared_slot_level" / (engine + ".txt")
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if "|" in line:
            key, value = line.split("|", 1)
            if key in rows:
                raise AssertionError(f"Duplicate reading: {key}")
            rows[key] = value
    assert rows.get("__done__") == "1", (engine, "incomplete")
    for index, level in enumerate(expected_levels):
        assert rows[f"{index}.level"] == str(level), (engine, index, "class level")
        assert rows[f"{index}.ready"] == "1", (engine, index, "not prepared")
        assert rows[f"{index}.after"] == ("0" if index in accepted else "1"), (engine, index, "acceptance")
    print(f"{engine}: 16 prepared-slot acceptance cases passed")
