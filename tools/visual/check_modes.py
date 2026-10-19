"""Compare script-selected mode states in the real-PC modes fixture; inspect PNGs for text styling."""
import re
import sys
from pathlib import Path

folder = Path(sys.argv[1])
if "modeswitch" in folder.name:
    expected = [(mode, int(mode == 3), int(mode == 8), int(mode == 10)) for mode in (10, 3, 10, 8, 10, 3, 8)]
    for engine in ("nwn", "openwinter"):
        notes = (folder / engine / "notes.txt").read_text(encoding="utf-8")
        actual = [tuple(map(int, m)) for m in re.findall(r"switch=(\d+) power=(\d+) expertise=(\d+) defensive=(\d+)", notes)]
        assert actual == expected, f"{engine}: expected {expected}, got {actual}"
    print("PASS: 14 mutually exclusive combat-mode transitions across NWN and OpenWinter")
    sys.exit(0)
expected = [(mode, on, 1 if mode == 0 and "keen_senses" in folder.name else on)
            for mode in range(12) for on in (1, 0)] + [(10, 1, 1)]
for engine in ("nwn", "openwinter"):
    notes = (folder / engine / "notes.txt").read_text(encoding="utf-8")
    keen = "1" if "keen_senses" in folder.name else "0"
    assert f"initial keen={keen} detect={keen} legacy={keen}" in notes, f"{engine}: initial Detect state differs"
    actual = [tuple(map(int, m)) for m in re.findall(r"mode=(\d+) requested=(\d+) actual=(\d+)", notes)]
    assert actual == expected, f"{engine}: expected {expected}, got {actual}"
print(f"PASS: {len(expected) * 2} mode transitions and initial Detect queries across NWN and OpenWinter")
