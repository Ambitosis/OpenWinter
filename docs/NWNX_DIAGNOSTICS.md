# Unsupported NWNX calls

OpenWinter implements part of the NWNX plugin interface. Calls it cannot honour do not fail silently: each is
reported once per world through the `NwnxRegistry.Log` sink.

## What is reported

- **Unknown functions** get one warning each and return their empty result stack.
- **Registered placeholders** (handlers marked with `NwnxFunc.UnsupportedReason`) warn the same way. They keep
  their argument, result and storage behaviour; the warning says the engine effect is missing. They are:
  Damage.SetEventScript, Dialog.End, Visibility.SetVisibilityOverride, Item.SetWeight, Player.SetCustomToken,
  Player.PossessCreature, Util.SetResourceOverride, Feedback.SetMessageHidden,
  Administration.DeletePlayerCharacter (boots the player but keeps the vault file). NWNX_Creature has none: every
  function has its engine behaviour (measured by the `nwnx_creature_*` parity suites).
- **Unknown Set/Get calls** use the generic compatibility store and warn that storing a value does not implement
  the engine behaviour.
- **Events.SubscribeEvent** warns, once per event name, when a script subscribes to an automatic `NWNX_ON_*` event
  outside the `NWNX_ON_DM_*` family that OpenWinter emits. The subscription is kept, so explicit script signals
  still work. Custom events and DM subscriptions do not warn.

`MissingCalls` counts every unsupported invocation, placeholders included. Plugin prefix and case aliases share
one warning, and calls from scripts name the calling script. Arguments and stored values are never logged.

This is OpenWinter's diagnostic policy; it makes no claim about NWNX or original-engine behaviour, and does not
certify every DM event name or complete plugin coverage.

## Testing

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~Nwnx` covers warning deduplication for the native
and legacy calling conventions, generic store round trips, placeholder reporting, retained arguments, results and
storage, and subscription diagnostics.
