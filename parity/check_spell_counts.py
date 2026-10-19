"""Require positive native legacy-count controls and matching complete observations."""
import sys
from pathlib import Path
root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results/spell_counts")
expected = {
    "wizard.1.4.0.count": "1", "wizard.2.2.0.count": "3",
    "wizard.2.0.0.count": "0", "cantrip.1.2.0.count": "3",
    "undead.8.0.0.count": "1", "undead.6.0.0.count": "0",
    "druid.4.0.0.count": "1", "druid.3.0.0.count": "0",
    "travel.3.0.0.count": "2", "travel.4.2.0.count": "2",
    "travel.5.1.0.count": "4", "travel.6.4.0.count": "1",
    "duplicate.3.0.0.count": "2", "both.4.0.0.count": "2",
    "haste.5.0.0.count": "2", "haste.5.2.0.count": "2",
    "aggregate.all": "5", "aggregate.two": "2",
    "aggregate.one": "1", "aggregate.empty": "0",
    "convert.0.3.2.0.count": "1", "convert.1.3.2.0.count": "1",
    "convert.0.3.2.1.count": "0", "convert.1.3.2.1.count": "0",
    "raw.3.3.count": "2", "raw.3.6.count": "2", "raw.3.7.count": "2",
    "raw.3.5.count": "1", "raw.4.2.count": "1", "raw.5.1.count": "2",
    "__done__": "1",
}
engines = []
for engine in ("nwn", "openwinter"):
    rows = dict(line.split("|", 1) for line in (root / f"{engine}.txt").read_text(encoding="utf-8").splitlines() if "|" in line)
    assert len(rows) == 17463, (engine, len(rows))
    for key, value in expected.items():
        assert rows.get(key) == value, f"{engine}: {key}: expected {value}, got {rows.get(key)}"
    for key, value in rows.items():
        if key.endswith(".spent"):
            assert value == "0", (engine, key, value)
        if key.startswith("travel.") and key.endswith(".count"):
            for profile in ("reverse", "allfeats", "partial"):
                assert rows.get(profile + key[len("travel"):]) == value, (engine, profile, key)
    engines.append(rows)
assert engines[0] == engines[1], "native and replacement readings differ"
print("spell counts: 17,462 observations match; aggregate, duplicate, exhaustion and authorization-independent reporting controls pass")
