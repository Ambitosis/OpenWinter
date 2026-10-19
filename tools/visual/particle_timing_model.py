"""Write the wholly authored particle timing fixture; no installed assets are copied.

The three single particles reference an installed four-frame rune texture at runtime.
Left: random initial frame, 2 fps. Middle: fixed initial frame, 2 fps. Right: random,
stationary frame. The lower fountain keeps keyed controllers changing after removal,
so client emission-stop diagnostics can detect an unintended restart.
"""
from pathlib import Path


def build():
    name = "vdr_spellturn"
    lines = [f"newmodel {name}", f"setsupermodel {name} NULL",
             "classification Effects", "setanimationscale 1", f"beginmodelgeom {name}",
             f"node dummy {name}", " parent NULL", "endnode"]
    emitters = [("random", -2, 1, 2), ("fixed", 0, 0, 2), ("still", 2, 1, 0)]
    for node, x, random, fps in emitters:
        lines += [f"node emitter {node}", f" parent {name}", f" position {x} 0 1.5",
                  " update Single", " render Normal", " blend Normal",
                  " texture fxpa_Symbl_001", " xgrid 2", " ygrid 2", " birthrate 1",
                  " lifeExp -1", " velocity 0", " sizeStart 1.5", " sizeEnd 1.5", " colorStart 1 1 1",
                  " colorEnd 1 1 1", " alphaStart 1", " alphaEnd 1", " frameStart 0",
                  " frameEnd 3", f" random {random}", f" fps {fps}", "endnode"]
    lines += ["node emitter fountain", f" parent {name}", " position 0 0 0.1",
              " update Fountain", " render Normal", " blend Normal", " texture fxpa_white",
              " birthrate 12", " lifeExp 2", " velocity 1", " sizeStart 0.15", " sizeEnd 0.15",
              " colorStart 1 0 0", " colorEnd 1 0 0", " alphaStart 1", " alphaEnd 1", "endnode",
              f"endmodelgeom {name}"]
    for anim, length in [("impact", 0.1), ("duration", 1.0)]:
        lines += [f"newanim {anim} {name}", f" length {length}", " transtime 0",
                  f" animroot {name}", f" node dummy {name}", "  parent NULL", " endnode",
                  " node emitter fountain", f"  parent {name}", "  birthratekey 3",
                  "   0 12", f"   {length / 2} 14", f"   {length} 12", " endnode",
                  f"doneanim {anim} {name}"]
    lines += [f"donemodel {name}", ""]
    return "\n".join(lines)


if __name__ == "__main__":
    dest = Path(__file__).resolve().parents[2] / "parity/visual/scenes/particletiming/vdr_spellturn.mdl"
    dest.parent.mkdir(parents=True, exist_ok=True)
    with dest.open("w", encoding="utf-8", newline="") as stream:
        stream.write(build())
