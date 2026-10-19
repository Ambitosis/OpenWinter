"""Authored texture, vertex-color and overlapping-particle calibration."""
from pathlib import Path
from particle_color_model import texture

VALUES = [0, 2, 8, 32, 128, 255]
OVERLAPS = [(32, 1), (32, 2), (32, 4), (128, 1), (128, 2), (128, 4)]

def build():
    name = "vdr_spellturn"
    lines = [f"newmodel {name}", f"setsupermodel {name} NULL", "classification Effects",
             "setanimationscale 1", f"beginmodelgeom {name}", f"node dummy {name}",
             " parent NULL", "endnode"]
    def emitter(label, cell, z, frame, color=1):
        lines.extend([f"node emitter {label}", f" parent {name}", f" position {(cell-2.5)*1.5} 0 {z}",
            " update Single", " render Normal", " blend Lighten", " texture vtest_gray",
            " birthrate 1", " lifeExp -1", " velocity 0", " sizeStart 1", " sizeEnd 1",
            f" colorStart {color} {color} {color}", f" colorEnd {color} {color} {color}",
            " alphaStart 1", " alphaEnd 1", " xgrid 6", " ygrid 1",
            f" frameStart {frame}", f" frameEnd {frame}", "endnode"])
    for i, value in enumerate(VALUES):
        emitter(f"texture{value}", i, 4, i)
        emitter(f"vertex{value}", i, 2, 5, value/255)
    for i, (value, copies) in enumerate(OVERLAPS):
        for copy in range(copies):
            emitter(f"overlap{i}_{copy}", i, 0, VALUES.index(value))
    lines.extend([f"endmodelgeom {name}", f"donemodel {name}", ""])
    return "\n".join(lines)

if __name__ == "__main__":
    dest = Path(__file__).resolve().parents[2] / "parity/visual/scenes/particleblend"
    dest.mkdir(parents=True, exist_ok=True)
    with (dest/"vdr_spellturn.mdl").open("w", encoding="utf-8", newline="") as f:
        f.write(build())
    (dest/"vtest_gray.tga").write_bytes(texture())
