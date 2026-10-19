# Spell and special-ability presentation

The spell script is authoritative for affected targets, saves, damage, buffs and individual impact visuals.
Presentation follows the effects the script emits; the client never duplicates a spell's target selection (Bless) or
invents a fixed number of projectiles (Magic Missile). The same world renderer serves the classic and modern
interfaces.

## Game-data coverage

Run this against the installation being tested (omit `--base` to include module/hak overrides):

```
dotnet run --project src/OpenWinter.Tools -c Release -- spell-presentation-survey <out file>.json --base
```

The report lists every named spell and special ability with its impact script, cast/conjure models and sounds,
projectile parameters and every programmed visual-effect reference, and flags unresolved resource references
(the base game has unused or obsolete scripts and malformed `*****` placeholders among them). It is a resource
inventory, not proof that every ability looks or behaves correctly.

| Path | Handling |
| --- | --- |
| Conjure | `ConjAnim`, time, head/hand/ground models and sounds come from spells.2da. Ordinary self-cast hand models keep the same instances through live Haste changes and end at the earlier of the initially scheduled total and the recalculated one, then disappear without an extra fade. Removing Haste during preparation lengthens the conjure clip (0.75 s to 1.5 s for an ordinary cast) without restarting it or replaying the chant. |
| Cast | `CastAnim`, time and caster models come from spells.2da. Models finish their own animation/particle lifetime; cast sounds finish naturally. |
| Release | The server marks the exact release moment of a timed ordinary cast (`spell-release`); the client starts the release models, release sound and table-driven projectile from it. The original releases at ConjTime + 0.5 s, then recovers for CastTime (blank times are zero). |
| Script projectiles | All six ProgFX type 10 definitions launch from the effect creator. Magic Missile, flame/acid arrows and electrical MIRVs are script-generated, independent of `spells.2da Proj`. |
| Spell projectiles | Models, particles and lights start at release from the authored body anchor when `HasProjectile` is enabled; model visibility is separate from travel delay. Sounds keep their own lifetime. Pending projectiles cancel with interrupted casts; released ones continue. Area changes remove them. |
| Impact / duration | Authored model `impact`, `duration` and `cessation` animations are played. Target-specific effects come from script notifications, including Bless. |
| Programmed effects | ProgFX type 3 (static aura: textured surface lighting plus silhouette glow, RGB from the table), type 6 (pulsing aura: a linear A-to-B-to-A cycle, Param7 milliseconds per half-cycle, phase from activation), type 12 (spell-failure attachments) and type 13 (`FreezeAnimations`: holds the creature's pose and action clock until the last freeze is removed). Pointer/Tab highlighting temporarily overrides an aura, which returns afterwards; a newer aura overrides an older one until it expires. |
| Self-illumination | Static mesh `selfillumcolor` (ASCII MDL) and controller 100 (binary MDL) are read separately from the diffuse colour. |
| Beams / rays | `EffectBeam` has a separate data-driven renderer. |
| Casting body animation | Hand/head and five ordinary cast codes map to model clips: an intro (`conjure1`), then the release clip once (`castarea`), then its `cast*lp` recovery loop, not a repeated release. Creature models play `ccastout` then `ccastoutlp` from the release. The `creature` cast style plays `cspecial` once for non-instant, non-item casting ([Extract Brain](../parity/visual/scenes/extractbrain/README.md)). Equipped-bow `attack` casting plays `bowshot` once ([Seeker Arrow](../parity/visual/scenes/seekerarrow/README.md), row 601). |

### Spell projectile flight time

A spell with spells.2da `Proj` 1 schedules its impact at release, after a flight time worked out once from the 3-D
distance d to the target: d over a speed of 3 ln(d) + 2 m/s, doubled for homing, x1.5 for accelerating, x0.8 for
bounce, unchanged for ballistic and an empty `ProjType`; high ballistic and linked take a flat 2 s and spiral adds
2.5 s. A speed of zero or less (d up to e^(-2/3), about 0.513 m) lands at release, spiral included; just above that
the flight is very long (13.6 s at 0.52 m, 81 s at 0.5145 m), with no upper bound. A script path type above 4 makes
`ActionCastSpellAt*` do nothing. Measured by the parity suites `projectile_paths`, `projectile_near_boundary`,
`projectile_spiral_short`, `projectile_clock` and `projectile_far` (`SpellTable.ProjectileFlightTime`). Weapon
projectiles follow a separate rule.

The EE ProgFX table distinguishes orientation from path. Stock Magic Missile uses no model rotation and an upward
burst path; acid arrows follow their path and fly at 25 m/s, matching their script's impact delay. Other stock MIRVs
use the script's logarithmic flight-time formula. Upward burst splines approximate the original's random fan.

### Familiar arrival

A summoned familiar arrives with the `vff_summund` model (VFX_FNF_SUMMON_UNDEAD, row 36): a white ground burst and
its sound at the master's ground position. `SummonFamiliar` creates the associate before the first 0.1-second
sample on both servers.

## Testing

- Parity suites (`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run <suite>`): `cast_timing` and
  `cast_clock` (release and recovery), the `projectile_*` suites above and `associate_timing`.
- Visual scenes (`python tools/visual/visual.py run <scene>`; pairs land in `out/visual/compare`): `spellpresent`
  (a script-generated Magic Missile, flame arrow and acid arrow, Bless conjuring and impact, and a familiar summon,
  cast by the player at a stationary plot creature), `summonpresent`, `summonidentity` and `summonconfirm`
  (familiar arrival), `castclips` and `castphases` (cast clips), `creaturecastloop` and `creaturecastboundary`
  (creature casts), `hasteconjure` (live Haste), `aurapresent` and `pulsecycle` (auras), `progfxpresent` (animation
  freeze), and `aurabox` and `particleblend` (renderer calibration with authored models). None needs focus or the
  real mouse.

## Not matched yet

- Particle cores and brightness: the original adds Lighten particle values in display space, which the client does
  not reproduce. Aura halo size and brightness differ too. See also [EMITTERS.md](EMITTERS.md).
- Bless's large ring timing, caster phase timing, preparation/release pose speed and blending, and the randomised
  curvature of Magic Missile (the exact spline control points are not measured).
- Animated self-illumination; freeze resumption phase; spell-failure timing; delete-model/chunk effects; the
  vision/fading variants of type 4 lighting and type 7 through ordinary VFX.
- Custom projectile path types outside homing, ballistic, high-ballistic, accelerating and burst-up.
- Familiar placement: the original puts an NPC's familiar at (-1,-1) from the master and a player's at the master's
  exact position; OpenWinter's placement is not verified against both. Animal companion arrival has not been
  identified.
- Other bow cast rows (600, 602-604), non-bow `attack` rows (769, 802), other prefixed creature cast families,
  special cast codes and alternate creature sizes are unmeasured. Rebuilding in-progress cast presentation after
  loading a save is not established.
- This is shared rendering, not per-spell acceptance: each spell's complete timeline and statistics belong to the
  rule parity suites.
