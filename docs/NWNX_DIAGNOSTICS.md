# Unsupported NWNX calls

OpenWinter implements part of the NWNX plugin interface. Calls it cannot honour do not fail silently: each is
reported once per world through the `NwnxRegistry.Log` sink.

## What is reported

- **Unknown functions** get one warning each and return their empty result stack.
- **Registered placeholders** (handlers marked with `NwnxFunc.UnsupportedReason`) warn the same way. They keep
  their argument, result and storage behaviour; the warning says the engine effect is missing. They are:
  Damage.SetEventScript, Dialog.End, Item.SetWeight, Player.SetCustomToken,
  Player.PossessCreature, Util.SetResourceOverride, Feedback.SetMessageHidden,
  Administration.DeletePlayerCharacter (boots the player but keeps the vault file),
  Creature.GetAttackBonus (base attack and ability only) and Creature.JumpToLimbo.
- **Unknown Set/Get calls** use the generic compatibility store and warn that storing a value does not implement
  the engine behaviour.
- **Events.SubscribeEvent** warns, once per event name, when a script subscribes to an automatic `NWNX_ON_*` event
  outside the `NWNX_ON_DM_*` family that OpenWinter emits. The subscription is kept, so explicit script signals
  still work. Custom events and DM subscriptions do not warn.

`MissingCalls` counts every unsupported invocation, placeholders included. Plugin prefix and case aliases share
one warning, and calls from scripts name the calling script. Arguments and stored values are never logged.

This is OpenWinter's diagnostic policy; it makes no claim about NWNX or original-engine behaviour, and does not
certify every DM event name or complete plugin coverage.

## Area, Tileset, Encounter, Reveal and Visibility

These plugins have no placeholders. Most of their behaviour was measured on nwserver with NWNX:EE (parity suites
`nwnx_area_props`, `nwnx_area_tiles`, `nwnx_area_objects`, `nwnx_tileset`, `nwnx_encounter`, `nwnx_reveal`). The NWNX
server runs without players, so some of it is implemented from the NWNX headers alone and is **not measured**:

- what a visibility override does to the objects a player's client receives (only the stored values are measured);
- what a reveal does for a player observer, when it ends and who counts as the party (only that NPC observers are
  unaffected is measured);
- NWNX_Area_TestDirectLine: calling it on the generated parity area crashed nwserver. It follows the header's codes
  (1 clear, -1 terrain, -2 a placeable or closed door, -3 a creature, using the given personal space) but ignores
  the creature height and may judge borderline cases differently.

`NwnxAreaTests` covers those parts. Other known limits:

- Area changes the original only shows clients on their next load of the area (tiles and their animation loops,
  flags, fog clip distance, shadows, sun and moon colours, the day/night cycle, RotateArea) do the same here.
- ExportARE and ExportGIT write to the user directory's `nwnx` folder. The ARE and the GIT's own fields (lists,
  area properties, variables, UUID) match the original's files (`nwnx_area_objects` compares them); the objects in
  the lists are written in OpenWinter's save format, which carries different fields from the original's. As in the
  original, areas can't be created from new files until the server restarts, and OpenWinter does not yet load that
  folder as a resource directory at start-up.
- GetPathExists samples the tiles' walkmesh (once per area until it changes) rather than the original's tile path
  nodes, so paths through narrow gaps may be judged differently.
- Default UI discovery masks are kept and applied on the server; the client does not draw discovery masks yet.

## Testing

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~Nwnx` covers warning deduplication for the native
and legacy calling conventions, generic store round trips, placeholder reporting, retained arguments, results and
storage, and subscription diagnostics.
