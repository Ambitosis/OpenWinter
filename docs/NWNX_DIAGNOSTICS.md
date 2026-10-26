# Unsupported NWNX calls

OpenWinter implements part of the NWNX plugin interface. Calls it cannot honour do not fail silently: each is
reported once per world through the `NwnxRegistry.Log` sink.

## What is reported

- **Unknown functions** get one warning each and return their empty result stack.
- **Registered placeholders** (handlers marked with `NwnxFunc.UnsupportedReason`) warn the same way. They keep
  their argument, result and storage behaviour; the warning says the engine effect is missing. They are:
  Damage.SetEventScript, Dialog.End, Visibility.SetVisibilityOverride, Item.SetWeight, Player.SetCustomToken,
  Player.PossessCreature, Util.SetResourceOverride, Feedback.SetMessageHidden,
  Administration.DeletePlayerCharacter (boots the player but keeps the vault file),
  Creature.GetAttackBonus (base attack and ability only) and Creature.JumpToLimbo.
- **Unknown Set/Get calls** use the generic compatibility store and warn that storing a value does not implement
  the engine behaviour.
- **Events.SubscribeEvent** (and SubscribeEventScriptChunk) warns, once per event name, when a script subscribes to
  an automatic `NWNX_ON_*` event OpenWinter never raises: `NWNX_ON_ITEM_SCROLL_LEARN` (no scroll learning yet),
  `NWNX_ON_COMBAT_DR_BROKEN` (not reproduced on the original server), `NWNX_ON_WEBHOOK_*` (no WebHook plugin),
  `NWNX_ON_RESOURCE_*` (resource folders aren't watched) and `NWNX_ON_DEBUG_RUN_SCRIPT[_CHUNK]` (no debug console).
  The subscription is kept, so explicit script signals still work. Custom events and every other automatic event
  (`Nwnx/NwnxEvent.cs` lists them) do not warn. A few are raised only for client notices the OpenWinter client
  doesn't send yet (`inventory-close`, `inventory-panel`, `item-inventory`, `journal-close`, `character-close`,
  `device-property`, `export-character`, `force-move`, `timing-bar-end`; `Requests/NwnxClientRequests.cs`).

`MissingCalls` counts every unsupported invocation, placeholders included. Plugin prefix and case aliases share
one warning, and calls from scripts name the calling script. Arguments and stored values are never logged.

This is OpenWinter's diagnostic policy; it makes no claim about NWNX or original-engine behaviour, and does not
certify every DM event name or complete plugin coverage.

## Testing

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~Nwnx` covers warning deduplication for the native
and legacy calling conventions, generic store round trips, placeholder reporting, retained arguments, results and
storage, and subscription diagnostics.
