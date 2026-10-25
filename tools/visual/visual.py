"""Visual parity harness: the same scripted scene on the original NWN:EE client and on OpenWinter, side by side.

    python tools/visual/visual.py run calib interior_lit      # build, capture both, compare
    python tools/visual/visual.py run calib --only nwn        # one engine
    python tools/visual/visual.py compare calib               # re-pair existing shots
    python tools/visual/visual.py run all                     # every scene under parity/visual/scenes

See parity/README.md ("Visual"). Shots go to out/visual/shots/<module>/{nwn,openwinter}/<shot>.png and the
pairs to out/visual/compare/<module>__<shot>.png, with out/visual/compare/index.html listing them all.
"""
import argparse
import hashlib
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / "out" / "visual"
sys.path.insert(0, str(HERE))


def config():
    return json.loads((REPO / "config" / "local.json").read_text())


def nwmain_lock_path():
    """The lock every harness holds while the original client runs: "nwmainLock" in config/local.json, else
    <temp>/openwinter-nwmain.lock."""
    try:
        path = config().get("nwmainLock")
    except (OSError, ValueError):
        path = None
    return Path(path) if path else Path(tempfile.gettempdir()) / "openwinter-nwmain.lock"


def scenes():
    return sorted(p.name for p in (REPO / "parity" / "visual" / "scenes").iterdir() if (p / "scene.json").exists())


def scene_json(name):
    return json.loads((REPO / "parity" / "visual" / "scenes" / name / "scene.json").read_text())


def build(name):
    """Builds a scene's module(s) with OpenWinter.Tools; returns their paths."""
    r = subprocess.run(["dotnet", "run", "--project", str(REPO / "src" / "OpenWinter.Tools"), "-c", "Release", "--", "visual-build", name],
                       cwd=REPO, capture_output=True, text=True)
    mods = [Path(l.strip()) for l in r.stdout.splitlines() if l.strip().endswith(".mod")]
    if r.returncode != 0 or not mods:
        sys.exit(f"visual-build {name} failed:\n{r.stdout}\n{r.stderr}")
    return mods


# ---------------------------------------------------------------- sandbox user folder

# Settings for the original client: windowed at the capture size, no intro movies, no pause in the background.
# "plain" also turns off the post-processing (bloom, SSAO, contrast, vibrance, sharpen) for raw lighting checks.
SETTINGS = """[game]
	[game.auto-pause]
		[game.auto-pause.on-focus-loss]
			enabled = false
{ui}[graphics]
{fbo}	[graphics.intro]
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
"""
PLAIN_FBO = """	[graphics.fbo]
		[graphics.fbo.hdr-bloom]
			enabled = false
		[graphics.fbo.high-contrast]
			enabled = false
		[graphics.fbo.sharpen]
			enabled = false
		[graphics.fbo.ssao]
			enabled = false
		[graphics.fbo.vibrance]
			enabled = false
"""


def find_character(name, cfg):
    """A .bic by name: the game's pregenerated characters, then the user's local vault (read only)."""
    for d in [Path(cfg["gameRoot"]) / "data" / "lcv", Path(cfg["userRoot"]) / "localvault"]:
        p = d / (name + ".bic")
        if p.exists():
            return p
    sys.exit(f"character {name}.bic not found in data/lcv or the local vault")


def dev_display(cfg):
    """Where windows go: the work area of config/local.json's "devDisplay" monitor (keeps the primary free for
    the user), else None (the primary)."""
    import wincap
    name = cfg.get("devDisplay")
    return wincap.monitor_work_area(name) if name else None


def keyhole_toml(spec):
    """The original's [graphics.keyholing] settings from "key=value,..." (e.g. "enabled=false,min-diameter=0.5"),
    for measuring its see-through hole; empty keeps the client's defaults."""
    if not spec:
        return ""
    lines = ["\t[graphics.keyholing]"]
    for kv in spec.split(","):
        k, v = kv.split("=", 1)
        lines.append(f"\t\t{k.strip()} = {v.strip()}")
    return "\n".join(lines) + "\n"


def prepare_user(cfg, character, size, profile, keyhole="", text_bubble_mode=None, large_font=None, ui_scale=None, software_cursor=False, chat_log=False, chat_split=None, difficulty=None, server_settings=None):
    """The sandbox user folder both engines read: out/visual/user. Never the real one."""
    user = OUT / "user"
    (user / "modules").mkdir(parents=True, exist_ok=True)
    # Scene haks (test models a scene ships, see visual-build) for both engines.
    (user / "hak").mkdir(parents=True, exist_ok=True)
    for hak in (OUT / "build").glob("vis_*.hak"):
        shutil.copy(hak, user / "hak" / hak.name)
    vault = user / "localvault"
    if vault.exists():
        shutil.rmtree(vault)
    vault.mkdir()
    # Keep the requested vault character isolated. TestNewModule also considers installed
    # pregenerated characters; authored characterFixture controls use a prefixed copy below.
    shutil.copy(find_character(character, cfg), vault / (character + ".bic"))
    settings = user / "settings.tml"
    text = settings.read_text() if settings.exists() else ""
    # The client rewrites settings.tml on exit with every default filled in; ours go first and win only if
    # the file is fresh, so rewrite it each run.
    area = dev_display(cfg)
    x, y = (area[0] + 20, area[1] + 20) if area else (40, 40)
    # Interface settings only when a scene or the command line asks; otherwise the original keeps its defaults.
    ui = [f"\t{k} = {v}\n" for k, v in (("text-bubble-mode", text_bubble_mode),
                                         ("large-font", None if large_font is None else str(bool(large_font)).lower()),
                                         ("scale", None if ui_scale is None else float(ui_scale))) if v is not None]
    settings.write_text(SETTINGS.format(ui="[ui]\n" + "".join(ui) if ui else "", w=size[0], h=size[1], x=x, y=y, fbo=(PLAIN_FBO if profile == "plain" else "") + keyhole_toml(keyhole)))
    if software_cursor:
        with open(settings, "a", encoding="utf-8", newline="") as f:
            f.write("\n[input.mouse]\n\thardware-pointer = false\n")
    if difficulty is not None:
        # The game difficulty (GAME_DIFFICULTY_*; the original's settings offer 1 to 4 and default to 1).
        with open(settings, "a", encoding="utf-8", newline="") as f:
            f.write(f"\n[ruleset]\n\tdifficulty = {int(difficulty)}\n")
    if chat_log:
        # The original copies its whole chat window into nwclientLog1.txt ("[CHAT WINDOW TEXT]" lines).
        with open(settings, "a", encoding="utf-8", newline="") as f:
            f.write("\n[game.log.chat.all]\n\tenabled = true\n")
    if server_settings:
        # "serverSettings": {"player-party-control": true, ...}: the original's [server] settings, which its in-process
        # server reads in single player too.
        with open(settings, "a", encoding="utf-8", newline="") as f:
            f.write("\n[server]\n" + "".join(f"\t{k} = {json.dumps(v)}\n" for k, v in server_settings.items()))
    # "chatSplit": [primary, secondary] writes the original's saved chat heights (ChatUIPrimaryHeight /
    # ChatUISecondaryHeight; the client copies them into settings.tml ui.chat.pane.*.height). They don't split its
    # window (ChatUISplitScreen is a deprecated key): it starts as one window, 8 lines tall for [2, 6] and 14 for [3, 1].
    ini = user / "nwnplayer.ini"
    kept = [l for l in (ini.read_text(encoding="latin-1").splitlines() if ini.exists() else [])
            if not l.startswith(("ChatUISplitScreen=", "ChatUIPrimaryHeight=", "ChatUISecondaryHeight=")) and l != "[Control Options]"]
    if chat_split:
        kept += ["[Control Options]", "ChatUISplitScreen=1", f"ChatUIPrimaryHeight={chat_split[0]:.2f}", f"ChatUISecondaryHeight={chat_split[1]:.2f}"]
    ini.write_text("\n".join(kept) + "\n", encoding="latin-1", newline="")
    return user


# ---------------------------------------------------------------- the original client

def verify_character_fixture(mod, shots):
    metadata = mod.with_suffix(".character.json")
    if not metadata.exists():
        return
    expected = json.loads(metadata.read_text(encoding="utf-8"))["expectedIdentity"]
    # Client setup can log identity before the capture loop opens notes.txt. Its
    # complete runtime log retains that earlier marker; do not mistake it for a wrong PC.
    lines = []
    for name in ("notes.txt", "godot.log"):
        path = shots / name
        if path.exists():
            lines.extend(line[line.index("VISUAL|"):] for line in path.read_text(encoding="utf-8", errors="replace").splitlines() if "VISUAL|" in line)
    if expected not in lines:
        raise RuntimeError(f"Character fixture identity not proved in {shots}: expected {expected}")


def save_native_framebuffer(hwnd, user, shots, name):
    """Public in-game screenshot, posted to the owned HWND; retain raw TGA and timing evidence."""
    import wincap
    from PIL import Image
    native_dir = (user / "screenshots").resolve()
    if not native_dir.is_relative_to(OUT.resolve()) or Path(name).name != name:
        raise RuntimeError("native screenshot path escaped visual sandbox")
    native_dir.mkdir(parents=True, exist_ok=True)
    before = {p.name: p.stat().st_mtime_ns for p in native_dir.glob("*.tga")}
    window_path = shots / (name + "_window.png")
    wincap.capture(hwnd).save(window_path)
    began = time.perf_counter()
    requested = time.time()
    # Microsoft's keyboard scan table specifies PrintScreen E0:37. MapVirtualKey
    # VK_SNAPSHOT returns SysRq54 on this machine, which produces no screenshot.
    scan = (0x37 << 16) | (1 << 24)
    wincap.user32.PostMessageW(hwnd, 0x0100, 44, 1 | scan)
    time.sleep(0.05)
    wincap.user32.PostMessageW(hwnd, 0x0101, 44, 1 | scan | (3 << 30))
    while time.perf_counter() - began < 2.0:
        saved = [p for p in native_dir.glob("*.tga") if before.get(p.name) != p.stat().st_mtime_ns]
        if len(saved) > 1:
            raise RuntimeError(f"ambiguous native screenshot files for {name}: {saved}")
        if saved:
            try:
                raw_bytes = saved[0].read_bytes()
                with Image.open(io.BytesIO(raw_bytes)) as source:
                    frame = source.convert("RGB")
                    frame.load()
            except (OSError, ValueError):
                time.sleep(0.025)  # The game may still be writing this new file.
                continue
            raw = shots / (name + ".tga")
            raw.write_bytes(raw_bytes)
            frame.save(shots / (name + ".png"))
            record = {"shot": name, "source": saved[0].name, "requestedUnixSeconds": requested,
                      "latencySeconds": time.perf_counter() - began, "size": list(frame.size),
                      "rawSha256": hashlib.sha256(raw.read_bytes()).hexdigest(),
                      "windowSha256": hashlib.sha256(window_path.read_bytes()).hexdigest(),
                      "nonblack": frame.getbbox() is not None}
            with open(shots / "native-framebuffer.jsonl", "a", encoding="utf-8", newline="") as f:
                f.write(json.dumps(record) + "\n")
            if not record["nonblack"]:
                print(f"[nwn] native screenshot {name} is entirely black; requires fixture-specific validation")
            return
        time.sleep(0.025)
    raise RuntimeError(f"native screenshot {name} produced no complete sandbox TGA")


def run_nwn(cfg, mod, user, shots, seconds, real_mouse=False, native_framebuffer=False):
    import wincap
    exe = Path(cfg["gameRoot"]) / "bin" / "win32" / "nwmain.exe"
    shutil.copy(mod, user / "modules" / mod.name)
    log = user / "logs" / "nwclientLog1.txt"
    if log.exists():
        log.unlink()
    shots.mkdir(parents=True, exist_ok=True)
    # Started minimised so it never takes focus; restored without activation once the window exists.
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 7  # SW_SHOWMINNOACTIVE
    proc = subprocess.Popen([str(exe), "-userdirectory", str(user), "+TestNewModule", mod.stem], cwd=exe.parent, startupinfo=si)
    taken = []
    restore_mouse = wincap.RealMouse() if real_mouse else None
    try:
        hwnd = wincap.wait_window(proc.pid, 90)
        if not hwnd:
            print("[nwn] no window")
            return taken
        start, pos, done = time.time(), 0, False
        restored = False
        hover = None  # (fx, fy): VISUAL|hover|fx,fy keeps a posted mouse position there (fractions of the client area)
        held = set()  # Multiple simultaneous drive keys; no real keyboard or focus changes.
        mouse_held = False
        # VISUAL|wall|<seconds>|<command ...>: the command runs that many wall-clock seconds after the line was read,
        # for steps inside windows that pause single player (DelayCommand stops there).
        pending = []
        # Win32 mouse messages carry modifier flags independently of posted key events.
        def pointer_modifiers():
            return (0x0004 if 16 in held else 0) | (0x0008 if 17 in held else 0)
        while time.time() - start < seconds and proc.poll() is None and not done:
            time.sleep(0.05)
            if hover:
                r = wincap.wt.RECT()
                wincap.user32.GetClientRect(hwnd, wincap.ctypes.byref(r))
                x, y = int(r.right * hover[0]), int(r.bottom * hover[1])
                wincap.user32.PostMessageW(hwnd, 0x0200, int(mouse_held) | pointer_modifiers(), (y << 16) | (x & 0xFFFF))  # WM_MOUSEMOVE
            for vk in held:
                wincap.user32.PostMessageW(hwnd, 0x0100, vk, 1 | (wincap.user32.MapVirtualKeyW(vk, 0) << 16) | (1 << 30))
            if not restored:
                # Onto the development monitor (if configured), then shown without taking focus.
                area = dev_display(cfg)
                wincap.user32.ShowWindow(hwnd, 4)  # SW_SHOWNOACTIVATE
                if area:
                    wincap.move_to(hwnd, area[0] + 20, area[1] + 20)
                r = wincap.wt.RECT()
                wincap.user32.GetWindowRect(hwnd, wincap.ctypes.byref(r))
                print(f"[nwn] window at {r.left},{r.top}")
                restored = True
            if not log.exists():
                continue
            with open(log, "rb") as f:
                f.seek(pos)
                chunk = f.read()
            # Only whole lines.
            cut = chunk.rfind(b"\n") + 1
            pos += cut
            now = time.time()
            due = sorted(p for p in pending if p[0] <= now)
            pending = [p for p in pending if p[0] > now]
            for line in chunk[:cut].decode("latin-1").splitlines() + [text for _, _, text in due]:
                at = line.find("VISUAL|")
                if at < 0:
                    continue
                parts = line[at + 7:].strip().split("|")
                if parts[0] == "wall" and len(parts) > 2:
                    pending.append((time.time() + float(parts[1]), len(pending), "VISUAL|" + "|".join(parts[2:])))
                    continue
                if parts[0] == "shot" and len(parts) > 1:
                    hwnd = wincap.main_window(proc.pid) or hwnd
                    if native_framebuffer:
                        try:
                            save_native_framebuffer(hwnd, user, shots, parts[1])
                        except RuntimeError as e:
                            # A focused text box takes the screenshot key: keep the window capture as the shot.
                            print(f"[nwn] {e}; using the window capture")
                            shutil.copy(shots / (parts[1] + "_window.png"), shots / (parts[1] + ".png"))
                    else:
                        wincap.capture(hwnd).save(shots / (parts[1] + ".png"))
                    with open(shots / "shots.txt", "a") as f:
                        f.write(line[at:].strip() + "\n")
                    taken.append(parts[1])
                    print(f"[nwn] {parts[1]}")
                elif parts[0] == "burst" and len(parts) >= 4:
                    # Short animation evidence, sampled on the capture clock rather than log polling.
                    # VISUAL|burst|name|seconds|fps. Scene movement runs independently; no focus/input.
                    duration = min(5.0, max(0.05, float(parts[2])))
                    fps = min(30.0, max(1.0, float(parts[3])))
                    began = time.perf_counter()
                    for frame in range(int(duration * fps)):
                        time.sleep(max(0, began + frame / fps - time.perf_counter()))
                        name = f"{parts[1]}_{frame:03d}"
                        elapsed = time.perf_counter() - began
                        wincap.capture(hwnd).save(shots / (name + ".png"))
                        with open(shots / "frames.txt", "a", encoding="utf-8", newline="") as f:
                            f.write(f"{name}|{elapsed:.4f}\n")
                        taken.append(name)
                    print(f"[nwn] burst {parts[1]} ({int(duration * fps)} frames)")
                elif parts[0] == "native-shot" and len(parts) > 1:
                    hwnd = wincap.main_window(proc.pid) or hwnd
                    save_native_framebuffer(hwnd, user, shots, parts[1] + "_native")
                    print(f"[nwn] native-shot {parts[1]}")
                elif parts[0] == "keyshot" and len(parts) > 2:
                    # VISUAL|keyshot|<vk>|<name>[|<seconds>[|<close vk>]]: taps a key, shoots after a wall-clock
                    # delay and optionally taps a key to close what it opened. For screens that pause single
                    # player (the game menu), where the scene's own delayed VShot would never run.
                    def tap(vk):
                        scan = wincap.user32.MapVirtualKeyW(vk, 0) << 16
                        wincap.user32.PostMessageW(hwnd, 0x0100, vk, 1 | scan)
                        time.sleep(0.05)
                        wincap.user32.PostMessageW(hwnd, 0x0101, vk, 1 | scan | (3 << 30))
                    tap(int(parts[1]))
                    time.sleep(float(parts[3]) if len(parts) > 3 else 1.5)
                    hwnd = wincap.main_window(proc.pid) or hwnd
                    wincap.capture(hwnd).save(shots / (parts[2] + ".png"))
                    taken.append(parts[2])
                    print(f"[nwn] {parts[2]}")
                    if len(parts) > 4:
                        tap(int(parts[4]))
                elif parts[0] == "note":
                    # VISUAL|note|...: a reading the scene logged (facings, states), kept beside the shots.
                    with open(shots / "notes.txt", "a") as f:
                        f.write(line[at:].strip() + "\n")
                elif parts[0] == "pointer" and len(parts) > 1 and restore_mouse:
                    # VISUAL|pointer|fx,fy (--real-mouse only): rests the real pointer there, after checking the window
                    # under it is ours, for native hover states (labels) that posted moves cannot reach.
                    fx, fy = (float(v) for v in parts[1].split(","))
                    r = wincap.wt.RECT()
                    wincap.user32.GetClientRect(hwnd, wincap.ctypes.byref(r))
                    restore_mouse.rest(hwnd, int(r.right * fx), int(r.bottom * fy))
                elif parts[0] == "hover" and len(parts) > 1:
                    hover = None if parts[1] == "off" else tuple(float(v) for v in parts[1].split(","))
                elif parts[0] == "mouse" and len(parts) >= 3:
                    mouse_point = tuple(float(v) for v in parts[1].split(","))
                    hover = None
                    right = len(parts) > 3 and parts[3] == "right"
                    down = parts[2] == "down"
                    mouse_held = (2 if right else 1) if down else 0
                    r = wincap.wt.RECT()
                    wincap.user32.GetClientRect(hwnd, wincap.ctypes.byref(r))
                    point = (int(r.bottom * mouse_point[1]) << 16) | (int(r.right * mouse_point[0]) & 0xFFFF)
                    if restore_mouse and not right:
                        restore_mouse.button(hwnd, int(r.right * mouse_point[0]), int(r.bottom * mouse_point[1]), down)
                    elif restore_mouse and right and len(parts) > 4 and parts[4] == "real":
                        # VISUAL|mouse|x,y|down|right|real: the real right button, after a short rest on the point.
                        px, py = int(r.right * mouse_point[0]), int(r.bottom * mouse_point[1])
                        if down and restore_mouse.rest(hwnd, px + 4, py):
                            time.sleep(0.1)
                        if not restore_mouse.button(hwnd, px, py, down, right=True):
                            mouse_held = 0
                    elif restore_mouse and right:
                        # The original reads the real pointer's resting place: rest it there (after checking the
                        # window under it is ours), then post the right button, as the death panel needed.
                        px, py = int(r.right * mouse_point[0]), int(r.bottom * mouse_point[1])
                        if not down or restore_mouse.rest(hwnd, px, py):
                            if down:
                                time.sleep(0.3)
                            wincap.user32.PostMessageW(hwnd, 0x0200, int(mouse_held) | pointer_modifiers(), point)
                            wincap.user32.PostMessageW(hwnd, 0x0204 if down else 0x0205, mouse_held | pointer_modifiers(), point)
                        else:
                            mouse_held = 0
                    else:
                        wincap.user32.PostMessageW(hwnd, 0x0200, int(mouse_held) | pointer_modifiers(), point)
                        message = (0x0204 if down else 0x0205) if right else (0x0201 if down else 0x0202)
                        wincap.user32.PostMessageW(hwnd, message, mouse_held | pointer_modifiers(), point)
                elif parts[0] in ("click-pair", "click-pair-dblclk") and len(parts) >= 5:
                    # Wall-clock double-click probe, independent of NWScript tick batching.
                    # x,y are client pixels; interval is down-to-down milliseconds.
                    # Optional excursion occurs between clicks and returns to the second point.
                    hover = None
                    label, first, second = parts[1], tuple(map(int, parts[2].split(","))), tuple(map(int, parts[3].split(",")))
                    interval = max(.07, min(2.0, float(parts[4]) / 1000))
                    def post_pointer(message, point, buttons=0):
                        wincap.user32.PostMessageW(hwnd, message, buttons, (point[1] << 16) | (point[0] & 0xFFFF))
                    post_pointer(0x0200, first)
                    # Let the native inventory update its hover target before the first press.
                    time.sleep(.15)
                    began = time.perf_counter()
                    post_pointer(0x0201, first, 1)
                    time.sleep(.025)
                    post_pointer(0x0202, first)
                    if len(parts) > 5:
                        time.sleep(.025)
                        post_pointer(0x0200, tuple(map(int, parts[5].split(","))))
                    time.sleep(max(0, interval - (time.perf_counter() - began)))
                    post_pointer(0x0200, second)
                    elapsed = time.perf_counter() - began
                    # Explicit Windows double-click is a delivery control only;
                    # it must never be used to infer native timing thresholds.
                    post_pointer(0x0203 if parts[0] == "click-pair-dblclk" else 0x0201, second, 1)
                    time.sleep(.025)
                    post_pointer(0x0202, second)
                    with open(shots / "input-timing.txt", "a", encoding="utf-8", newline="") as f:
                        f.write(f"{label}|mode={parts[0]}|down_interval_ms={elapsed * 1000:.3f}|first={first}|second={second}|excursion={parts[5:]!r}\n")
                elif parts[0] in ("click", "rightclick") and len(parts) > 1:
                    # Post only to this harness-owned window; never move the real pointer or activate it.
                    # SDL may ignore these when unfocused: verify movement in shots.txt before claiming parity.
                    fx, fy = (float(v) for v in parts[1].split(","))
                    r = wincap.wt.RECT()
                    wincap.user32.GetClientRect(hwnd, wincap.ctypes.byref(r))
                    x, y = int(r.right * fx), int(r.bottom * fy)
                    point = (y << 16) | (x & 0xFFFF)
                    wincap.user32.PostMessageW(hwnd, 0x0200, pointer_modifiers(), point)
                    if len(parts) > 2:
                        time.sleep(0.15)  # Optional held-click control lets native hover settle first.
                    right = parts[0] == "rightclick"
                    wincap.user32.PostMessageW(hwnd, 0x0204 if right else 0x0201, (2 if right else 1) | pointer_modifiers(), point)
                    time.sleep(max(0.05, min(1.0, float(parts[2]))) if len(parts) > 2 else 0.05)
                    wincap.user32.PostMessageW(hwnd, 0x0205 if right else 0x0202, pointer_modifiers(), point)
                elif parts[0] == "text" and len(parts) > 1:
                    # Text-input delivery control for an already-open sandbox editor. Unlike a
                    # key tap, WM_CHAR does not depend on the foreground keyboard translation.
                    value = "|".join(parts[1:])[:512].encode("utf-16-le")
                    for offset in range(0, len(value), 2):
                        wincap.user32.PostMessageW(hwnd, wincap.WM_CHAR, int.from_bytes(value[offset:offset + 2], "little"), 1)
                        time.sleep(0.03)
                elif parts[0] == "key" and len(parts) > 2:
                    vk = int(parts[1])
                    scan = wincap.user32.MapVirtualKeyW(vk, 0) << 16
                    if parts[2] == "tap":
                        # One press and release (VISUAL|key|<vk>|tap): toggles a window without key repeat.
                        wincap.user32.PostMessageW(hwnd, 0x0100, vk, 1 | scan)
                        time.sleep(0.05)
                        wincap.user32.PostMessageW(hwnd, 0x0101, vk, 1 | scan | (3 << 30))
                    elif restore_mouse and vk in (16, 17, 18):
                        restore_mouse.modifier(hwnd, vk, parts[2] == "down")
                    elif parts[2] == "down":
                        held.add(vk)
                        wincap.user32.PostMessageW(hwnd, 0x0100, vk, 1 | scan)
                    else:
                        held.discard(vk)
                        wincap.user32.PostMessageW(hwnd, 0x0101, vk, 1 | (wincap.user32.MapVirtualKeyW(vk, 0) << 16) | (3 << 30))  # WM_KEYUP
                elif parts[0] == "done":
                    done = True
        if not done:
            print("[nwn] time limit reached before VISUAL|done")
    finally:
        if restore_mouse:
            restore_mouse.restore()
        proc.kill()
        proc.wait()
        # What reached the chat window, when the scene asked for chat logging ("chatLog": true).
        if log.exists():
            chat = [l for l in log.read_text(encoding="latin-1").splitlines() if "[CHAT WINDOW TEXT]" in l]
            if chat:
                (shots / "chat.txt").write_text("\n".join(chat) + "\n", encoding="utf-8", newline="")
    return taken


# ---------------------------------------------------------------- OpenWinter

def godot():
    env = os.environ.get("GODOT")
    if env:
        return env
    base = Path(os.environ["LOCALAPPDATA"]) / "Microsoft" / "WinGet" / "Packages"
    found = sorted(base.glob("GodotEngine.GodotEngine.Mono_*/Godot_v4*/Godot_v4*_mono_win64_console.exe"))
    if not found:
        sys.exit("Godot .NET console binary not found (set GODOT)")
    return str(found[0])


def run_openwinter(cfg, mod, user, shots, seconds, character, size, extra=(), engine_args=()):
    # Building the tools/fixture does not rebuild Godot's managed client. Never
    # capture an old DLL while attributing its output to the current source tree.
    result = subprocess.run(["dotnet", "build", str(REPO / "client" / "OpenWinter.Client.csproj"),
                             "--nologo", "-v:q"], cwd=REPO, capture_output=True,
                            text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError("Client build failed; capture was not started:\n" + result.stdout + result.stderr)
    # OpenWinter reads the same sandbox user folder (no development folder, no overrides) through its own
    # config; what it writes goes to out/visual/openwinter/out/sandbox-userdata.
    home = OUT / "openwinter"
    (home / "config").mkdir(parents=True, exist_ok=True)
    local = dict(cfg, userRoot=str(user).replace("\\", "/"), module=str(mod).replace("\\", "/"))
    (home / "config" / "local.json").write_text(json.dumps(local, indent=2))
    shots.mkdir(parents=True, exist_ok=True)
    # A crash leaves a .NET minidump in out/visual/dumps (managed failures and faults inside the runtime).
    dumps = OUT / "dumps"
    dumps.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, OPENWINTER_CONFIG=str(home / "config" / "local.json"), DOTNET_DbgEnableMiniDump="1",
               DOTNET_DbgMiniDumpType="2", DOTNET_DbgMiniDumpName=str(dumps / "godot_%p.dmp"), DOTNET_CreateDumpDiagnostics="1")
    position = []
    area = dev_display(cfg)
    if area:
        import wincap
        ox, oy = wincap.virtual_origin()
        position = ["--position", f"{area[0] - ox + 20},{area[1] - oy + 20}"]
    args = [godot(), *engine_args, "--path", str(REPO / "client"), "--resolution", f"{size[0]}x{size[1]}", *position, "--",
            "--module", str(mod), "--character", character, "--capture-visual", str(shots), "--seconds", str(seconds), *extra]
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 4  # SW_SHOWNOACTIVATE
    try:
        r = subprocess.run(args, cwd=REPO / "client", env=env, capture_output=True, text=True, timeout=seconds + 240, startupinfo=si,
                           encoding="utf-8", errors="replace")
    except subprocess.TimeoutExpired as t:
        # Keep what it printed: a timeout looks like a crash otherwise (the first run after a client build is slow).
        # With text=True the partial output may come back as str or bytes.
        partial = "".join(x.decode("utf-8", "replace") if isinstance(x, bytes) else x for x in (t.stdout or "", t.stderr or ""))
        (shots / "godot.log").write_text(partial + f"\n[harness] timed out after {seconds + 240} s and was killed\n", encoding="utf-8")
        print(f"[openwinter] timed out after {seconds + 240} s and was killed")
        return [p.stem for p in shots.glob("*.png")]
    (shots / "godot.log").write_text(r.stdout + r.stderr + f"\n[harness] exit code {r.returncode}\n", encoding="utf-8")
    if r.returncode != 0 or "VISUAL|done" not in r.stdout:
        print(f"[openwinter] exited with code {r.returncode} ({r.returncode & 0xFFFFFFFF:#010x}) before the scene finished")
    taken = [l.split("captured ", 1)[1].strip() for l in r.stdout.splitlines() if l.startswith("captured ")]
    for t in taken:
        print(f"[openwinter] {t}")
    errors = [l for l in (r.stdout + r.stderr).splitlines() if "ERROR" in l or "[script error]" in l or "Exception" in l]
    for e in errors[:15]:
        print("[openwinter] " + e)
    return taken


# ---------------------------------------------------------------- pairing

def luma_stats(img):
    import numpy as np
    a = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    y = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    return float(y.mean()), a.reshape(-1, 3).mean(axis=0)


def compare(module_name):
    """Pairs out/visual/shots/<module>/{nwn,openwinter} side by side with brightness numbers; returns rows."""
    from PIL import Image, ImageDraw
    src = OUT / "shots" / module_name
    dst = OUT / "compare"
    dst.mkdir(parents=True, exist_ok=True)
    rows = []
    names = sorted({p.stem for side in ("nwn", "openwinter") for p in (src / side).glob("*.png") if "." not in p.stem}) if src.exists() else []
    for name in names:
        imgs = [Image.open(src / side / (name + ".png")).convert("RGB") if (src / side / (name + ".png")).exists() else None for side in ("nwn", "openwinter")]
        w = max(i.width for i in imgs if i)
        h = max(i.height for i in imgs if i)
        sheet = Image.new("RGB", (w * 2 + 8, h + 28), (24, 24, 24))
        d = ImageDraw.Draw(sheet)
        stats = []
        for k, (img, label) in enumerate(zip(imgs, ("NWN:EE", "OpenWinter"))):
            x = k * (w + 8)
            if img:
                sheet.paste(img, (x, 28))
                m, rgb = luma_stats(img)
                stats.append(m)
                label += f"   mean luma {m:.3f}   rgb {rgb[0]:.2f} {rgb[1]:.2f} {rgb[2]:.2f}"
            else:
                stats.append(None)
                label += "   (missing)"
            d.text((x + 6, 7), f"{module_name} / {name}   {label}", fill=(230, 230, 230))
        out = dst / f"{module_name}__{name}.png"
        sheet.save(out)
        rows.append((module_name, name, out.name, *stats))
    return rows


def write_index():
    """out/visual/compare/index.html: every pair, newest run's numbers."""
    from PIL import Image
    dst = OUT / "compare"
    dst.mkdir(parents=True, exist_ok=True)
    items = []
    for p in sorted(dst.glob("*__*.png")):
        module, shot = p.stem.split("__", 1)
        items.append(f'<figure><figcaption>{module} / {shot}</figcaption><a href="{p.name}"><img src="{p.name}" loading="lazy"></a></figure>')
    (dst / "index.html").write_text("<!doctype html><meta charset=utf-8><title>Visual parity</title>"
                                    "<style>body{background:#181818;color:#ddd;font:14px sans-serif}img{width:100%}figure{margin:0 0 24px}</style>"
                                    + "\n".join(items))


def main():
    global SETTINGS
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=["run", "compare", "build"])
    ap.add_argument("scenes", nargs="+")
    ap.add_argument("--only", choices=["nwn", "openwinter"])
    ap.add_argument("--native-framebuffer", action="store_true", help="native VShot markers use owned in-game PrintScreen; retain raw TGA, window control and timing/hashes (not burst/keyshot)")
    ap.add_argument("--real-mouse", action="store_true", help="moves the real mouse (secondary display only): temporarily use real pointer/focus for native held-mouse calibration, then restore both")
    ap.add_argument("--character", default=None, help="pregenerated or local-vault character (default: the scene's, else aluviandarkstar: the original's +TestNewModule always plays the first pregenerated character)")
    ap.add_argument("--size", default="1280x720")
    ap.add_argument("--openwinter-args", default="", help='extra client arguments, e.g. "--visual-graphics Glow=false"')
    ap.add_argument("--silent", action="store_true", help="mute the native sandbox and use Godot Dummy audio for non-audio checks")
    ap.add_argument("--keyhole", default="", help='keyholing settings for both engines, e.g. "enabled=false" or "min-diameter=0.5,max-diameter=0.5"')
    ap.add_argument("--second-story", type=int, choices=[0, 1, 2], help="the original's Hide Second Story Tiles (graphics.second-story-tiles.mode; 1 by default)")
    ap.add_argument("--tag", default="", help="suffix for the shots and pairs folder name (keeps runs with different settings apart)")
    ap.add_argument("--text-bubble-mode", type=int, choices=[0, 1, 2], help="nameplates: 0 off, 1 text, 2 text/portrait/bubble (both engines)")
    ap.add_argument("--ui-scale", type=float, help="interface scale for both engines (overrides the scene's uiScale)")
    ap.add_argument("--large-font", choices=["true", "false"], help="the large (serif) font for both engines (overrides the scene's largeFont; the original's own default is true)")
    ap.add_argument("--profile", choices=["default", "plain"], default="default", help="original client post-processing")
    ap.add_argument("--difficulty", type=int, choices=[0, 1, 2, 3, 4], help="game difficulty for both engines (overrides the scene's difficulty; both default to 1; 0 is Very Easy, which the original's settings page doesn't offer)")
    a = ap.parse_args()
    if a.silent:
        SETTINGS += "\n" + "\n".join(f"[sound.{kind}]\nvolume = 0.0" for kind in ("effects", "music", "voices", "streams")) + "\n"
    names = scenes() if a.scenes == ["all"] else a.scenes
    size = tuple(int(x) for x in a.size.split("x"))
    cfg = config()
    if a.real_mouse and (a.only != "nwn" or cfg.get("devDisplay") != "DISPLAY2" or not dev_display(cfg)):
        ap.error("--real-mouse requires --only nwn and the configured DISPLAY2 monitor")
    rows = []
    for name in names:
        sj = scene_json(name)
        if a.ui_scale:
            sj["uiScale"] = a.ui_scale
        if a.large_font:
            sj["largeFont"] = a.large_font == "true"
        if a.difficulty is not None:
            sj["difficulty"] = a.difficulty
        mods = [OUT / "build" / f"vis_{name}.mod"] if a.command == "compare" else build(name)
        if a.command == "compare" and "variants" in sj:
            mods = [OUT / "build" / f"vis_{name}_{v}.mod" for v in sj["variants"]]
        if a.command == "build":
            print("\n".join(map(str, mods)))
            continue
        character = a.character or sj.get("character", "aluviandarkstar")
        seconds = int(sj.get("seconds", 60))
        for mod in mods:
            shots = OUT / "shots" / (mod.stem + a.tag)
            if a.command == "run":
                bubble = a.text_bubble_mode if a.text_bubble_mode is not None else sj.get("textBubbleMode")
                user = prepare_user(cfg, character, size, a.profile, a.keyhole, bubble, sj.get("largeFont"), sj.get("uiScale"), sj.get("softwareCursor", False), sj.get("chatLog", False), sj.get("chatSplit"), sj.get("difficulty"), sj.get("serverSettings"))
                if a.second_story is not None:
                    with open(user / "settings.tml", "a", encoding="utf-8", newline="") as f:
                        f.write(f"\n[graphics.second-story-tiles]\n\tmode = {a.second_story}\n")
                run_character = character
                # Optional generated BIC: never modify the installed character or real vault.
                if mod.with_suffix(".bic").exists():
                    if a.character:
                        sys.exit("--character cannot override a scene with character controls")
                    fixture_meta = mod.with_suffix(".character.json")
                    if fixture_meta.exists():
                        run_character = json.loads(fixture_meta.read_text(encoding="utf-8"))["vaultResRef"]
                    shutil.copy(mod.with_suffix(".bic"), user / "localvault" / (run_character + ".bic"))
                if a.only != "openwinter":
                    if (shots / "nwn").exists():
                        shutil.rmtree(shots / "nwn")
                    run_nwn(cfg, mod, user, shots / "nwn", seconds + 60, a.real_mouse, a.native_framebuffer)
                    verify_character_fixture(mod, shots / "nwn")
                if a.only != "nwn":
                    if (shots / "openwinter").exists():
                        shutil.rmtree(shots / "openwinter")
                    extra = (["--large-font", str(sj["largeFont"]).lower()] if "largeFont" in sj else []) + (["--text-bubble-mode", str(bubble)] if bubble is not None else []) + (["--ui-scale", str(sj["uiScale"])] if "uiScale" in sj else []) + (["--keyhole", a.keyhole] if a.keyhole else []) + (["--visual-gui"] if sj.get("gui") else []) + (["--visual-chat"] if sj.get("chatLog") else []) + (["--difficulty", str(sj["difficulty"])] if "difficulty" in sj else []) + a.openwinter_args.split()  # explicit client arguments win
                    run_openwinter(cfg, mod, user, shots / "openwinter", seconds + 30, run_character, size, extra,
                                engine_args=("--audio-driver", "Dummy") if a.silent else ())
                    verify_character_fixture(mod, shots / "openwinter")
            rows += compare(mod.stem + a.tag)
    write_index()
    if rows:
        print(f"\n{'scene':<28} {'shot':<20} {'nwn':>6} {'openwinter':>8} {'ratio':>6}")
        for module, shot, _, n, e in rows:
            ratio = f"{e / n:.2f}" if n and e else "-"
            print(f"{module:<28} {shot:<20} {n if n is None else round(n, 3)!s:>6} {e if e is None else round(e, 3)!s:>8} {ratio:>6}")
        print(f"\npairs: {OUT / 'compare'}")


if __name__ == "__main__":
    main()
