"""A sandboxed NWN:EE server with NWNX and the WSNetLayer plugin, in WSL, for testing the NWN-protocol client.

    python tools/nwnws/nwnws.py setup                 one-time: build packages, NWNX:EE and WSNetLayer in WSL
    python tools/nwnws/nwnws.py start [options]       start the server in the background
        --module <.mod path>    default: Contest Of Champions from the game's data/mod
        --character <name>      local-vault character copied into the server vault (default: the first one)
        --player <name>         the player name the vault folder is for (default: probe)
        --port <n>              UDP and websocket port (default 5121)
    python tools/nwnws/nwnws.py stop
    python tools/nwnws/nwnws.py status                server process and the end of its log
    python tools/nwnws/nwnws.py run-parity ...        a parity suite's module with NWNX (parity-run does this)
    python tools/nwnws/nwnws.py scripts --dest <dir>  NWNX:EE's NWScript headers, for compiling suites

The server runs as the WSL user "nwnws" in /home/nwnws/sandbox (its -userdirectory). It can't open connections
to the outside (iptables drops new outbound traffic from that user except to WSL itself), so it never reaches the
master server: CD keys are not checked (settings.tml: key authentication only "if reachable", never on private
networks) and the server is not listed. Clients on Windows reach it at ws://localhost:<port>. config/local.json
"wslDistro" names the distribution (default Ubuntu-24.04).
"""
import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
NWNX_REPO = "https://github.com/nwnxee/unified.git"
NWNX_COMMIT = "93762f0fad0fa9dc96a77c48fc69bcba118ebb27"  # master for 8193.37.17
WSNET_REPO = "https://github.com/niv/NWNX_WSNetLayer.git"
WSNET_COMMIT = "822e3d5"
LWS_REPO = "https://github.com/warmcat/libwebsockets.git"
LWS_TAG = "v4.3.3"
HOME = "/home/nwnws"
BUILD = "/opt/openwinter-nwnws"

SERVER_SETTINGS = """[masterserver]
\t[masterserver.key-authentication]
\t\tenforce-on-private-networks = false
\t\tmode = "if-reachable"
[server]
\t[server.identity]
\t\tname = "openwinter-nwnws"
\t\tpublic = false
\t[server.login]
\t\tmax-players = 8
\t[server.vault]
\t\tby-player-name = true
\t\tmode = 1
"""


def config():
    cfg = json.loads((REPO / "config" / "local.json").read_text(encoding="utf-8"))
    cfg.setdefault("wslDistro", "Ubuntu-24.04")
    return cfg


def wsl_path(p) -> str:
    p = Path(p).resolve()
    drive = p.drive.rstrip(":").lower()
    return f"/mnt/{drive}" + p.as_posix()[len(p.drive):]


def wsl(cfg, script: str, user: str = "root", check=True, capture=False):
    """Runs a bash script in the distribution as user; the script arrives on stdin (LF line endings)."""
    cmd = ["wsl", "-d", cfg["wslDistro"], "-u", user, "--", "bash", "-se"]
    return subprocess.run(cmd, input=script.replace("\r\n", "\n").encode("utf-8"), check=check,
                          stdout=subprocess.PIPE if capture else None, stderr=subprocess.STDOUT if capture else None)


def setup(cfg, _args):
    script = f"""
export DEBIAN_FRONTEND=noninteractive
apt-get update -q
apt-get install -y -q build-essential cmake git pkg-config zlib1g-dev libzstd-dev iptables
id nwnws >/dev/null 2>&1 || useradd -m -s /bin/bash nwnws
mkdir -p {BUILD}
cd {BUILD}
[ -d unified ] || git clone -q {NWNX_REPO} unified
git -C unified fetch -q origin
git -C unified checkout -q {NWNX_COMMIT}
rm -rf unified/Plugins/WSNetLayer
git clone -q {WSNET_REPO} unified/Plugins/WSNetLayer
git -C unified/Plugins/WSNetLayer checkout -q {WSNET_COMMIT}
# libwebsockets built position-independent (the distribution's static library can't go into a plugin) with
# permessage-deflate; TLS is left to a reverse proxy, as the plugin's README asks.
if [ ! -f lws/lib/cmake/libwebsockets/libwebsocketsConfig.cmake ]; then
  rm -rf lws-src && git clone -q --depth 1 --branch {LWS_TAG} {LWS_REPO} lws-src
  cmake -S lws-src -B lws-build -DCMAKE_INSTALL_PREFIX={BUILD}/lws -DCMAKE_POSITION_INDEPENDENT_CODE=ON     -DLWS_WITH_SSL=OFF -DLWS_WITHOUT_EXTENSIONS=OFF -DLWS_WITH_ZLIB=ON -DLWS_WITH_SHARED=OFF -DLWS_WITH_STATIC=ON     -DLWS_WITHOUT_TESTAPPS=ON -DLWS_WITH_MINIMAL_EXAMPLES=OFF > lws.log 2>&1 || {{ tail -30 lws.log; exit 1; }}
  make -C lws-build -j"$(nproc)" install >> lws.log 2>&1 || {{ grep -n error lws.log | head -20; exit 1; }}
fi
rm -rf build
cmake -S unified -B build -DCMAKE_BUILD_TYPE=RelWithDebInfo -Dlibwebsockets_DIR={BUILD}/lws/lib/cmake/libwebsockets   > cmake.log 2>&1 || {{ tail -40 cmake.log; exit 1; }}
make -C build -j"$(nproc)" > make.log 2>&1 || {{ grep -n -B4 "error" make.log | head -60; exit 1; }}
ls unified/Binaries/NWNX_Core.so unified/Binaries/NWNX_WSNetLayer.so
"""
    wsl(cfg, script)
    print("NWNX:EE and WSNetLayer built in", BUILD)


def find_character(cfg, name):
    vault = Path(cfg["userRoot"]) / "localvault"
    bics = sorted(vault.glob("*.bic"))
    if not bics:
        sys.exit(f"no characters in {vault}")
    if not name:
        return bics[0]
    for b in bics:
        if b.stem.lower() == name.lower():
            return b
    sys.exit(f"no character {name} in {vault}")


def start(cfg, args):
    game = wsl_path(cfg["gameRoot"])
    module = Path(args.module) if args.module else Path(cfg["gameRoot"]) / "data" / "mod" / "Contest Of Champions 0492.mod"
    character = find_character(cfg, args.character)
    port = args.port
    # Stage the module, the character and the settings in a Windows temp folder; the server copies them in.
    stage = Path(tempfile.mkdtemp(prefix="nwnws-"))
    try:
        shutil.copy(module, stage / "module.mod")
        shutil.copy(character, stage / "character.bic")
        (stage / "settings.tml").write_text(SERVER_SETTINGS, encoding="utf-8", newline="\n")
        mod_name = module.stem
        script = f"""
uid=$(id -u nwnws)
# The server user may answer connections but not open them, so the master server is unreachable.
iptables -C OUTPUT -m owner --uid-owner $uid -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT 2>/dev/null || \\
  iptables -A OUTPUT -m owner --uid-owner $uid -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
iptables -C OUTPUT -m owner --uid-owner $uid -o lo -j ACCEPT 2>/dev/null || iptables -A OUTPUT -m owner --uid-owner $uid -o lo -j ACCEPT
iptables -C OUTPUT -m owner --uid-owner $uid -j REJECT 2>/dev/null || iptables -A OUTPUT -m owner --uid-owner $uid -j REJECT
pkill -u nwnws -f nwserver-linux || true
# Let a running server finish shutting down before its folder goes.
for i in $(seq 1 60); do pgrep -u nwnws -f nwserver-linux >/dev/null || break; sleep 0.5; done
pkill -9 -u nwnws -f nwserver-linux || true
rm -rf {HOME}/sandbox
mkdir -p {HOME}/sandbox/modules "{HOME}/sandbox/servervault/{args.player}"
cp "{wsl_path(stage)}/module.mod" "{HOME}/sandbox/modules/{mod_name}.mod"
cp "{wsl_path(stage)}/character.bic" "{HOME}/sandbox/servervault/{args.player}/{character.stem.lower()}.bic"
cp "{wsl_path(stage)}/settings.tml" {HOME}/sandbox/settings.tml
chown -R nwnws:nwnws {HOME}/sandbox
cat > {HOME}/run.sh <<'EOF'
cd "{game}/bin/linux-x86"
export LD_PRELOAD="{BUILD}/unified/Binaries/NWNX_Core.so"
export NWNX_CORE_LOAD_PATH="{BUILD}/unified/Binaries"
export NWNX_CORE_LOG_LEVEL=6
export NWNX_CORE_SKIP_ALL=y
export NWNX_WSNETLAYER_SKIP=n
export NWNX_SERVERLOGREDIRECTOR_SKIP=n
export NWNX_WSNETLAYER_PORT={port}
exec ./nwserver-linux -userdirectory {HOME}/sandbox -module "{mod_name}" -port {port} -publicserver 0 \\
  -servername openwinter-nwnws -maxclients 8 -servervault 1 -elc 0 -ilr 0
EOF
chmod +x {HOME}/run.sh
chown nwnws:nwnws {HOME}/run.sh
rm -f {HOME}/server.log
su nwnws -c "nohup {HOME}/run.sh > {HOME}/server.log 2>&1 &"
# Ready when the module has loaded (the websocket listens from then on).
for i in $(seq 1 120); do
  grep -q "Module loaded" {HOME}/server.log 2>/dev/null && break
  pgrep -u nwnws -f nwserver-linux >/dev/null || {{ tail -30 {HOME}/server.log; exit 1; }}
  sleep 0.5
done
grep -q "Module loaded" {HOME}/server.log && echo "nwserver ready on port {port}" || {{ tail -30 {HOME}/server.log; exit 1; }}
"""
        wsl(cfg, script)
    finally:
        shutil.rmtree(stage, ignore_errors=True)
    print(f"connect with ws://localhost:{port}; player {args.player}, character {character.stem.lower()}")


# The plugins parity suites measure. The rest need outside services, change engine behaviour (Optimizations,
# Experimental) or move the server log (ServerLogRedirector).
PARITY_PLUGINS = ["ADMINISTRATION", "APPEARANCE", "AREA", "CHAT", "CREATURE", "DAMAGE", "DIALOG", "ELC", "EFFECT",
                  "ENCOUNTER", "EVENTS", "FEAT", "FEEDBACK", "ITEM", "ITEMPROPERTY", "MAXLEVEL", "NOSTACK",
                  "NWSQLITEEXTENSIONS", "OBJECT", "PLAYER", "RACE", "RENAME", "RESOURCES", "REVEAL", "SKILLRANKS", "SQL",
                  "STORE", "TILESET", "TWEAKS", "UTIL", "VISIBILITY", "WEAPON"]


def run_parity(cfg, args):
    """Runs a parity module with NWNX until it logs PARITY|__done__|1 (or the time runs out) and copies the server
    log back. Each suite has its own sandbox and port, and only its own server is stopped."""
    game = wsl_path(cfg["gameRoot"])
    binaries = cfg.get("nwnxBinaries") or f"{BUILD}/unified/Binaries"
    name = args.name
    sandbox = f"{HOME}/parity/{name}"
    module = Path(args.module)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    overrides = [Path(p) for p in args.override]
    plugins = PARITY_PLUGINS + (["RISENHOLM"] if args.risenholm else [])
    enabled = "\n".join(f"export NWNX_{p}_SKIP=n" for p in plugins)
    # --collect: copy what the server wrote to its nwnx folder (NWNX_Area exports) back for comparison.
    collect = ""
    if args.collect:
        dest = wsl_path(args.collect)
        collect = f'rm -rf "{dest}"\nmkdir -p "{dest}"\ncp {sandbox}/nwnx/* "{dest}/" 2>/dev/null || true\n'
    script = f"""
uid=$(id -u nwnws)
iptables -C OUTPUT -m owner --uid-owner $uid -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT 2>/dev/null || \\
  iptables -A OUTPUT -m owner --uid-owner $uid -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
iptables -C OUTPUT -m owner --uid-owner $uid -o lo -j ACCEPT 2>/dev/null || iptables -A OUTPUT -m owner --uid-owner $uid -o lo -j ACCEPT
iptables -C OUTPUT -m owner --uid-owner $uid -j REJECT 2>/dev/null || iptables -A OUTPUT -m owner --uid-owner $uid -j REJECT
[ -f {sandbox}/server.pid ] && kill -9 "$(cat {sandbox}/server.pid)" 2>/dev/null || true
rm -rf {sandbox}
mkdir -p {sandbox}/modules {sandbox}/override {sandbox}/database
cp "{wsl_path(module)}" "{sandbox}/modules/{module.stem}.mod"
{"".join(f'cp "{wsl_path(o)}" {sandbox}/override/' + chr(10) for o in overrides)}
cat > {sandbox}/settings.tml <<'EOF'
{SERVER_SETTINGS}EOF
cat > {sandbox}/run.sh <<'EOF'
cd "{game}/bin/linux-x86"
export LD_PRELOAD="{binaries}/NWNX_Core.so"
export NWNX_CORE_LOAD_PATH="{binaries}"
export NWNX_CORE_LOG_LEVEL=6
export NWNX_CORE_SKIP_ALL=y
{enabled}
export NWNX_SQL_TYPE=SQLITE
export NWNX_SQL_DATABASE=nwnx_sql
exec ./nwserver-linux -userdirectory {sandbox} -module "{module.stem}" -port {args.port} -publicserver 0 \\
  -servername openwinter-parity -maxclients 1 -difficulty {args.difficulty}
EOF
chmod +x {sandbox}/run.sh
chown -R nwnws:nwnws {HOME}/parity
su nwnws -c "nohup {sandbox}/run.sh > {sandbox}/stdout.log 2>&1 & echo \\$! > {sandbox}/server.pid"
log={sandbox}/logs.0/nwserverLog1.txt
for i in $(seq 1 {(args.seconds + 60) * 2}); do
  grep -q "PARITY|__done__|1" $log 2>/dev/null && break
  kill -0 "$(cat {sandbox}/server.pid)" 2>/dev/null || break
  sleep 0.5
done
kill "$(cat {sandbox}/server.pid)" 2>/dev/null || true
sleep 1
kill -9 "$(cat {sandbox}/server.pid)" 2>/dev/null || true
cat $log {sandbox}/logs.0/nwserverError1.txt 2>/dev/null > "{wsl_path(out)}" || true
echo "--- stdout" >> "{wsl_path(out)}"
grep -v "^$" {sandbox}/stdout.log >> "{wsl_path(out)}" || true
{collect}"""
    wsl(cfg, script)


def scripts(cfg, args):
    """Copies NWNX:EE's NWScript headers (nwnx_*.nss) from the build into a folder, for compiling suites."""
    dest = Path(args.dest)
    dest.mkdir(parents=True, exist_ok=True)
    source = cfg.get("nwnxSource") or f"{BUILD}/unified"
    wsl(cfg, f'find "{source}" -name "nwnx*.nss" -exec cp {{}} "{wsl_path(dest)}/" \\;')


def stop(cfg, _args):
    wsl(cfg, "pkill -u nwnws -f nwserver-linux && echo stopping || echo 'not running'; "
             "for i in $(seq 1 60); do pgrep -u nwnws -f nwserver-linux >/dev/null || break; sleep 0.5; done")


def status(cfg, _args):
    wsl(cfg, f"pgrep -a -u nwnws -f nwserver-linux || echo 'not running'; tail -25 {HOME}/server.log 2>/dev/null || true")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("setup")
    s = sub.add_parser("start")
    s.add_argument("--module")
    s.add_argument("--character", default="")
    s.add_argument("--player", default="probe")
    s.add_argument("--port", type=int, default=5121)
    sub.add_parser("stop")
    sub.add_parser("status")
    r = sub.add_parser("run-parity")
    r.add_argument("--module", required=True)
    r.add_argument("--name", required=True)
    r.add_argument("--out", required=True)
    r.add_argument("--port", type=int, required=True)
    r.add_argument("--seconds", type=int, default=30)
    r.add_argument("--difficulty", type=int, default=3)
    r.add_argument("--override", action="append", default=[])
    r.add_argument("--risenholm", action="store_true")
    r.add_argument("--collect")
    c = sub.add_parser("scripts")
    c.add_argument("--dest", required=True)
    args = ap.parse_args()
    {"setup": setup, "start": start, "stop": stop, "status": status, "run-parity": run_parity,
     "scripts": scripts}[args.cmd](config(), args)


if __name__ == "__main__":
    main()
