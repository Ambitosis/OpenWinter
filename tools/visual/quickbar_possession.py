"""Silent, isolated quickbar ownership regression for both HUD styles."""
import argparse
import os
import tempfile
import visual

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("mode", choices=("classic", "modern"))
args = parser.parse_args()
cfg = visual.config()
profile = tempfile.TemporaryDirectory(prefix="quickbar-possession-" + args.mode + "-", dir=visual.REPO / "out")
os.environ["APPDATA"] = profile.name
mod = visual.build("hotbarstate")[0]
user = visual.prepare_user(cfg, "cleric", (1280, 720), "default")
shots = visual.OUT / "shots" / ("vis_quickbarpossess_" + args.mode) / "openwinter"
visual.run_openwinter(cfg, mod, user, shots, 35, "cleric", (1280, 720),
    ["--ui", args.mode, "--capture-hotbar-state", str(shots), "--quickbar-possession-only"],
    engine_args=("--audio-driver", "Dummy"))
profile.cleanup()
