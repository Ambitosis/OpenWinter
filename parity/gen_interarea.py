"""Writes the suite.json files of the inter-area movement suites (docs/INTER_AREA_MOVEMENT.md).

    python parity/gen_interarea.py

Each suite is a set of small flat areas (30 x 30 m of open tde01 floor) joined by door pairs and transition
triggers. A door pair is two stock generic doors facing each other's LinkedTo; a trigger pair is two 4 x 2 m
transition triggers, each leading to a waypoint 2.5 m inside the other. No game data is copied: the builder
clones the installed blueprints at build time.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
NOSCRIPTS = {k: "" for k in ["ScriptSpawn", "ScriptHeartbeat", "ScriptUserDefine", "ScriptDeath", "ScriptOnNotice",
    "ScriptAttacked", "ScriptDamaged", "ScriptDialogue", "ScriptDisturbed", "ScriptEndRound", "ScriptRested", "ScriptSpellAt"]}


def templates():
    return {
        # A walker without AI scripts; its OnBlocked logs (and opens the door when told to).
        "ia_walk": {"from": "nw_bandit001", "type": "utc", "set": dict(NOSCRIPTS, Tag="IA_WALK", ScriptOnBlocked="ia_blocked")},
        # The stock bandit with every default nw_c2_default* script, for the POST and AI cases.
        "ia_stock": {"from": "nw_bandit001", "type": "utc", "set": {"Tag": "IA_STOCK"}},
        "ia_door": {"from": "nw_door_normal", "type": "utd", "set": {"OnDeath": "", "OnOpen": "ia_open", "OnFailToOpen": "ia_fail"}},
        "ia_tr": {"from": "newtransition", "type": "utt", "set": {"ScriptOnEnter": "ia_tenter"}},
        "ia_wp": {"from": "nw_waypoint001", "type": "utw"},
    }


def graph(areas, links, points, door="ia_door"):
    """areas {resref: size}; links (kind, A, xA, yA, B, xB, yB, tag[, opts]); points (area, x, y, tag).

    kind "d": doors <tag> in A and <tag>_R in B linked to each other (opts "_fa"/"_fb" set their facing in degrees,
    other opts go on A's door); "d1": one door <tag> in A without a link; "t": triggers <tag> in A (to waypoint <tag>_RWP 2.5 m north of B's trigger) and
    <tag>_R in B (back to <tag>_WP 2.5 m south of A's); "t1": only A's trigger, to waypoint <tag>_RWP at (xB, yB).
    """
    inst = {a: [] for a in areas}
    for kind, a, xa, ya, b, xb, yb, tag, *rest in links:
        opts = dict(rest[0]) if rest else {}
        fa, fb = opts.pop("_fa", 0), opts.pop("_fb", 0)
        if kind == "d":
            inst[a].append({"type": "utd", "from": door, "x": xa, "y": ya, "facing": fa,
                            "set": dict({"Tag": tag, "LinkedTo": tag + "_R", "LinkedToFlags": 1}, **opts)})
            inst[b].append({"type": "utd", "from": door, "x": xb, "y": yb, "facing": fb,
                            "set": {"Tag": tag + "_R", "LinkedTo": tag, "LinkedToFlags": 1}})
        elif kind == "d1":
            inst[a].append({"type": "utd", "from": door, "x": xa, "y": ya, "facing": fa, "set": dict({"Tag": tag}, **opts)})
        elif kind == "t":
            inst[a].append({"type": "utt", "from": "ia_tr", "x": xa, "y": ya, "w": 4, "h": 2,
                            "set": dict({"Tag": tag, "LinkedTo": tag + "_RWP", "LinkedToFlags": 2}, **opts)})
            inst[b].append({"type": "utw", "from": "ia_wp", "x": xb, "y": yb + 2.5, "set": {"Tag": tag + "_RWP"}})
            inst[b].append({"type": "utt", "from": "ia_tr", "x": xb, "y": yb, "w": 4, "h": 2,
                            "set": {"Tag": tag + "_R", "LinkedTo": tag + "_WP", "LinkedToFlags": 2}})
            inst[a].append({"type": "utw", "from": "ia_wp", "x": xa, "y": ya - 2.5, "set": {"Tag": tag + "_WP"}})
        elif kind == "t1":
            inst[a].append({"type": "utt", "from": "ia_tr", "x": xa, "y": ya, "w": 4, "h": 2,
                            "set": dict({"Tag": tag, "LinkedTo": tag + "_RWP", "LinkedToFlags": 2}, **opts)})
            inst[b].append({"type": "utw", "from": "ia_wp", "x": xb, "y": yb, "set": {"Tag": tag + "_RWP"}})
    for a, x, y, tag in points:
        inst[a].append({"type": "utw", "from": "ia_wp", "x": x, "y": y, "set": {"Tag": tag}})
    return [{"resref": a, "tag": a.upper(), "size": size, "instances": inst[a]} for a, size in areas.items()]


def chain(prefix, kinds, areas, links, points, goal=(5, 20)):
    """Areas <prefix>a, <prefix>b, ... joined northwards (exit at y=25, arrival at y=5); START and GOAL waypoints."""
    names = [f"{prefix}{chr(97 + i)}" for i in range(len(kinds) + 1)]
    for n in names:
        areas[n] = 3
    for i, k in enumerate(kinds):
        links.append((k, names[i], 15, 25, names[i + 1], 15, 5, f"{prefix.upper()}_{i}"))
    points += [(names[0], 15, 5, prefix.upper() + "_START"), (names[-1], goal[0], goal[1], prefix.upper() + "_GOAL")]


def write(name, areas, seconds, extra_templates=None):
    folder = os.path.join(HERE, "suites", name)
    suite = {"seconds": seconds, "tile": 4, "templates": dict(templates(), **(extra_templates or {})),
             "areaEvents": {"OnEnter": "ia_aenter", "OnExit": "ia_aexit"},
             "areas": [{"resref": "ia_hub", "tag": "IA_HUB", "size": 2, "instances": []}] + areas}
    with open(os.path.join(folder, "suite.json"), "w", encoding="utf-8", newline="") as f:
        json.dump(suite, f, indent=1)
        f.write("\n")


def move_suite():
    areas, links, points = {}, [], []
    chain("md", "ddd", areas, links, points)   # MoveToObject, walking, open doors
    chain("mt", "ttt", areas, links, points)   # MoveToObject, running, triggers
    chain("mr", "dtd", areas, links, points)   # MoveToObject, running, mixed
    chain("ld", "ddd", areas, links, points)   # MoveToLocation through doors
    chain("lt", "tdt", areas, links, points)   # MoveToLocation through a trigger
    chain("fd", "dtd", areas, links, points)   # ForceMoveToObject
    chain("cl", "dd", areas, links, points)    # closed door, opened by OnBlocked
    chain("al", "dd", areas, links, points)    # default AI level
    chain("ck", "dd", areas, links, points)    # door with an OnClick script
    chain("ct", "td", areas, links, points)    # trigger with an OnClick script
    # Route choice: two exits, a 2-area route through one big area and a 3-area route through small ones, and a
    # dead end.
    areas.update({"rca": 3, "rcl": 8, "rcr1": 2, "rcr2": 2, "rcx": 2, "rcd": 3})
    links += [("d", "rca", 8, 25, "rcl", 8, 5, "RC_L"), ("d", "rcl", 8, 75, "rcd", 8, 5, "RC_LD"),
              ("d", "rca", 22, 25, "rcr1", 10, 5, "RC_R"), ("d", "rcr1", 10, 15, "rcr2", 10, 5, "RC_R1"),
              ("d", "rcr2", 10, 15, "rcd", 22, 5, "RC_R2"), ("d", "rca", 15, 25, "rcx", 10, 5, "RC_X")]
    points += [("rca", 15, 5, "RC_START"), ("rcd", 15, 20, "RC_GOAL")]
    # A locked door with a longer unlocked way round; a locked door that is the only way.
    areas.update({"laa": 3, "lab": 3, "lac": 3, "lka": 3, "lkb": 3})
    links += [("d", "laa", 8, 25, "lab", 8, 5, "LA_AB", {"Locked": 1}), ("d", "laa", 22, 25, "lac", 22, 5, "LA_AC"),
              ("d", "lac", 15, 25, "lab", 22, 5, "LA_CB"), ("d", "lka", 15, 25, "lkb", 15, 5, "LK_AB", {"Locked": 1})]
    points += [("laa", 15, 5, "LA_START"), ("lab", 15, 20, "LA_GOAL"), ("lka", 15, 5, "LK_START"), ("lkb", 15, 20, "LK_GOAL")]
    # No route; a door whose link names nothing; a trigger given its target by SetTransitionTarget.
    areas.update({"nra": 3, "nrz": 3, "mla": 3, "mlb": 3, "sta": 3, "stb": 3})
    links += [("d", "mla", 15, 25, "mlb", 15, 5, "ML_AB", {"LinkedTo": "NOWHERE"}),
              ("t1", "sta", 15, 25, "stb", 15, 7.5, "ST_AB", {"LinkedTo": "", "LinkedToFlags": 0})]
    points += [("nra", 15, 5, "NR_START"), ("nra", 20, 5, "NL_START"), ("nrz", 15, 20, "NR_GOAL"), ("mla", 15, 5, "ML_START"),
               ("mlb", 15, 20, "ML_GOAL"), ("sta", 15, 5, "ST_START"), ("stb", 15, 7.5, "ST_DEST"), ("stb", 15, 20, "ST_GOAL")]
    # Landing beside destination doors facing 180 and 90 degrees.
    areas.update({"pfa": 3, "pfb": 3, "pga": 3, "pgb": 3})
    links += [("d", "pfa", 15, 25, "pfb", 15, 5, "PF_AB", {"_fb": 180}), ("d", "pga", 15, 25, "pgb", 15, 15, "PG_AB", {"_fb": 90})]
    points += [("pfa", 15, 5, "PF_START"), ("pfb", 25, 25, "PF_GOAL"), ("pga", 15, 5, "PG_START"), ("pgb", 25, 25, "PG_GOAL")]
    # One door: walked south through it, then (measured) back to the far door's use point coordinates first.
    areas.update({"oha": 3, "ohb": 3})
    links += [("d", "oha", 15, 5, "ohb", 15, 25, "OH_AB")]
    points += [("oha", 15, 15, "OH_START"), ("ohb", 15, 15, "OH_GOAL")]
    # A same-area walk across a transition trigger.
    areas.update({"saa": 3, "sab": 3})
    links += [("t1", "saa", 15, 15, "sab", 15, 15, "SA_T")]
    points += [("saa", 15, 5, "SA_START"), ("saa", 15, 25, "SA_GOAL")]
    # Moving away from an object in another area.
    areas.update({"maa": 3, "mab": 3})
    links += [("d", "maa", 15, 25, "mab", 15, 5, "MA_AB")]
    points += [("maa", 15, 10, "MA_START"), ("mab", 15, 15, "MA_FROM")]
    # The OnClick doors and triggers.
    for i, l in enumerate(links):
        if l[7] in ("CK_0", "CT_0"):
            opts = dict(l[8]) if len(l) > 8 else {}
            opts["OnClick"] = {"resref": "ia_click"}
            links[i] = (*l[:8], opts)
    write("interarea_move", graph(areas, links, points), 85)


def pursuit_suite():
    areas, links, points = {}, [], []
    def trio(p, kind):
        for n in "abc":
            areas[f"{p}{n}"] = 3
        links.extend([(kind, f"{p}a", 15, 25, f"{p}b", 15, 5, f"{p.upper()}_AB"), (kind, f"{p}b", 15, 25, f"{p}c", 15, 5, f"{p.upper()}_BC")])
    # Fleeing targets: through doors from an attacker without scripts, from a stock bandit, through triggers.
    for p, kind in (("ka", "d"), ("kb", "d"), ("kc", "t")):
        trio(p, kind)
        points += [(f"{p}a", 15, 15, f"{p.upper()}_T"), (f"{p}a", 15, 11, f"{p.upper()}_A"), (f"{p}c", 20, 20, f"{p.upper()}_GOAL")]
    # A target jumped two areas away; a target in another area from the start; an attacker 38 m behind.
    trio("ja", "d")
    points += [("jaa", 15, 15, "JA_T"), ("jaa", 15, 11, "JA_A"), ("jac", 20, 20, "JA_JUMP")]
    trio("jb", "d")
    points += [("jba", 15, 15, "JB_A"), ("jbc", 20, 20, "JB_T")]
    areas.update({"jca": 5, "jcb": 3})
    links += [("d", "jca", 25, 45, "jcb", 15, 5, "JC_AB")]
    points += [("jca", 25, 2, "JC_A"), ("jca", 25, 40, "JC_T"), ("jcb", 20, 20, "JC_GOAL")]
    # A master and its summoned creature.
    trio("sm", "d")
    points += [("sma", 15, 5, "SM_START"), ("smc", 20, 20, "SM_GOAL")]
    # Stock bandits taken away from their POST_ waypoints: default AI level, two doors, door-trigger-door, one door.
    areas.update({"r1a": 3, "r1b": 3, "r2a": 3, "r2b": 3, "r2c": 3, "r3a": 3, "r3b": 3, "r3c": 3, "r3d": 3, "r5a": 3, "r5b": 3})
    links += [("d", "r1a", 15, 25, "r1b", 15, 5, "R1_AB"),
              ("d", "r2a", 15, 25, "r2b", 15, 5, "R2_AB"), ("d", "r2b", 15, 25, "r2c", 15, 5, "R2_BC"),
              ("d", "r3a", 15, 25, "r3b", 15, 5, "R3_AB"), ("t", "r3b", 15, 25, "r3c", 15, 5, "R3_BC"), ("d", "r3c", 15, 25, "r3d", 15, 5, "R3_CD"),
              ("d", "r5a", 15, 25, "r5b", 15, 5, "R5_AB")]
    points += [("r1a", 15, 15, "POST_IA_R1"), ("r1b", 15, 15, "R1_DROP"), ("r2a", 15, 15, "POST_IA_R2"), ("r2c", 15, 15, "R2_DROP"),
               ("r3a", 15, 15, "POST_IA_R3"), ("r3d", 15, 15, "R3_DROP"), ("r5a", 15, 15, "POST_IA_R5"), ("r5b", 15, 15, "R5_DROP")]
    write("interarea_pursuit", graph(areas, links, points), 90)


def bashed_suite():
    """Doors killed by damage, EffectDeath and bashing (door_bashed): what is left, and who goes through."""
    areas, links, points = {}, [], []
    door = {"from": "nw_door_normal", "type": "utd", "set": {
        "OnDeath": "bt_death", "OnDamaged": "bt_damaged", "OnOpen": "bt_open", "OnClosed": "bt_closed",
        "OnFailToOpen": "bt_fail", "OnLock": "bt_lock", "OnUnlock": "bt_unlock", "OnHeartbeat": "", "OnMeleeAttacked": "bt_attacked",
        "Plot": 0, "HP": 10, "CurrentHP": 10, "Hardness": 0}}

    def pair(p, opts=None, back=True):
        """Areas <p>a and <p>b joined by door <P>_0 (a, y=25) / <P>_0_R (b, y=5); a walker each way."""
        areas.update({p + "a": 3, p + "b": 3})
        links.append(("d", p + "a", 15, 25, p + "b", 15, 5, p.upper() + "_0") + ((opts,) if opts else ()))
        points.extend([(p + "a", 15, 5, p.upper() + "_START"), (p + "b", 15, 20, p.upper() + "_GOAL")])
        if back:
            points.extend([(p + "b", 25, 15, p.upper() + "R_START"), (p + "a", 25, 12, p.upper() + "R_GOAL")])

    pair("ed")                      # EffectDamage
    pair("ek")                      # EffectDeath
    pair("ba", {"Locked": 1, "HP": 1, "CurrentHP": 1}, back=False)   # bashed by a walker that then goes through
    points.append(("baa", 15, 21, "BA_BASH"))
    pair("cc", back=False)          # closed by a creature after its death and locked, then walked through
    points.append(("cca", 13, 21, "CC_USER"))
    pair("cu", back=False)          # closed by a creature after its death, not locked, then walked through
    points.append(("cua", 13, 21, "CU_USER"))
    pair("cx", back=False)          # closed by a creature after its death, then walked across inside its area
    points.extend([("cxa", 13, 21, "CX_USER"), ("cxa", 15, 22, "CXX_START"), ("cxa", 15, 28, "CXX_GOAL")])
    pair("co", back=False)          # opened by a creature after its death
    points.append(("coa", 13, 21, "CO_USER"))
    pair("op")                      # killed while open
    pair("ck", {"OnClick": {"resref": "bt_click"}}, back=False)   # OnAreaTransitionClick script
    pair("ds")                      # DestroyObject on a door
    pair("jp", back=False)          # jumps to the dead door and its location
    points.extend([("jpb", 15, 20, "JP_J1"), ("jpb", 18, 20, "JP_J2"), ("jpb", 21, 20, "JP_J3")])
    pair("pu", back=False)          # pursuit through the dead door
    points.extend([("pua", 15, 15, "PU_T"), ("pua", 15, 11, "PU_A")])
    # Doors without a transition: killed by damage, by EffectDeath, and removed with DestroyObject; a walk across a
    # live closed door.
    areas.update({"sxa": 3})
    links.extend([("d1", "sxa", 10, 15, "", 0, 0, "SX_DEAD"), ("d1", "sxa", 20, 15, "", 0, 0, "SX_LIVE"),
                  ("d1", "sxa", 25, 26, "", 0, 0, "UK"), ("d1", "sxa", 5, 26, "", 0, 0, "UD")])
    points.extend([("sxa", 20, 10, "SXL_START"), ("sxa", 20, 20, "SXL_GOAL")])
    # Doors leading to a waypoint and to nothing.
    areas.update({"ula": 3, "ulb": 3})
    links.extend([("d1", "ula", 8, 15, "", 0, 0, "UW", {"LinkedTo": "UW_WP", "LinkedToFlags": 2}),
                  ("d1", "ula", 20, 15, "", 0, 0, "UN", {"LinkedTo": "NOWHERE", "LinkedToFlags": 1})])
    points.append(("ulb", 15, 15, "UW_WP"))
    # The leaf: walkers 1 m either side of a live door opened by a creature (LO), one opened by a creature and then
    # killed (LD) and one killed while closed (KD).
    areas.update({"lfa": 3, "lfb": 3})
    for tag, x in (("LO", 6), ("LD", 15), ("KD", 24)):
        links.append(("d", "lfa", x, 15, "lfb", x, 15, tag))
        for side, y in (("S", 14), ("N", 16)):
            points.extend([("lfa", x - 3, y, tag + side + "_START"), ("lfa", x + 3, y, tag + side + "_GOAL")])
        # Then straight across the doorway.
        points.extend([("lfa", x, 11, tag + "X_START"), ("lfa", x, 19, tag + "X_GOAL")])
        # The opener, on the north side, and where it goes afterwards.
        points.extend([("lfa", x, 17, tag + "_OPENER"), ("lfa", x + 2, 23, tag + "_AWAY")])
    points.append(("lfa", 9, 20, "LO_USER"))   # opens the open live door again
    write("door_bashed", graph(areas, links, points, door="bt_door"), 80, {"bt_door": door})


def route_ties_suite():
    """Routes of equal length (route_ties): which of two exits a walker takes when both lead to the goal
    in the same number of areas, by their X in the first area; and the wait before an inter-area move sets off."""
    areas, links, points = {}, [], []
    # A walker in <p>a near the exit at x=22; the exit at x=8 leads through a much larger area. Both routes are two
    # transitions long.
    for p, lowbig in (("ta", True), ("ua", False)):
        areas.update({p + "a": 3, p + "l": 5 if lowbig else 3, p + "h": 3 if lowbig else 5, p + "c": 3})
        hx, hy = (15, 25) if lowbig else (40, 45)
        lx, ly = (40, 45) if lowbig else (15, 25)
        links += [("t", p + "a", 8, 25, p + "l", 8, 5, p.upper() + "_L"), ("t", p + "a", 22, 25, p + "h", 22, 5, p.upper() + "_H"),
                  ("t", p + "l", lx, ly, p + "c", 8, 5, p.upper() + "_LC"), ("t", p + "h", hx, hy, p + "c", 22, 5, p.upper() + "_HC")]
        points += [(p + "a", 25, 12, p.upper() + "_START"), (p + "c", 15, 20, p.upper() + "_GOAL")]
    folder = os.path.join(HERE, "suites", "route_ties")
    suite = {"seconds": 90, "tile": 4, "templates": {"ia_tr": {"from": "newtransition", "type": "utt"}, "ia_wp": {"from": "nw_waypoint001", "type": "utw"}},
             "areaEvents": {"OnEnter": "rt_aenter"},
             "areas": [{"resref": "rt_hub", "tag": "RT_HUB", "size": 2, "instances": []}] + graph(areas, links, points)}
    with open(os.path.join(folder, "suite.json"), "w", encoding="utf-8", newline="") as f:
        json.dump(suite, f, indent=1)
        f.write("\n")


if __name__ == "__main__":
    move_suite()
    pursuit_suite()
    bashed_suite()
    route_ties_suite()
