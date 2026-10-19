"""Record a native inventory fixture; optionally identify placement resources with tones.

python tools/visual/inventory_audio.py inventorysounds --markers
Optional recorder dependencies: out/audio-probe (pycaw, comtypes). Each installed inventorysnds
resource gets a temporary, exclusively created two-tone override. The frequency
ratio identifies its row even if the native engine changes playback pitch. This
is resource-selection evidence, not proof of original waveform/gain fidelity.
Only the harness-owned process is captured. Overrides are removed in finally.
"""
import argparse
import json
import math
import os
from pathlib import Path
import runpy
import struct
import subprocess
import sys
import wave

sys.path[:0] = [str(Path('out/audio-probe').resolve()), str(Path(__file__).resolve().parent)]
import visual

args = argparse.ArgumentParser(description=__doc__)
args.add_argument('scene', choices=['inventorysounds', 'inventoryidentify'])
args.add_argument('--markers', action='store_true')
args.add_argument('--swap-leather-row', action='store_true', help='Verify table lookup by redirecting row4 to it_genericlarge; requires --markers')
args = args.parse_args()
if args.swap_leather_row and not args.markers:
    raise SystemExit("--swap-leather-row requires --markers")
original = visual.prepare_user
created = []


def prepare(*a, **kw):
    user = original(*a, **kw)
    if not args.markers:
        return user
    cfg = dict(visual.config(), userRoot=str(visual.OUT / 'resource-user'), module=str(visual.OUT / 'build' / ('vis_' + args.scene + '.mod')), liveUserData=False)
    Path(cfg['userRoot']).mkdir(exist_ok=True)
    config = visual.REPO / 'out' / 'inventory-audio-config.json'
    config.write_text(json.dumps(cfg), encoding='utf-8', newline='')
    table = subprocess.run(['dotnet', 'run', '--no-build', '--project', str(visual.REPO / 'src/OpenWinter.Tools'),
                            '-c', 'Release', '--', '2da', 'inventorysnds', '256', '--base'],
                           cwd=visual.REPO, env=dict(os.environ, OPENWINTER_CONFIG=str(config)),
                           capture_output=True, text=True, check=True).stdout
    folder = user / 'override'
    folder.mkdir(exist_ok=True)
    mapping = []
    for line in table.splitlines()[1:]:
        row, label, resource = line.split('\t')
        row = int(row)
        if not resource or resource == '****':
            continue
        path = folder / (resource + '.wav')
        low, high = 300, 300 * (2 + row * .125)
        with path.open('xb') as raw:
            created.append(path)
            with wave.open(raw, 'wb') as output:
                output.setparams((1, 2, 44100, 0, 'NONE', 'not compressed'))
                samples = []
                for i in range(11025):
                    t = i / 44100
                    fade = min(1, i / 220, (11024 - i) / 220)
                    samples.append(int(4000 * fade * (math.sin(2 * math.pi * low * t) + math.sin(2 * math.pi * high * t))))
                output.writeframes(struct.pack('<' + 'h' * len(samples), *samples))
        mapping.append(dict(row=row, label=label, resource=resource, frequencies=[low, high]))
    if args.swap_leather_row:
        path = folder / 'inventorysnds.2da'
        lines = ['2DA V2.0', '', 'Label InventorySound']
        for entry in mapping:
            resource = 'it_genericlarge' if entry['row'] == 4 else entry['resource']
            lines.append(f"{entry['row']} {entry['label']} {resource}")
        with path.open('x', encoding='utf-8', newline='') as output:
            created.append(path)
            output.write('\n'.join(lines) + '\n')
    (visual.REPO / 'out' / (args.scene + '_inventory-markers.json')).write_text(
        json.dumps(mapping, indent=2), encoding='utf-8', newline='')
    return user


visual.prepare_user = prepare
sys.argv = ['spellvoice_audio.py', args.scene, 'all']
try:
    runpy.run_path(str(Path(__file__).with_name('spellvoice_audio.py')), run_name='__main__')
finally:
    visual.prepare_user = original
    for path in created:
        path.unlink()
