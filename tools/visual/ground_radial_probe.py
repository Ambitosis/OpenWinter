"""Identify native ground radial resources with authored markers in the owned sandbox.

Never reads or writes real userdata; aborts rather than overwriting any existing
sandbox override. Every marker is removed after the harness exits.
"""
from pathlib import Path
import json
import subprocess
import sys
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ICONS = "ir_beg ir_bored ir_bow ir_cheer ir_cuss ir_drink ir_fallback ir_fallfront ir_goodbye ir_greet ir_laugh ir_lookfar ir_meditate ir_moveto ir_nod ir_read ir_salute ir_scratch ir_sit ir_threaten ir_tired ir_use ir_woosey ir_worship ir_blank ir_cktrap ir_dice ir_distract ir_empower ir_extend ir_knock ir_maximize ir_quicken ir_silent ir_skillself ir_skilltgt ir_still ir_textmacro ir_tileaction ir_attack ir_cast".split()

def main():
    override = ROOT / "out/visual/user/override"
    override.mkdir(parents=True, exist_ok=True)
    paths = [override / (name + ".tga") for name in ICONS]
    if any(path.exists() for path in paths):
        raise RuntimeError("Existing marker-name override: refusing to overwrite")
    legend = {}
    try:
        for index, (name, path) in enumerate(zip(ICONS, paths)):
            color = ((index * 73 + 60) % 180 + 50, (index * 41 + 20) % 180 + 50, (index * 97 + 30) % 180 + 50)
            img = Image.new("RGBA", (32, 32), color + (255,))
            draw = ImageDraw.Draw(img)
            draw.rectangle((0, 0, 31, 31), outline="white", width=2)
            draw.rectangle((4, 9, 27, 22), fill="black")
            draw.text((9, 10), f"{index:02}", fill="white")
            img.save(path)
            legend[str(index)] = name
        out = ROOT / "out/ground-marker-legend.json"
        out.write_text(json.dumps(legend, indent=2) + "\n", encoding="utf-8")
        return subprocess.call([sys.executable, str(ROOT / "tools/visual/visual.py"), "run", "groundheld", "--only", "nwn", "--tag", "_markers"], cwd=ROOT)
    finally:
        for path in paths:
            if path.exists():
                path.unlink()

if __name__ == "__main__":
    raise SystemExit(main())
