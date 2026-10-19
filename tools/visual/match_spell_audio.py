"""Compare timed spell captures to local installed audio references.

Requires numpy, scipy and ffmpeg. This is evidence, not a parity verdict: mixing,
pitch, filtering, truncation and repeated/identical assets can hide identity.
Only strong, mutually consistent waveform anchors are marked identified.
No game assets are copied or extracted by this tool.
"""
import argparse
import csv
import io
import json
import re
import subprocess
from pathlib import Path

import numpy as np
from scipy.signal import correlate, find_peaks

RATE = 11025


def decode(path):
    raw = Path(path).read_bytes()
    if raw[:8] == b"BMU V1.0":
        raw = raw[8:]
    result = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", "pipe:0", "-f", "f32le",
         "-ac", "1", "-ar", str(RATE), "pipe:1"],
        input=raw, capture_output=True, check=True)
    return np.frombuffer(result.stdout, dtype="<f4").astype(np.float64)


def identify(capture, reference):
    # Pick up to three disjoint energetic anchors. A full clip often extends
    # past native stop events; local anchors can still prove the played part.
    width = min(len(reference), int(.30 * RATE))
    if width < int(.04 * RATE) or len(capture) < width:
        return {"status": "too-short"}
    power = np.concatenate(([0.], np.cumsum(reference * reference)))
    energy = power[width:] - power[:-width]
    anchors = []
    for unused in range(3):
        offset = int(np.argmax(energy))
        if energy[offset] <= 1e-8:
            break
        anchors.append(offset)
        energy[max(0, offset-width):min(len(energy), offset+width)] = 0
    if not anchors:
        return {"status": "silent-reference"}
    sums = np.concatenate(([0.], np.cumsum(capture)))
    squares = np.concatenate(([0.], np.cumsum(capture*capture)))
    local_power = squares[width:] - squares[:-width]
    local_sum = sums[width:] - sums[:-width]
    local_power -= local_sum*local_sum/width
    candidates = []
    anchor_peaks = []
    for offset in anchors:
        template = reference[offset:offset+width].copy()
        template -= template.mean()
        denominator = np.sqrt(np.maximum(local_power, 0) * np.dot(template, template))
        score = np.abs(correlate(capture, template, mode="valid", method="fft")) / np.maximum(denominator, 1e-12)
        score[local_power < width * (5/32768)**2] = 0
        peaks, unused = find_peaks(score, distance=int(.05*RATE))
        # Include boundaries, which find_peaks deliberately excludes.
        peaks = np.unique(np.concatenate((peaks, [0, len(score)-1])))
        best = sorted(peaks, key=lambda i: score[i], reverse=True)[:8]
        points = [(float(score[i]), (int(i)-offset)/RATE) for i in best]
        anchor_peaks.append(points)
        candidates.extend(point[1] for point in points)
    best = None
    for origin in candidates:
        aligned = [max((s for s,t in points if abs(t-origin) < .025), default=0.)
                   for points in anchor_peaks]
        support = sum(s >= .65 for s in aligned)
        rank = (support, sum(aligned))
        if best is None or rank > best[0]:
            best = (rank, origin, aligned)
    support, unused = best[0]
    # A lone short anchor has less discriminating information; require .85.
    verified = support >= 2 or (len(anchors) == 1 and best[2][0] >= .85)
    return dict(status="identified" if verified else "unresolved",
                offset=round(best[1], 4),
                anchorScores=[round(v, 5) for v in best[2]],
                anchors=[round(v/RATE, 4) for v in anchors],
                duration=round(len(reference)/RATE, 4))


def identify_candidates(capture, reference, prefixes=()):
    attempts = [dict(referencePrefixSeconds=None, **identify(capture, reference))]
    for seconds in prefixes:
        if int(seconds*RATE) >= len(reference):
            continue
        attempts.append(dict(referencePrefixSeconds=seconds,
                             **identify(capture, reference[:int(seconds*RATE)])))
    selected = attempts[0]
    if selected["status"] != "identified":
        matches = [m for m in attempts[1:] if m["status"] == "identified"]
        if matches:
            selected = max(matches, key=lambda m: (sum(v >= .65 for v in m["anchorScores"]), sum(m["anchorScores"])))
    return dict(**selected, referenceDuration=round(len(reference)/RATE,4), attempts=attempts)


def main():
    global RATE
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("metadata", type=Path)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("table", type=Path, help="spells table JSON keyed by row, or TSV")
    parser.add_argument("clips", type=Path, help="local resref.wav or .bmu files")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rate", type=int, choices=(11025,22050,44100,48000), default=RATE,
                        help="Analysis sample rate; 44100 avoids 11025 decimation-phase losses for native-rate PCM")
    parser.add_argument("--conjure-prefix", type=float, action="append", default=[], metavar="SECONDS",
                        help="Also test this reference prefix for ConjSoundVFX (repeatable); records every attempt")
    args = parser.parse_args()
    if any(not np.isfinite(p) or p < .04 for p in args.conjure_prefix):
        parser.error("Conjure prefixes must be finite and at least 0.04 seconds")
    RATE = args.rate
    metadata = json.loads(args.metadata.read_text(encoding="utf-8"))
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    if args.table.suffix == ".json":
        table = json.loads(args.table.read_text(encoding="utf-8"))
    else:
        rows = list(csv.DictReader(io.StringIO(args.table.read_text(encoding="utf-8-sig")), delimiter="\t"))
        table = {str(r.get("id", r.get("ID", r.get("row", r.get("Row", i))))): r for i,r in enumerate(rows)}
    recording = metadata["recording"]
    capture = decode(recording["path"])
    starts = {}
    for timestamp, line in metadata["notes"]:
        match = re.search(r"VISUAL\|note\|start\.(\d+)", line)
        if match:
            starts[int(match[1])] = timestamp - recording["started"]
    cache = {}
    result = []
    for row in manifest["rows"]:
        spell = int(row["id"])
        output = dict(id=spell, label=row["label"], sounds=[])
        result.append(output)
        if spell not in starts:
            output["status"] = "missing-start-marker"
            continue
        start = starts[spell]
        end = min(start + row["span"], len(capture)/RATE)
        segment = capture[max(0, int((start-.15)*RATE)):int(end*RATE)]
        output.update(start=round(start, 4), end=round(end, 4), status="analyzed")
        for field, ref in table[str(spell)].items():
            if "Sound" not in field or ref in ("****", "", None):
                continue
            ref = ref.lower()
            path = next((args.clips/(ref+ext) for ext in (".wav", ".bmu")
                         if (args.clips/(ref+ext)).exists()), None)
            sound = dict(role=field, resref=ref)
            output["sounds"].append(sound)
            if path is None:
                sound["status"] = "reference-not-extracted"
                continue
            if ref not in cache:
                cache[ref] = decode(path)
            prefixes = args.conjure_prefix if field == "ConjSoundVFX" else ()
            match = identify_candidates(segment, cache[ref], prefixes)
            if "offset" in match:
                match["offset"] = round(match["offset"]-.15, 4)
            for attempt in match["attempts"]:
                if "offset" in attempt: attempt["offset"] = round(attempt["offset"]-.15,4)
            sound.update(match)
    report = dict(metadata=str(args.metadata), manifest=str(args.manifest),
                  method=f"normalized waveform anchors; {RATE} Hz mono; no pitch search",
                  configuration=dict(analysisRate=RATE, conjurePrefixesSeconds=args.conjure_prefix,
                                     multipleAnchorThreshold=.65, singleAnchorThreshold=.85,
                                     requiredMultipleAnchors=2, originToleranceSeconds=.025),
                  limitation="Unresolved does not prove absent. Identified does not prove correct volume, spatialization, timing, or complete spell audio. Prefix identity proves only the tested portion, not complete playback. Table references exclude sounds selected dynamically by scripts/VFX.",
                  rows=result)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8", newline="")
    print(f"Analyzed {len(result)} spell intervals; wrote {args.output}")


if __name__ == "__main__":
    main()
