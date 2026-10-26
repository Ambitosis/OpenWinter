# Unsupported NWNX calls

OpenWinter implements part of the NWNX plugin interface. Calls it cannot honour do not fail silently: each is
reported once per world through the `NwnxRegistry.Log` sink.

## What is reported

- **Unknown functions** get one warning each and return their empty result stack.
- **Registered placeholders** (handlers marked with `NwnxFunc.UnsupportedReason`) warn the same way. They keep
  their argument, result and storage behaviour; the warning says the engine effect is missing. They are:
  Damage.SetEventScript, Visibility.SetVisibilityOverride, Item.SetWeight,
  Player.GetTURD and Player.RemoveEffectFromTURD (OpenWinter keeps no TURDs for players who leave),
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

## Testing

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~Nwnx` covers warning deduplication for the native
and legacy calling conventions, generic store round trips, placeholder reporting, retained arguments, results and
storage, and subscription diagnostics.

## Player, Administration, Chat, Dialog, Feedback, Rename and Util

- Per-player views (NWNX_Player's effects, sounds, music, fog, sky box, wind, highlight colours, cursors, discovery
  masks, visual transforms) go to that player's client alone, with the notify kinds the area-wide versions use. The
  views the client has no general notify for go as `nwnx-player` notifies (`{op, ...}`): the client applies
  `resman-override` (NWNX_Player_SetResManOverride) to the resources it loads from then on; timing bars, placeable
  usability, per-player TLK overrides and palette reloads are sent but not yet drawn.
- NWNX_Administration's settings are the server's own: the player password and the bans are checked at login, the
  level limits when a character enters, the play options are the rules' settings. Changes made before the server is
  attached (OnModuleLoad) carry over to it.
- The NWNX chat script runs for every line the server sends out (players' chat, SpeakString, ActionSpeakString,
  NWNX_Chat_SendMessage); a skipped line reaches no player or listening creature. Hearing distances apply to players.
- NWNX_Feedback hides feedback messages, combat log lines (by kind) and the journal-updated line where the server
  sends them; SendMessageToPC counts as combat log kind 11.

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~NwnxPlayerGroupTests` covers the calls that need a
connected player; the parity suites `nwnx_administration`, `nwnx_util`, `nwnx_chat`, `nwnx_feedback`, `nwnx_dialog`,
`nwnx_rename` and `nwnx_player` measure the rest against nwserver with NWNX.
