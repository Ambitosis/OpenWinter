# Rest behaviour and presentation

Refusal and progress lines, the enemy range, recovery and what is scripted (wandering monsters) are in
[DEATH_AND_DYING.md](DEATH_AND_DYING.md).

## Starting rest

- The module's rest STARTED hook runs first and may cancel the rest.
- Then the resting player's magical effects are removed from every creature in the loaded world that carries them.
  The rule is the effect's creator plus the magical subtype, whatever its spell id and whether temporary or
  permanent. Effects created by others, and the player's own effects of other subtypes, stay.
- An assigned ClearAllActions is deferred, so the cleanup follows the hook's queued commands and rechecks
  cancellation.
- Losing one's own Constitution or temporary hit point buff is not treated as damage; later damage still cancels the
  rest.

In the stock-buff control (`reststockduration`), both recipients lose the player's Shield of Faith 0.3 s after
STARTED, with 595 s left on it, and cancelling fires event 3. The player's AC stays 20 and the other target's falls
from 15 to 12, in both engines.

## Finishing rest

- The usual cleanup keeps permanent supernatural and unyielding effects and restores the rest benefits.
- A player character's creature OnRested does not fire; the queued successor action precedes the module's
  FINISHED event.
- Module STARTED, FINISHED and CANCELLED are player events. A rest refused in a no-rest area emits none of them.
- An NPC's ActionRest completes at once. Its creature OnRested sees it resting (resting1, action 65535) with its
  effects still on, before cleanup. ForceRest does not run that hook.

## Animation

The client plays `sitcross` into the `sitcrossps` loop and plays `sitcross` in reverse to stand when the server ends
the rest. The reverse phase survives an appearance rebuild; death, new actions and cancellation interrupt it. The
loop, recovery and standing phases follow the original's sequence; exact blending speed and frame timing are not
measured.

## Testing

- Parity suite `rest_start_effects`: NPC rest and ForceRest effects and hook order
  (`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run rest_start_effects`).
- Visual scenes (`python tools/visual/visual.py run <scene>`): `restspellrules` (finishing, veto, no-rest refusal,
  effects created by others), `reststockduration` (a stock buff removed from both recipients), `restcreatorrules`
  (creator and subtype matrix, accepted and vetoed starts), `restpresentation` and `restplayercontrols`
  (presentation).
- `RestEffectTimingTests`: hook order, the cleanup matrix, veto and refusal, the NPC and ForceRest exceptions, and
  buff cleanup followed by real cancellation.

## Not measured yet

- Why the player's own AC stays unchanged in the stock-buff control (OpenWinter does not add a stat-cache delay).
- Cleanup of effects created by a resting NPC or by ForceRest, behaviour across areas, links mixing subtypes,
  area-of-effect objects, and removal callbacks that re-enter.
