# Seeker Arrow spiral flight timing

This script-only native control uses six separate equipped AA4 NPCs, each
executing `ActionUseFeat(451)` once against a stationary target. The unchanged
stock `x1_s2_seeker` script delivers event 601. Each pair records actual positions
and distance, ability/equipment/sight state, queued action completion, real stock
event time, final use count, event count and damage. No UI or original game assets
are included in this fixture.

```powershell
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run seekerarrow_flight --only nwn
```

Native safe-interior repeat, seconds after requesting the feat:

| Distance | Sight | Queue finish | Stock impact | `3 + d/(3 ln(d)+2)` | Residual |
| --- | --- | --- | --- | --- | --- |
| 2 m | 1 | 1.513 | 3.503 | 3.490 | +0.013 |
| 6 m | 1 | 1.513 | 3.834 | 3.814 | +0.020 |
| 10 m | 1 | 1.513 | 4.145 | 4.123 | +0.022 |
| 20 m | 0 | 1.513 | 4.834 | 4.820 | +0.014 |

Every pair passes requested-position checks, retains exactly its requested
distance through arrival, consumes its one feat use, emits exactly one real stock
event with the expected caster and damages the target. Sight zero at 20 m is an
observed state, not grounds to discard successful stock dispatch. Perception
settings are unchanged. The prior sweep also executes at all four distances but
clamps two caster/target x coordinates near the area edge; its full readings are
preserved in `evidence.json`. The repeat places all actors well inside the area.

The candidate request-to-arrival law above has maximum residual 0.023 s across
the four distances. The prior `0.5 + d/(3 ln(d)+2)` is short by about 2.5 s;
a best-fit constant has maximum error 0.755 s. Combining the candidate with the
independently measured 0.5 s cast-release phase gives a candidate spiral flight
time of **`2.5 + d/(3 ln(d)+2)`**. This fixture directly observes requests,
queued completion and stock impact, not the exact projectile-launch instant.
It does not establish below-one-metre/zero/negative/extreme-distance behavior or all other
resources using the spiral path. No runtime change is included here.

This is independent of the body-clip identity experiment in `seekerarrow`.
The native and fixed-client images also show differing post-cast ready stances
for caster/targets. Their combat-state cause is not established and is
outside this flight control. Full first/repeat readings, measured values and
candidate comparisons are retained in this folder's `evidence.json`; raw native
results are `out/parity/results/seekerarrow_flight/nwn.txt`.


A final bounded repeat adds 1 m and 1.5 m while retaining the four controls.
All six position and stationary-arrival gates pass; every ability executes once.
The new impacts are **3.518 s at 1 m** and **3.469 s at 1.5 m**, versus candidate
3.500/3.466 s. This explicitly rejects carrying the prior MIRV `distance <= 1`
0.1 s clamp into the spiral branch: that would predict 3.100 s at 1 m. The four
repeat controls give 3.501/3.815/4.130/4.832 s at 2/6/10/20 m, with action finish
1.500 s throughout. Below 1 m remains unmeasured. Complete final readings and
stationary checks are preserved alongside the earlier sweeps in `evidence.json`.

The runtime correction uses this positive-range timing and the independently
measured zero/negative-flight boundary from `projectile_spiral_short`. A fresh
comparison matches 149 readings and retains one difference: `d20.seen` is native
0 versus OpenWinter 1. All six timing, stationary position, real stock event, use and
damage checks match. The perception discrepancy was not removed from the suite
or changed as part of this fix.


One below-one-metre attempt requested 0/0.25/0.5/0.75 m against separate creature
objects. Native spawning separated their bodies to actual 1.082/1.040/1.100/1.050 m.
Every requested-position gate failed. Casts still consumed one use and emitted
one stock event at unchanged actual positions, but **no below-1 m timing is
established**. No overlaps were forced or further creature-distance probes run.
`sf_short.nss` preserves this rejected-layout entry point; selecting it through
`suite.json` `events.OnModLoad` reproduces the attempt. The default entry remains
`parity_main` for the accepted six-distance sweep. Both share `sf_inc.nss`.
