"""Compares what an OpenWinter multiplayer run's server held with what each client showed (tools/multiplayer/mp.py).

    python tools/multiplayer/compare.py out/multiplayer/mp_sync/openwinter

Checkpoints (each MPShot in the scene): for each client, the objects in its area that it should show and does
not, ones it shows that the server doesn't have there, and per object: position (the last one the server sent,
and the drawn one), facing, door state, death, HP bar, lasting visual effects told and drawn, animation versus
movement; the party it was told; then the two clients against each other; then the chat lines each got.
Samples (every 0.2 s): how far each client's view of every creature trailed the server's (distance at the same
moment and the time lag that fits best). Writes report.md beside the run and returns its text.
"""
import json
import math
import sys
from pathlib import Path

POSITION = 0.35      # metres: the last position the server sent versus the server's, at a checkpoint
DRAWN = 0.75         # metres: the drawn position versus the server's (the model eases towards the sent one)
FACING = 12.0        # degrees


def load(path):
    if not path.exists():
        return []
    out = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = line.strip()
        if line:
            try:
                out.append(json.loads(line))
            except json.JSONDecodeError:
                pass
    return out


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def angle(a, b):
    d = abs(a - b) % 360
    return min(d, 360 - d)


def label(obj):
    return f"{obj.get('kind', '?')} {obj.get('tag') or obj.get('name') or ''} #{obj['id']}"


def moving(o):
    return o.get("speed", 0) > 0.2


def settling_ids(server_samples, t, objects):
    """Creatures the server turned or moved in the 0.6 s before a checkpoint (a turn, a combat sidestep): clients
    show it a moment later, so their facing, drawn position and animation aren't compared (the sent position is)."""
    now = {o["id"]: o for o in objects if "facing" in o}
    settling = set()
    for s in server_samples:
        if t - 600 <= s["t"] <= t:
            for o in s["objects"]:
                c = now.get(o["id"])
                if c and "facing" in o and (angle(o["facing"], c["facing"]) > FACING or dist(o["pos"], c["pos"]) > 0.3):
                    settling.add(o["id"])
    return settling


def compare_client(name, player, server_objects, view, issues, turning=frozenset()):
    """One client's checkpoint against the server's."""
    area = player["area"]
    expected = {o["id"]: o for o in server_objects if o.get("area") == area and o["id"] in set(player["known"]) | {player["id"]}}
    in_area = {o["id"]: o for o in server_objects if o.get("area") == area}
    shown = {o["id"]: o for o in view["objects"]}
    if view.get("area") != area:
        issues.append(f"{name}: shows area {view.get('area')}, server has the player in {area}")
    for oid, o in in_area.items():
        if oid not in shown and o["kind"] in ("creature", "door", "item"):
            known = "known to the server" if oid in expected else "never sent"
            issues.append(f"{name}: missing {label(o)} ({known})")
    for oid, v in shown.items():
        o = in_area.get(oid)
        if o is None:
            issues.append(f"{name}: shows {label(v)} that the server doesn't have in {area}")
            continue
        tol = POSITION + (o.get("speed", 0) * 0.15)
        if "pos" in o and dist(v["net"], o["pos"]) > tol:
            issues.append(f"{name}: {label(o)} sent position {v['net'][:2]} vs server {o['pos'][:2]} ({dist(v['net'], o['pos']):.2f} m)")
        elif "pos" in o and oid not in turning and dist(v["pos"], o["pos"]) > DRAWN + o.get("speed", 0) * 0.3:
            issues.append(f"{name}: {label(o)} drawn at {v['pos'][:2]} vs server {o['pos'][:2]} ({dist(v['pos'], o['pos']):.2f} m)")
        if o["kind"] in ("creature", "item") and "facing" in o and not moving(o) and oid not in turning and angle(v["facing"], o["facing"]) > FACING:
            issues.append(f"{name}: {label(o)} facing {v['facing']} vs server {o['facing']}")
        if o["kind"] in ("door", "placeable") and v["open"] != o.get("open"):
            issues.append(f"{name}: {label(o)} open={v['open']} vs server {o.get('open')}")
        if o["kind"] == "creature":
            if v["dead"] != o["dead"]:
                issues.append(f"{name}: {label(o)} dead={v['dead']} vs server {o['dead']}")
            if v["hp"] != o["damageLevel"]:
                issues.append(f"{name}: {label(o)} HP bar {v['hp']} vs server {o['damageLevel']} ({o['hp']}/{o['maxHp']} hp)")
            drive = (v.get("anim") or {}).get("drive", "")
            if not o["dead"] and drive and oid not in turning and ((drive == "Stand") != (o["mode"] == "Stand")) and (moving(o) or drive != "Stand"):
                issues.append(f"{name}: {label(o)} animates {drive} while the server has it {o['mode']} at {o['speed']} m/s")
        if sorted(v.get("vfx", [])) != sorted(o.get("vfx", [])):
            issues.append(f"{name}: {label(o)} lasting visuals told {v.get('vfx')} vs server {o.get('vfx')}")
        if sorted(v.get("vfxDrawn", [])) != sorted(v.get("vfx", [])):
            issues.append(f"{name}: {label(o)} lasting visuals drawn {v.get('vfxDrawn')} of {v.get('vfx')} told")
    party = view.get("party")
    told = sorted(m["id"] for m in party["members"] if m.get("pc")) if party else [player["id"]]
    if told != sorted(player["party"]):
        issues.append(f"{name}: party {told} vs server {sorted(player['party'])}")


def compare_clients(names, views, server_objects, issues):
    """What both clients show of the same objects must agree."""
    (na, a), (nb, b) = zip(names, views)
    if a.get("area") != b.get("area"):
        return
    by_id = {o["id"]: o for o in server_objects}
    bo = {o["id"]: o for o in b["objects"]}
    for o in a["objects"]:
        other = bo.get(o["id"])
        if other is None:
            continue
        s = by_id.get(o["id"], {})
        tol = POSITION * 2 + s.get("speed", 0) * 0.3
        if dist(o["net"], other["net"]) > tol:
            issues.append(f"{na}/{nb}: {label(o)} at {o['net'][:2]} vs {other['net'][:2]}")
        for key in ("open", "dead", "hp"):
            if o[key] != other[key]:
                issues.append(f"{na}/{nb}: {label(o)} {key} {o[key]} vs {other[key]}")
        if sorted(o.get("vfx", [])) != sorted(other.get("vfx", [])):
            issues.append(f"{na}/{nb}: {label(o)} visuals {o.get('vfx')} vs {other.get('vfx')}")
        da, db = (o.get("anim") or {}).get("drive"), (other.get("anim") or {}).get("drive")
        if da and db and da != db and s.get("speed", 0) > 0.05:
            issues.append(f"{na}/{nb}: {label(o)} animates {da} vs {db}")


def interpolate(track, t):
    """The server's position of one object at time t (ms), from its samples."""
    if not track or t < track[0][0] or t > track[-1][0]:
        return None
    for (t0, p0), (t1, p1) in zip(track, track[1:]):
        if t0 <= t <= t1:
            f = 0 if t1 == t0 else (t - t0) / (t1 - t0)
            return [p0[0] + (p1[0] - p0[0]) * f, p0[1] + (p1[1] - p0[1]) * f]
    return None


def trailing(root, names, server_samples):
    """Per client and creature: mean and largest distance between its view and the server's at the same moment, and
    the lag (ms) that fits best, over the samples where the server had the creature moving."""
    tracks, speeds = {}, {}
    for s in server_samples:
        for o in s["objects"]:
            if "pos" in o:
                tracks.setdefault(o["id"], []).append((s["t"], o["pos"]))
                speeds.setdefault(o["id"], []).append((s["t"], o.get("speed", 0)))
    rows = []
    for name in names:
        samples = load(root / name / "samples.jsonl")
        per = {}
        for s in samples:
            for o in s["objects"]:
                if "net" in o and o["id"] in tracks:
                    per.setdefault(o["id"], []).append((s["t"], o["net"], o["pos"]))
        for oid, points in per.items():
            track = tracks[oid]
            moving_times = {t for t, v in speeds[oid] if v > 0.05}
            if not moving_times:
                continue
            lo, hi = min(moving_times) - 300, max(moving_times) + 300
            pts = [p for p in points if lo <= p[0] <= hi]
            if len(pts) < 3:
                continue
            def err(lag, which):
                d = [dist(p[which], q) for p in pts if (q := interpolate(track, p[0] - lag)) is not None]
                return (sum(d) / len(d), max(d)) if d else (float("nan"), float("nan"))
            sent_mean, sent_max = err(0, 1)
            drawn_mean, drawn_max = err(0, 2)
            best = min(range(0, 1001, 50), key=lambda lag: err(lag, 2)[0] if not math.isnan(err(lag, 2)[0]) else 1e9)
            rows.append((name, oid, len(pts), sent_mean, sent_max, drawn_mean, drawn_max, best))
    return rows


def report(root):
    root = Path(root)
    server_cps = {c["name"]: c for c in load(root / "server" / "checkpoints.jsonl")}
    server_samples = load(root / "server" / "samples.jsonl")
    names = sorted(p.name for p in root.iterdir() if p.is_dir() and p.name != "server" and (p / "checkpoints.jsonl").exists())
    clients = {n: {c["name"]: c for c in load(root / n / "checkpoints.jsonl")} for n in names}
    lines = [f"# Multiplayer sync report: {root.parent.name}", ""]
    total = 0
    for cp, s in server_cps.items():
        state = s["state"]
        issues = []
        views = []
        for n in names:
            player = next((p for p in state["players"] if p["player"].lower() == n.lower()), None)
            view = clients[n].get(cp)
            if player is None and view is None:
                continue  # not logged in (yet, or any more): nothing to compare
            if view is None:
                issues.append(f"{n}: no checkpoint (client missed it)")
                continue
            if player is None:
                issues.append(f"{n}: wrote a checkpoint but isn't logged in on the server")
                continue
            views.append(view["state"])
            lag = view["t"] - s["t"]
            if lag > 500:
                issues.append(f"{n}: checkpoint arrived {lag} ms after the server's")
            compare_client(n, player, state["objects"], view["state"], issues, settling_ids(server_samples, s["t"], state["objects"]))
        if len(views) == 2:
            compare_clients(names, views, state["objects"], issues)
        total += len(issues)
        lines.append(f"## {cp} ({len(issues)} differences)")
        lines += [f"- {i}" for i in issues]
        for n in names:
            view = clients[n].get(cp)
            if view and (view["state"].get("chat") or view["state"].get("floating")):
                lines.append(f"- chat to {n}: " + " / ".join(f"[{c['channel']}] {c['speakerName'] + ': ' if c['speakerName'] else ''}{c['text']}" for c in view["state"]["chat"])
                             + ("" if not view["state"].get("floating") else "  floating: " + " / ".join(f["text"] for f in view["state"]["floating"])))
        lines.append("")
    missing = [cp for n in names for cp in clients[n] if cp not in server_cps]
    if missing:
        lines.append(f"Client checkpoints the server never wrote: {sorted(set(missing))}")
    lines += ["## Trailing (moving creatures, sampled every 0.2 s)", "",
              "| client | creature | samples | sent mean m | sent max m | drawn mean m | drawn max m | best lag ms |", "|---|---|---|---|---|---|---|---|"]
    for row in trailing(root, names, server_samples):
        lines.append("| {} | #{} | {} | {:.2f} | {:.2f} | {:.2f} | {:.2f} | {} |".format(*row))
    lines.insert(1, f"{len(server_cps)} checkpoints, {total} differences.")
    text = "\n".join(lines) + "\n"
    (root / "report.md").write_text(text, encoding="utf-8", newline="")
    return text


if __name__ == "__main__":
    print(report(sys.argv[1]))
