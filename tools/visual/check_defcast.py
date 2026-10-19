"""Check the real-PC defensive-casting observations from tools/visual/visual.py.

Usage: python tools/visual/check_defcast.py [vis_defcast capture folder]
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
folder = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "out/visual/shots/vis_defcast"
expected = {
    "off.low.idle": 1,
    "on.low.idle": 0,
    "on.high.idle": 1,
    "on.low.far": 0,
    "off.low.fight": 1,
    "on.low.fight": 0,
    "on.high.fight": 1,
    "on.low.alone": 0,
    "off.low.direct": 1,
    "on.high.direct": 1,
}


def readings(engine):
    result = {}
    for line in (folder / engine / "notes.txt").read_text(encoding="utf-8").splitlines():
        if "VISUAL|note|" not in line:
            continue
        line = line.split("VISUAL|note|", 1)[1]
        key = line.split(" mode=", 1)[0]
        if line.startswith("character "):
            key = "character"
        if key in result:
            raise AssertionError(f"{engine}: duplicate reading {key}")
        result[key] = dict(re.findall(r"(\w+)=(-?[\d.]+)", line))
    return result


checks = 0
for engine in ("nwn", "openwinter"):
    data = readings(engine)
    assert data.get("character") == {"race": "1", "combatCasting": "0" if "no_combat_casting" in folder.name else "1", "keen": "1"}, f"{engine}: wrong character control"
    checks += 3
    for case, impacts in expected.items():
        mode = "0" if case.startswith("off.") else "1"
        for stage, want in {
            " start": {"mode": mode, "conc": "55" if ".high." in case else "-45", "slots": "1"},
            ".conjure": {"mode": mode, "slots": "1", "impacts": "0"},
            ".result": {"mode": mode, "slots": "0", "impacts": str(impacts)},
        }.items():
            key = case + stage
            assert key in data, f"{engine}: missing {key}"
            for field, value in want.items():
                assert data[key].get(field) == value, f"{engine}: {key} {field}: expected {value}, got {data[key]}"
                checks += 1
    assert data.get("detect.key") == {"mode": "1", "defensive": "1"}, f"{engine}: Detect and Defensive Casting must remain independent"
    checks += 1
print(f"PASS: {checks} defensive-casting checks across NWN and OpenWinter")
print("Scope: concentration outcomes/timing, slot use and selected modes. Attack counts and GetDefensiveCastingMode remain diagnostic.")
