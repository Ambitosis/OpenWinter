"""Save/reload a native parity module through its owned Windows server controls.

No desktop input or activation. Archives and logs stay inside out/parity. The server
uses its real Save Game and Load controls, not reconstructed object blueprints.
"""
import argparse
import ctypes
import ctypes.wintypes as wt
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/visual"))
import wincap

SLOT, NAME = 7, "ParitySave"


def controls(hwnd):
    found = []
    def visit(h, unused):
        name, cls = ctypes.create_unicode_buffer(512), ctypes.create_unicode_buffer(128)
        wincap.user32.GetWindowTextW(h, name, 512)
        wincap.user32.GetClassNameW(h, cls, 128)
        found.append((h, wincap.user32.GetDlgCtrlID(h), cls.value, name.value,
                      bool(wincap.user32.IsWindowEnabled(h))))
        return True
    wincap.user32.EnumChildWindows(hwnd, wincap.WNDENUMPROC(visit), 0)
    return found


def read_log(path):
    try:
        return path.read_text(encoding="cp1252").splitlines()
    except (FileNotFoundError, PermissionError):
        return []


def launch(cfg, user, module, port, difficulty, reload=False):
    exe = Path(cfg["gameRoot"]) / "bin/win32/nwserver.exe"
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 0  # SW_HIDE: nwserver.exe is a GUI program and would otherwise open a window
    mode = [] if reload else ["-module", module]
    proc = subprocess.Popen([str(exe), "-userdirectory", str(user), *mode,
        "-port", str(port), "-publicserver", "0", "-difficulty", str(difficulty),
        "-maxclients", "1", "-servername", "openwinter-save-parity"],
        cwd=exe.parent, startupinfo=si)
    try:
        hwnd = wincap.wait_window(proc.pid, 20)
        if not hwnd:
            raise RuntimeError("No owned server window")
        area = wincap.monitor_work_area(cfg["devDisplay"])
        wincap.move_to(hwnd, area[0] + 20, area[1] + 20)
        # Left hidden: its controls take window messages without being shown.
        if reload:
            load_saved(hwnd)
        return proc, hwnd
    except BaseException:
        stop(proc)
        raise


def stop(proc):
    if proc.poll() is None:
        proc.kill()
    proc.wait()


def wait_for(proc, log, marker, seconds):
    end = time.monotonic() + seconds
    while time.monotonic() < end and proc.poll() is None:
        lines = read_log(log)
        if any(marker in line for line in lines):
            return lines
        time.sleep(0.05)
    raise RuntimeError("Native save parity did not reach " + marker)


def send(hwnd, message, wparam=0, pointer=None):
    fn = wincap.user32.SendMessageTimeoutW
    fn.argtypes = [wt.HWND, wt.UINT, wt.WPARAM, ctypes.c_void_p, wt.UINT, wt.UINT,
                   ctypes.POINTER(ctypes.c_size_t)]
    fn.restype = ctypes.c_size_t
    result = ctypes.c_size_t()
    if not fn(hwnd, message, wparam, pointer, 2, 2000, ctypes.byref(result)):
        raise RuntimeError("Owned native control did not respond")
    return result.value


def load_saved(hwnd):
    end = time.monotonic() + 10
    by_id = {}
    while time.monotonic() < end:
        by_id = {c[1]: c for c in controls(hwnd)}
        if 1003 in by_id and 1000 in by_id and by_id[1000][3] == "Load":
            break
        time.sleep(0.1)
    combo, button = by_id.get(1003), by_id.get(1000)
    if not combo or combo[2] != "ComboBox" or not button or button[3] != "Load":
        raise RuntimeError("Native load controls unavailable: " + repr(list(by_id.values())))
    entries = []
    count = send(combo[0], 0x0146)  # CB_GETCOUNT
    if count > 4096:
        raise RuntimeError("Native module dropdown returned invalid count")
    for i in range(count):
        length = send(combo[0], 0x0149, i)  # CB_GETLBTEXTLEN
        if length > 4096:
            raise RuntimeError("Native module dropdown returned invalid text length")
        text = ctypes.create_unicode_buffer(length + 1)
        send(combo[0], 0x0148, i, ctypes.cast(text, ctypes.c_void_p))  # CB_GETLBTEXT
        entries.append(text.value)
    matches = [i for i, entry in enumerate(entries) if entry.startswith("Save: " + NAME + " -")]
    if len(matches) != 1:
        raise RuntimeError("Native save dropdown entry not unique: " + repr(entries))
    send(combo[0], 0x014E, matches[0])  # CB_SETCURSEL
    wincap.user32.PostMessageW(hwnd, 0x0111, (1 << 16) | 1003, combo[0])  # CBN_SELCHANGE
    end = time.monotonic() + 3
    while not wincap.user32.IsWindowEnabled(button[0]) and time.monotonic() < end:
        time.sleep(0.05)
    if not wincap.user32.IsWindowEnabled(button[0]):
        raise RuntimeError("Native Load button did not enable")
    wincap.user32.PostMessageW(hwnd, 0x0111, 1000, button[0])


def save(hwnd):
    found = controls(hwnd)
    by_id = {c[1]: c for c in found}
    for ident, cls in [(1029, "Button"), (1030, "Edit"), (1031, "Edit")]:
        if ident not in by_id or by_id[ident][2] != cls or not by_id[ident][4]:
            raise RuntimeError("Native Save Game control unavailable: " + str(ident))
    if by_id[1029][3] != "Save":
        raise RuntimeError("Unexpected native save button label")
    # SendMessageTimeout bounds a stalled server. WM_SETTEXT changes only these
    # two controls of the process we launched; no focus or global input required.
    for ident, value in [(1030, NAME), (1031, str(SLOT))]:
        text = ctypes.create_unicode_buffer(value)
        send(by_id[ident][0], 0x000C, 0, ctypes.cast(text, ctypes.c_void_p))
    wincap.user32.PostMessageW(hwnd, 0x0111, 1029, by_id[1029][0])  # WM_COMMAND/BN_CLICKED
    return [{"id": c[1], "class": c[2], "text": c[3], "enabled": c[4]}
            for c in found if c[1] in (1029, 1030, 1031)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("module", type=Path)
    parser.add_argument("user", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--difficulty", type=int, default=2)
    parser.add_argument("--seconds", type=int, default=45)
    args = parser.parse_args()
    cfg = json.loads((ROOT / "config/local.json").read_text(encoding="utf-8"))
    mod, user, output = args.module.resolve(), args.user.resolve(), args.output.resolve()
    safe = (ROOT / "out/parity").resolve()
    for target in (mod, user, output):
        if not target.is_relative_to(safe):
            raise RuntimeError("Save parity paths must stay under " + str(safe))
    (user / "modules").mkdir(parents=True, exist_ok=True)
    output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(mod, user / "modules" / mod.name)
    slot = user / "saves" / f"{SLOT:06d} - {NAME}"
    if not slot.resolve().is_relative_to(user):
        raise RuntimeError("Native save slot escapes its owned user directory")
    if slot.exists():
        # Exact fixed sandbox slot, resolved containment verified above.
        shutil.rmtree(slot)
    log = user / "logs/nwserverLog1.txt"
    engine_log = user / "logs/nwengineLog.txt"
    for path in (log, engine_log):
        path.unlink(missing_ok=True)
    proc, hwnd = launch(cfg, user, mod.stem, args.port, args.difficulty)
    try:
        before = wait_for(proc, log, "PARITY|__save__|1", args.seconds)
        observed = save(hwnd)
        archive = slot / (mod.stem + ".sav")
        end = time.monotonic() + 10
        previous, stable = 0, 0
        while time.monotonic() < end:
            size = archive.stat().st_size if archive.exists() else 0
            stable = stable + 1 if size and size == previous else 0
            previous = size
            if stable >= 10:
                break
            time.sleep(0.1)
        if stable < 10:
            raise RuntimeError("Native server did not finish writing a save archive")
        before = read_log(log)
        if any("Script error:" in line for line in before + read_log(engine_log)):
            raise RuntimeError("Native setup script failed before save")
        metadata = {"slot": SLOT, "name": NAME, "module": mod.stem,
            "archive": str(archive.relative_to(ROOT)), "size": archive.stat().st_size,
            "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
            "moduleSha256": hashlib.sha256(mod.read_bytes()).hexdigest(),
            "harnessSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
            "controls": observed, "before": before}
    finally:
        stop(proc)
    output.with_suffix(".json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8", newline="")
    log.unlink(missing_ok=True)
    proc, hwnd = launch(cfg, user, mod.stem, args.port, args.difficulty, reload=True)
    try:
        after = wait_for(proc, log, "PARITY|__done__|1", args.seconds)
        if any("PARITY|__save__|1" in line for line in after):
            raise RuntimeError("Reload restarted module setup instead of restoring saved state")
        after = ["PARITY|__reloaded__|1", *after, *read_log(engine_log)]
        output.write_text("\n".join(after) + "\n", encoding="utf-8", newline="")
        metadata["after"] = after
        output.with_suffix(".json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8", newline="")
        print("Native save/reload completed: " + str(archive))
    finally:
        stop(proc)


if __name__ == "__main__":
    main()
