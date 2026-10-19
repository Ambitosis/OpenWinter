"""Require native defaults and actual domain-cast positive/negative controls."""
import sys
from pathlib import Path
root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results/domain_defaults")
expected = {
    "dd_none.domain1": "0", "dd_none.domain2": "1",
    "dd_first.domain1": "13", "dd_first.domain2": "1",
    "dd_second.domain1": "0", "dd_second.domain2": "18",
    "dd_invalid1.domain1": "255", "dd_invalid1.domain2": "1",
    "dd_invalid2.domain1": "0", "dd_invalid2.domain2": "255",
    "dd_duplicate.domain1": "13", "dd_duplicate.domain2": "13",
    "dd_none.cats.catsEffect": "1", "dd_none.cats.catsReady": "0",
    "dd_first.cats.catsEffect": "1", "dd_first.mage.mageEffect": "1",
    "dd_second.cats.catsEffect": "0", "dd_invalid2.cats.catsEffect": "0",
    "dd_wizfields.domain1": "0", "dd_wizfields.domain2": "1",
    "dd_fightfields.domain1": "0", "dd_fightfields.domain2": "1",
    "dd_wizfields.cats.catsEffect": "0", "dd_wizfields.mage.mageEffect": "0",
    "dd_wizfields.ordinary.catsEffect": "1", "dd_wizard.ordinary.catsEffect": "1",
}
for engine in ("nwn", "openwinter"):
    rows = dict(line.split("|", 1) for line in (root / f"{engine}.txt").read_text(encoding="utf-8").splitlines() if "|" in line)
    for key, value in expected.items():
        assert rows.get(key) == value, f"{engine}: {key}: expected {value}, got {rows.get(key)}"
print("domain defaults: independent fields, non-domain classes and actual casting controls pass")
