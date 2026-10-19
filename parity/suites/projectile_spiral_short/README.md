# Short spiral projectile timing

This native script-only control adapts the existing `projectile_short` machinery.
An authored public `spells.2da` defines two harmless location-target spells with
the same 1.5 s conjure and 1 s recovery: one has no projectile, the other uses
the installed `wamar_001` model and `ProjType=spiral`. Actual impact script
callbacks record timing, target location and caster stationarity. No creature
overlap, asset modification or impact-script shortcut is involved.

```powershell
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run projectile_spiral_short --only nwn
```

One native run passes all twelve valid-position, stationary, target-location and
exactly-one-impact gates. Distances at impact remain the requested distances.

| Distance | No-projectile impact | Spiral impact | Difference |
| --- | --- | --- | --- |
| 0 m | 2.027 s | 2.023 s | -0.004 s |
| 0.25 m | 2.020 s | 2.023 s | +0.003 s |
| 0.5 m | 2.000 s | 2.023 s | +0.023 s |
| 0.75 m | 2.012 s | 5.172 s | +3.160 s |
| 1 m | 2.004 s | 5.012 s | +3.008 s |
| 1.5 m | 2.000 s | 4.980 s | +2.980 s |

At zero and the two sampled distances whose logarithmic denominator is negative,
the projectile adds no observable delay. At the remaining distances the delay
matches `2.5 + d/(3 ln(d)+2)` within native timer sampling. Therefore the added
2.5 s applies only when the logarithmic flight component is positive; adding it
after clamping a negative component to zero would contradict this control.
The exact denominator-zero point near 0.5134 m is not sampled. Negative distances
are not meaningful location inputs and have not been measured.

The stock Seeker Arrow suite independently establishes the positive-range curve
at 1-20 m through actual feat use and unchanged stock impacts. Its attempted
sub-metre creature targets were separated by the native engine and are not used
as short-distance evidence. This location control avoids that restriction and
isolates the public spiral path.

Full readings, gate results and candidate residuals are in `evidence.json`.
Raw native output is `out/parity/results/projectile_spiral_short/nwn.txt`.

After the runtime timing correction, a fresh native/client comparison matches
all 168 readings, including the twelve real impacts, unchanged target locations
and action completion. Log: `out/spiral-short-parity.txt`. The shared server
flight duration also drives client projectile lifetime; its geometric trajectory
remains a separate, visibly unmatched presentation issue.
