"""Check real spell-slot application and produce scoped peak-damage evidence."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parent.parent


def main():
    errors, cases = [], []
    helper = ROOT / 'parity/include/parity_damage.nss'
    total_samples = 0
    for folder in sorted((ROOT / 'parity/suites').glob('spell_damage_[0-9][0-9][0-9]')):
        suite = folder.name
        script = folder / 'parity_main.nss'
        match = re.search(r'DCase\((\d+),"([^"]+)",(\w+),(\w+),(\d+)\)', script.read_text(encoding='utf-8'))
        if not match:
            errors.append(f'{suite}: invalid case declaration')
            continue
        spell, blueprint, cls, meta, count = match.groups()
        count = int(count)
        total_samples += count
        expected_keys = {f'{key}.expected{count}' for key in ['samples', 'targets', 'seen', 'slots', 'consumed']}
        expected_keys.update(['damage.expected1', 'peak'])
        inputs = [script, folder / 'suite.json', helper]
        readings = {}
        for engine in ['nwn', 'openwinter']:
            path = ROOT / 'out/parity/results' / suite / (engine + '.txt')
            if not path.exists():
                errors.append(f'{suite}/{engine}: missing results')
                continue
            if path.stat().st_mtime < max(p.stat().st_mtime for p in inputs):
                errors.append(f'{suite}/{engine}: stale results')
            values = dict(line.split('|', 1) for line in path.read_text(encoding='utf-8-sig').splitlines())
            if values.get('__done__') != '1' or any(k.startswith(('error.', 'unimplemented.')) for k in values):
                errors.append(f'{suite}/{engine}: invalid completion')
            prefix = f'spell.{spell}.'
            readings[engine] = {k[len(prefix):]: v for k, v in values.items() if k.startswith(prefix)}
            if set(readings[engine]) != expected_keys:
                errors.append(f'{suite}/{engine}: incomplete measurements')
            for key, value in readings[engine].items():
                if '.expected' in key and value != key.rsplit('.expected', 1)[1]:
                    errors.append(f'{suite}/{engine}: {key} was {value}')
        if len(readings) != 2 or readings.get('nwn') != readings.get('openwinter'):
            errors.append(f'{suite}: different or missing damage measurements')
        cases.append(dict(id=int(spell), suite=suite, caster=blueprint, casterClass=cls,
                          target='nw_bandit001; living hostile; no spell resistance',
                          status='verified-peak-context', metamagic=meta, cheat=False, instant=True,
                          samples=count, damageStatistic='maximum observed HP loss five seconds after cast request',
                          readings=readings, inputHashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}))
    if len(cases) != 25:
        errors.append(f'Expected 25 offensive spell contexts, got {len(cases)}')
    if errors:
        raise SystemExit('\n'.join(errors))
    report = dict(revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  scope='Installed level-20 caster templates, real spell slots (Maximize except Inflict Minor Wounds), living hostile targets, difficulty 3',
                  notVerified=['all levels or targets', 'random damage/save/touch-attack distributions',
                               'normal and other metamagic damage', 'spell resistance and immunity',
                               'secondary area targets', 'ongoing damage after five seconds',
                               'visuals, animations and audio'], rows=cases)
    out = ROOT / 'out/spell-audit/damage-ledger.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open('w', encoding='utf-8', newline='') as stream:
        json.dump(report, stream, indent=2)
        stream.write('\n')
    print(f'PASS: {len(cases)} peak-damage contexts / {total_samples} casts / {len(cases) * 7} readings')


if __name__ == '__main__':
    main()
