"""Attribute V-menu sample identities and retain raw module/associate callbacks."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def summarize(path, paths):
    doc = json.loads(path.read_text(encoding='utf-8'))
    if doc['scene'] != 'voiceactors_quickchat_slots' or not doc.get('validated'):
        raise ValueError('Completed quickchat capture required')
    if doc['engine'] == 'native' and not doc['recording'].get('session', {}).get('restored'):
        raise ValueError('Owned native audio session restoration required')
    origin = doc['recording']['started']
    notes = [(t-origin, line.split('VISUAL|note|', 1)[1].strip())
             for t, line in doc['notes'] if 'VISUAL|note|' in line]
    if not any(n.startswith('quickchat.ready.1.pc.1.name.') and n.endswith('.master.1') for _, n in notes):
        raise ValueError('Actual player, soundset and henchman context required')
    if not any(n == 'quickchat.completed.34' for _, n in notes):
        raise ValueError('Completion marker missing')
    rows = json.loads(subprocess.check_output([
        sys.executable, str(Path(__file__).with_name('check_voice_actors.py')), str(path)], encoding='utf-8'))
    by_slot = {int(r['family'][4:]): r for r in rows}
    positives = []
    for t, n in notes:
        if n.startswith('positive.slot.'):
            slot = int(n.split('.')[2])
            positives.append(dict(slot=slot, identified=any(abs(c-t) < .5 for c in by_slot[slot]['onsets'])))
    if (len(positives) != 49 or len({p['slot'] for p in positives}) != 49
            or not all(p['identified'] for p in positives)):
        raise ValueError('Every one of the49 reference samples must identify')
    starts = [(t, n.split('.')) for t, n in notes if n.startswith('quickchat.start.')]
    ends = [(t, n.split('.')) for t, n in notes if n.startswith('quickchat.end.')]
    if (sorted(int(p[2]) for _, p in starts) != list(range(34))
            or sorted(int(p[2]) for _, p in ends) != list(range(34))):
        raise ValueError('All34 distinct menu input intervals required')
    paths_hash = hashlib.sha256(paths.read_bytes()).hexdigest()
    if doc['fixtureSha256'].get('paths.json') != paths_hash:
        raise ValueError('Path inventory does not match the captured fixture')
    paths_doc = json.loads(paths.read_text(encoding='utf-8'))
    if [p['index'] for p in paths_doc] != list(range(34)):
        raise ValueError('Complete indexed keyboard-path inventory required')
    stop = {int(p[2]): t for t, p in ends}
    entries = []
    for start, parts in starts:
        index, expected = int(parts[2]), int(parts[4])
        if paths_doc[index]['slot'] != expected:
            raise ValueError('Input inventory disagrees with captured fixture')
        end = stop[index]
        cues = [dict(slot=int(r['family'][4:]), time=t, duration=d)
                for r in rows for t, d in zip(r['onsets'], r['durations']) if start <= t < end]
        allowed = (1, 2, 3) if expected == 1 else (expected,)
        gui = [n for _, n in notes if n.startswith(f'quickchat.gui.trial.{index}.')]
        orders = [n for _, n in notes if n.startswith(f'quickchat.order.trial.{index}.')]
        keys = paths_doc[index]['path']
        expected_gui = [f'quickchat.gui.trial.{index}.type.18.integer.0.pc.1'] + [
            f'quickchat.gui.trial.{index}.type.19.integer.{ord(key)}.pc.1' for key in keys[1:]]
        command = {0: -3, 4: -4, 9: -7, 10: -2, 22: -5}.get(expected)
        expected_orders = ([] if command is None else [
            f'quickchat.order.trial.{index}.command.{command}.pattern.{command}.masterPC.1'])
        entries.append(dict(**paths_doc[index], start=start, end=end, cues=cues,
                            sampleMatched=len(cues) == 1 and cues[0]['slot'] in allowed,
                            guiEvents=gui, guiMatched=gui == expected_gui,
                            associateEvents=orders, associateMatched=orders == expected_orders))
    return dict(source=str(path), sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
                pathsSha256=paths_hash, fixtureSha256=doc['fixtureSha256'],
                entries=entries, positiveControls=positives,
                allMenuSamplesMatched=all(e['sampleMatched'] for e in entries),
                allGuiEventsMatched=all(e['guiMatched'] for e in entries),
                allAssociateEventsMatched=all(e['associateMatched'] for e in entries),
                cancellationEvents=[n for _, n in notes if n.startswith('quickchat.gui.trial.-2.')],
                limitations='Synthetic sample identity, explicit context and module callback evidence only. Missing/mixed menu clips remain unresolved rather than silent by assumption. Battlecry1 menu choice may select1/2/3. No installed-sound gain/envelope parity or general associate AI response claimed. Native menu frames require separate inspection.')


if __name__ == '__main__':
    print(json.dumps(summarize(Path(sys.argv[1]), Path(sys.argv[2])), indent=2))
