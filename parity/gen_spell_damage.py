"""Generate maximized stock offensive-spell application probes; no assets are copied."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parent.parent
# Explicitly selected living-creature damage contexts. Each needs native validation.
WIZARD = [10, 14, 25, 58, 59, 101, 107, 115, 368, 370, 371, 424, 440, 447, 448, 516, 523]
CLERIC = [61, 156, 167, 431, 432, 433, 434, 435]
for spell in WIZARD + CLERIC:
    folder = ROOT / 'parity' / 'suites' / f'spell_damage_{spell:03d}'
    folder.mkdir(exist_ok=True)
    blueprint = 'nw_drowmage020' if spell in WIZARD else 'nw_drowcler020'
    cls = 'CLASS_TYPE_WIZARD' if spell in WIZARD else 'CLASS_TYPE_CLERIC'
    meta = 'METAMAGIC_NONE' if spell == 431 else 'METAMAGIC_MAXIMIZE'
    # Ball Lightning rolls fifteen independent saves. More targets make the unsaved peak observable.
    count = 21 if spell == 516 else 11
    with (folder / 'parity_main.nss').open('w', encoding='utf-8', newline='') as f:
        f.write(f'#include "parity_damage"\nvoid main() {{ DCase({spell},"{blueprint}",{cls},{meta},{count}); }}\n')
    with (folder / 'suite.json').open('w', encoding='utf-8', newline='') as f:
        json.dump(dict(seconds=20, size=24 if count > 11 else 16, tile=4), f)
        f.write('\n')
print(f'{len(WIZARD) + len(CLERIC)} offensive spell contexts generated')
