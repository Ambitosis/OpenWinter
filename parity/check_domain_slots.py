"""Check native-observed domain slot decisions and script context, not just equality."""
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "out/parity/results/domain_slots"
# Native successful cases: (base spell level, metamagic).
accepted = {0: (1, 0), 5: (1, 0), 18: (4, 0), 22: (3, 0), 29: (4, 0),
            31: (1, 0), 32: (1, 2), 33: (4, 2), 34: (3, 2), 36: (1, 0),
            37: (4, 0), 39: (1, 2)}
results = []
for engine in ("nwn", "openwinter"):
    rows = [line.split("|", 1) for line in (root / f"{engine}.txt").read_text(encoding="utf-8-sig").splitlines() if "|" in line]
    values = dict(rows)
    assert len(rows) == len(values) == 601, (engine, "missing/duplicate readings")
    assert values["__done__"] == "1", (engine, "incomplete")
    for case in range(40):
        def value(field): return int(values[f"{case}.{field}"])
        success = case in accepted
        assert value("classlevel") == 20 and value("ready") == 1, (engine, case, "fixture")
        assert value("after") == int(not success), (engine, case, "consumption")
        assert value("effect") == value("casts") == int(success), (engine, case, "actual cast")
        expected = accepted.get(case, (0, 0))
        assert value("castlevel") == expected[0] and value("castmeta") == expected[1], (engine, case, "context")
        assert value("castclass") == (2 if success else 0), (engine, case, "class")
        assert value("casterlevel") == (20 if success else 0), (engine, case, "caster level")
        assert value("castdc") == (15 + expected[0] if success else 0), (engine, case, "DC")
    results.append(values)
assert results[0] == results[1], "native/replacement fields differ"
print("domain_slots: 600 matching readings; all 40 eligibility and script-context controls pass")
