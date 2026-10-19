# Scripted module transitions

`StartNewModule(name)` stages a new world after the current server tick. The calling script completes normally.
Missing, corrupt, path-traversal and invalid entry-area destinations leave the current world and connections
intact. Archive handles from failed loads and earlier transitions are released.

## What carries over

Matching the original:

- Connected characters keep their character data (gold, level, hit points), inventory and item locals.
- The new module receives fresh player characters: the old character's locals, self-references, permanent and
  temporary effects and queued actions do not survive.
- Sessions keep their login identities.
- The destination's module load, player enter, item acquire (one per carried item) and area enter hooks run, in the
  original's order. No client-leave hook runs in the old module, and no module-start hook in the destination.
- Old world timers stop. A fresh content announcement precedes each destination area packet, so module-embedded
  presentation resources reach the client (see [NETWORKING.md](NETWORKING.md#module-presentation-resources)).
- With no connected player characters, an existing destination is refused, as the original refuses it (it has no
  characters to carry over).

## Testing

- `python tools/parity/module_transition.py`: a missing module and an existing destination with no connected
  players, on nwserver and OpenWinter.
- `python tools/visual/module_transition.py --only nwn`, then `--only openwinter`: sandboxed, silent transitions of
  a real player character on both engines; `python tools/visual/check_module_transition.py` compares identity, load
  and enter order, and what survives. The destination frames are smoke captures: camera framing, HUD and lighting
  differ.
- `ModuleTransitionTests`: two connected sessions, packet ordering, inventory persistence and recovery from a
  failed load.

## Not measured yet

- Multiplayer on the original, DM and familiar possession during a transfer, persistent-world database behaviour,
  and choosing old saves across modules.
