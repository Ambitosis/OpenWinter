# NPC rest effect and hook control

Run with `dotnet src/OpenWinter.Tools/bin/Release/net8.0/openwinter-tools.dll parity-run rest_start_effects --only nwn`.
Accepted original readings: `out/parity/results/rest_start_effects/nwn.txt`; log:
`out/rest-start-effects-native.txt`. This is a headless sandbox `nwserver` control.

Each effect is freshly constructed. NPC `ActionRest` completes immediately despite
the creature's positive `restduration.2da` duration. Its creature OnRested callback
observes `GetIsResting=1`, action 65535, and all eight pre-rest effects. Cleanup then
removes temporary magical/supernatural/extraordinary effects and permanent
magical/extraordinary effects, including a temporary effect added in that callback.
Permanent supernatural and both unyielding durations survive. ForceRest has the
same target-effect survivors and does not invoke creature OnRested.

The suite runs with Prelude's faction table (`"factions": "Prelude"`): without one every NPC faction likes
every other on nwserver, and the first runs had `reject.enemy=0`. Re-run with it, the actor
named `reject` (a Merchant with a Hostile creature 5 m away, `reject.enemy=1`) is refused: no OnRested, no
effect cleanup, its queued command still runs. OpenWinter matches (its rest refusal near an enemy).
Limits:
The actor named `cancel` has already finished when ClearAllActions runs at +0.5 s.
The `late` effect is applied after completion, not during rest. Neither proves PC
start or cancellation behavior; those are measured by `restspellrules` and
`restcreatorrules`. Caster-origin cleanup elsewhere is not measured for NPCs or
ForceRest. The initial rejected matrix reused one effect value, confounding its
identity; its readings/log remain in `out/rest-start-effects-first-rejected-*`.

`evidence.json` records final input and accepted output hashes. No original asset
was copied.
