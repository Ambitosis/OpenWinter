# Creature-prefixed cast recovery

This authored public-API control isolates ordinary `CastAnim=out` on installed
rat appearance 386 (`APPEARANCE_TYPE_RAT`, also used by `nameplates`). It does
not claim legal stock spell availability for rats. Three matched, quiet actors
cast one harmless location-target spell: 1000 ms conjuring, 3500 ms recovery,
no projectile. The impact script checks spell/caster/location identity and
counts execution; an action queued after casting records completion. Position,
appearance, current action and event counts are recorded at each shot.

From left to right: STOCK is untouched; RELEASE replaces `ccastout` with
`ctaunt`; LOOP replaces `ccastoutlp` with `cdead` in the final fixture. The dead
pose is only an animation replacement: no death effect or HP change is applied.
Installed model candidates come from the existing `out/label-rat-model.txt`
inspection: `ccastout` 1 s, `ccastoutlp` 2 s, `ctaunt` 2 s and `cdead` 1 s.

```powershell
python tools/visual/visual.py run creaturecastloop --only nwn --native-framebuffer --tag _deadloop
```

The first `_reference` run used `ctaunt` for both replacements. Its center actor
visibly differs in `c_release_120`, supporting `ccastout`, but late recovery
looks the same across all actors. That run is inconclusive for recovery identity,
not a negative result. Its original source hash and notes remain in
`evidence.json`; a source copy is beside the local reference shots.

The second `_deadloop` run provides the distinguishable recovery control:

- `c_release_120`: STOCK and LOOP share the release movement while RELEASE
  differs, repeating the release-clip positive control.
- `d_release_170`, `e_recovery_240`, `f_recovery_330`, `g_recovery_440`:
  only LOOP lies on its side. This identifies `ccastoutlp` during recovery.
- `h_finished_560`: all three return to their normal idle pose.

Both runs exit 0 and all actor/appearance/stationary/identity/location gates
pass. Each actor executes exactly one impact and finishes exactly once:

| Run | LOOP impact | STOCK impact | RELEASE impact | All queue finishes |
| --- | --- | --- | --- | --- |
| reference | 1.529s | 1.559s | 1.590s | 5.027s |
| deadloop | 1.523s | 1.556s | 1.587s | 5.022s |

These are public script timestamps relative to each request. In the second
run, the right actor already shows the replaced recovery at the +1.680s note
for `d_release_170`; that framebuffer arrives about 0.120 s later, plus log
polling. This is earlier than a naive 1.0 s conjure plus full authored 1.0 s intro.
The evidence does **not** establish whether native compresses or truncates the
intro, nor an exact loop-transition timestamp. The center replacement is 2 s,
so its timing must not be assumed to preserve the stock 1 s intro. This fixture
supports clip identity and sustained recovery; it cannot justify a universal
natural-length transition rule or other prefixed cast families.

All 16 native frames were inspected. Framebuffer latency is approximately
0.116-0.151 s plus polling. Native framebuffer images are nonblack; repeated
GDI/window captures are excluded. Raw/frame PNG hashes, fixture-source hashes,
notes and gates are retained in `evidence.json`. Local artifacts are
`out/visual/shots/vis_creaturecastloop_{reference,deadloop}/nwn/`, with logs
`out/creaturecastloop-native.txt` and `out/creaturecastloop-native-deadloop.txt`.
No runtime or client changes were made while collecting this evidence.

## Client correction

The baseline snapshots contain `ccastout` throughout d/e/f/g: it repeats the
release rather than entering recovery. The fixed client contains `ccastout` at c,
`ccastoutlp` at d/e/f/g, and no action at h. All three actors preserve one impact
at +1.533 s and one queue completion at +5.033 s in both client runs. Both runs
pass actor, position, spell and location gates. Eight frames per client were
inspected beside the native dead-loop run; the right actor now shows the same
replaced recovery pose and returns to idle at completion.

The generic animator recognizes the measured `ccastout`/`ccastoutlp` pair. For
ordinary out spells, SpellFx supplies the existing release duration so this
handoff does not wait the full installed/replaced intro length. Stale action
versions, stopped actions, missing recovery clips and nonfinite times cannot
change the sequence. The real-Godot CastTimingProbe passes 22 checks, including
one/two-second creature intros, sustained recovery and cancellation.

Fixed captures: `out/visual/shots/vis_creaturecastloop_fixed/openwinter/`; baseline
uses `_baseline`. Notes, snapshots and hashes are appended to `evidence.json`.
Contact sheet: `out/creature-recovery-contact.jpg`. Exact preparation/release
poses still differ: compression versus truncation and per-frame speed/blending
remain unmeasured. This change establishes the phase transition and sustained
recovery, not complete creature casting animation parity. Other prefixed cast
families, live Haste changes and item casting remain outside this measured hook.
