"""Compare bardpartypc raw native/client logs, rejecting missing or failed runs.

Usage: python tools/visual/check_bard_party.py <nwclientLog1.txt> <godot.log>
Uses raw logs because the client initializes capture after area OnEnter readings.
This validates rules observations, never pixels or audio.
"""
import re
import sys
from pathlib import Path


def read(path):
    text = path.read_text(encoding="utf-8", errors="replace")
    if "VISUAL|done|1" not in text:
        raise ValueError(f"{path}: missing completion marker")
    if re.search(r"Script error:|unimplemented (function|engine)", text, re.I):
        raise ValueError(f"{path}: script failure")
    rows = {}
    for key, value in re.findall(r"VISUAL\|note\|([^|\r\n]+)\|([^\r\n]*)", text):
        if key in rows:
            raise ValueError(f"{path}: duplicate reading {key}")
        rows[key] = value
    required = {"context.pc": "1", "context.bardLevel": "20", "context.perform": "22",
                "context.songFeat": "1", "context.dirgeKnown": "445",
                "beforeSong.songUses": "14", "song16.songUses": "13", "song28.songUses": "13",
                "beforeDirge.dirgeUses": "4", "dirge60.dirgeUses": "3"}
    for target in range(6):
        for phase in ("song16", "song28"):
            required[f"{phase}.target{target}.has"] = str(int(target < 3))
        required[f"dirge60.target{target}.has"] = str(int(target in (0, 4)))
        for stat in ("str", "dex", "con"):
            required[f"dirge60.target{target}.{stat}Decreased"] = str(int(target == 4 and stat != "con"))
    for key, expected in required.items():
        if rows.get(key) != expected:
            raise ValueError(f"{path}: {key}: expected {expected}, got {rows.get(key)}")
    return rows


def main():
    native, client = (read(Path(p)) for p in sys.argv[1:])
    failures = []
    for key in sorted(native.keys() | client.keys()):
        if native.get(key) != client.get(key):
            failures.append(f"{key}: native={native.get(key)} client={client.get(key)}")
    if failures:
        print("\n".join(failures))
        return 1
    print(f"bard party: {len(native)} matching rules readings; pixels/audio not evaluated")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1)
