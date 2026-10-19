"""The original side of the multiplayer harness: a sandboxed nwserver and two nwmain clients joined to it.

Called by mp.py (`run <scene> --only nwn`). Every user folder is under out/multiplayer/<scene>/nwn/ (never the real
one); the windows go side by side on the dev monitor without taking focus; the nwmain lock is held for the whole run.
The server's script log drives it: each VISUAL|shot takes both clients' screenshots, VISUAL|note lines are kept and
VISUAL|done ends the run. VISUAL|do (OpenWinter client input) is ignored here. Chat windows are logged
([game.log.chat.all]) and written to <player>/chat.txt. See docs/MULTIPLAYER_HARNESS.md.
"""
import hashlib
import io
import json
import os
import shutil
import subprocess
import sys
import time
from contextlib import contextmanager
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / "out" / "multiplayer"
import visual  # noqa: E402
import wincap  # noqa: E402

LOCK = visual.nwmain_lock_path()

# The original client's settings for each sandbox: windowed at the harness size and place, no movies, no pause when
# the window is in the background, the whole chat window copied into its log.
CLIENT_SETTINGS = """[client]
	[client.identity]
		name = "{name}"
[game]
	[game.auto-pause]
		[game.auto-pause.on-focus-loss]
			enabled = false
	[game.log]
		[game.log.chat]
			[game.log.chat.all]
				enabled = true
[graphics]
	[graphics.intro]
		[graphics.intro.splash]
			enabled = false
	[graphics.movies]
		enabled = false
		[graphics.movies.intro]
			enabled = false
	[graphics.window]
		mode = "windowed"
		[graphics.window.windowed]
			borderless = false
			height = {h}
			width = {w}
			[graphics.window.windowed.position]
				mode = "remember"
				x = {x}
				y = {y}
[ui]
	[ui.multiplayer]
		[ui.multiplayer.direct-connect]
			last-address = "{address}"
[sound]
	[sound.effects]
		volume = 0.0
	[sound.music]
		volume = 0.0
	[sound.voices]
		volume = 0.0
	[sound.streams]
		volume = 0.0
"""

# The server: characters by player name (servervault/<player name>/), no master server needed on the local network.
# The DM password the sandboxed nwserver takes when a scene has a "dm": true player.
DM_PASSWORD = "dmpass"

SERVER_SETTINGS = """[masterserver]
	[masterserver.key-authentication]
		enforce-on-private-networks = false
		mode = "if-reachable"
[server]
	[server.identity]
		name = "openwinter-mp"
		public = false
	[server.login]
		max-players = 4
	[server.vault]
		by-player-name = true
		mode = 1
"""


def hidden_window():
    """nwserver.exe is a GUI program: CREATE_NO_WINDOW does nothing for it, so ask for its window hidden."""
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 0  # SW_HIDE
    return si


@contextmanager
def nwmain_lock(owner):
    """The shared lock every harness holds while the original client runs (waits for it to be free)."""
    waited = 0
    while LOCK.exists():
        if waited % 60 == 0:
            print(f"[nwn] waiting for {LOCK} ({LOCK.read_text(errors='replace').strip()})")
        time.sleep(5)
        waited += 5
    LOCK.write_text(owner)
    try:
        yield
    finally:
        try:
            LOCK.unlink()
        except FileNotFoundError:
            pass


def branch():
    r = subprocess.run(["git", "-C", str(REPO), "branch", "--show-current"], capture_output=True, text=True)
    return r.stdout.strip() or "multiplayer-harness"


def native_shot(hwnd, user, folder, name):
    """The game's own screenshot (PrintScreen posted to its window; works with the display asleep), as visual.py's
    --native-framebuffer, saved as <name>.png beside a <name>_window.png window capture."""
    from PIL import Image
    shots = (user / "screenshots")
    shots.mkdir(parents=True, exist_ok=True)
    before = {p.name: p.stat().st_mtime_ns for p in shots.glob("*.tga")}
    try:
        wincap.capture(hwnd).save(folder / (name + "_window.png"))
    except Exception as e:  # a minimised or vanished window
        print(f"[nwn] window capture {name} failed: {e}")
    scan = (0x37 << 16) | (1 << 24)
    wincap.user32.PostMessageW(hwnd, 0x0100, 44, 1 | scan)
    time.sleep(0.05)
    wincap.user32.PostMessageW(hwnd, 0x0101, 44, 1 | scan | (3 << 30))
    began = time.time()
    while time.time() - began < 3.0:
        saved = [p for p in shots.glob("*.tga") if before.get(p.name) != p.stat().st_mtime_ns]
        if saved:
            try:
                raw = saved[0].read_bytes()
                with Image.open(io.BytesIO(raw)) as img:
                    frame = img.convert("RGB")
                    frame.load()
            except (OSError, ValueError):
                time.sleep(0.05)
                continue
            frame.save(folder / (name + ".png"))
            saved[0].unlink()
            return True
        time.sleep(0.05)
    print(f"[nwn] no native screenshot for {name}")
    return False


def prepare_client(cfg, folder, size, pos, player, address):
    user = folder / "user"
    (user / "logs").mkdir(parents=True, exist_ok=True)
    # [client.identity] name is the player name (the profile's "Player Name"): servervault/<player name>/ on the server.
    (user / "settings.tml").write_text(CLIENT_SETTINGS.format(w=size[0], h=size[1], x=pos[0], y=pos[1], name=player["name"], address=address), encoding="utf-8", newline="")
    log = user / "logs" / "nwclientLog1.txt"
    if log.exists():
        log.unlink()
    return user


def read_new(path, pos):
    """Whole new lines of a log file from byte `pos`; returns (lines, new pos)."""
    if not path.exists():
        return [], pos
    with open(path, "rb") as f:
        f.seek(pos)
        chunk = f.read()
    cut = chunk.rfind(b"\n") + 1
    return chunk[:cut].decode("latin-1").splitlines(), pos + cut


# Where the login clicks land, as fractions of the client area (944x708 windows; the menus are centred).
MULTIPLAYER_BUTTON = (0.499, 0.596)    # main menu: Multiplayer
DIRECT_CONNECT_BUTTON = (0.499, 0.740) # multiplayer menu: Direct Connect (Enter then presses its OK)
PLAY_BUTTON = (0.608, 0.826)           # character selection: Play (the player's server-vault character is selected)


def login(c):
    """Joins the server through the menus: Multiplayer, Direct Connect (address prefilled), OK, Play. Window
    captures of each step go to <player>/login_*.png."""
    h = wincap.main_window(c["proc"].pid) or c["hwnd"]
    steps = [("menu", lambda: real_click(h, *MULTIPLAYER_BUTTON), 2.0),
             ("direct", lambda: real_click(h, *DIRECT_CONNECT_BUTTON), 2.0),
             ("connect", lambda: wincap.key(h, 13), 8.0),
             ("play", lambda: real_click(h, *PLAY_BUTTON), 10.0)]
    for name, act, wait in steps:
        act()
        time.sleep(wait)
        try:
            wincap.capture(h).save(c["folder"] / f"login_{name}.png")
        except Exception:
            pass
    log = c["user"] / "logs" / "nwclientLog1.txt"
    joined = log.exists() and "has joined as a player" in log.read_text(encoding="latin-1", errors="replace")
    print(f"[nwn] {c['player']['name']} " + ("is in the game" if joined else
          f"did not get in (see {c['folder'].name}/login_play.png: a second client on the same CD key is refused)"))
    return joined


def run(cfg, scene, mod, sj, size, seconds, native_framebuffer, probe=False):
    import mp
    players = mp.players_of(sj)
    root = OUT / scene / "nwn"
    if root.exists():
        shutil.rmtree(root)
    exe_dir = Path(cfg["gameRoot"]) / "bin" / "win32"
    # A scene can fix the port ("multiplayer": {"port": n}) when its scripts must name the server (mp_portal).
    port = sj.get("multiplayer", {}).get("port") or 5800 + sum(map(ord, str(REPO))) % 150
    address = f"127.0.0.1:{port}"
    positions = mp.layout(cfg, size, len(players))
    # "nwnClientKeys": {"MPB": "<path of an nwncdkey.ini>"} in config/local.json gives that client its own licence
    # (copied into its sandbox; it then runs without Steam). Without one every client uses the Steam account's key,
    # and the server refuses the second: "Your CD Key is already in use on that server".
    keys = cfg.get("nwnClientKeys", {})

    server_user = root / "server" / "user"
    (server_user / "modules").mkdir(parents=True, exist_ok=True)
    shutil.copy(mod, server_user / "modules" / mod.name)
    for p in players:
        vault = server_user / "servervault" / p["name"]
        vault.mkdir(parents=True, exist_ok=True)
        shutil.copy(visual.find_character(p["character"], cfg), vault / (p["character"] + ".bic"))
    settings = SERVER_SETTINGS
    # One Party Only is on by default on nwserver (server.one-party-only); a scene can turn it off ("oneParty": false).
    if not sj.get("multiplayer", {}).get("oneParty", True):
        settings = settings.replace("[server]\n", "[server]\n\tone-party-only = false\n", 1)
    (server_user / "settings.tml").write_text(settings, encoding="utf-8", newline="")
    server_log = server_user / "logs" / "nwserverLog1.txt"

    procs = []
    clients = []
    with nwmain_lock(branch()):
        try:
            server = subprocess.Popen([str(exe_dir / "nwserver.exe"), "-userdirectory", str(server_user), "-module", mod.stem, "-port", str(port),
                                       "-publicserver", "0", "-servername", "openwinter-mp", "-maxclients", str(len(players) + 2), "-servervault", "1",
                                       "-difficulty", "3"] + (["-dmpassword", DM_PASSWORD] if any(p.get("dm") for p in players) else []),
                                      cwd=exe_dir, stdout=open(root / "server" / "stdout.txt", "w"), stderr=subprocess.STDOUT,
                                      creationflags=subprocess.CREATE_NO_WINDOW, startupinfo=hidden_window())
            procs.append(server)
            time.sleep(8)
            print(f"[nwn] nwserver on UDP {port}")
            for p, pos in zip(players, positions):
                folder = root / p["name"]
                user = prepare_client(cfg, folder, size, pos, p, address)
                if p.get("dm"):
                    # The DM client lists the characters in its own dmvault.
                    (user / "dmvault").mkdir(exist_ok=True)
                    shutil.copy(visual.find_character(p["character"], cfg), user / "dmvault" / (p["character"] + ".bic"))
                si = subprocess.STARTUPINFO()
                si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                si.wShowWindow = 7  # SW_SHOWMINNOACTIVE: never takes focus; restored without activation below
                if p["name"] in keys:
                    shutil.copy(keys[p["name"]], user / "nwncdkey.ini")
                    env = dict(os.environ)
                else:
                    # Started outside Steam the client has no CD key ("Steam API not connected", then a failed GOG
                    # sign-in) and asks for one. SteamAppId attaches it to the running Steam client (NWN:EE is 704450).
                    env = dict(os.environ, SteamAppId="704450", SteamGameId="704450")
                # +connect is accepted but always answers "Could not translate address", so login() uses the menus.
                # "dm": true starts the client as the DM client (-dmc); the server then takes DMs with DM_PASSWORD.
                proc = subprocess.Popen([str(exe_dir / "nwmain.exe"), "-userdirectory", str(user)] + (["-dmc"] if p.get("dm") else []), cwd=exe_dir, startupinfo=si, env=env)
                procs.append(proc)
                hwnd = wincap.wait_window(proc.pid, 90)
                if not hwnd:
                    print(f"[nwn] {p['name']}: no window")
                    clients.append(None)
                    continue
                wincap.user32.ShowWindow(hwnd, 4)  # SW_SHOWNOACTIVATE
                wincap.move_to(hwnd, pos[0], pos[1])
                clients.append({"player": p, "proc": proc, "hwnd": hwnd, "user": user, "folder": folder})
                print(f"[nwn] {p['name']} window at {pos}")
                time.sleep(6)
            if os.environ.get("MP_NWN_INTERACTIVE"):
                interactive(root, clients, seconds)
                return
            if probe:
                # Login discovery: a window capture of each client every few seconds.
                for k in range(int(seconds / 3)):
                    time.sleep(3)
                    for c in clients:
                        if c and c["proc"].poll() is None:
                            h = wincap.main_window(c["proc"].pid) or c["hwnd"]
                            native_shot(h, c["user"], c["folder"], f"probe_{k:02d}") if native_framebuffer else wincap.capture(h).save(c["folder"] / f"probe_{k:02d}.png")
                    print(f"[nwn] probe {k}: " + ", ".join("gone" if not c or c["proc"].poll() is not None else "running" for c in clients))
                return
            for c in clients:
                if c:
                    login(c)
            watch(root, server, server_log, clients, seconds, native_framebuffer)
        finally:
            for c in clients:
                if c and c["proc"].poll() is None:
                    # A polite close first, so the client writes its settings and logs out of the server.
                    wincap.user32.PostMessageW(c["hwnd"], 0x0010, 0, 0)  # WM_CLOSE
            time.sleep(3)
            for proc in procs:
                if proc.poll() is None:
                    proc.kill()
                    proc.wait()
            for c in clients:
                if c:
                    log = c["user"] / "logs" / "nwclientLog1.txt"
                    if log.exists():
                        chat = [l for l in log.read_text(encoding="latin-1").splitlines() if "[CHAT WINDOW TEXT]" in l]
                        (c["folder"] / "chat.txt").write_text("\n".join(chat) + "\n", encoding="utf-8", newline="")
            if server_log.exists():
                notes = [l[l.index("VISUAL|"):] for l in server_log.read_text(encoding="latin-1").splitlines() if "VISUAL|" in l]
                (root / "server" / "notes.txt").write_text("\n".join(notes) + "\n", encoding="utf-8", newline="")


def client_point(hwnd, fx, fy):
    r = wincap.wt.RECT()
    wincap.user32.GetClientRect(hwnd, wincap.ctypes.byref(r))
    return int(r.right * fx), int(r.bottom * fy)


def real_click(hwnd, fx, fy, right=False):
    """A left click at a fraction of the client area. The original reads the real pointer's place (a posted click's
    own coordinates are ignored), so the pointer is parked there for the click (dev monitor only) and
    the button is posted to the window, which needs no focus; the pointer goes straight back."""
    point = wincap.wt.POINT()
    wincap.user32.GetCursorPos(wincap.ctypes.byref(point))
    x, y = client_point(hwnd, fx, fy)
    screen = wincap.wt.POINT(x, y)
    wincap.user32.ClientToScreen.argtypes = [wincap.wt.HWND, wincap.ctypes.POINTER(wincap.wt.POINT)]
    wincap.user32.ClientToScreen(hwnd, wincap.ctypes.byref(screen))
    area = visual.dev_display(json.loads((REPO / "config" / "local.json").read_text()))
    if not area or not (area[0] <= screen.x < area[2] and area[1] <= screen.y < area[3]):
        raise RuntimeError(f"refusing a real-pointer click outside the dev monitor at {screen.x},{screen.y}")
    lp = (y << 16) | (x & 0xFFFF)
    try:
        wincap.user32.SetCursorPos(screen.x, screen.y)
        time.sleep(0.25)
        wincap.user32.PostMessageW(hwnd, 0x0200, 0, lp)
        time.sleep(0.1)
        wincap.user32.PostMessageW(hwnd, 0x0204 if right else 0x0201, 2 if right else 1, lp)
        time.sleep(0.1)
        wincap.user32.PostMessageW(hwnd, 0x0205 if right else 0x0202, 0, lp)
        time.sleep(0.25)
    finally:
        wincap.user32.SetCursorPos(point.x, point.y)


def interactive(root, clients, seconds):
    """Development aid for login discovery (MP_NWN_INTERACTIVE=1): runs commands appended to <root>/cmd.txt —
    "<player> click fx,fy", "<player> key vk", "<player> text abc", "<player> shot name", "quit" — and takes a window
    capture of that client after each (with MP_NWN_INTERACTIVE=1 the run stops after starting the clients)."""
    cmd = root / "cmd.txt"
    cmd.write_text("")
    done, pos, n, start = False, 0, 0, time.time()
    by_name = {c["player"]["name"]: c for c in clients if c}
    while not done and time.time() - start < seconds:
        time.sleep(0.3)
        lines, pos = read_new(cmd, pos)
        for line in lines:
            parts = line.strip().split(" ", 2)
            if parts[0] == "quit":
                done = True
                break
            c = by_name.get(parts[0])
            if not c or len(parts) < 2:
                continue
            h = wincap.main_window(c["proc"].pid) or c["hwnd"]
            arg = parts[2] if len(parts) > 2 else ""
            if parts[1] == "click":
                real_click(h, *(float(v) for v in arg.split(",")))
            elif parts[1] == "login":
                login(c)
            elif parts[1] == "rclick":
                real_click(h, *(float(v) for v in arg.split(",")), right=True)
            elif parts[1] == "key":
                wincap.key(h, int(arg))
            elif parts[1] == "text":
                for ch in arg:
                    wincap.user32.PostMessageW(h, wincap.WM_CHAR, ord(ch), 1)
                    time.sleep(0.03)
            time.sleep(1.5)
            n += 1
            name = arg if parts[1] in ("shot", "nshot") else f"step_{n:02d}"
            if parts[1] == "nshot":
                native_shot(h, c["user"], c["folder"], name)
            else:
                wincap.capture(h).save(c["folder"] / (name + ".png"))
            print(f"[nwn] {line.strip()} -> {c['player']['name']}/{name}.png", flush=True)


def watch(root, server, server_log, clients, seconds, native_framebuffer):
    """Follows the server's script log: screenshots at each checkpoint until VISUAL|done or the time limit."""
    pos, start, done = 0, time.time(), False
    while not done and time.time() - start < seconds + 60 and server.poll() is None:
        time.sleep(0.1)
        lines, pos = read_new(server_log, pos)
        for line in lines:
            at = line.find("VISUAL|")
            if at < 0:
                continue
            parts = line[at + 7:].strip().split("|")
            if parts[0] == "shot" and len(parts) > 1:
                for c in clients:
                    if c and c["proc"].poll() is None:
                        h = wincap.main_window(c["proc"].pid) or c["hwnd"]
                        if native_framebuffer:
                            native_shot(h, c["user"], c["folder"], parts[1])
                        else:
                            wincap.capture(h).save(c["folder"] / (parts[1] + ".png"))
                print(f"[nwn] checkpoint {parts[1]}")
            elif parts[0] in ("click", "key", "text") and len(parts) > 2:
                # VISUAL|click|<player>|fx,fy (a real-pointer click, dev monitor only), VISUAL|key|<player>|<vk>,
                # VISUAL|text|<player>|<characters>: input for one original client, from the scene's own timeline.
                c = next((c for c in clients if c and c["player"]["name"] == parts[1] and c["proc"].poll() is None), None)
                if c:
                    h = wincap.main_window(c["proc"].pid) or c["hwnd"]
                    if parts[0] == "click":
                        real_click(h, *(float(v) for v in parts[2].split(",")))
                    elif parts[0] == "key":
                        wincap.key(h, int(parts[2]))
                    else:
                        for ch in "|".join(parts[2:]):
                            wincap.user32.PostMessageW(h, wincap.WM_CHAR, ord(ch), 1)
                            time.sleep(0.03)
            elif parts[0] == "done":
                done = True
    if not done:
        print("[nwn] time limit reached before VISUAL|done")
