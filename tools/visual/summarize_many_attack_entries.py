"""Gate actor-coded native Attack entries before counting their voice samples."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def summarize(path):
    doc = json.loads(path.read_text(encoding='utf-8'))
    if doc['scene'] != 'voiceactors_entrynpcmany8_slots' or not doc.get('validated'):
        raise ValueError('Completed twenty-actor fixture required')
    if not doc['recording'].get('session', {}).get('restored'):
        raise ValueError('Owned audio session restoration required')
    origin = doc['recording']['started']
    notes = [(t-origin, line.split('VISUAL|note|', 1)[1].strip())
             for t, line in doc['notes'] if 'VISUAL|note|' in line]
    starts = [(t, n.split('.')) for t, n in notes if n.startswith('entry.start.')]
    stops = [(t, n.split('.')) for t, n in notes if n.startswith('entry.stop.')]
    if (sorted(int(p[2]) for _, p in starts) != list(range(20))
            or any(p[4] != '1' or p[6] != '0' for _, p in starts)):
        raise ValueError('Twenty distinct ready NPC entries required')
    if (sorted(int(p[2]) for _, p in stops) != list(range(20))
            or any(int(p[4]) < 1 or p[6] != '0' for _, p in stops)):
        raise ValueError('Every living actor must actually attack')
    attacks = [n for _, n in notes if n.startswith('entry.attack.')]
    if set(int(n.split('.')[2]) for n in attacks if n.endswith('.attackerPC.0')) != set(range(20)):
        raise ValueError('Every NPC requires its own native attack callback')
    if not any(n == 'entry.completed.20' for _, n in notes):
        raise ValueError('Completion marker missing')
    rows = json.loads(subprocess.check_output([
        sys.executable, str(Path(__file__).with_name('check_voice_actors.py')), str(path)], encoding='utf-8'))
    by_key = {(r['actor'], r['family']): r for r in rows}
    positives = []
    for t, n in notes:
        if not n.startswith('positive.actor.'):
            continue
        parts = n.split('.')
        actor, slot = int(parts[2]), int(parts[4])
        family = 'attack0' if slot == 0 else f'battlecry{slot}'
        identified = any(abs(c-t) < .5 for c in by_key[(actor, family)]['onsets'])
        positives.append(dict(actor=actor, slot=slot, identified=identified))
    if (len(positives) != 80 or len({(p['actor'], p['slot']) for p in positives}) != 80
            or not all(p['identified'] for p in positives)):
        raise ValueError('All eighty actor/slot sample positives must identify')
    ends = {int(p[2]): (t, int(p[4])) for t, p in stops}
    entries = []
    for start, parts in starts:
        actor = int(parts[2]); end, count = ends[actor]
        cues = [dict(family=r['family'], time=t, duration=d)
                for r in rows if r['actor'] == actor
                for t, d in zip(r['onsets'], r['durations']) if start <= t < end]
        entries.append(dict(actor=actor, start=start, end=end, attacks=count, cues=cues))
    first_positive = min(t for t, n in notes if n.startswith('positive.actor.'))
    windows = {e['actor']: (e['start'], e['end']) for e in entries}
    outside = [dict(actor=r['actor'], family=r['family'], time=t, duration=d)
               for r in rows for t, d in zip(r['onsets'], r['durations'])
               if t < first_positive-.5 and not windows[r['actor']][0] <= t < windows[r['actor']][1]]
    return dict(source=str(path), sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
                fixtureSha256=doc['fixtureSha256'], entries=entries, positiveControls=positives,
                unattributedPreControlCues=outside,
                limitations='Single fresh stock NPC entries against passive targets. Effects muted; synthetic120ms samples. Fixed detector minimum50ms; no changed thresholds. Buffered note timestamps do not prove playback latency. Count evidence alone does not establish an exact chance or cooldown.')


if __name__ == '__main__':
    print(json.dumps(summarize(Path(sys.argv[1])), indent=2))
