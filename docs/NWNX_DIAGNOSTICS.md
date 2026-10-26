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

These plugins have no placeholders. Their behaviour was measured on nwserver with NWNX:EE (parity suites
`nwnx_area_props`, `nwnx_area_tiles`, `nwnx_area_objects`, `nwnx_tileset`, `nwnx_encounter`, `nwnx_reveal`); what
needs a player (visibility overrides, reveals to player observers) is covered by `NwnxAreaTests`. Known limits:

- Area changes the original only shows clients on their next load of the area (tiles and their animation loops,
  flags, fog clip distance, shadows, sun and moon colours, the day/night cycle, RotateArea) do the same here.
- ExportARE and ExportGIT write to the user directory's `nwnx` folder; as in the original, areas can't be created
  from the new files until the server restarts, and OpenWinter does not yet load that folder as a resource
  directory at start-up.
- GetPathExists samples the tiles' walkmesh rather than the original's tile path nodes, so paths through narrow
  gaps may be judged differently.
- Default UI discovery masks are kept and applied on the server; the client does not draw discovery masks yet.

## Testing

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~Nwnx` covers warning deduplication for the native
and legacy calling conventions, generic store round trips, placeholder reporting, retained arguments, results and
storage, and subscription diagnostics.
