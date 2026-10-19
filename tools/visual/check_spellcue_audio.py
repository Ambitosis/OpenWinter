"""Keep original-cue identity separate from quiet-control execution gates."""
import argparse
import hashlib
import json
from pathlib import Path
import re

import match_spell_audio as matcher
from spellcue_audio import execution_gates


def check(metadata, reference):
    doc = json.loads(metadata.read_text(encoding='utf-8'))
    probe = doc['probe']
    if not probe.get('validated'):
        raise ValueError('Successful actual-player execution gates required')
    record = doc['recording']
    if probe['engine'] == 'native' and not record.get('session', {}).get('restored'):
        raise ValueError('Owned native audio session restoration required')
    if probe['candidate'] != reference.stem:
        raise ValueError('Reference must name the unmodified candidate')
    if any(s['resref'] == probe['candidate'] for s in probe['substitutions']):
        raise ValueError('Original candidate cannot be replaced')
    waveform = Path(record['path'])
    digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    if digest(waveform) != probe['waveformSha256']:
        raise ValueError('Recorded waveform changed')
    log = Path(probe['scriptLog'])
    if digest(log) != probe['scriptLogSha256']:
        raise ValueError('Original execution log changed')
    scene_file = Path(__file__).resolve().parents[2]/'parity'/'visual'/'scenes'/probe['scene']/'scene.json'
    if digest(scene_file) != probe['fixtureSha256'].get('scene.json'):
        raise ValueError('Scene inventory changed since capture')
    spell = json.loads(scene_file.read_text(encoding='utf-8'))['cueControl']['spell']
    log_text = log.read_text(encoding='utf-8', errors='replace')
    valid, _, difficulty = execution_gates(log_text, probe['candidate'], spell)
    if not valid:
        raise ValueError('Execution log does not satisfy actual-player gates')
    matcher.RATE = 44100
    pcm, sample = matcher.decode(waveform), matcher.decode(reference)
    notes = [(t-record['started'], s.split('VISUAL|note|', 1)[1].strip())
             for t, s in doc['notes'] if 'VISUAL|note|' in s]
    if any('INVALID_' in s for _, s in notes):
        raise ValueError('Rejected execution context')
    rows = []
    for label, pattern, span in [('before', r'positive\.before\.', 7.0),
                                 ('cast', r'start\.\d+$', 8.0),
                                 ('after', r'positive\.after\.', 6.0)]:
        times = [t for t, s in notes if re.match(pattern, s)]
        if len(times) != 1:
            raise ValueError('Exactly one marker required for '+label)
        start, end = max(0, times[0]-.25), min(len(pcm)/matcher.RATE, times[0]+span)
        result = matcher.identify_candidates(pcm[int(start*matcher.RATE):int(end*matcher.RATE)], sample,
                                             (.1, .2, .3, .5, 1.0, 1.5))
        rows.append(dict(context=label, start=start, end=end, identity=result))
    positives = all(row['identity']['status'] == 'identified' for row in (rows[0], rows[2]))
    return dict(candidate=probe['candidate'], scene=probe['scene'], engine=probe['engine'],
                executionValidated=True, independentReferencesIdentified=positives,
                actualDifficulty=difficulty,
                intervals=rows, substitutions=probe['substitutions'],
                fixtureSha256=probe['fixtureSha256'],
                configuration=dict(rate=44100, multipleAnchorThreshold=.65, singleAnchorThreshold=.85,
                                   requiredMultipleAnchors=2, originToleranceSeconds=.025,
                                   prefixes=[.1,.2,.3,.5,1.0,1.5]),
                inputs={str(p): digest(p) for p in (metadata, reference, waveform, log)},
                limitations='Quiet substituted mix only. Unresolved does not prove absent; partial-reference identity proves only that portion. No original mix, spatialization, exact onset or complete sound-family parity.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('metadata', type=Path)
    parser.add_argument('reference', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = check(args.metadata, args.reference)
    args.output.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8', newline='')
    print(report['candidate'], [(r['context'], r['identity']['status']) for r in report['intervals']])
