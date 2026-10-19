"""Wholly authored grayscale texture/emitters for additive sampling measurements."""
from pathlib import Path
import struct

VALUES = [0, 2, 8, 32, 128, 255]


def build():
    name = "vdr_spellturn"
    lines = [f"newmodel {name}", f"setsupermodel {name} NULL", "classification Effects",
             "setanimationscale 1", f"beginmodelgeom {name}", f"node dummy {name}",
             " parent NULL", "endnode"]
    for i, value in enumerate(VALUES):
        lines += [f"node emitter gray{value}", f" parent {name}", f" position {(i-2.5)*1.5} 0 2",
                  " update Single", " render Normal", " blend Lighten", " texture vtest_gray",
                  " birthrate 1", " lifeExp -1", " velocity 0", " sizeStart 1", " sizeEnd 1",
                  " colorStart 1 1 1", " colorEnd 1 1 1", " alphaStart 1", " alphaEnd 1",
                  " xgrid 6", " ygrid 1", f" frameStart {i}", f" frameEnd {i}", "endnode"]
    lines += [f"endmodelgeom {name}", f"donemodel {name}", ""]
    return "\n".join(lines)


def texture():
    width, height = len(VALUES)*16, 16
    header = struct.pack("<BBBHHBHHHHBB", 0, 0, 2, 0, 0, 0, 0, 0, width, height, 24, 0x20)
    row = b"".join(bytes([value]*3)*16 for value in VALUES)
    return header + row*height


if __name__ == "__main__":
    dest = Path(__file__).resolve().parents[2] / "parity/visual/scenes/particlecolor"
    dest.mkdir(parents=True, exist_ok=True)
    with (dest/"vdr_spellturn.mdl").open("w", encoding="utf-8", newline="") as stream:
        stream.write(build())
    (dest/"vtest_gray.tga").write_bytes(texture())
