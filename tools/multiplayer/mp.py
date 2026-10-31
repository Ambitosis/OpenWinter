"""Multiplayer sync harness: one server and two clients on monitor 2, side by side, driven by a scene's scripts.

    python tools/multiplayer/mp.py run mp_sync                    # OpenWinter: openwinter-server + two clients, then compare
    python tools/multiplayer/mp.py run mp_sync --only nwn         # the original: nwserver + two nwmain (holds the nwmain lock)
    python tools/multiplayer/mp.py compare mp_sync                # re-run the comparison on the last OpenWinter run

Scenes are visual-harness scenes (parity/visual/scenes/<name>) with a "multiplayer" block naming the players:
    "multiplayer": { "players": [{ "name": "MPA", "character": "fighter" }, { "name": "MPB", "character": "aluviandarkstar" }] }
Their scripts use parity/visual/include/mp_inc.nss (MPShot checkpoints, MPNote readings, MPDo client input, MPDone).

Output: out/multiplayer/<scene>/openwinter/{server,MPA,MPB}/ (checkpoints.jsonl, samples.jsonl, events.jsonl, notes, shots)
and report.md; out/multiplayer/<scene>/nwn/{server,MPA,MPB}/ for the original (notes, chat, screenshots).
See docs/MULTIPLAYER_HARNESS.md.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / "out" / "multiplayer"
sys.path.insert(0, str(REPO / "tools" / "visual"))
sys.path.insert(0, str(HERE))

import visual  # noqa: E402  (scene building, characters, the monitor, Godot)


def config():
    return json.loads((REPO / "config" / "local.json").read_text())


def scene_json(name):
    return json.loads((REPO / "parity" / "visual" / "scenes" / name / "scene.json").read_text())


def players_of(sj):
    mp = sj.get("multiplayer") or sys.exit("scene has no \"multiplayer\" block")
    return mp["players"]


def layout(cfg, size, count):
    """Desktop positions (left, top) for `count` windows side by side on the dev monitor, each `size` wide."""
    import wincap
    area = visual.dev_display(cfg)
    if not area:
        sys.exit("the multiplayer harness needs \"devDisplay\" in config/local.json (windows never go on the primary monitor)")
    width = (area[2] - area[0]) // count
    if size[0] > width - 16:
        sys.exit(f"{count} windows of {size[0]} px don't fit side by side on {cfg['devDisplay']} ({area[2] - area[0]} px)")
    return [(area[0] + 8 + k * width, area[1] + 8) for k in range(count)]


def tail_until(path, needle, timeout, proc=None):
    """Waits until a log file contains `needle`; False on timeout or when the process ended first."""
    end = time.time() + timeout
    while time.time() < end:
        if path.exists() and needle in path.read_text(encoding="utf-8", errors="replace"):
            return True
        if proc is not None and proc.poll() is not None:
            return False
        time.sleep(0.25)
    return False


def write_config(home, cfg, user, mod):
    """A config/local.json for one process: its own user folder, so writes go to <home>/out/sandbox-userdata."""
    (home / "config").mkdir(parents=True, exist_ok=True)
    user.mkdir(parents=True, exist_ok=True)
    local = {k: v for k, v in cfg.items() if k != "liveUserData"}
    local.update(userRoot=str(user).replace("\\", "/"), module=str(mod).replace("\\", "/"))
    path = home / "config" / "local.json"
    path.write_text(json.dumps(local, indent=2), encoding="utf-8")
    return path


# ---------------------------------------------------------------- OpenWinter

def build(project, configuration):
    r = subprocess.run(["dotnet", "build", str(REPO / project), "-c", configuration, "--nologo", "-v:q"], cwd=REPO,
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode:
        sys.exit(f"build of {project} failed:\n{r.stdout}{r.stderr}")


def run_openwinter(cfg, scene, mod, sj, size, seconds, client_args, silent=False):
    root = OUT / scene / "openwinter"
    if root.exists():
        shutil.rmtree(root)
    players = players_of(sj)
    build("src/OpenWinter.Server/OpenWinter.Server.csproj", "Release")
    build("client/OpenWinter.Client.csproj", "Debug")
    server_exe = REPO / "src" / "OpenWinter.Server" / "bin" / "Release" / "net10.0" / "openwinter-server.exe"
    # A scene can fix the port ("multiplayer": {"port": n}) when its scripts must name the server (mp_portal).
    port = sj.get("multiplayer", {}).get("port") or 5400 + sum(map(ord, str(REPO))) % 400

    # The server: its own sandbox, the players' characters in its server vault (servervault/<player name>/).
    home = root / "server"
    server_cfg = write_config(home, cfg, home / "user", mod)
    vault = home / "out" / "sandbox-userdata" / "servervault"
    for p in players:
        (vault / p["name"]).mkdir(parents=True, exist_ok=True)
        shutil.copy(visual.find_character(p["character"], cfg), vault / p["name"] / (p["character"] + ".bic"))
    settings = {"Name": "openwinter-mp", "Module": str(mod), "Port": port, "MaxPlayers": len(players) + 2, "Pvp": sj.get("multiplayer", {}).get("pvp", "party"),
                "Vault": "server", "VaultByPlayerName": True, "ServeContent": True, "AutoSaveMinutes": 0,
                # One Party Only: nwserver's default is on; a scene can turn it off ("oneParty": false) for both engines.
                "OneParty": bool(sj.get("multiplayer", {}).get("oneParty", True))}
    (home / "server.json").write_text(json.dumps(settings, indent=2), encoding="utf-8")
    env = dict(os.environ, OPENWINTER_CONFIG=str(server_cfg))
    log = home / "server.log"
    procs = []
    with open(log, "w", encoding="utf-8") as server_log:
        server = subprocess.Popen([str(server_exe), "--config", str(home / "server.json"), "--harness", str(home)], cwd=home, env=env,
                                  stdout=server_log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        procs.append(server)
        try:
            if not tail_until(log, "listening on UDP", 120, server):
                print(log.read_text(encoding="utf-8", errors="replace")[-3000:])
                sys.exit("[openwinter] the server did not start")
            print(f"[openwinter] server on UDP {port}")
            import wincap
            ox, oy = wincap.virtual_origin()
            for p, (x, y) in zip(players, layout(cfg, size, len(players))):
                d = root / p["name"]
                client_cfg = write_config(d, cfg, d / "user", mod)
                # APPDATA gives each client its own Godot user:// folder: settings, player key and content cache.
                (d / "appdata").mkdir(parents=True, exist_ok=True)
                cenv = dict(os.environ, OPENWINTER_CONFIG=str(client_cfg), APPDATA=str(d / "appdata"))
                args = [visual.godot(), *(("--audio-driver", "Dummy") if silent else ()), "--path", str(REPO / "client"), "--resolution", f"{size[0]}x{size[1]}", "--position", f"{x - ox},{y - oy}", "--",
                        "--connect", f"127.0.0.1:{port}", "--name", p["name"], "--mp-trace", str(d), "--mp-character", p["character"], *client_args]
                si = subprocess.STARTUPINFO()
                si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                si.wShowWindow = 4  # SW_SHOWNOACTIVATE
                procs.append(subprocess.Popen(args, cwd=REPO / "client", env=cenv, startupinfo=si,
                                              stdout=open(d / "godot.log", "w", encoding="utf-8"), stderr=subprocess.STDOUT))
                print(f"[openwinter] {p['name']} ({p['character']}) at {x},{y}")
                time.sleep(3)
            end = time.time() + seconds + 180
            while time.time() < end and server.poll() is None:
                time.sleep(0.5)
            if server.poll() is None:
                print("[openwinter] time limit reached before the scene finished")
            for c in procs[1:]:
                try:
                    c.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    pass
        finally:
            for c in procs:
                if c.poll() is None:
                    c.kill()
                    c.wait()
    for p in players:
        # Large scratch output: the clients' downloaded module package and Godot caches.
        shutil.rmtree(root / p["name"] / "appdata", ignore_errors=True)
    return root


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=["run", "compare", "build"])
    ap.add_argument("scene")
    ap.add_argument("--only", choices=["openwinter", "nwn"], default="openwinter")
    ap.add_argument("--size", default="944x708", help="each window's client size (two must fit side by side on the dev monitor)")
    ap.add_argument("--client-args", default="", help='extra OpenWinter client arguments, e.g. "--ui classic"')
    ap.add_argument("--probe", action="store_true", help="the original: only start everything and capture both windows every 3 s (login discovery)")
    ap.add_argument("--seconds", type=int, help="time limit (default: the scene's)")
    ap.add_argument("--silent", action="store_true", help="OpenWinter clients use Godot's Dummy audio driver (nothing reaches the speakers)")
    ap.add_argument("--native-framebuffer", action="store_true", help="the original: screenshots with its own PrintScreen (needed when the display sleeps)")
    a = ap.parse_args()
    cfg = config()
    sj = scene_json(a.scene)
    size = tuple(int(x) for x in a.size.split("x"))
    import compare
    if a.command == "compare":
        print(compare.report(OUT / a.scene / "openwinter"))
        return
    mod = visual.build(a.scene)[0]
    if a.command == "build":
        print(mod)
        return
    seconds = a.seconds or int(sj.get("seconds", 120))
    if a.only == "openwinter":
        root = run_openwinter(cfg, a.scene, mod, sj, size, seconds, a.client_args.split(), a.silent)
        print(compare.report(root))
    else:
        import nwn
        nwn.run(cfg, a.scene, mod, sj, size, seconds, a.native_framebuffer, a.probe)


if __name__ == "__main__":
    main()
