"""Authored variable-rate emitter: low, high, stopped, then low again.

The pool must accommodate 200 births/second, but the keyed low phases ask for 20.
Used both for normal native/client captures and temporary client GPU slot diagnostics.
No installed asset is copied; the white texture is resolved at runtime.
"""
from pathlib import Path


def build():
    name = "vdr_spellturn"
    return f"""newmodel {name}
setsupermodel {name} NULL
classification Effects
setanimationscale 1
beginmodelgeom {name}
node dummy {name}
 parent NULL
endnode
node emitter rate
 parent {name}
 position 0 0 1.5
 update Fountain
 render Normal
 blend Normal
 texture fxpa_white
 birthrate 20
 lifeExp 1
 velocity 1
 sizeStart 0.06
 sizeEnd 0.06
 colorStart 1 0 1
 colorEnd 1 0 1
 alphaStart 1
 alphaEnd 1
 xsize 200
 ysize 200
endnode
endmodelgeom {name}
newanim impact {name}
 length 13
 transtime 0
 animroot {name}
 node dummy {name}
  parent NULL
 endnode
 node emitter rate
  parent {name}
  birthratekey 8
   0 20
   3.9 20
   4 200
   6.9 200
   7 0
   9.9 0
   10 20
   13 20
 endnode
doneanim impact {name}
donemodel {name}
"""


if __name__ == "__main__":
    dest = Path(__file__).resolve().parents[2] / "parity/visual/scenes/particlerate/vdr_spellturn.mdl"
    dest.parent.mkdir(parents=True, exist_ok=True)
    with dest.open("w", encoding="utf-8", newline="") as stream:
        stream.write(build())
