"""Drives the original client (nwmain) from its main menu, for screens no module script can open: character
creation, the module list, character selection. Started without +TestNewModule in its own sandbox user folder
(out/visual/user_menu, never the real one), on the development monitor.

    python tools/visual/menudrive.py start [--module out/visual/build/vis_chargen.mod] [--size 1280x720] [--minutes 20]
    python tools/visual/menudrive.py do "click 458 174" "shot cg_main"      # appended to the running session
    python tools/visual/menudrive.py stop

`start` runs the session (run it in the background): it holds the nwmain lock
(visual.nwmain_lock_path(), waiting while another run has it) for as long as the game
runs and removes it on exit. Commands are read from out/visual/menudrive/cmd.txt and acknowledged in log.txt:

    click X Y          left click at client pixel (X, Y) with the real pointer (monitor 2 only), then the pointer
                       and the foreground window are put back
    dclick X Y         a double click there
    hover X Y [S]      rests the real pointer there for S seconds (default 0.6), then puts it back
    hovershot X Y S NAME  rests the real pointer there, captures the window after S seconds (still resting:
                       hover help), then puts the pointer back
    key VK             a posted key tap (no focus change)
    text STRING        posted characters (WM_CHAR) for an edit box with the keyboard focus
    wheel X Y N        N wheel notches (negative: down) with the pointer at (X, Y)
    wait S             sleeps S seconds
    shot NAME          the window's picture, saved as NAME.png beside log.txt
    nshot NAME         the game's own screenshot (PrintScreen into the sandbox) instead
    quit               ends the session

The real pointer is needed because the original reads clicks at the pointer's resting place (posted clicks land
wherever the real pointer is); use it only on a secondary display. Keep sessions short.
"""
import argparse
import ctypes
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import visual  # noqa: E402  (config, settings template, native screenshots)
import wincap  # noqa: E402

OUT = visual.OUT / "menudrive"
USER = visual.OUT / "user_menu"
LOCK = visual.nwmain_lock_path()


def prepare(cfg, module, size, character=None):
    (USER / "modules").mkdir(parents=True, exist_ok=True)
    for old in (USER / "modules").glob("*.mod"):
        old.unlink()
    if module:
        shutil.copy(module, USER / "modules" / Path(module).name)
    vault = USER / "localvault"
    if vault.exists():
        shutil.rmtree(vault)
    vault.mkdir()
    if character:
        shutil.copy(visual.find_character(character, cfg), vault / (character + ".bic"))
    shots = USER / "screenshots"
    if shots.exists():
        shutil.rmtree(shots)
    area = visual.dev_display(cfg)
    x, y = (area[0] + 20, area[1] + 20) if area else (40, 40)
    (USER / "settings.tml").write_text(visual.SETTINGS.format(ui="", w=size[0], h=size[1], x=x, y=y, fbo=""))


def acquire_lock(branch):
    while LOCK.exists():
        print("[menudrive] waiting for", LOCK, flush=True)
        time.sleep(5)
    LOCK.write_text(branch)


class Pointer:
    """The real pointer, used for one gesture and put back (with the foreground window) at once."""

    def __init__(self, hwnd):
        self.hwnd = hwnd

    def screen(self, x, y):
        p = wincap.wt.POINT(int(x), int(y))
        wincap.user32.ClientToScreen(self.hwnd, ctypes.byref(p))
        return p

    def gesture(self, x, y, action):
        mouse = wincap.RealMouse()
        try:
            p = self.screen(x, y)
            wincap.user32.SetForegroundWindow(self.hwnd)
            wincap.user32.SetCursorPos(p.x, p.y)
            # The first click right after the window comes forward was sometimes lost.
            time.sleep(0.35)
            # A small move so the game sees the pointer arrive (hover states, list rows).
            wincap.user32.SetCursorPos(p.x + 1, p.y)
            time.sleep(0.05)
            wincap.user32.SetCursorPos(p.x, p.y)
            time.sleep(0.08)
            action()
        finally:
            mouse.restore()

    def click(self, x, y, count=1):
        def act():
            for i in range(count):
                wincap.user32.mouse_event(0x0002, 0, 0, 0, 0)
                time.sleep(0.05)
                wincap.user32.mouse_event(0x0004, 0, 0, 0, 0)
                time.sleep(0.08 if i + 1 < count else 0.25)
        self.gesture(x, y, act)

    def hover(self, x, y, seconds, then=None):
        def act():
            time.sleep(seconds)
            if then:
                then()
        self.gesture(x, y, act)

    def wheel(self, x, y, notches):
        def act():
            for _ in range(abs(notches)):
                wincap.user32.mouse_event(0x0800, 0, 0, 120 if notches > 0 else -120, 0)
                time.sleep(0.08)
            time.sleep(0.2)
        self.gesture(x, y, act)


def tap(hwnd, vk):
    scan = wincap.user32.MapVirtualKeyW(vk, 0) << 16
    wincap.user32.PostMessageW(hwnd, 0x0100, vk, 1 | scan)
    time.sleep(0.05)
    wincap.user32.PostMessageW(hwnd, 0x0101, vk, 1 | scan | (3 << 30))


def start(a):
    cfg = visual.config()
    if cfg.get("devDisplay") != "DISPLAY2" or not visual.dev_display(cfg):
        sys.exit("menudrive needs the DISPLAY2 development monitor (config/local.json devDisplay)")
    size = tuple(int(v) for v in a.size.split("x"))
    OUT.mkdir(parents=True, exist_ok=True)
    cmd = OUT / "cmd.txt"
    cmd.write_text("")
    log = OUT / "log.txt"
    log.write_text("")

    def note(text):
        with open(log, "a", encoding="utf-8", newline="") as f:
            f.write(text + "\n")
        print("[menudrive]", text, flush=True)

    acquire_lock(a.branch)
    proc = None
    try:
        prepare(cfg, a.module, size, a.character)
        exe = Path(cfg["gameRoot"]) / "bin" / "win32" / "nwmain.exe"
        si = subprocess.STARTUPINFO()
        si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        si.wShowWindow = 7  # SW_SHOWMINNOACTIVE
        proc = subprocess.Popen([str(exe), "-userdirectory", str(USER)], cwd=exe.parent, startupinfo=si)
        hwnd = wincap.wait_window(proc.pid, 90)
        if not hwnd:
            note("error|no window")
            return
        area = visual.dev_display(cfg)
        wincap.user32.ShowWindow(hwnd, 4)  # SW_SHOWNOACTIVATE
        wincap.move_to(hwnd, area[0] + 20, area[1] + 20)
        r = wincap.wt.RECT()
        wincap.user32.GetClientRect(hwnd, ctypes.byref(r))
        note(f"ready|client {r.right}x{r.bottom}")
        pointer = Pointer(hwnd)
        pos, idle_since, end = 0, time.time(), time.time() + a.minutes * 60
        while proc.poll() is None and time.time() < end and time.time() - idle_since < a.idle * 60:
            time.sleep(0.1)
            text = cmd.read_text(encoding="utf-8")
            if len(text) <= pos or not text.endswith("\n"):
                continue
            lines, pos = text[pos:].splitlines(), len(text)
            for line in lines:
                parts = line.strip().split(" ", 1)
                if not parts or not parts[0]:
                    continue
                verb, rest = parts[0], (parts[1] if len(parts) > 1 else "")
                hwnd = wincap.main_window(proc.pid) or hwnd
                pointer.hwnd = hwnd
                try:
                    args = rest.split()
                    if verb == "click":
                        pointer.click(float(args[0]), float(args[1]))
                    elif verb == "dclick":
                        pointer.click(float(args[0]), float(args[1]), 2)
                    elif verb == "hovershot":
                        pointer.hover(float(args[0]), float(args[1]), float(args[2]),
                                      lambda n=args[3]: wincap.capture(hwnd).save(OUT / (n + ".png")))
                    elif verb == "hover":
                        pointer.hover(float(args[0]), float(args[1]), float(args[2]) if len(args) > 2 else 0.6)
                    elif verb == "wheel":
                        pointer.wheel(float(args[0]), float(args[1]), int(args[2]))
                    elif verb == "key":
                        tap(hwnd, int(args[0]))
                    elif verb == "text":
                        for ch in rest.encode("utf-16-le").decode("utf-16-le"):
                            wincap.user32.PostMessageW(hwnd, wincap.WM_CHAR, ord(ch), 1)
                            time.sleep(0.03)
                    elif verb == "wait":
                        time.sleep(min(30.0, float(args[0])))
                    elif verb == "shot":
                        # The window's own picture (PrintWindow); "nshot" asks the game for its screenshot instead
                        # (black window captures happen with the display asleep, but the game's own fails on its
                        # NUI module browser).
                        wincap.capture(hwnd).save(OUT / (args[0] + ".png"))
                    elif verb == "nshot":
                        visual.save_native_framebuffer(hwnd, USER, OUT, args[0])
                        (OUT / (args[0] + ".tga")).unlink(missing_ok=True)
                        (OUT / (args[0] + "_window.png")).unlink(missing_ok=True)
                    elif verb == "quit":
                        note("ok|quit")
                        return
                    else:
                        note(f"error|unknown {line}")
                        continue
                    note(f"ok|{line.strip()}")
                except Exception as e:  # a bad command must not leave the game or the lock behind
                    note(f"error|{line.strip()}|{e}")
                idle_since = time.time()
        note("end|" + ("game exited" if proc.poll() is not None else "time limit"))
    finally:
        if proc and proc.poll() is None:
            proc.kill()
            proc.wait()
        log_file = USER / "logs" / "nwclientLog1.txt"
        if log_file.exists():
            notes = [l for l in log_file.read_text(encoding="latin-1").splitlines() if "VISUAL|" in l]
            (OUT / "notes.txt").write_text("\n".join(notes) + "\n", encoding="utf-8", newline="")
        LOCK.unlink(missing_ok=True)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("start")
    s.add_argument("--module", default=str(visual.OUT / "build" / "vis_chargen.mod"))
    s.add_argument("--size", default="1280x720")
    s.add_argument("--minutes", type=float, default=25)
    s.add_argument("--idle", type=float, default=8, help="minutes without commands before the session ends")
    s.add_argument("--branch", default="menudrive")
    s.add_argument("--character", default=None, help="a pregenerated or local-vault character copied into the sandbox vault")
    d = sub.add_parser("do")
    d.add_argument("commands", nargs="+")
    sub.add_parser("stop")
    a = ap.parse_args()
    if a.cmd == "start":
        start(a)
    else:
        lines = a.commands if a.cmd == "do" else ["quit"]
        with open(OUT / "cmd.txt", "a", encoding="utf-8", newline="") as f:
            f.write("".join(l + "\n" for l in lines))


if __name__ == "__main__":
    main()
