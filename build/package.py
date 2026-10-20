"""Build the release archives: the exported Windows client and the self-contained Windows and Linux servers.

Uses the version in Directory.Build.props; writes out/dist/OpenWinter-<version>-windows-x64.zip,
OpenWinter-Server-<version>-windows-x64.zip and OpenWinter-Server-<version>-linux-x64.tar.gz and prints
their SHA-256. Needs Godot 4.7 .NET (GODOT, or the winget install) with its Windows export templates.

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
import tarfile
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


def stage(folder, readme, version, newline="\r\n"):
    shutil.copy(ROOT / "LICENSE", folder)
    shutil.copy(ROOT / "LEGAL.md", folder)
    text = (HERE / readme).read_text(encoding="utf-8").replace("{version}", version)
    (folder / "README.txt").write_text(text.replace("\n", newline), encoding="utf-8", newline="")


def zip_dir(folder, target):
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in sorted(folder.rglob("*")):
            if path.is_file():
                z.write(path, path.relative_to(folder.parent))
    return hashlib.sha256(target.read_bytes()).hexdigest()


def tar_dir(folder, target, executable):
    def mode(info):
        info.uid = info.gid = 0
        info.uname = info.gname = ""
        info.mode = 0o755 if info.isdir() or Path(info.name).name == executable else 0o644
        return info
    with tarfile.open(target, "w:gz", compresslevel=9) as t:
        t.add(folder, folder.name, filter=mode)
    return hashlib.sha256(target.read_bytes()).hexdigest()


def publish_server(rid, folder, env):
    subprocess.run(["dotnet", "publish", str(ROOT / "src" / "OpenWinter.Server"), "-c", "Release", "-r", rid,
                    "--self-contained", "-p:DebugType=none", "-o", str(folder)], check=True, env=env)


def main():
    ap = argparse.ArgumentParser(description="Build the release archives.")
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
    client, dist = OUT / "release" / "OpenWinter", OUT / "dist"
    server, linux = OUT / "release-server" / "OpenWinter-Server", OUT / "release-server-linux" / "OpenWinter-Server"
    for d in (client.parent, server.parent, linux.parent, dist):
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
    publish_server("win-x64", server, env)
    publish_server("linux-x64", linux, env)

    stage(client, "client-README.txt", version)
    stage(server, "server-README.txt", version)
    stage(linux, "server-README-linux.txt", version, "\n")
    for folder, name in ((client, f"OpenWinter-{version}-windows-x64.zip"),
                         (server, f"OpenWinter-Server-{version}-windows-x64.zip")):
        digest = zip_dir(folder, dist / name)
        print(f"{digest}  {name}  ({(dist / name).stat().st_size / 1e6:.1f} MB)")
    name = f"OpenWinter-Server-{version}-linux-x64.tar.gz"
    digest = tar_dir(linux, dist / name, "openwinter-server")
    print(f"{digest}  {name}  ({(dist / name).stat().st_size / 1e6:.1f} MB)")
    for d in (client.parent, server.parent, linux.parent):
        shutil.rmtree(d)
    # The export imports assets next to their sources; those files are regenerated each time.
    subprocess.run(["git", "clean", "-fq", "--", "client/*.uid", "client/**/*.uid", "client/*.import",
                    "client/**/*.import"], cwd=ROOT, check=True)


if __name__ == "__main__":
    main()
