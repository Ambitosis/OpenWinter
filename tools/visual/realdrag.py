"""Real right-button drag on OpenWinter's client (moves the real pointer: development monitor only).

Runs the OpenWinter side of the rightdragreal scene and, once its a_ready shot is taken, presses the right button on
the summon, drags towards the radial's east wedge with the button held, comes back to the middle and lets go. Restores
the pointer and the foreground window afterwards. Checks a held right drag doesn't stop the client
(docs/ASSOCIATE_COMMANDS.md).

    python tools/visual/realdrag.py
"""
import ctypes, subprocess, sys, time
from ctypes import wintypes as wt
from pathlib import Path

import psutil

sys.path.insert(0, str(Path(__file__).parent))
import wincap  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
SHOTS = REPO / "out" / "visual" / "shots" / "vis_rightdragreal" / "openwinter"
user32 = ctypes.windll.user32


def godot_window(proc):
    for child in psutil.Process(proc.pid).children(recursive=True):
        if "godot" in child.name().lower():
            h = wincap.main_window(child.pid)
            if h:
                return h
    return None


def main():
    if not wincap.monitor_work_area("DISPLAY2"):
        sys.exit("needs the DISPLAY2 development monitor")
    import shutil
    shutil.rmtree(SHOTS, ignore_errors=True)
    proc = subprocess.Popen([sys.executable, str(REPO / "tools" / "visual" / "visual.py"), "run", "rightdragreal", "--only", "openwinter",
                             "--openwinter-args", "--visual-input-log 1"], cwd=REPO)
    shots = SHOTS / "shots.txt"
    deadline = time.time() + 240
    while time.time() < deadline and proc.poll() is None:
        if shots.exists() and "a_ready" in shots.read_text():
            break
        time.sleep(0.1)
    hwnd = godot_window(proc)
    area = wincap.monitor_work_area("DISPLAY2")
    user32.GetForegroundWindow.restype = wt.HWND
    foreground = user32.GetForegroundWindow()
    pointer = wt.POINT()
    user32.GetCursorPos(ctypes.byref(pointer))
    down = False
    try:
        if not hwnd:
            print("[realdrag] no client window")
            return
        r = wt.RECT()
        user32.GetClientRect(hwnd, ctypes.byref(r))

        def screen(fx, fy):
            p = wt.POINT(int(r.right * fx), int(r.bottom * fy))
            user32.ClientToScreen(hwnd, ctypes.byref(p))
            if not (area[0] <= p.x < area[2] and area[1] <= p.y < area[3]):
                raise RuntimeError(f"point {p.x},{p.y} is not on the development monitor")
            return p.x, p.y

        start = screen(0.734, 0.600)
        # Only ever press on our own window: anything covering it (another game window) would get the input.
        user32.WindowFromPoint.restype = wt.HWND
        user32.WindowFromPoint.argtypes = [wt.POINT]
        user32.GetAncestor.restype = wt.HWND
        under = user32.GetAncestor(user32.WindowFromPoint(wt.POINT(*start)), 2)
        if under != hwnd:
            title = ctypes.create_unicode_buffer(256)
            user32.GetWindowTextW(under, title, 256)
            pid = wt.DWORD()
            user32.GetWindowThreadProcessId(under, ctypes.byref(pid))
            print(f"[realdrag] another window is over the client there: '{title.value}' (pid {pid.value}); not pressing")
            return
        user32.SetForegroundWindow(hwnd)
        user32.SetCursorPos(*start)
        time.sleep(0.3)
        print("[realdrag] client in the foreground:", user32.GetForegroundWindow() == hwnd)
        user32.mouse_event(0x0008, 0, 0, 0, 0)  # right down
        down = True
        print("[realdrag] right button down on the summon")
        for i in range(1, 21):
            x, y = screen(0.734 + (0.830 - 0.734) * i / 20, 0.600)
            user32.SetCursorPos(x, y)
            time.sleep(0.05)
        for i in range(4):  # b_drag is taken meanwhile, with the button held over the east wedge
            time.sleep(0.6)
            wincap.capture(hwnd).save(SHOTS / f"held_{i}.png")
        for i in range(1, 11):
            x, y = screen(0.830 - (0.830 - 0.734) * i / 10, 0.600)
            user32.SetCursorPos(x, y)
            time.sleep(0.03)
        user32.mouse_event(0x0010, 0, 0, 0, 0)  # right up, back in the middle (nothing chosen)
        down = False
        print("[realdrag] released in the middle")
    finally:
        if down:
            user32.mouse_event(0x0010, 0, 0, 0, 0)
        user32.SetCursorPos(pointer.x, pointer.y)
        if foreground:
            user32.SetForegroundWindow(foreground)
    proc.wait(timeout=300)
    print("[realdrag] client exit", proc.returncode)


if __name__ == "__main__":
    main()
