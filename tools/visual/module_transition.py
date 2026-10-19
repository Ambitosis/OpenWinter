"""Run an authored cross-module transfer through the silent DISPLAY2 visual harness."""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import visual

parser = argparse.ArgumentParser()
parser.add_argument("--only", choices=("nwn", "openwinter"), default="nwn")
a = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
command = ["dotnet", "run", "--project", "src/OpenWinter.Tools", "-c", "Release", "--"]
subprocess.run(command + ["visual-build", "moduledest"], cwd=repo, check=True)
destination = repo / "out/visual/user/modules/vis_moduledest.mod"
destination.parent.mkdir(parents=True, exist_ok=True)
shutil.copyfile(repo / "out/visual/build/vis_moduledest.mod", destination)
sys.argv = ["visual.py", "run", "modulesource", "--only", a.only, "--silent"]
visual.main()
