"""Check the actual key-driven familiar scene; image inspection remains required."""
import json
import math
import re
import sys
from pathlib import Path


def check(folder):
    folder = Path(folder)
    notes = (folder / "notes.txt").read_text(encoding="utf-8")
    assert "possessed.pc.0.wizard.1.type.0.valid.1.master.1.pcposs.0.famposs.1.fampc.1.firstPCmaster.1" in notes
    assert "ui_release.pc.1.wizard.1.type.0.valid.1.master.1.pcposs.0.famposs.0.fampc.0.firstPCmaster.1" in notes
    positions = {}
    for phase, mx, my, fx, fy in re.findall(
        r"position\.(before|after)\.master\.\s*([-\d.]+),\s*([-\d.]+)\.familiar\.\s*([-\d.]+),\s*([-\d.]+)", notes
    ):
        positions[phase] = tuple(map(float, (mx, my, fx, fy)))
    a, b = positions["before"], positions["after"]
    assert math.dist(a[:2], b[:2]) < .01, "original body moved"
    assert math.dist(a[2:], b[2:]) > 1, "controlled familiar did not move"
    states_path = folder / "state.jsonl"
    if not states_path.exists():
        print(f"PASS native identity/movement/return: {folder}")
        return
    states = {s["shot"]: s for s in map(json.loads, states_path.read_text(encoding="utf-8").splitlines())}
    ready, possessed, returned = (states[k] for k in ("a_ready", "d_possessed", "i_returned"))
    master, familiar = ready["controlledId"], possessed["controlledId"]
    assert master != familiar and returned["controlledId"] == master
    for s in states.values():
        assert s["cameraFollowsPlayer"] and s["cameraTargetId"] == s["controlledId"], s["shot"]
        assert s["hudSelfId"] == s["controlledId"], s["shot"]
        assert s["controlledId"] not in s["hudPartyIds"], s["shot"]
    assert possessed["hudPartyIds"] == [master]
    assert returned["hudPartyIds"] == [familiar]
    if "owner_windows_open" in states:
        opened = states["owner_windows_open"]
        assert any("Inventory" in n for n in opened["visibleWindows"])
        assert any("Character" in n for n in opened["visibleWindows"])
        assert opened["targeting"]
    assert not possessed["visibleWindows"] and not possessed["targeting"], "old-owner UI survived control transfer"
    assert all(slot is None for page in possessed["quickbar"] for slot in page)
    assert ready["quickbar"] == returned["quickbar"], "original bindings changed"
    inventory, sheet = states["f_inventory"], states["g_character"]
    assert inventory["inventoryDenials"] > 0
    assert not any("Inventory" in name for name in inventory["visibleWindows"])
    assert any("Character" in name for name in sheet["visibleWindows"])
    assert sheet["characterSheetId"] == familiar
    print(f"PASS camera, movement, HUD, quickbar, inventory, character sheet and return: {folder}")


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        check(arg)
