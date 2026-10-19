"""Prove parity ignores foreign user development overrides; all writes stay under out/.

Requires a Release OpenWinter.Tools build and configured original game/compiler.
Run from the repository root: python tools/parity/check_isolation.py
"""
import json
from pathlib import Path
import shutil
import subprocess


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "out" / "parity-isolation-check"
TOOL = ROOT / "src" / "OpenWinter.Tools" / "bin" / "Release" / "net8.0" / "openwinter-tools.dll"


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        stream.write(text)


def run(*args):
    subprocess.run(["dotnet", str(TOOL), *args], cwd=WORK, check=True)


def main():
    with (ROOT / "config" / "local.json").open(encoding="utf-8-sig") as stream:
        cfg = json.load(stream)
    # This is a synthetic foreign user folder, never a real user folder.
    cfg["userRoot"] = str(WORK / "foreign-user")
    campaign = Path(cfg["gameRoot"]) / "data" / "nwm" / "Prelude.nwm"
    cfg["module"] = str(campaign if campaign.exists() else Path(cfg["module"]).resolve())
    cfg["liveUserData"] = False
    write(WORK / "config" / "local.json", json.dumps(cfg, indent=2) + "\n")
    include = WORK / "parity" / "include"
    include.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / "parity" / "include" / "parity_inc.nss", include / "parity_inc.nss")
    for suite, value in [("isolgood", 17), ("isolforeign", 91)]:
        folder = WORK / "parity" / "suites" / suite
        write(folder / "suite.json", '{"seconds":3,"tile":4}\n')
        write(folder / "parity_main.nss", '#include "parity_inc"\n'
              f'void main() {{ PLogI("source",{value}); PDone(); }}\n')
        run("parity-build", suite)
    foreign = WORK / "foreign-user" / "development"
    foreign.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(WORK / "out" / "parity" / "build" / "isolforeign" / "parity_main.ncs",
                    foreign / "parity_main.ncs")
    run("parity-run", "isolgood")
    for engine in ["nwn", "openwinter"]:
        result = WORK / "out" / "parity" / "results" / "isolgood" / f"{engine}.txt"
        assert "source|17" in result.read_text(encoding="utf-8-sig"), result
    print("PASS: both engines ran module source 17, ignoring foreign development source 91")


if __name__ == "__main__":
    main()
