"""Build the Windows release zips: the exported client and the self-contained server.

Uses the version in Directory.Build.props; writes out/dist/OpenWinter-<version>-windows-x64.zip and
OpenWinter-Server-<version>-windows-x64.zip and prints their SHA-256. Needs Godot 4.7 .NET (GODOT, or
the winget install) with its Windows export templates.

    python build/package.py [--suffix dev.42]
"""
import argparse
import glob
import hashlib
import os
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HERE = Path(__file__).parent / "release"
OUT = ROOT / "out"


def godot():
    if os.environ.get("GODOT"):
        return os.environ["GODOT"]
    found = glob.glob(os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages",
                                   "GodotEngine.GodotEngine.Mono_*", "Godot_v4*", "Godot_v4*_mono_win64_console.exe"))
    if not found:
        sys.exit("Godot .NET not found; set GODOT")
    return found[0]


def stage(folder, readme, version):
    shutil.copy(ROOT / "LICENSE", folder)
    shutil.copy(ROOT / "LEGAL.md", folder)
    text = (HERE / readme).read_text(encoding="utf-8").replace("{version}", version)
    (folder / "README.txt").write_text(text.replace("\n", "\r\n"), encoding="utf-8", newline="")


def zip_dir(folder, target):
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in sorted(folder.rglob("*")):
            if path.is_file():
                z.write(path, path.relative_to(folder.parent))
    return hashlib.sha256(target.read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser(description="Build the Windows release zips.")
    ap.add_argument("--suffix", help="pre-release suffix, e.g. dev.42 for a dev build")
    args = ap.parse_args()

    version = re.search(r"<VersionPrefix>([^<]+)</VersionPrefix>",
                        (ROOT / "Directory.Build.props").read_text()).group(1)
    # The exe's Windows file version comes from the export preset; keep it in step with VersionPrefix.
    preset = (ROOT / "client" / "export_presets.cfg").read_text(encoding="utf-8")
    for key in ("file_version", "product_version"):
        if f'application/{key}="{version}.0"' not in preset:
            sys.exit(f'client/export_presets.cfg: set application/{key} to "{version}.0"')
    env = dict(os.environ)
    if args.suffix:
        version += "-" + args.suffix
        env["VersionSuffix"] = args.suffix  # read by MSBuild in both the Godot export and dotnet publish
    client, server, dist = OUT / "release" / "OpenWinter", OUT / "release-server" / "OpenWinter-Server", OUT / "dist"
    for d in (client.parent, server.parent, dist):
        shutil.rmtree(d, ignore_errors=True)
    client.mkdir(parents=True)
    dist.mkdir(parents=True)

    export = subprocess.run([godot(), "--headless", "--path", ".", "--export-release", "Windows",
                             str(client / "OpenWinter.exe")], cwd=ROOT / "client", capture_output=True, text=True,
                            env=env)
    log = (export.stdout + export.stderr).splitlines()
    errors = [l for l in log if l.startswith("ERROR: Export") or "failed" in l]
    if errors or not any(client.glob("data_*")):
        sys.exit("client export failed:\n" + "\n".join(errors or log[-40:]))
    subprocess.run(["dotnet", "publish", str(ROOT / "src" / "OpenWinter.Server"), "-c", "Release", "-r", "win-x64",
                    "--self-contained", "-p:DebugType=none", "-o", str(server)], check=True, env=env)

    stage(client, "client-README.txt", version)
    stage(server, "server-README.txt", version)
    for folder, name in ((client, f"OpenWinter-{version}-windows-x64.zip"),
                         (server, f"OpenWinter-Server-{version}-windows-x64.zip")):
        digest = zip_dir(folder, dist / name)
        print(f"{digest}  {name}  ({(dist / name).stat().st_size / 1e6:.1f} MB)")
    shutil.rmtree(client.parent)
    shutil.rmtree(server.parent)
    # The export imports assets next to their sources; those files are regenerated each time.
    subprocess.run(["git", "clean", "-fq", "--", "client/*.uid", "client/**/*.uid", "client/*.import",
                    "client/**/*.import"], cwd=ROOT, check=True)


if __name__ == "__main__":
    main()
