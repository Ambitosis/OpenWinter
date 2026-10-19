"""Generate wholly authored rigid box geometry for aura silhouette measurements."""
from pathlib import Path
import argparse

def build(axis="width"):
    name = "c_a_chicken"
    out = [f"newmodel {name}", f"setsupermodel {name} NULL", "classification Character",
           "setanimationscale 1", f"beginmodelgeom {name}", f"node dummy {name}", " parent NULL", "endnode"]
    faces = [(0,3,2,1), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7)]
    for index, (offset, width) in enumerate([(-2, .5), (0, 1), (2, 1.5)]):
        height = width if axis == "height" else 1
        if axis == "height":
            width = .5
        r = width / 2
        vertices = [(-r,-r,0),(r,-r,0),(r,r,0),(-r,r,0),(-r,-r,height),(r,-r,height),(r,r,height),(-r,r,height)]
        out += [f"node trimesh box{index}", f" parent {name}", f" position {offset} 0 1",
                " orientation 0 0 1 0", " render 1", " shadow 0", " bitmap NULL",
                " ambient 1 1 1", " diffuse 1 1 1", " specular 0 0 0", " selfillumcolor 0.125 0.125 0.125", " verts 24"]
        for face in faces:
            for v in face:
                out.append("  " + " ".join(str(c) for c in vertices[v]))
        out += [" tverts 24"]
        out += ["  " + v for _ in faces for v in ["0 0 0", "1 0 0", "1 1 0", "0 1 0"]]
        out += [" faces 12"]
        for f in range(6):
            a=f*4
            out += [f"  {a} {a+1} {a+2} 0 {a} {a+1} {a+2} 0", f"  {a} {a+2} {a+3} 0 {a} {a+2} {a+3} 0"]
        out += ["endnode"]
    out += [f"endmodelgeom {name}", f"donemodel {name}", ""]
    return "\n".join(out)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--axis", choices=["width", "height"], default="width")
    args = parser.parse_args()
    scene = "aurabox" if args.axis == "width" else "auraboxheight"
    dest=Path(__file__).resolve().parents[2]/"parity/visual/scenes"/scene
    dest.mkdir(parents=True,exist_ok=True)
    with (dest/"c_a_chicken.mdl").open("w",encoding="utf-8",newline="") as f:f.write(build(args.axis))
