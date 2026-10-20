# Hostile combat-state refresh control

Three quiet ordinary NPC pairs stand 60 m apart on open floor, each target 6 m
from its caster. Every pair receives one public harmful `EventSpellCastAt` at
5 s. Four seconds later, the cases receive a second harmful event, a nonhostile
event, or one point of magical `EffectDamage` in the original caster's context.

State snapshots at +7.5, +8.5, +11.5 and +12.1 s separate refresh from the
original eight-second lifetime. The fixture verifies actor validity, positions,
own-target sight, cross-pair isolation, callback identity, spell ID, harmful
flag, event count and actual HP loss. Each callback has a numbered unique key.
All action readings must remain idle; this control does not execute a spell or
measure a ready animation.

```powershell
dotnet src/OpenWinter.Tools/bin/Release/net10.0/openwinter-tools.dll parity-run combat_hostile_refresh --only nwn
dotnet src/OpenWinter.Tools/bin/Debug/net10.0/openwinter-tools.dll parity-run combat_hostile_refresh --only openwinter
```

One native run and one current Debug OpenWinter run both completed successfully.
They produce **302 matching readings, zero differences and zero missing keys**
(excluding the completion marker), with no duplicate keys. Full readings and
the comparison are retained in `evidence.json`. The authored float
tolerances are 0.10 s for sample times and 0.01 m for distance; state and event
gates compare exactly. This control does not cover self/plot/dead actors,
temporary-HP absorption, fully resisted damage, physical attacks or players.


| Follow-up at native +4.004 s | +7.506 s | +8.505 s | +11.505 s | +12.104 s |
| --- | --- | --- | --- | --- |
| Harmful event | both active | both active | both active | both clear |
| Nonhostile event | both active | both clear | both clear | both clear |
| One-point damage | both active | both active | both active | both clear |

Every actor/position/sight/isolation/callback/HP gate passes. The harmful and
nonhostile cases each report exactly two spell callbacks, with the second
harmful flag respectively one and zero, and no damage. The damage case reports
one spell callback and one damage callback, from the expected caster, with
exactly one HP lost. Every sampled action remains idle. OpenWinter has identical
states at +7.5/+8.5/+11.5/+12.1 s.

This proves refresh for these two harmful follow-up types and no refresh for
the friendly signal. It is consistent with resetting the previously measured
eight-second lifetime; these wider snapshots do not independently resolve the
exact expiry tick.
