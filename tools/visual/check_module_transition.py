"""Compare the authored transfer's public script observations, not screenshot pixels."""
import hashlib
import json
from pathlib import Path

repo = Path(__file__).resolve().parents[2]
root = repo / "out/visual/shots/vis_modulesource"
expected = {
    "source.local": "73", "source.gold": "3900", "source.level": "15", "source.hp": "134",
    "source.before": "1", "source.after": "1", "event.load": "1", "event.enter": "1",
    "destination.entered": "1", "destination.acquires": "10", "destination.itemLocal": "91",
    "destination.local": "0", "destination.selfRef": "0", "destination.gold": "3900",
    "destination.level": "15", "destination.hp": "134", "destination.action": "65535",
    "destination.item": "1", "destination.permanent": "0", "destination.temporary": "0",
    "destination.entry": "1",
}
evidence = {"checksPerEngine": len(expected), "observations": expected, "engines": {}}
for engine in ("nwn", "openwinter"):
    notes = root / engine / "notes.txt"
    lines = notes.read_text(encoding="utf-8").splitlines()
    values = {}
    for line in lines:
        if line.startswith("VISUAL|note|"):
            key, value = line.split("|", 3)[2:]
            if key != "character-fixture":
                assert key not in values, (engine, "duplicate", key)
                values[key] = value
    assert values == expected, (engine, values)
    assert any("|character-fixture|000 Module Transfer Probe|pc=1" in line for line in lines)
    assert list(values).index("event.load") < list(values).index("event.enter") < list(values).index("destination.entered")
    capture = root / engine / "destination.png"
    assert capture.exists()
    evidence["engines"][engine] = {
        "notesSha256": hashlib.sha256(notes.read_bytes()).hexdigest(),
        "captureSha256": hashlib.sha256(capture.read_bytes()).hexdigest(),
    }
path = repo / "parity/visual/scenes/modulesource/evidence.json"
with path.open("w", encoding="utf-8", newline="") as stream:
    json.dump(evidence, stream, indent=2)
    stream.write("\n")
print(f"Module transfer: {len(expected)} observations match in both engines; event order and actual PC verified.")
