# Projectile model axis and visibility

Three quiet stock bandits cast authored location spells over exactly 20 m in
east, north and diagonal directions. Both rows use the installed `wamar_001`,
`Proj=1`, homing flight and path orientation. Row 0 has `HasProjectile=1`;
row 1 differs only in its label and `HasProjectile=0`. Each caster executes
both rows; the real impact script records spell, coordinates and elapsed time.

```powershell
python tools/visual/visual.py run projectileaxis --native-framebuffer --profile plain --openwinter-args="--visual-delay 0 --visual-state" --tag _fixed
```

The fixed comparison has six real impacts on each engine, correct spell IDs,
unchanged caster/target coordinates, and two final impacts per caster. Native
visible impacts occur at +1.458 s and hidden impacts at +1.447 s; OpenWinter records
+1.433 s for both. Hiding the model does not remove the authoritative flight
delay. Seven frames per engine were inspected. Native framebuffer PNGs are used;
the unreliable `_window.png` companions are excluded.

Visible frames show the native arrow nose following travel. The client baseline
points the shaft sideways; the fix aligns Aurora +Y with travel instead of +X.
The installed model's arrowhead extends along +Y (head y=0.095..0.168, tail near
-0.71). No model data is copied into this fixture. Both engines show no arrows
during the second, explicitly hidden wave, while still resolving all impacts.

Earlier one-wave controls are retained under `vis_projectileaxis_reference`
(HasProjectile omitted, no native arrows), `vis_projectileaxis_flag` (set to 1,
native arrows visible), and `vis_projectileaxis_baseline` (client sideways).
Those earlier inputs predate the two-wave fixture; current source hashes must
not be attributed to them. The explicit 0/1 comparison above is the accepted
visibility control. Captures are under `out/visual/shots/`, and the contact sheet
is `out/projectile-axis-contact.jpg`. `evidence.json` records accepted notes and
source/frame hashes.

The shared orientation helper also has direction/handedness and near-vertical
roll-continuity tests. Exactly vertical heading is inherently ambiguous for a
stateless orientation; native roll has not been measured. The final pole-edge
correction does not change the captured near-horizontal paths.

## Not matched yet

Native trails/streaks and precise projected positions differ. This fixture does
not isolate attachment, trajectory, render latency or trail contributions to
those differences. Spiral geometry remains a straight-path approximation.
Vertical/target-oriented presentation, moving targets and other model families
need separate controls. Hidden-projectile audio was not measured; the change
preserves existing audio and lifetime routing. This is an axis/visibility fix,
not complete projectile visual or audio parity.
