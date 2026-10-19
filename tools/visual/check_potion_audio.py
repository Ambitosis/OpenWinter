"""Identify the stock healing cue in all three doses of potionqueuepc.

Extract sim_heal01.wav locally from clean installed data; no assets are bundled.
The bounded windows prevent a later dose from satisfying an earlier dose's match.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import match_spell_audio as matcher

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("metadata", type=Path)
parser.add_argument("reference", type=Path)
parser.add_argument("--output", type=Path, required=True)
a = parser.parse_args()
if a.reference.stem.lower() != "sim_heal01":
    raise ValueError("Expected the clean installed sim_heal01 reference")
d = json.loads(a.metadata.read_text(encoding="utf-8"))
r = d["recording"]
notes = "\n".join(line for _, line in d["notes"])
if re.search(r"TOO MANY INSTRUCTIONS|SCRIPT ERROR|\[script error\]", notes, re.I):
    raise ValueError("Script error")
if "VISUAL|done|1" not in notes or "phase0.stack.0.time." not in notes:
    raise ValueError("Incomplete three-dose scenario")
if d.get("engine") != "openwinter" and not r.get("session", {}).get("restored"):
    raise ValueError("Native session was not restored")
starts = [t-r["started"] for t, line in d["notes"]
          if "VISUAL|note|start.0.pc.1.base.49.stack.3" in line]
if len(starts) != 1:
    raise ValueError("Expected exactly one validated three-bottle actual-PC start")
matcher.RATE = 44100
wave = Path(r["path"])
pcm, reference = matcher.decode(wave), matcher.decode(a.reference)
results = []
for dose in range(3):
    start = max(0, starts[0] + dose*2.5 + 1.3)
    end = min(len(pcm)/matcher.RATE, start + 3.2)
    identity = matcher.identify_candidates(pcm[int(start*matcher.RATE):int(end*matcher.RATE)], reference, ())
    results.append(dict(dose=dose+1, windowStart=start, identity=identity))
report = dict(engine=d.get("engine", "nwn"), scene="potionqueuepc", resref="sim_heal01", source="visualeffects.2da row69 SoundImpact; stock Cure Light Wounds impact script",
    configuration=dict(rate=44100, multipleAnchorThreshold=.65, singleAnchorThreshold=.85,
                       originToleranceSeconds=.025, prefixes=[]), results=results,
    sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (a.metadata,a.reference,wave)},
    limitations="Sample identity only; no complete-tail, exact onset, mixing, spatial parity, or all-cue claim.")
a.output.write_text(json.dumps(report, indent=2), encoding="utf-8", newline="")
for result in results:
    print(result["dose"], result["identity"]["status"], result["identity"].get("anchorScores"))
