"""Writes the emitter calibration model (a text MDL replacing vdr_spellturn) for parity/visual/scenes/emitcal."""
import sys

BASE = dict(update="Fountain", render="Normal", blend="Lighten", texture="fxpa_white", xgrid=1, ygrid=1,
            birthrate=0, lifeExp=1, velocity=0, randvel=0, spread=0, mass=0, sizeStart=0.3, sizeEnd=0.3,
            sizeStart_y=0, sizeEnd_y=0, colorStart="1 1 1", colorEnd="1 1 1", alphaStart=1, alphaEnd=1,
            xsize=0, ysize=0, fps=0, frameStart=0, frameEnd=0, particleRot=0, drag=0, grav=0, bounce=0, bounce_co=0,
            random=0, inherit=0, inherit_local=0, inherit_part=0, inheritvel=0, spawntype=0, renderorder=0,
            twosidedtex=0, loop=0, deadspace=0, affectedByWind=0, m_isTinted=0, p2p=0, p2p_sel=1, splat=0,
            blurlength=10, lightningDelay=0, lightningRadius=0, lightningScale=0, blastRadius=0, blastLength=0,
            combinetime=0, threshold=0, p2p_bezier2=0, p2p_bezier3=0)

EMITTERS = [
    # name, position, overrides
    # A column: 2 m/s straight up for 2 s (4 m if velocity is metres per second).
    ("column", (-4, 0, 0.1), dict(birthrate=30, lifeExp=2, velocity=2, sizeStart=0.25, sizeEnd=0.25, texture="fxpa_flare")),
    # A 2-unit white square flat on the ground (is size the full width?).
    ("square", (-1.5, 0, 0.05), dict(update="Single", render="Billboard_to_World_Z", birthrate=1, lifeExp=-1, sizeStart=2, sizeEnd=2)),
    # A fall from 5 m with mass 1 for 1 s (4.9 m if mass is g).
    ("fall", (1.5, 0, 5), dict(birthrate=30, lifeExp=1, mass=1, sizeStart=0.25, sizeEnd=0.25, texture="fxpa_flare")),
    # Additive grey square at alpha 0.5, 1 m, standing at 1.5 m.
    ("add", (4, 0, 1.5), dict(update="Single", birthrate=1, lifeExp=-1, sizeStart=1, sizeEnd=1, colorStart="0.5 0.5 0.5", colorEnd="0.5 0.5 0.5", alphaStart=0.5, alphaEnd=0.5)),
    # Alpha-blended red square at alpha 0.5.
    ("mix", (6, 0, 1.5), dict(update="Single", blend="Normal", birthrate=1, lifeExp=-1, sizeStart=1, sizeEnd=1, colorStart="1 0 0", colorEnd="1 0 0", alphaStart=0.5, alphaEnd=0.5)),
    # Tinted (lit?) white square, alpha blended, and an untinted one beside it.
    ("tinted", (8, 0, 1.5), dict(update="Single", blend="Normal", birthrate=1, lifeExp=-1, sizeStart=1, sizeEnd=1, m_isTinted=1)),
    ("untinted", (9.5, 0, 1.5), dict(update="Single", blend="Normal", birthrate=1, lifeExp=-1, sizeStart=1, sizeEnd=1)),
    # A colour ramp over a 2 s rise: does the colour pass through grey or go red to blue directly?
    ("ramp", (-6.5, 0, 0.1), dict(birthrate=30, lifeExp=2, velocity=1.5, sizeStart=0.3, sizeEnd=0.3, colorStart="1 0 0", colorEnd="0 0 1", texture="fxpa_flare")),
    # A flipbook: 2x2 frames at 2 fps on a 4 s life; does it loop or stop on the last frame?
    ("spread", (-9, 0, 0.1), dict(birthrate=60, lifeExp=1, velocity=3, spread=1.5708, sizeStart=0.2, sizeEnd=0.2, texture="fxpa_flare")),
]

name = sys.argv[1] if len(sys.argv) > 1 else "vdr_spellturn"
out = [f"# Emitter calibration model (OpenWinter parity lab): known emitter settings, measured on both engines.",
       f"newmodel {name}", f"setsupermodel {name} NULL", "classification Effects", "setanimationscale 1",
       f"beginmodelgeom {name}", f"node dummy {name}", "  parent NULL", "endnode"]
for n, pos, o in EMITTERS:
    props = dict(BASE); props.update(o)
    out += [f"node emitter {n}", f"  parent {name}", f"  position {pos[0]} {pos[1]} {pos[2]}", "  orientation 0 0 0 0"]
    out += [f"  {k} {v}" for k, v in props.items()]
    out += ["endnode"]
out += [f"endmodelgeom {name}"]
# The effect's animations (a lasting effect plays impact, then duration in a loop); they key nothing.
for anim, length in (("impact", 0.5), ("duration", 2.0), ("cessation", 0.5)):
    out += [f"newanim {anim} {name}", f"  length {length}", "  transtime 0", f"animroot {name}", f"node dummy {name}", "  parent NULL", "endnode"]
    for n, pos, o in EMITTERS:
        out += [f"node emitter {n}", f"  parent {name}", "endnode"]
    out += [f"doneanim {anim} {name}"]
out += [f"donemodel {name}", ""]
open(sys.argv[2], "w", newline="\n").write("\n".join(out))
