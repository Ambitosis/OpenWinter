"""Capture the hotbar fixture with isolated Godot preferences and sandbox NWN data."""
import argparse
import os
import subprocess
from pathlib import Path
import visual


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("classic", "modern"))
    parser.add_argument("--with-spells", action="store_true", help="Requires the separate player class/META_ANY fix.")
    args = parser.parse_args()
    cfg = visual.config()
    profile = visual.REPO / "out" / ("hotbar-profile-" + args.mode)
    profile.mkdir(parents=True, exist_ok=True)
    os.environ["APPDATA"] = str(profile)
    subprocess.run(["dotnet", "build", str(visual.REPO / "client" / "OpenWinter.Client.csproj"), "-v", "quiet"], cwd=visual.REPO, check=True)
    mod = visual.build("hotbarstate")[0]
    user = visual.prepare_user(cfg, "cleric", (1280, 720), "default")
    variant = "full" if args.with_spells else "items"
    shots = visual.OUT / "shots" / ("vis_hotbarstate_" + args.mode + "_" + variant) / "openwinter"
    extra = ["--ui", args.mode, "--capture-hotbar-state", str(shots)]
    if not args.with_spells:
        extra.append("--hotbar-items-only")
    visual.run_openwinter(cfg, mod, user, shots, 90, "cleric", (1280, 720), extra)


if __name__ == "__main__":
    main()
