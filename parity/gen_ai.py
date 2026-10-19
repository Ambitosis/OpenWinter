"""Writes the generated parts of the stock AI suites (docs/STOCK_AI.md): the one-line event wrappers pai_e0..pai_e12
(each logs the event through pai_inc and then runs the creature's own script) and each suite's suite.json, whose
fights get one small flat area each so the replicas cannot perceive one another.

    python parity/gen_ai.py

The suites' parity_main.nss files are written by hand.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))

# suite: (seconds, number of areas or a list of (prefix, count, size), area size in tiles, extra suite.json fields)
SUITES = {
    "ai_events": (70, [("aih", 1, 4), ("aip", 16, 4), ("aic", 8, 2), ("air", 6, 3)], 2, {}),
    "ai_melee": (95, 16, 3, {}),
    "ai_caster": (110, 16, 3, {}),
    "ai_feats": (110, 16, 3, {}),
    "ai_ranged": (100, 12, 4, {}),
    "ai_flee": (70, 12, 4, {}),
    "ai_associate": (110, 12, 4, {}),
}


def wrappers(folder):
    for n in range(13):
        if n == 8:
            continue  # OnSpawn has already run when the wrappers go on
        with open(os.path.join(folder, "pai_e%d.nss" % n), "w", encoding="utf-8", newline="") as f:
            f.write('#include "pai_inc"\nvoid main() { PaiEvent(%d); }\n' % n)


def main():
    for name, (seconds, areas, size, extra) in SUITES.items():
        folder = os.path.join(HERE, "suites", name)
        os.makedirs(folder, exist_ok=True)
        wrappers(folder)
        suite = {"seconds": seconds, "tile": 4, "size": size, "factions": "Prelude"}
        if isinstance(areas, list):
            suite["areas"] = [{"resref": "%s%02d" % (p, i), "tag": ("%s%02d" % (p, i)).upper(), "size": sz}
                              for p, count, sz in areas for i in range(count)]
        elif areas > 1:
            suite["areas"] = [{"resref": "ai%02d" % i, "tag": "AI%02d" % i, "size": size} for i in range(areas)]
        suite.update(extra)
        with open(os.path.join(folder, "suite.json"), "w", encoding="utf-8", newline="") as f:
            json.dump(suite, f, indent=1)
            f.write("\n")


if __name__ == "__main__":
    main()
