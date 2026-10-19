# Creature cast release boundary

A single authored native control follows `creaturecastloop` without changing
its fixture. Three rat actors execute the same harmless `out` cast with 1000 ms
conjuring and 3500 ms recovery. STOCK (left) is untouched; RELEASE (center)
replaces `ccastout` with `cdead`; LOOP (right) replaces `ccastoutlp` with `cdead`.
These replacements change body poses only. Both the original intro and `cdead`
have 1 s authored lengths in installed `c_rat` data.

```powershell
python tools/visual/visual.py run creaturecastboundary --only nwn --native-framebuffer --tag _reference
```

The original run exits 0. All three actors remain valid, appearance 386, exactly
stationary at their expected positions/facing. Each gets exactly one spell 0
impact at the correct location in its own caster context: LOOP +1.523 s,
STOCK +1.554 s, RELEASE +1.585 s. All three queued completion callbacks arrive
+5.026 s; all finish idle. Source and frame hashes, notes and gates are in
`evidence.json`.

| Frame | Actual script note elapsed | Framebuffer latency | Center intro marker | Right recovery marker |
| --- | --- | --- | --- | --- |
| b_release_110 | 1.108s | 0.121s | prone | release movement |
| c_boundary_135 | 1.359s | 0.118s | prone | release movement |
| d_boundary_155 | 1.554s | 0.115s | upright | prone |
| e_boundary_175 | 1.774s | 0.121s | upright | prone |
| f_recovery_240 | 2.403s | 0.120s | upright | prone |
| g_finished_560 | 5.615s | 0.120s | idle | idle |

The opposite marker changes bracket the transition around the public release
phase. They rule out holding the intro until its full authored length completes
at about request+2s. This supports handing `ccastout` to `ccastoutlp` on the
half-second release phase rather than waiting for the 1 s model clip. It does
**not** distinguish compression from truncation inside the intro, or establish
an exact transition timestamp, different cast families, Haste or other models.

Frame timestamps are later than script notes: capture latency is measured
separately, and log polling/serialized capture requests add delay. In particular,
closely spaced c/d/e requests are not exact script-time screenshots. All seven
native framebuffer images were inspected and are nonblack; repeated GDI/window
captures are excluded. The comparison relies on the opposite body markers and
their ordering, not a claimed millisecond boundary.

Artifacts: `out/visual/shots/vis_creaturecastboundary_reference/nwn/` and
`out/creaturecastboundary-native.txt`.
