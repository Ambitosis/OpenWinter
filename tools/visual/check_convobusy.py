"""Compares busy refusals and fights during conversations on the original and on OpenWinter (scenes convobusy,
convobusy2, convobusy3, convobusy4).

    python tools/visual/check_convobusy.py

For every case it lists the hooks that ran in order (the creature's OnConversation, the dialog's lines, end and
abort scripts, BeginConversation's result) with the talking flags of the half-second polls, and the chat window
without the combat lines (their dice differ), then says where the engines differ. Run the scenes first:
    python tools/visual/visual.py run convobusy --silent --native-framebuffer   (and convobusy2, 3, 4)
"""
import re
import sys
from pathlib import Path

SHOTS = Path(__file__).resolve().parents[2] / "out" / "visual" / "shots"
SCENES = ("convobusy", "convobusy2", "convobusy3", "convobusy4")
# On the original the dummy's first blow put the player in combat only after the creature had asked in this case (the
# same schedule did in pc_attacked), so it measured nothing; convobusy2 pc_combat_npc_asks waits 4 s instead.
TIMING = {"pc_attacked_npc_asks"}
# The original missed the scene's Esc tap here: the dialog stayed open until the talker was destroyed (neither script).
INPUT = {"pc_combat_begin"}
COMBAT = re.compile(r" attacks | damages |Initiative Roll|casting |casts |cannot damage your target")


def events(path, scene):
    """case -> [hook or 'talking@poll', ...] in log order."""
    out = {}
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        p = line.strip().split("|")
        if len(p) < 6 or p[2] != scene:
            continue
        case, what = p[3], p[4]
        flags = dict(f.split("=", 1) for f in p[5:] if "=" in f)
        seq = out.setdefault(case, [])
        if re.fullmatch(r"t\d+", what):
            # Talking flags per poll, collapsed while they stay the same.
            state = "talking" if flags.get("pcTalking") == "1" else "idle"
            if not seq or seq[-1] != state:
                seq.append(state)
        elif what in ("ended", "aborted"):
            # With IsInConversation as the script sees it (both sides still talking on the original).
            seq.append(f"{what}/{flags.get('pcTalking')}{flags.get('npcTalking')}")
        elif what.startswith(("onconv", "line", "begin=")):
            seq.append(what.split(":")[0])
    # Whether the last poll caught the end of the conversation is a race with the scene's Esc.
    for seq in out.values():
        if len(seq) > 1 and seq[-1] == "idle" and seq[-2].startswith(("ended", "aborted")):
            seq.pop()
    return out


def chat(path):
    if not path.exists():
        return []
    lines = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        text = re.sub(r"^\[CHAT WINDOW TEXT\]\s*(\[[^\]]*\d\d:\d\d:\d\d\]\s*)?", "", line).strip()
        if text.startswith(("Loading Screen", "[Server] You are now", "PAUSED")) or "has joined as a player" in text or COMBAT.search(text):
            continue
        lines.append(text)
    return lines


def main():
    bad = 0
    for scene in SCENES:
        root = SHOTS / f"vis_{scene}"
        if not (root / "nwn" / "notes.txt").exists() or not (root / "openwinter" / "notes.txt").exists():
            print(f"{scene}: missing captures")
            bad += 1
            continue
        nwn, ow = events(root / "nwn" / "notes.txt", scene), events(root / "openwinter" / "notes.txt", scene)
        print(f"== {scene}")
        for case in nwn:
            a, b = nwn[case], ow.get(case, [])
            mark = "ok " if a == b else "tim" if case in TIMING else "inp" if case in INPUT else "DIFF"
            if mark == "DIFF":
                bad += 1
            print(f"  {mark} {case:<22} nwn={a}  openwinter={b}")
        cn, co = chat(root / "nwn" / "chat.txt"), chat(root / "openwinter" / "chat.txt")
        if scene == "convobusy":
            # The timing case's line (the original opened the dialog, OpenWinter refused it) is its last one.
            cn, co = cn[:-1], co[:-1]
        if cn != co:
            bad += 1
            print("  chat differs:")
            for i in range(max(len(cn), len(co))):
                x = cn[i] if i < len(cn) else ""
                y = co[i] if i < len(co) else ""
                print(f"    {'  ' if x == y else '!='} {x!r:<60} {y!r}")
        else:
            print(f"  chat: {len(cn)} lines match")
    print("all match" if bad == 0 else f"{bad} difference(s)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
