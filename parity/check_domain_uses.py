"""Require native/client positive controls in the domain_uses parity output."""
import sys
from pathlib import Path

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results/domain_uses")
expected = {
    "wizard.level": "20", "wizard.ready.none.d0": "1", "wizard.ready.extend.d0": "1",
    "wizard.ready.max.d0": "1",
    "2.ready.none.d1": "1", "2.ready.none.d0": "0", "2.ready.any.d1": "1",
    "4.ready.extend.d1": "1", "8.ready.max.d1": "1", # Unsupported cast metamagic still reports a use.
    "22.ready.none.d1": "0", # Mage Armor without the selected Magic domain.
    "46.ready.none.d3": "1", "48.ready.none.d0": "1", "48.ready.extend.d3": "1",
    "104.ready.extend.d1": "0", "124.ready.silent.d1": "1", "124.ready.still.d1": "1",
    "aggregate.three.extend.d1": "3", "aggregate.two.extend.d1": "2",
    "140.ready.none.d1": "1", "141.ready.none.d1": "1",
    "142.ready.extend.d1": "1", "143.ready.extend.d1": "1",
}
for engine in ("nwn", "openwinter"):
    rows = dict(line.split("|", 1) for line in (root / f"{engine}.txt").read_text(encoding="utf-8").splitlines() if "|" in line)
    for key, value in expected.items():
        assert rows.get(key) == value, f"{engine}: {key}: expected {value}, got {rows.get(key)}"
    for case in range(148):
        assert rows.get(f"{case}.level") == "20", f"{engine}: missing/invalid context {case}"
        for key, value in rows.items():
            if key.startswith(f"{case}.spent."):
                assert value == "0", f"{engine}: exhausted slot counted: {key}"
print("domain uses: selected-domain, metamagic, marker, aggregate and exhaustion controls pass")
