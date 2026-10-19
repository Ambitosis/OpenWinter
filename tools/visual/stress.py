"""Runs one scene on OpenWinter again and again to catch intermittent client crashes.

    python tools/visual/stress.py castle 10

Each run's output stays in out/visual/stress/<scene>_<n>.log; a summary of exit codes is printed.
"""
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import visual  # noqa: E402

scene, count = sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 10
cfg = visual.config()
mods = visual.build(scene)
out = visual.OUT / "stress"
out.mkdir(parents=True, exist_ok=True)
results = []
for n in range(count):
    for mod in mods:
        user = visual.prepare_user(cfg, "aluviandarkstar", (1280, 720), "default")
        shots = visual.OUT / "shots" / mod.stem / "openwinter"
        if shots.exists():
            shutil.rmtree(shots)
        taken = visual.run_openwinter(cfg, mod, user, shots, 90, "aluviandarkstar", (1280, 720))
        log = (shots / "godot.log").read_text(encoding="utf-8") if (shots / "godot.log").exists() else ""
        shutil.copy(shots / "godot.log", out / f"{mod.stem}_{n}.log") if (shots / "godot.log").exists() else None
        code = log.rsplit("[harness] exit code ", 1)[-1].strip() if "[harness] exit code" in log else "timeout"
        results.append((mod.stem, n, code, len(taken), "VISUAL|done" in log))
        print(f"{mod.stem} run {n}: exit {code}, {len(taken)} shots, done={'VISUAL|done' in log}", flush=True)
bad = [r for r in results if r[2] != "0" or not r[4]]
print(f"\n{len(bad)} of {len(results)} runs failed")
