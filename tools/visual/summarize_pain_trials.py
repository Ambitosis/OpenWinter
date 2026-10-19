"""Summarize single-victim pain probes from original process or client Master PCM.
Usage: python tools/visual/summarize_pain_trials.py out/voice_slots_*pain*.json
Requires numpy and scipy. Thresholds are fixed in check_voice_actors.py.
Log receipt times are buffered: nearby events are not exact audio latency proof.
"""
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys
from scipy.stats import binomtest, chisquare


def wilson(k, n):
    if not n:
        return None
    z = 1.959963984540054
    p = k/n
    center = (p+z*z/(2*n))/(1+z*z/n)
    half = z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/(1+z*z/n)
    return [center-half, center+half]


def trial(path):
    doc = json.loads(path.read_text(encoding='utf-8'))
    if not doc.get('validated'):
        raise ValueError(f'{path}: fixture completion not validated')
    if doc.get('engine', 'native') == 'native' and not doc['recording'].get('session', {}).get('restored'):
        raise ValueError(f'{path}: own process audio-session restoration unproven')
    origin = doc['recording']['started']
    notes = [(t-origin, line.split('VISUAL|note|', 1)[1].strip())
             for t, line in doc['notes'] if 'VISUAL|note|' in line]
    if not any('VISUAL|done|1' in line for _, line in doc['notes']):
        raise ValueError(f'{path}: completion marker missing from recording')
    rows = json.loads(subprocess.check_output([sys.executable,
        str(Path(__file__).with_name('check_voice_actors.py')), str(path)], encoding='utf-8'))
    cues = sorted((t, duration, row['family']) for row in rows
        if row['actor'] == 0 and row['family'].startswith('pain')
        for t, duration in zip(row['onsets'], row['durations']))
    events = []
    for t, note in notes:
        m = re.match(r'damage.event.(\d+).phase.(\d+).amount.(\d+).hp.(\d+)', note)
        if m:
            events.append(dict(time=t, event=int(m[1]), phase=int(m[2]), amount=int(m[3]), hp=int(m[4])))
    starts = [(t, note) for t, note in notes if note.startswith(('combat.seen.', 'combat.immune.', 'combat.start.')) or note == 'combat.normal']
    if not starts or not any('seen.1.enemy.1' in note for _, note in starts):
        raise ValueError(f'{path}: no valid perception/hostility control')
    segments = []
    for index, (start, label) in enumerate(starts):
        following = starts[index+1][0] if index+1 < len(starts) else float('inf')
        stop = next((t+2 for t, n in notes if t > start and n.startswith('combat.done.')), following)
        positive = next((t for t, n in notes if t > start and n.startswith('positive.slot.') and n != 'positive.slot.34'), following)
        end = min(following, stop, positive)
        if not math.isfinite(end):
            raise ValueError(f'{path}: missing bounded combat end')
        subset = [(t, d, slot) for t, d, slot in cues if start <= t < end]
        damage = [e for e in events if start <= e['time'] < end and e['amount'] > 0]
        segments.append(dict(label=label, start=start, end=end, damagingEvents=len(damage),
            painCues=len(subset), perSlot={slot:sum(s == slot for _, _, s in subset) for slot in sorted({s for _, _, s in subset})},
            cues=[dict(time=t, duration=d, slot=slot, nearbyDamage=[e['event'] for e in damage if abs(e['time']-t) <= .5]) for t, d, slot in subset]))
    first_combat = starts[0][0]
    first_request = next((t for t, note in notes if note.startswith('damage.request.')), None)
    scripted = [e for e in events if e['time'] < first_combat]
    scripted_cues = [t for t, _, _ in cues if first_request is not None and first_request-.1 <= t < first_combat]
    positive_controls = []
    for t, note in notes:
        if note.startswith('positive.slot.'):
            slot = int(note.split('.')[-1])
            if slot in (14, 15, 16):
                positive_controls.append(dict(slot=slot, time=t, identified=any(abs(t-c) <= .5 and family in ('pain', f'pain{slot}') for c, _, family in cues)))
    hello = [dict(onset=t, duration=d) for row in rows if row['family'] == 'hello' for t, d in zip(row['onsets'], row['durations'])]
    normal = [s for s in segments if not s['label'].startswith('combat.immune.')]
    k, n = sum(s['painCues'] for s in normal), sum(s['damagingEvents'] for s in normal)
    if k > n:
        raise ValueError(f'{path}: more pain cues than damaging events')
    return dict(source=str(path), sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        waveformSha256=hashlib.sha256(Path(doc['recording']['path']).read_bytes()).hexdigest(),
        fixtureSha256=doc['fixtureSha256'], engine=doc.get('engine', 'native'),
        scriptedDamageEvents=len(scripted), scriptedPainCues=len(scripted_cues),
        positiveControls=positive_controls, hello=hello, segments=segments,
        outsideCombatCues=[dict(time=t,duration=d,slot=slot) for t,d,slot in cues if not any(s['start'] <= t < s['end'] for s in segments)],
        damagingEvents=n, painCues=k, wilson95=wilson(k,n))


def summarize(paths):
    trials = [trial(p) for p in paths]
    if len({t['engine'] for t in trials}) != 1:
        raise ValueError('Do not pool native and client observations')
    k, n = sum(t['painCues'] for t in trials), sum(t['damagingEvents'] for t in trials)
    slots = {str(slot):sum(s['perSlot'].get(f'pain{slot}', 0) for t in trials for s in t['segments']) for slot in (14,15,16)}
    return dict(trials=trials, combined=dict(painCues=k, damagingEvents=n, rate=k/n if n else None,
        wilson95=wilson(k,n), perSlot=slots,
        uniformSlotChiSquarePValue=float(chisquare(list(slots.values())).pvalue) if sum(slots.values()) else None,
        candidateTwoSidedPValues={str(p):float(binomtest(k,n,p).pvalue) for p in (.5,.6,.65,2/3,.7,.75)} if n else {}),
        limitations='Confidence intervals and binomial tests assume independent hits. Buffered log timestamps support event association only, not exact latency. Fixed tone thresholds are unchanged. Missing startup positive controls stay explicit. Synthetic clips, sampled attack contexts, immortality/healing and current soundsets do not prove other weapons or special states, exact RNG or visual parity.')


if __name__ == '__main__':
    print(json.dumps(summarize([Path(p) for p in sys.argv[1:]]), indent=2))
