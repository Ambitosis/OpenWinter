"""Window capture and message-posted input for the visual parity harness (Windows only).

Captures one window by process id with PrintWindow(PW_RENDERFULLCONTENT), which works for
GPU-rendered windows without bringing them to the front, and posts keyboard/mouse messages
straight to the window so the real mouse and focus are never touched.
"""
import ctypes
import ctypes.wintypes as wt
import time

user32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)
user32.SetProcessDPIAware()

WNDENUMPROC = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
user32.GetWindowThreadProcessId.argtypes = [wt.HWND, ctypes.POINTER(wt.DWORD)]
user32.PostMessageW.argtypes = [wt.HWND, wt.UINT, wt.WPARAM, wt.LPARAM]
user32.PrintWindow.argtypes = [wt.HWND, wt.HDC, wt.UINT]
user32.GetDC.argtypes = [wt.HWND]
user32.GetDC.restype = wt.HDC
user32.ReleaseDC.argtypes = [wt.HWND, wt.HDC]
gdi32.CreateCompatibleDC.argtypes = [wt.HDC]
gdi32.CreateCompatibleDC.restype = wt.HDC
gdi32.CreateCompatibleBitmap.argtypes = [wt.HDC, ctypes.c_int, ctypes.c_int]
gdi32.CreateCompatibleBitmap.restype = wt.HBITMAP
gdi32.SelectObject.argtypes = [wt.HDC, wt.HGDIOBJ]
gdi32.SelectObject.restype = wt.HGDIOBJ
gdi32.DeleteObject.argtypes = [wt.HGDIOBJ]
gdi32.DeleteDC.argtypes = [wt.HDC]
gdi32.GetDIBits.argtypes = [wt.HDC, wt.HBITMAP, wt.UINT, wt.UINT, ctypes.c_void_p, ctypes.c_void_p, wt.UINT]

WM_KEYDOWN, WM_KEYUP, WM_CHAR = 0x0100, 0x0101, 0x0102
WM_MOUSEMOVE, WM_LBUTTONDOWN, WM_LBUTTONUP = 0x0200, 0x0201, 0x0202


def windows_of(pid):
    """Visible top-level windows of a process, largest client area first."""
    found = []

    def cb(hwnd, _):
        p = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(hwnd):
            r = wt.RECT()
            user32.GetClientRect(hwnd, ctypes.byref(r))
            found.append((r.right * r.bottom, hwnd))
        return True

    user32.EnumWindows(WNDENUMPROC(cb), 0)
    return [h for _, h in sorted(found, reverse=True)]


def main_window(pid):
    """The process's titled top-level window (found even while minimised), or None."""
    for h in windows_of(pid):
        if user32.GetWindowTextLengthW(h) > 0:
            return h
    return None


def wait_window(pid, timeout=60.0):
    end = time.time() + timeout
    while time.time() < end:
        h = main_window(pid)
        if h:
            return h
        time.sleep(0.25)
    return None


def capture(hwnd):
    """The window's client area as a PIL image (PrintWindow, PW_CLIENTONLY | PW_RENDERFULLCONTENT)."""
    from PIL import Image
    r = wt.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(r))
    w, h = r.right, r.bottom
    hdc = user32.GetDC(hwnd)
    mem = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    old = gdi32.SelectObject(mem, bmp)
    user32.PrintWindow(hwnd, mem, 1 | 2)
    class BIH(ctypes.Structure):
        _fields_ = [("biSize", wt.DWORD), ("biWidth", wt.LONG), ("biHeight", wt.LONG), ("biPlanes", wt.WORD),
                    ("biBitCount", wt.WORD), ("biCompression", wt.DWORD), ("biSizeImage", wt.DWORD),
                    ("biXPelsPerMeter", wt.LONG), ("biYPelsPerMeter", wt.LONG), ("biClrUsed", wt.DWORD), ("biClrImportant", wt.DWORD)]
    bih = BIH(ctypes.sizeof(BIH), w, -h, 1, 32, 0, 0, 0, 0, 0, 0)
    buf = ctypes.create_string_buffer(w * h * 4)
    gdi32.GetDIBits(mem, bmp, 0, h, buf, ctypes.byref(bih), 0)
    gdi32.SelectObject(mem, old)
    gdi32.DeleteObject(bmp)
    gdi32.DeleteDC(mem)
    user32.ReleaseDC(hwnd, hdc)
    return Image.frombuffer("RGB", (w, h), buf.raw, "raw", "BGRX", 0, 1)


def key(hwnd, vk, hold=0.05):
    """A key press posted to the window (no focus change)."""
    scan = user32.MapVirtualKeyW(vk, 0)
    user32.PostMessageW(hwnd, WM_KEYDOWN, vk, 1 | (scan << 16))
    time.sleep(hold)
    user32.PostMessageW(hwnd, WM_KEYUP, vk, 1 | (scan << 16) | (3 << 30))


def click(hwnd, x, y):
    """A left click at client pixel (x, y), posted to the window (the real cursor does not move)."""
    lp = (y << 16) | (x & 0xFFFF)
    user32.PostMessageW(hwnd, WM_MOUSEMOVE, 0, lp)
    time.sleep(0.05)
    user32.PostMessageW(hwnd, WM_LBUTTONDOWN, 1, lp)
    time.sleep(0.08)
    user32.PostMessageW(hwnd, WM_LBUTTONUP, 0, lp)


class RealMouse:
    """Explicit opt-in only: native held-pointer calibration cannot use SDL's posted relative motion.

    This moves the real mouse pointer: use it only on a secondary display.
    Always restore pointer and foreground in the harness's finally block.
    """
    def __init__(self):
        user32.GetForegroundWindow.restype = wt.HWND
        user32.SetForegroundWindow.argtypes = [wt.HWND]
        user32.ClientToScreen.argtypes = [wt.HWND, ctypes.POINTER(wt.POINT)]
        self.foreground = user32.GetForegroundWindow()
        self.pointer = wt.POINT()
        user32.GetCursorPos(ctypes.byref(self.pointer))
        self.down = False
        self.right_down = False
        self.modifiers = set()

    def modifier(self, hwnd, vk, down):
        if vk not in (16, 17, 18):
            raise ValueError("real-pointer chords support only Shift, Ctrl and Alt")
        user32.SetForegroundWindow(hwnd)
        user32.keybd_event(vk, user32.MapVirtualKeyW(vk, 0), 0 if down else 2, 0)
        if down:
            self.modifiers.add(vk)
        else:
            self.modifiers.discard(vk)

    def owns_point(self, hwnd, sx, sy):
        """Whether the top-level window under screen point (sx, sy) is the harness's own game window, on the
        development monitor. Anything else there (a Windows dialog, another game window) must not get a press."""
        user32.WindowFromPoint.restype = wt.HWND
        user32.WindowFromPoint.argtypes = [wt.POINT]
        user32.GetAncestor.restype = wt.HWND
        user32.GetAncestor.argtypes = [wt.HWND, wt.UINT]
        under = user32.GetAncestor(user32.WindowFromPoint(wt.POINT(sx, sy)), 2)  # GA_ROOT
        mine = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(mine))
        theirs = wt.DWORD()
        user32.GetWindowThreadProcessId(under, ctypes.byref(theirs))
        area = monitor_work_area("DISPLAY2")
        on_dev = bool(area) and area[0] <= sx < area[2] and area[1] <= sy < area[3]
        if under == hwnd and theirs.value == mine.value and on_dev:
            return True
        title = ctypes.create_unicode_buffer(256)
        user32.GetWindowTextW(under, title, 256)
        print(f"[realmouse] not pressing at {sx},{sy}: window '{title.value}' (pid {theirs.value}) is there, dev monitor {on_dev}")
        return False

    def rest(self, hwnd, x, y):
        """Rests the real pointer on client point (x, y) of hwnd after checking the window under it; False if refused."""
        point = wt.POINT(x, y)
        user32.ClientToScreen(hwnd, ctypes.byref(point))
        if not self.owns_point(hwnd, point.x, point.y):
            return False
        user32.SetForegroundWindow(hwnd)
        user32.SetCursorPos(point.x, point.y)
        return True

    def button(self, hwnd, x, y, down, right=False):
        if not self.rest(hwnd, x, y):
            return False
        if right:
            # The real right button (the quickbar's ring opened only now and then from posted ones).
            if down != self.right_down:
                user32.mouse_event(0x0008 if down else 0x0010, 0, 0, 0, 0)
            self.right_down = down
            return True
        if down != self.down:
            user32.mouse_event(0x0002 if down else 0x0004, 0, 0, 0, 0)
        self.down = down
        return True

    def restore(self):
        for vk in self.modifiers:
            user32.keybd_event(vk, user32.MapVirtualKeyW(vk, 0), 2, 0)
        if self.down:
            user32.mouse_event(0x0004, 0, 0, 0, 0)
        if self.right_down:
            user32.mouse_event(0x0010, 0, 0, 0, 0)
        user32.SetCursorPos(self.pointer.x, self.pointer.y)
        user32.SetForegroundWindow(self.foreground)


if __name__ == "__main__":
    import sys
    pid, out = int(sys.argv[1]), sys.argv[2]
    h = wait_window(pid, 5)
    capture(h).save(out)
    print("saved", out)


# ---------------------------------------------------------------- the development monitor

class MONITORINFOEX(ctypes.Structure):
    _fields_ = [("cbSize", wt.DWORD), ("rcMonitor", wt.RECT), ("rcWork", wt.RECT), ("dwFlags", wt.DWORD), ("szDevice", wt.WCHAR * 32)]


MONITORENUMPROC = ctypes.WINFUNCTYPE(wt.BOOL, wt.HMONITOR, wt.HDC, ctypes.POINTER(wt.RECT), wt.LPARAM)


def monitor_work_area(display):
    """The work area (left, top, right, bottom, desktop coordinates) of the monitor whose device name ends with
    `display` (e.g. "DISPLAY2"), or None when it isn't connected."""
    found = []

    def cb(hmon, _hdc, _rect, _):
        info = MONITORINFOEX()
        info.cbSize = ctypes.sizeof(MONITORINFOEX)
        user32.GetMonitorInfoW(hmon, ctypes.byref(info))
        if info.szDevice.upper().endswith(display.upper()):
            r = info.rcWork
            found.append((r.left, r.top, r.right, r.bottom))
        return True

    user32.EnumDisplayMonitors(None, None, MONITORENUMPROC(cb), 0)
    return found[0] if found else None


def virtual_origin():
    """Top-left of the whole desktop (Godot measures window positions from there)."""
    return user32.GetSystemMetrics(76), user32.GetSystemMetrics(77)  # SM_XVIRTUALSCREEN, SM_YVIRTUALSCREEN


def move_to(hwnd, x, y):
    """Moves a window without resizing it, changing its z-order or activating it."""
    user32.SetWindowPos(hwnd, None, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010)  # SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
