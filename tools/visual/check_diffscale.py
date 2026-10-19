"""Reads a diffscale run (visual scene parity/visual/scenes/diffscale, docs/DIFFICULTY.md) and prints, for each kind of
hit, how often each weapon-dice value came up next to what the game difficulty's rule predicts.

    python tools/visual/check_diffscale.py out/visual/shots/vis_diffscale_d1/nwn [difficulty]

Hits on creatures come from the targets' OnDamaged notes, hits on the player from chat.txt. Every attacker has
Strength 14 (+2) and the player Strength 10 (+0), so the dice part is the damage minus 2 (or minus 0 for her);
"x2" rows are her critical hits (keen longsword on D3, the dice part being the total; told apart by size)."""
import collections, re, sys
from pathlib import Path
from fractions import Fraction

DIFF = {  # diffsettings.2da MinPCDamagePercent, MaxNPCDamagePercent by GetGameDifficulty
    0: (100, 25), 1: (50, 50), 2: (0, 100), 3: (0, 100), 4: (0, 200)}


def player_rule(n, die, p):
    """Distribution of the dice part when a player hits an NPC: R + ((p + random(100 - p)) * M) / 100, capped at M."""
    m = n * die
    rolls = roll_dist(n, die)
    if p == 0:
        return rolls
    out = collections.Counter()
    for r, pr in rolls.items():
        if p * m // 100 == m:
            out[m] += pr
            continue
        for k in range(100 - p):
            out[min(m, r + (p + k) * m // 100)] += pr / (100 - p)
    return out


def npc_rule(n, die, p):
    """Distribution of the dice part when an NPC hits a player."""
    m = n * die
    rolls = roll_dist(n, die)
    if p in (0, 100):
        return rolls
    out = collections.Counter()
    for r, pr in rolls.items():
        if p < 100:
            out[max(1, r - m * (100 - p) // 100)] += pr
        else:
            for k in range(p - 100):
                out[r + m * k // 100] += pr / (p - 100)
    return out


def roll_dist(n, die):
    d = collections.Counter({0: Fraction(1)})
    for _ in range(n):
        nd = collections.Counter()
        for s, pr in d.items():
            for f in range(1, die + 1):
                nd[s + f] += pr / die
        d = nd
    return d


def crit_rule(die, p, mult=2):
    one = player_rule(1, die, p)
    d = collections.Counter({0: Fraction(1)})
    for _ in range(mult):
        nd = collections.Counter()
        for s, pr in d.items():
            for v, q in one.items():
                nd[s + v] += pr * q
        d = nd
    return d


def show(name, values, expect):
    c = collections.Counter(values)
    n = len(values)
    keys = sorted(set(c) | {k for k, v in expect.items() if v > 0})
    print(f"\n{name}: {n} hits, mean {sum(values) / max(1, n):.2f} (rule {float(sum(k * v for k, v in expect.items())):.2f})")
    for k in keys:
        print(f"  {k:>3}: {c[k]:>4} ({c[k] / max(1, n):6.1%})   rule {float(expect.get(k, 0)):6.1%}")


def main():
    shots = Path(sys.argv[1])
    notes = (shots / "notes.txt").read_text(encoding="utf-8").splitlines()
    diff = int(sys.argv[2]) if len(sys.argv) > 2 else next(int(l.split("|")[4]) for l in notes if "|ds|difficulty|" in l)
    pc_p, npc_p = DIFF[diff]
    print("difficulty", diff)
    phase, seq = "", collections.defaultdict(list)
    for l in notes:
        p = l.split("|")
        if p[3] == "phase":
            phase = p[4]
        elif p[3] == "dmg":
            seq[(p[4], p[5], phase)].append(int(p[6]))
    chat = collections.defaultdict(list)
    for l in (shots / "chat.txt").read_text(encoding="utf-8").splitlines():
        m = re.search(r"\] (\w[\w ]*?) damages (\w[\w ]*?): (\d+) \(", l)
        if m:
            chat[(m.group(1), m.group(2))].append(int(m.group(3)))
    show("player -> NPC, longsword 1d8", seq[("D1", "", "pc.nw_wswls001")], player_rule(1, 8, pc_p))
    show("player -> NPC, greatsword 2d6", seq[("D1", "", "pc.nw_wswgs001")], player_rule(2, 6, pc_p))
    # The chat's damage lines don't follow their attack lines one for one, so a critical hit is told by its size:
    # 1d8 alone deals at most 8, two scaled 1d8 rolls at least 10.
    d3 = seq[("D3", "", "pc.nw_wswls001")]
    show("player -> NPC, keen longsword, not critical", [v for v in d3 if v <= 8], player_rule(1, 8, pc_p))
    show("player -> NPC, keen longsword, critical x2 (each roll scaled)", [v for v in d3 if v > 8], crit_rule(8, pc_p))
    show("  ... the same if the doubled dice were scaled as one (2d8)", [v for v in d3 if v > 8], player_rule(2, 8, pc_p))
    show("henchman -> NPC, longsword 1d8 (+2)", [v - 2 for v in seq[("D2", "H", "pc.nw_wswls001")] + seq[("D2", "H", "pc.nw_wswgs001")]], roll_dist(1, 8))
    show("NPC -> henchman, dagger 1d4 (+2)", [v - 2 for k, v2 in seq.items() if k[0] == "H" and k[1] == "GH" for v in v2], roll_dist(1, 4))
    show("NPC -> player, dagger 1d4 (+2)", [v - 2 for v in chat[("Dagger", "Aluvian Darkstar")]], npc_rule(1, 4, npc_p))
    show("NPC -> player, longsword 1d8 (+2)", [v - 2 for v in chat[("Sword", "Aluvian Darkstar")]], npc_rule(1, 8, npc_p))
    on_pc = [l for l in (shots / "chat.txt").read_text(encoding="utf-8").splitlines() if "attacks Aluvian Darkstar : *" in l]
    print(f"\nattacks on the player: {len(on_pc)}, threat rolls {sum('Threat Roll' in l for l in on_pc)}, "
          f"critical hits {sum('*critical hit*' in l for l in on_pc)}")


if __name__ == "__main__":
    main()
