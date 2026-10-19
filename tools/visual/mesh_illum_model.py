"""Authored self-illumination ramps: untextured, diffuse, gray texture and additive."""
from pathlib import Path
import struct


def build():
    name = "c_a_chicken"
    lines = [f"newmodel {name}", f"setsupermodel {name} NULL", "classification Character",
             "setanimationscale 1", f"beginmodelgeom {name}", f"node dummy {name}", " parent NULL", "endnode"]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    vertices = [(-.3,-.3,0),(.3,-.3,0),(.3,.3,0),(-.3,.3,0),
                (-.3,-.3,.6),(.3,-.3,.6),(.3,.3,.6),(-.3,.3,.6)]
    for row in range(4):
        for col, emission in enumerate([.125, .25, .5, 1]):
            if row == 3 and col == 0:
                emission = 0  # Additive material must not become unlit full-bright by itself.
            diffuse = .25 if row == 1 else 1
            texture = "vis_illumgray" if row == 2 else "vis_illumadd" if row == 3 else "NULL"
            lines += [f"node trimesh sample{row}{col}", f" parent {name}",
                      f" position {-2.4 + col * 1.6} 0 {0.5 + row * 1.2}",
                      " orientation 0 0 1 0", " render 1", " shadow 0", f" bitmap {texture}",
                      " ambient 1 1 1", f" diffuse {diffuse} {diffuse} {diffuse}",
                      " specular 0 0 0", f" selfillumcolor {emission} {emission} {emission}", " verts 24"]
            for face in faces:
                lines += ["  " + " ".join(str(c) for c in vertices[v]) for v in face]
            lines += [" tverts 24"]
            lines += ["  " + uv for _ in faces for uv in ["0 0 0", "1 0 0", "1 1 0", "0 1 0"]]
            lines += [" faces 12"]
            for face in range(6):
                a = face * 4
                lines += [f"  {a} {a+1} {a+2} 0 {a} {a+1} {a+2} 0",
                          f"  {a} {a+2} {a+3} 0 {a} {a+2} {a+3} 0"]
            lines += ["endnode"]
    return "\n".join(lines + [f"endmodelgeom {name}", f"donemodel {name}", ""])


if __name__ == "__main__":
    dest = Path(__file__).resolve().parents[2] / "parity/visual/scenes/meshillum"
    dest.mkdir(parents=True, exist_ok=True)
    with (dest / "c_a_chicken.mdl").open("w", encoding="utf-8", newline="") as stream:
        stream.write(build())
    # Uncompressed, top-origin 32-bit TGA, wholly authored uniform 128 gray.
    header = struct.pack("<BBBHHBHHHHBB", 0, 0, 2, 0, 0, 0, 0, 0, 2, 2, 32, 0x28)
    (dest / "vis_illumgray.tga").write_bytes(header + bytes([128, 128, 128, 255]) * 4)
    (dest / "vis_illumadd.tga").write_bytes(header + bytes([128, 128, 128, 255]) * 4)
    with (dest / "vis_illumadd.txi").open("w", encoding="utf-8", newline="") as stream:
        stream.write("blending additive\n")
