"""Coverage regression evidence: metrics are not a claim of framebuffer parity."""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SHOTS = ROOT / "out/visual/shots"
artifacts = {}

def read(path):
    artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    return np.asarray(Image.open(path).convert("RGB"))

def erode(mask):
    return np.asarray(Image.fromarray(mask.astype(np.uint8)*255).filter(ImageFilter.MinFilter(5))) > 0

rows = []
for angle in range(0, 360, 45):
    name = f"hidden_{angle:03}.png"
    native = read(SHOTS / "vis_tilebacks_baseline/nwn" / name).max(2)
    before = read(SHOTS / "vis_tilebacks_baseline/openwinter" / name).max(2)
    after = read(SHOTS / "vis_tilebacks_occluders/openwinter" / name).max(2)
    assert (native > 20).sum() > 10000, "invalid native positive control"
    black = erode(native < 5)
    old = int(((before > 20) & black).sum())
    new = int(((after > 20) & black).sum())
    blocked = int(((after < 5) & erode((native > 20) & (before > 20))).sum())
    assert new < old, f"coverage did not improve at {angle}"
    name = f"known_{angle:03}.png"
    old_known = read(SHOTS / "vis_tilebacks_baseline/openwinter" / name)
    new_known = read(SHOTS / "vis_tilebacks_occluders/openwinter" / name)
    lost = int(((new_known.max(2) < 5) & erode(old_known.max(2) > 20)).sum())
    assert lost < 100, f"discovered scene lost coverage at {angle}"
    rows.append(dict(angle=angle, oldLightOnNativeBlack=old, newLightOnNativeBlack=new,
                     newlyBlackWhereNativeAndBaselineLit=blocked, discoveredNewBlackPixels=lost,
                     discoveredMeanChannelDelta=float(np.abs(old_known.astype(float)-new_known).mean())))

notes = []
for engine in ["nwn", "openwinter"]:
    path = SHOTS / f"vis_tilebackmap_settled/{engine}/notes.txt"
    artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    notes.append(path.read_text(encoding="utf-8").splitlines())
assert notes[0] == notes[1] and len(notes[0]) == 5, "settled/reveal/reset discovery masks differ"
probe_path = ROOT / "out/tile-occlusion-probe/result.json"
probe = json.loads(probe_path.read_text(encoding="utf-8"))
assert probe["passed"] and len(probe["checks"]) == 9
for path in (ROOT / "out/tile-occlusion-probe").glob("*.png"):
    read(path)
audit = json.loads((ROOT / "out/tile-geometry-audit.json").read_text(encoding="utf-8"))
assert not audit["problems"] and audit["mirrored"] == 0
for path in [ROOT / "out/tile-geometry-audit.json", probe_path]:
    artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
door_notes = []
for engine in ["nwn", "openwinter"]:
    path = SHOTS / f"vis_doorreveal_backfaces/{engine}/notes.txt"
    artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    door_notes.append([line for line in path.read_text(encoding="utf-8").splitlines() if "|map|" in line])
assert door_notes[0] == door_notes[1] and len(door_notes[0]) == 9
for engine in ["nwn", "openwinter"]:
    for angle in [45,135,225,315]:
        for phase in ["entry"]:
            frame = read(SHOTS / f"vis_tileentrybacks_closed_entry/{engine}/{phase}_{angle:03}.png")
            assert (frame.max(2)>20).sum()>10000, "invalid entrance camera control"
entry_notes=[]
for engine in ["nwn", "openwinter"]:
    path=SHOTS/f"vis_tileentrybacks_closed_entry/{engine}/notes.txt"
    artifacts[str(path.relative_to(ROOT))]=hashlib.sha256(path.read_bytes()).hexdigest()
    entry_notes.append(path.read_text(encoding="utf-8").splitlines())
assert entry_notes[0]==entry_notes[1] and len(entry_notes[0])==4
assert all(line.endswith("valid=1|open=0") for line in entry_notes[0])
for scene in ["vis_tilebacks_baseline", "vis_tilebackmap_settled", "vis_tileentrybacks_closed_entry"]:
    for path in (SHOTS / scene / "nwn").iterdir():
        if path.suffix == ".tga" or path.name == "native-framebuffer.jsonl":
            artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
for directory in ["tilebacks", "tilebackmap", "tileentrybacks"]:
    for path in (ROOT / f"parity/visual/scenes/{directory}").iterdir():
        artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
report = dict(passed=True, exactFramebufferMatch=False, renderedProbe=probe, nativeDiscoveryMasks=notes[0],
              geometryAudit=audit, coverage=rows, artifacts=artifacts,
              doorTransitionMasks=door_notes[0], entranceFramesChecked=8,
              entranceDoorStates=entry_notes[0],
              rejectedEntranceAttempt="_entrance: native transition door stayed closed when self-open was requested; client opened it. Those captures are excluded from the paired entrance control.",
              retainedDoorReference="Native doorreveal_settled captures; unchanged fixture, copied here for comparison.",
              limitations=["Distant room darkening still differs despite identical tile discovery bits.",
                           "Keyhole contours, lighting and flame particles are not pixel-identical.",
                           "The geometry audit is not an all-model, all-camera visual comparison.",
                           "Two installed tileset entries reference unavailable models; no substitute assets were created."])
out = ROOT / "docs/evidence/tile-occlusion.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8",newline="")
print(f"PASS: {len(rows)} camera pairs, 5 discovery masks, 9 rendered controls; {audit['models']} model references audited")
print(f"Native-black leaks: {sum(r['oldLightOnNativeBlack'] for r in rows)} -> {sum(r['newLightOnNativeBlack'] for r in rows)}")
