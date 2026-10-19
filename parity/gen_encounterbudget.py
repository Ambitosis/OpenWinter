"""Writes the visual scene parity/visual/scenes/encounterbudget, or with the argument "encounterparty" that scene
(scene.json, creature fixtures, vis_main.nss).

Each walk-in triggers six encounters at once that differ only in their one list entry's challenge rating (each
with its own creature copy, tag EB_1..EB_6, so the counts can be told apart). The original's counts are
deterministic (encounterrules), so one walk-in per configuration is enough. Walk-ins cover the player alone at
levels 1-20 and the five difficulties, a few parties, the player's experience without levels, and continuous
encounters' respawn waves. docs/ENCOUNTERS.md has what was learned.
"""
import json, math, os

VALUE = [0, 1, 2, 5, 9]
XP = {1: 0, 2: 1000, 3: 3000, 4: 6000, 5: 10000, 6: 15000, 8: 28000, 10: 45000, 12: 66000, 15: 105000, 20: 190000}


def guess(level, d):
    """A rough guess of the budget in CR 1 creatures, only to pick list CRs that give readable counts."""
    pts = [(1, 0.0), (5, 1.39), (10, 3.5)]
    if level >= 10:
        lg = 3.5 + 0.42 * (level - 10)
    else:
        (a, la), (b, lb) = [(p, q) for p, q in zip(pts, pts[1:]) if p[0] <= level <= q[0]][0]
        lg = la + (lb - la) * (level - a) / (b - a)
    return math.exp(lg) * 0.2 * 2 ** (2 * VALUE[d] / 3)


def crs(b):
    out = []
    for f in (40, 16, 6, 3, 1.5, 1):
        c = b / f
        c = float(f"{max(c, 0.05):.2g}")
        out.append(c)
    return out


FIXED = {"SA": [0.1, 0.2, 0.25, 0.3, 0.5, 1], "SB": [1, 1.5, 2, 3, 4, 6], "SC": [2, 3, 4, 6, 8, 12], "SD": [0.1, 0.25, 0.5, 1, 2, 4]}
import sys
SCENE = sys.argv[1] if len(sys.argv) > 1 else "encounterbudget"
HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "visual", "scenes", SCENE)
os.makedirs(HERE, exist_ok=True)
steps = []  # (kind, label, args)
if SCENE == "encounterbudget":
    # The player's experience without levels: level 1 with 45000 experience, then back to 0.
    steps.append(("xp", "", 45000))
    steps.append(("walk", "xp45k", (2, FIXED["SD"])))
    steps.append(("xp", "", 0))
    # Parties at level 1.
    for hench, label in (([1], "p1h1"), ([1, 1], "p1h11"), ([5], "p1h5"), ([10], "p1h10")):
        steps.append(("hire", "", hench))
        steps.append(("walk", label, (2, FIXED["SD"])))
        steps.append(("fire", "", None))
    # Continuous respawn waves at level 1.
    steps.append(("cont", "k1", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 1, "cr": 0.1}))
    steps.append(("cont", "k1t", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 10, "cr": 0.1}))
    steps.append(("cont", "k1r0", {"SpawnOption": 0, "Respawns": -1, "Reset": 0, "ResetTime": 1, "cr": 0.1}))
    steps.append(("cont", "k1n2", {"SpawnOption": 0, "Respawns": 2, "Reset": 1, "ResetTime": 1, "cr": 0.1}))
    # The player alone.
    for level in (1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20):
        if level > 1:
            steps.append(("level", "", level))
        for d in range(5):
            steps.append(("walk", f"l{level}d{d}", (d, crs(guess(level, d)))))
        if level == 1:
            steps.append(("walk", "l1d4b", (4, FIXED["SB"])))
        if level == 5:
            steps.append(("walk", "l5d4c", (4, FIXED["SC"])))
            steps.append(("hire", "", [5]))
            steps.append(("walk", "p5h5", (2, FIXED["SB"])))
            steps.append(("fire", "", None))
            steps.append(("cont", "k5", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 1, "cr": 0.25}))
        if level == 10:
            steps.append(("walk", "l10d2b", (2, FIXED["SB"])))
else:
    # encounterparty: parties (henchmen of other levels beside the player, or far away) and respawn wave sizes.
    WIDE = [0.05, 0.17, 0.25, 0.5, 1, 2]
    def party(pc, hench, far=()):
        label = f"p{pc}h" + "_".join(map(str, hench)) + ("f" + "_".join(map(str, far)) if far else "")
        steps.append(("hire", "", list(hench)))
        if far:
            steps.append(("hirefar", "", list(far)))
        steps.append(("walk", label, (4, [c for c in (0.05, 0.25, 1, 2, 4, 8)]) if pc >= 5 else (2, WIDE)))
        steps.append(("fire", "", None))
    steps.append(("walk", "p1", (2, WIDE)))
    for h in ([1], [1, 1], [1, 1, 1], [2], [3], [4], [5], [6], [8]):
        party(1, h)
    party(1, [], far=[10])
    steps.append(("cont", "kmax14", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 1, "cr": 1, "DifficultyIndex": 4, "MaxCreatures": 14}))
    steps.append(("cont", "kmax25", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 1, "cr": 1, "DifficultyIndex": 4, "MaxCreatures": 25}))
    steps.append(("cont", "kmax12", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 1, "cr": 1, "DifficultyIndex": 4, "MaxCreatures": 12}))
    steps.append(("contout", "kout", {"SpawnOption": 0, "Respawns": -1, "Reset": 1, "ResetTime": 1, "cr": 0.1}))
    steps.append(("level", "", 5))
    steps.append(("walk", "p5", (4, [0.05, 0.25, 1, 2, 4, 8])))
    for h in ([1], [3], [4], [5], [5, 5], [6], [10]):
        party(5, h)
    steps.append(("level", "", 10))
    for h in ([10], [5, 5]):
        party(10, h)

BASE = {"PlayerOnly": 1, "Faction": 1, "MaxCreatures": 25, "RecCreatures": 1, "SpawnOption": 1, "Reset": 0, "Respawns": 0,
        "DifficultyIndex": 2, "Active": 1, "OnEntered": "", "OnExit": "", "OnExhausted": "eb_exh", "OnHeartbeat": ""}
cols, step = 9, 17
encs, spots, i = [], [], 0
for kind, label, args in steps:
    if kind not in ("walk", "cont", "contout"):
        continue
    x, y = 12 + (i % cols) * step, 12 + (i // cols) * step
    i += 1
    spots.append((label, x, y))
    if kind == "walk":
        d, cs = args
        for k, c in enumerate(cs, 1):
            a = math.radians(30 + 60 * (k - 1))
            s = dict(BASE); s["DifficultyIndex"] = d
            encs.append({"from": "nw_goblingrp", "tag": f"EB_{label.upper()}_{k}", "x": x, "y": y, "w": 6, "h": 6, "set": s,
                         "creatures": [{"resref": f"eb_{k}", "cr": c}],
                         "spawnPoints": [[round(x + 6 * math.cos(a), 2), round(y + 6 * math.sin(a), 2), 90]]})
    else:
        s = dict(BASE); s.update({k: v for k, v in args.items() if k != "cr"})
        if kind == "contout":
            s["OnExit"] = "eb_exit"
        encs.append({"from": "nw_goblingrp", "tag": f"EB_{label.upper()}_1", "x": x, "y": y, "w": 6, "h": 6, "set": s,
                     "creatures": [{"resref": "eb_1", "cr": args["cr"]}], "spawnPoints": [[x + 5, y + 5, 90]]})

scene = {"tileset": "tde01", "tile": 4, "size": 16, "pc": [4, 4, 0], "facing": 90, "seconds": 600, "gui": True,
         "factions": "Prelude", "encounters": encs}
with open(os.path.join(HERE, "scene.json"), "w", encoding="utf-8", newline="") as f:
    json.dump(scene, f, indent=1)
for k in range(1, 7):
    with open(os.path.join(HERE, f"eb_{k}.utc.json"), "w", encoding="utf-8", newline="") as f:
        f.write(json.dumps({"template": "nw_goblina", "hitPoints": 1, "tag": f"EB_{k}", "scripts": {"ScriptSpawn": "eb_spawn"}}) + "\n")
for lv in (1, 2, 3, 4, 5, 6, 8, 10):
    with open(os.path.join(HERE, f"eb_h{lv}.utc.json"), "w", encoding="utf-8", newline="") as f:
        f.write(json.dumps({"template": "nw_bandit001", "hitPoints": 200, "classLevels": [{"class": 4, "level": lv}]}) + "\n")

# The timeline.
lines = ['#include "eb_inc"', "// Generated by parity/gen_encounterbudget.py.", "", "void main()", "{",
         "    object pc = GetEnteringObject();",
         '    if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;',
         '    SetLocalInt(GetModule(), "started", 1);', "    SetPlotFlag(pc, TRUE);",
         '    Log("start|pchd=" + IntToString(GetHitDice(pc)));', "    float t = 4.0;"]
spot = {l: (x, y) for l, x, y in spots}
for kind, label, args in steps:
    if kind == "walk":
        x, y = spot[label]
        d, cs = args
        lines.append(f'    Walk("{label}", {x}.0, {y}.0, {d}, "{",".join(str(c) for c in cs)}", t); t += 4.5;')
    elif kind == "cont":
        x, y = spot[label]
        lines.append(f'    Cont("{label}", {x}.0, {y}.0, t); t += 30.0;')
    elif kind == "contout":
        x, y = spot[label]
        lines.append(f'    ContOut("{label}", {x}.0, {y}.0, t); t += 20.0;')
    elif kind == "level":
        lines.append(f"    DelayCommand(t, LevelTo({XP[args]}, {args})); t += 1.0;")
    elif kind == "xp":
        lines.append(f"    DelayCommand(t, SetXPTo({args})); t += 1.0;")
    elif kind == "hire":
        for n, lv in enumerate(args):
            lines.append(f'    DelayCommand(t, Hire("eb_h{lv}", {n})); t += 0.5;')
    elif kind == "hirefar":
        for n, lv in enumerate(args):
            lines.append(f'    DelayCommand(t, HireFar("eb_h{lv}", {n})); t += 0.5;')
    elif kind == "fire":
        lines.append("    DelayCommand(t, FireAll()); t += 0.5;")
lines += ['    DelayCommand(t, Log("end"));', "    DelayCommand(t + 1.0, VDone());", "}"]
with open(os.path.join(HERE, "vis_main.nss"), "w", encoding="utf-8", newline="") as f:
    f.write("\n".join(lines) + "\n")
print(len(encs), "encounters,", len(spots), "spots, seconds needed ~",
      sum(4.5 if k == "walk" else 30 if k == "cont" else 20 if k == "contout" else 1 for k, _, _ in steps))
