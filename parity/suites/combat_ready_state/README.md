# Public combat state around bow and Seeker actions

This headless native control uses fresh, quiet pairs at six metres. One archer
makes a normal bow attack and receives `ClearAllActions(TRUE)` when the target
reports its first actual attack event. A separate archer uses stock Seeker Arrow
once through `ActionUseFeat(451)`. Neither stock impact scripts nor attack rules
are replaced. The fixture records `GetIsInCombat` and `GetCurrentAction` before
the requests, at 0.25/0.75/1.5/3/6/7 s after requests, after stopping, and after
the real stock spell event. A final sample occurs 14 s after the requests.

```powershell
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run combat_ready_state --only nwn
```

The first native run passes actual-position, six-metre stationary distance,
equipment and sight gates. The bow target reports exactly one real attack, and
the Seeker target reports exactly one stock 601 event from its assigned caster.
The bow archer retains its unused Seeker use; the Seeker archer spends one use.
No further attacks occur after the event-driven stop.

Times below are relative to each request. Combat columns are attacker/target;
action 65535 is idle, 3 is attack, 4 is cast.

| Observation | Time | Combat | Actions |
| --- | --- | --- | --- |
| Both pairs before request | 0 | 0 / 0 | idle / idle |
| Ordinary bow attack event, before stop | 0.172 | 1 / 1 | 3 / idle |
| After `ClearAllActions` | 0.234 | 0 / 1 | idle / idle |
| Bow request sample | 0.763 | 1 / 1 | idle / idle |
| Last sampled stopped-bow state | 7.186 | 1 / 1 | idle / idle |
| Seeker cast samples and queued finish | 0.251-1.516 | 0 / 0 | 4 then idle / idle |
| Seeker before arrival | 3.003 | 0 / 0 | idle / idle |
| Real Seeker impact event | 3.827 | 1 / 1 | idle / idle |
| Seeker impact +7 s | 10.832 | 1 / 1 | idle / idle |
| Final, both pairs | 14.016 | 0 / 0 | idle / idle |

Immediate stop does clear the attacker's combat flag, but that flag returns in a
later sample without a new queued attack. This suite does not isolate the cause
of that return. Fresh Seeker casting itself leaves both flags clear; they are set
by the time the real impact callback runs. Exact expiry boundaries are not
measured. Both pairs share standard factions in one area, so these observations
must not be promoted into a universal combat timer or faction-propagation rule.

The public flags and idle action state do not identify a model's ready animation.
No native pose or client comparison is included in this control. In OpenWinter,
`CombatSystem.Stop` clears the flag at once, its idle update expires `LastHostile`
after six seconds, and the client uses a generic `cpause1`/`nwreadyr` combat idle.
Those are leads, not a measured bow-ready mapping.

Full native readings, compact state rows and the initial OpenWinter baseline are
in `evidence.json`; raw output is `out/parity/results/combat_ready_state/nwn.txt`.
The baseline never enters combat from Seeker impact and clears stopped-bow state
differently. The follow-up `combat_hostile_signal`/`combat_hostile_refresh`
controls justify a scoped script-hostility fix. The stopped-bow re-entry/expiry
and visual pose differences remain open; this entire suite is not accepted as
matching merely because the separate script-hostility control is corrected.
