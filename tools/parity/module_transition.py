"""Stage an authored destination then run StartNewModule through the sandbox parity harness."""
import argparse
from pathlib import Path
import shutil
import subprocess

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--only", choices=("nwn", "openwinter"))
args = parser.parse_args()
command = ["dotnet", "run", "--project", "src/OpenWinter.Tools", "-c", "Release", "--"]
subprocess.run(command + ["parity-build", "module_destination"], cwd=repo, check=True)
source = repo / "out/parity/build/parity_module_destination.mod"
destination = repo / "out/parity/user/module_transition/modules/parity_module_destination.mod"
destination.parent.mkdir(parents=True, exist_ok=True)
shutil.copyfile(source, destination)
run = command + ["parity-run", "module_transition"]
if args.only:
    run += ["--only", args.only]
subprocess.run(run, cwd=repo, check=True)
