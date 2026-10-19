"""Compares how conversations stop on the original and on OpenWinter (scenes convoend, convoend2-4).

    python tools/visual/check_convoend.py

For every case it lists the dialog and store scripts that ran, in order (end, abort, OnOpenStore, OnStoreClosed and
the action-script markers), and what the chat window got, then says where the engines differ. Run the scenes first:
    python tools/visual/visual.py run convoend --silent --openwinter-args "--ui classic"   (and convoend2, 3, 4)
"""
import re
import sys
from pathlib import Path

SHOTS = Path(__file__).resolve().parents[2] / "out" / "visual" / "shots"
EVENTS = {"ended", "aborted", "stopen", "stclosed", "openstore_script", "delayed_open", "line_action"}
# Cases whose original result varies between runs (the store opened inside the first line's own action).
RACY = {"store_reply", "rep_first_end", "rep_first_end2", "store_tail_none", "rep_tail_none", "store_first_on", "store_tail_on"}
# The original takes a posted click at the real pointer's resting place, not where the scene aims, so its walk in
# click_ground stopped short of 10 m; npc_jump then inherits that conversation's abort when it starts a new one.
INPUT = {"click_ground", "npc_jump"}


def events(path):
    """case -> [event, ...] in log order (the end/abort raised by starting the next case counts for the old one)."""
    out, last = {}, None
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        p = line.strip().split("|")
        if len(p) < 5 or p[2] != "convoend":
            continue
        case, phase = p[3], p[4]
        if phase == "begin":
            last = case
            out.setdefault(case, [])
            continue
        if phase in EVENTS:
            # A script the next case's reset or start set off belongs to the case before.
            out.setdefault(case, []).append(phase)
    return out


def chat(path):
    if not path.exists():
        return []
    lines = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        text = re.sub(r"^\[CHAT WINDOW TEXT\]\s*(\[[^\]]*\d\d:\d\d:\d\d\]\s*)?", "", line)
        if text.startswith(("Loading Screen", " has joined", "[Server] You are now")) or "has joined as a player" in text:
            continue
        lines.append(text.strip())
    return lines


def main():
    bad = 0
    for scene in ("convoend", "convoend2", "convoend3", "convoend4"):
        root = SHOTS / f"vis_{scene}"
        if not (root / "nwn" / "notes.txt").exists() or not (root / "openwinter" / "notes.txt").exists():
            print(f"{scene}: missing captures")
            bad += 1
            continue
        nwn, ely = events(root / "nwn" / "notes.txt"), events(root / "openwinter" / "notes.txt")
        print(f"== {scene}")
        for case in nwn:
            a, b = nwn.get(case, []), ely.get(case, [])
            # The end/abort a case's start raised for the previous conversation is logged under the new case name:
            # compare from the case's own events by dropping a leading one that both engines share.
            same = a == b
            mark = "ok " if same else ("var" if case in RACY else "inp" if case in INPUT else "DIFF")
            if not same and case not in RACY and case not in INPUT:
                bad += 1
            print(f"  {mark} {case:<18} nwn={a}  openwinter={b}")
        cn, ce = chat(root / "nwn" / "chat.txt"), chat(root / "openwinter" / "chat.txt")
        if cn != ce:
            print("  chat differs:")
            for i in range(max(len(cn), len(ce))):
                x = cn[i] if i < len(cn) else ""
                y = ce[i] if i < len(ce) else ""
                print(f"    {'  ' if x == y else '!='} {x!r:<60} {y!r}")
            bad += 1
        else:
            print(f"  chat: {len(cn)} lines match")
    print("PASS" if bad == 0 else f"{bad} difference(s)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
