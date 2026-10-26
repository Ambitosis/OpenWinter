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
  Creature.GetAttackBonus (base attack and ability only), Creature.JumpToLimbo, Ruby.Evaluate (there is no Ruby
  interpreter; the result is empty), Risenholm.ForceUpdateMageArmorStats (the plugin's MAGE_ARMOR armour rule is
  not documented, so only the stats are recomputed) and Risenholm.RemoveAttackImmunityFeedback (it is meant for the
  NWNX_Damage attack event, which combat does not run).
- **Services OpenWinter does not run** answer as if absent: Profiler scopes are matched but not recorded (one
  warning), Watchcat has no watchdog to disable, WSNetLayer reports no websocket players, and
  `NWNX_Core_PluginExists` returns FALSE for all four (and for Ruby) so scripts can check first.
- **Unknown Set/Get calls** use the generic compatibility store and warn that storing a value does not implement
  the engine behaviour.
- **Events.SubscribeEvent** warns, once per event name, when a script subscribes to an automatic `NWNX_ON_*` event
  outside the `NWNX_ON_DM_*` family that OpenWinter emits. The subscription is kept, so explicit script signals
  still work. Custom events and DM subscriptions do not warn.

`MissingCalls` counts every unsupported invocation, placeholders included. Plugin prefix and case aliases share
one warning, and calls from scripts name the calling script. Arguments and stored values are never logged.

This is OpenWinter's diagnostic policy; it makes no claim about NWNX or original-engine behaviour, and does not
certify every DM event name or complete plugin coverage.

## Settings

NWNX:EE is configured through environment variables, and so are OpenWinter's plugins: `NWNX_SQL_*` (type, host,
port, user, password, database, character set, UTF-8), `NWNX_TWEAKS_*`, `NWNX_NOSTACK_*`,
`NWNX_NWSQLITEEXTENSIONS_*`, `NWNX_HTTPCLIENT_REQUEST_TIMEOUT` and `NWNX_RISENHOLM_*` (the RPC listener and the AI
trimming switches) are read from the server's environment. `NwnxRegistry.EnvironmentVariable` lets a host supply
them another way; the parity lab gives each suite its `nwnxEnv`.

Features that reach outside the game are off unless the host is a dedicated server (`OpenWinter.Server`, which sets
`NwnxRegistry.DedicatedServer`; single player never does) and their own setting is on:
`NWNX_RISENHOLM_ALLOW_EXECUTE_COMMAND` (ExecuteCommand, which waits at most `NWNX_RISENHOLM_EXECUTE_COMMAND_TIMEOUT`
milliseconds), `NWNX_HTTPCLIENT_ENABLE` (requests otherwise fail at once with status -1), `NWNX_WEBHOOK_ENABLE`
(messages are otherwise only logged) and `NWNX_RISENHOLM_RPC_PORT` with its secret (the RPC listener). Each refusal
is reported once.

## Testing

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~Nwnx` covers warning deduplication for the native
and legacy calling conventions, generic store round trips, placeholder reporting, retained arguments, results and
storage, and subscription diagnostics.
