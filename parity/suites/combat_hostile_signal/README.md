# Hostile event and damage combat-state controls

Four quiet NPC pairs stand 60 m apart, with each target 6 m from its caster.
The fixture verifies valid actors, exact stationary positions, own-target sight
and no sight of any other pair's caster. All actors have their AI scripts cleared.
Each pair receives exactly one of these public operations:

- `SignalEvent(target, EventSpellCastAt(caster, 601, TRUE))`, without damage.
- The identical event with `FALSE`, without damage.
- One point of magical `EffectDamage`, applied in the caster's context, without
  a spell event.
- No operation.

Target event callbacks verify caster, spell and harmful flag; the damage callback
verifies last damager, amount and actual one-point HP loss. State observations
record both combat flags and current actions. No stock spell actually casts in
this suite: it isolates the public event's core side effect from damage.

```powershell
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run combat_hostile_signal --only nwn
```

The first native run passes all actor/position/isolation/callback/HP gates.
The hostile event callback itself sees both flags clear; by +0.064 s both are
set despite zero damage. The damage-only callback already sees both set. The
nonhostile event and no-op control remain clear. Every current action remains
idle throughout, so setting a combat flag does not queue an attack.

An existing Debug Tools binary run against the same first fixture passes its
event identities, HP and control states, but leaves both flags clear for hostile
signal and damage. There are **18 combat-flag differences** in the first shared
samples. This baseline is retained; it predates the refinement points below.

A single native refinement adds early activation and 7.5/8/8.25/8.5 s samples,
and delays the damage trigger by a requested 0.37 s (observed 0.386 s). All gates
pass again:

| Trigger | Callback flags | First active sample | Last active | First clear |
| --- | --- | --- | --- | --- |
| Hostile spell event | 0 / 0 | +0.031 s | +7.523 s | +8.020 s |
| One-point damage | 1 / 1 | callback | +7.510 s | +8.009 s |

The nonhostile/no-op controls remain clear. These measurements are consistent
with about eight seconds relative to each trigger, but do not prove an exact
eight-second timer: the absolute last-active/first-clear intervals overlap by
about 0.124 s despite staggering, so a shared coarse update boundary is not
rigorously excluded. The exact post-callback activation instant is also unmeasured.

No runtime change or ready-animation selection is included. Scope is ordinary
quiet NPC pairs, not self/dead/invalid actors, players, active attacks or repeated
hostility refreshes. Full first native/Debug readings, the refined native readings,
state rows and bounds are retained in `evidence.json`; raw runs are under
`out/parity/results/combat_hostile_signal/`.


A final bounded run resolves the overlapping-bracket ambiguity with 25 ms
requested polling from relative 7 s, logging only each actor's first clear state
and preceding active sample. Native scheduling yields about 32-33 ms between
those samples:

| Trigger | Absolute trigger | Last active absolute | First clear absolute | Relative expiry interval |
| --- | --- | --- | --- | --- |
| Hostile event | 1565.052 | 1573.048 | 1573.081 | (7.996, 8.029] s |
| Damage | 1565.427 | 1573.428 | 1573.460 | (8.001, 8.033] s |

Caster and target match within each pair; both initial-active and completion
poll gates pass. The 0.375 s trigger separation becomes 0.379 s clearance
separation, with disjoint absolute expiry intervals. This excludes a single
shared coarse expiry boundary and supports an approximately **eight-second
relative lifetime**, with native tick/poll resolution retained. It does not
identify an exact sub-tick timestamp. All earlier raw evidence is preserved,
alongside the final polling run, in `evidence.json`.


## Implemented comparison and refresh follow-up

The scoped server implementation now enters combat after the harmful event's
callback, or before the direct positive-HP-loss damage callback, and keeps a
separate eight-second deadline without initiating attacks. A fresh full signal
comparison reports **621 matches, nine differences, zero missing**. Seven
failures are absolute-clock diagnostic offsets; two are combat flags at the
requested eight-second boundary (native sampled at +8.013 s, clear; OpenWinter at
+8.000 s, active). First-clear relative observations agree within the original
50 ms tolerance: hostile +8.028 versus +8.033 s; damage +8.035 versus +8.033 s.
The exact boundary mismatch remains recorded; no fixture tolerances changed.

The independent `combat_hostile_refresh` follow-up now proves the previously
unmeasured refresh behavior for ordinary quiet NPCs. After initial harmful
signals, a second harmful signal or one-point damage at +4.004 s keeps both
actors active at +8.505 and +11.505 s, then clear at +12.104 s. A nonhostile
signal leaves the original expiry intact, clear by +8.505 s. All 302 readings
match the compiled fix, with valid identity/position/isolation/HP/event gates
and idle actions throughout. Its complete evidence is in the neighboring
suite's `evidence.json`; refresh is no longer merely an implementation
assumption for these measured cases. Self/plot/dead actors, players, absorbed
or fatal damage and mixed physical attacks remain outside this control.
