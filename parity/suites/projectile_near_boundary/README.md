# Projectile logarithm boundary

Twenty stationary casters cast an authored non-projectile control, then one of
five projectile paths at an exact location 0.52, 0.55, 0.6 or 0.75 metres away.
Public impact scripts record actual target location and impact time; the control
release is subtracted independently for each actor. All 40 casts hit once, and
queued completion, position and impact gates pass. These are authored diagnostic
spell rows, not installed game data.

`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run projectile_near_boundary --only nwn`

`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run projectile_near_boundary --only openwinter`

Native and client outputs are in `out/parity/results/projectile_near_boundary`.
All 221 readings agree within the fixture tolerances; the largest normalized
flight difference is 0.051 seconds. `evidence.json` preserves inputs, raw hashes
and all 20 paired flight measurements.

The original really takes 13.602 seconds for ballistic travel at 0.52 metres,
6.793 for homing, 9.086 for accelerating, and 16.125 for spiral. High ballistic
remains approximately two seconds. This is not grounds to replace measured
near-range timing with an assumed fast arrival.

The server now rejects non-finite distance and nonpositive denominators, and caps
ordinary spell projectile scheduling at 60 seconds. That cap applies only beyond
these measured values, including the narrower logarithmic singularity and extreme
input. It is an explicit safety bound, not a measured native maximum. Script MIRV
timing is unchanged. Tests cover the native matrix and finite/bounded results
around adjacent floating-point representations of the pole.
