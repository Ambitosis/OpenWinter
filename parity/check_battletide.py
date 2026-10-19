"""Reject incomplete or jointly wrong Battletide save-control comparisons."""
import math
from pathlib import Path
import sys


def read(path):
    return dict(line.strip().split("|", 1) for line in path.read_text(encoding="utf-8").splitlines() if "|" in line)


def probability(count, trials, chance):
    mass = lambda k: math.comb(trials, k) * chance**k * (1-chance)**(trials-k)
    observed = mass(count)
    return sum(mass(k) for k in range(trials+1) if mass(k) <= observed + 1e-15)


def check(path):
    data = read(path)
    assert data.get("__done__") == "1", f"{path}: incomplete"
    for key, chance in (("lowSave", .95), ("highSave", .05)):
        for name, expected in (("badEffectData", 0), ("badCasterEffects", 0), ("casterBuffs", 20), ("consumed", 20), ("aoeSpell", 517), ("aoeLevel", 20), ("aoeDC", 20)):
            assert int(data[f"{key}.{name}"]) == expected, f"{path}: {key}.{name}"
        fraction = float(data[f"{key}.debuffFraction"])
        count = round(fraction * 80)
        assert 0 <= count <= 80 and abs(fraction-count/80) <= .00051
        p = probability(count, 80, chance)
        assert p >= .001, f"{path}: {key} {count}/80 rejects expected {chance}, p={p}"
        print(f"{path.name} {key}: {count}/80; exact binomial p={p:.5f}")
    assert float(data["lowSave.debuffFraction"]) > float(data["highSave.debuffFraction"])


if __name__ == "__main__":
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results/battletide_saves")
    for engine in ("nwn", "openwinter"):
        check(root / (engine + ".txt"))
