"""Authored front/back faces with ordinary and additive textures."""
from pathlib import Path
import struct

dest = Path(__file__).resolve().parents[2] / "parity/visual/scenes/meshfaces"
dest.mkdir(parents=True, exist_ok=True)
name = "c_a_chicken"
lines = [f"newmodel {name}", f"setsupermodel {name} NULL", "classification Character",
         "setanimationscale 1", f"beginmodelgeom {name}", f"node dummy {name}", " parent NULL", "endnode"]
for index, x in enumerate([-2.4, -.8, .8, 2.4]):
    texture = "vis_faceadd" if index >= 2 else "vis_facegray"
    lines += [f"node trimesh plane{index}", f" parent {name}", f" position {x} 0 1.5",
              " render 1", " shadow 0", f" bitmap {texture}", " diffuse 1 1 1", " ambient 1 1 1",
              " specular 0 0 0", " selfillumcolor 1 1 1", " verts 4",
              " -.5 -.5 0", " .5 -.5 0", " .5 .5 0", " -.5 .5 0", " tverts 4",
              " 0 0 0", " 1 0 0", " 1 1 0", " 0 1 0", " faces 2"]
    triangles = [(0, 1, 2), (0, 2, 3)] if index % 2 == 0 else [(2, 1, 0), (3, 2, 0)]
    for a, b, c in triangles:
        lines += [f" {a} {b} {c} 0 {a} {b} {c} 0"]
    lines += ["endnode"]
lines += [f"endmodelgeom {name}", f"donemodel {name}", ""]
with (dest / f"{name}.mdl").open("w", encoding="utf-8", newline="") as stream:
    stream.write("\n".join(lines))
header = struct.pack("<BBBHHBHHHHBB", 0, 0, 2, 0, 0, 0, 0, 0, 2, 2, 32, 0x28)
for texture in ["vis_faceadd", "vis_facegray"]:
    (dest / f"{texture}.tga").write_bytes(header + bytes([128, 128, 128, 255]) * 4)
with (dest / "vis_faceadd.txi").open("w", encoding="utf-8", newline="") as stream:
    stream.write("blending additive\n")
