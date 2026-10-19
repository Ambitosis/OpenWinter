"""Run an authored embedded-content transfer through the silent DISPLAY2 visual harness."""
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
subprocess.run(command + ["visual-build", "modcontentb"], cwd=repo, check=True)
destination = repo / "out/visual/user/modules/vis_modcontentb.mod"
destination.parent.mkdir(parents=True, exist_ok=True)
shutil.copyfile(repo / "out/visual/build/vis_modcontentb.mod", destination)
sys.argv = ["visual.py", "run", "modcontenta", "--only", a.only, "--silent"]
visual.main()
