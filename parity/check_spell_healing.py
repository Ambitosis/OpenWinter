"""Validate the narrow self-healing contexts, not complete spell parity."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent.parent
IDS = [31, 32, 33, 34, 35, 79, 80, 114, 374, 525]
folder = ROOT / 'parity/suites/spell_healing'
inputs = [folder / 'parity_main.nss', folder / 'suite.json', ROOT / 'parity/include/parity_inc.nss']
expected = {f'spell.{spell}.{key}' for spell in IDS for key in
            ['before', 'max', 'prepared'] + [f't{t}.{key}' for t in [2, 8, 14] for key in ['hp', 'consumed', 'hasEffect']]}
readings = {}
for engine in ['nwn', 'openwinter']:
    path = ROOT / 'out/parity/results/spell_healing' / (engine + '.txt')
    if path.stat().st_mtime < max(p.stat().st_mtime for p in inputs):
        raise SystemExit(f'{engine}: stale results')
    values = dict(line.split('|', 1) for line in path.read_text(encoding='utf-8-sig').splitlines())
    if values.pop('__done__', None) != '1' or set(values) != expected:
        raise SystemExit(f'{engine}: incomplete, errored or unexpected result set')
    for spell in IDS:
        def value(key): return int(values[f'spell.{spell}.{key}'])
        if value('before') != 1 or value('prepared') != 1 or value('max') <= 53:
            raise SystemExit(f'{engine}/{spell}: invalid damaged-caster/prepared-slot context')
        if any(value(f't{t}.consumed') != 1 for t in [2, 8, 14]) or value('t14.hp') <= 1:
            raise SystemExit(f'{engine}/{spell}: spell did not cast/heal')
        if any(not 1 <= value(f't{t}.hp') <= value('max') for t in [2, 8, 14]):
            raise SystemExit(f'{engine}/{spell}: HP outside valid range')
    readings[engine] = values
if readings['nwn'] != readings['openwinter']:
    raise SystemExit('Different healing measurements')
report = dict(revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
              scope='Ten stock level-20 Cleric self-healing contexts, real slots, difficulty 3, HP/effects at2/8/14 seconds',
              ids=IDS, status='verified-self-healing-context', readings=readings,
              inputHashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
              notVerified=['other levels or targets', 'undead interactions', 'area party/henchman targeting',
                           'random unmodified cure distributions', 'later regeneration/expiry', 'animation, VFX and sound'])
out = ROOT / 'out/spell-audit/healing-ledger.json'
out.parent.mkdir(parents=True, exist_ok=True)
with out.open('w', encoding='utf-8', newline='') as stream:
    json.dump(report, stream, indent=2)
    stream.write('\n')
print('PASS: 10 self-healing contexts / 120 matching readings')
