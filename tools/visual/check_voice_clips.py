"""Match local installed voice references inside a marked capture interval.
No resource extraction; callers supply their local, uncommitted reference files.
Uses the unchanged fixed waveform identity thresholds from match_spell_audio.
"""
import argparse
import hashlib
import json
from pathlib import Path
import match_spell_audio as match


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('metadata', type=Path)
    parser.add_argument('references', nargs='+', type=Path)
    parser.add_argument('--from-note', required=True)
    parser.add_argument('--to-note', required=True)
    parser.add_argument('--wave', type=Path, help='Optional isolated bus from the same capture clock')
    parser.add_argument('--rate', type=int, choices=(11025,22050,44100,48000), default=44100)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    doc = json.loads(args.metadata.read_text(encoding='utf-8'))
    origin = doc['recording']['started']
    notes = [(t-origin, line.split('VISUAL|note|',1)[1])
             for t,line in doc['notes'] if 'VISUAL|note|' in line]
    starts = [t for t,note in notes if note.startswith(args.from_note)]
    ends = [t for t,note in notes if note.startswith(args.to_note)]
    if len(starts) != 1 or len(ends) != 1 or starts[0] >= ends[0]:
        raise ValueError('Require one ordered pair of interval markers')
    if not any('VISUAL|done|1' in line for _,line in doc['notes']):
        raise ValueError('Capture completion marker missing')
    wave = args.wave or Path(doc['recording']['path'])
    match.RATE = args.rate
    samples = match.decode(wave)
    start,end = starts[0],ends[0]
    if start < 0 or end*args.rate > len(samples):
        raise ValueError('Interval lies outside recorded PCM')
    segment = samples[int(start*args.rate):int(end*args.rate)]
    def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
    report = dict(metadata=str(args.metadata), metadataSha256=digest(args.metadata),
        waveform=str(wave), waveformSha256=digest(wave), rate=args.rate,
        interval=dict(start=start,end=end,fromNote=args.from_note,toNote=args.to_note),
        matches={str(path):dict(sha256=digest(path), **match.identify(segment,match.decode(path))) for path in args.references},
        limitations='Positive waveform identity only. Unresolved mixtures are not absence. This does not prove every cue, exact gain/timing, voice priority or visual parity.')
    with args.output.open('w',encoding='utf-8',newline='') as out:
        json.dump(report,out,indent=2)
    print(json.dumps(report,indent=2))


if __name__ == '__main__':
    main()
