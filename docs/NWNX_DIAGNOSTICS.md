# Unsupported NWNX calls

OpenWinter implements part of the NWNX plugin interface. Calls it cannot honour do not fail silently: each is
reported once per world through the `NwnxRegistry.Log` sink.

## What is reported

- **Unknown functions** get one warning each and return their empty result stack.
- **Registered placeholders** (handlers marked with `NwnxFunc.UnsupportedReason`) warn the same way. They keep
  their argument, result and storage behaviour; the warning says the engine effect is missing. They are:
  Damage.SetEventScript, Item.SetWeight,
  Player.GetTURD and Player.RemoveEffectFromTURD (OpenWinter keeps no TURDs for players who leave). NWNX_Creature
  has none: every function has its engine behaviour (measured by the `nwnx_creature_*` parity suites); only
  SetMulticlassLimit with bPersist warns, as that limit isn't kept in the character file (the other bPersist
  settings are, in NWNX_POS as NWNX:EE keeps them).
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

## Player, Administration, Chat, Dialog, Feedback, Rename and Util

- Per-player views (NWNX_Player's effects, sounds, music, fog, sky box, wind, highlight colours, cursors, discovery
  masks, visual transforms) go to that player's client alone, with the notify kinds the area-wide versions use. The
  views the client has no general notify for go as `nwnx-player` notifies (`{op, ...}`): the client applies
  `resman-override` (NWNX_Player_SetResManOverride) to the resources it loads from then on; timing bars, placeable
  usability, per-player TLK overrides and palette reloads are sent but not yet drawn.
- NWNX_Administration's settings are the server's own: the player password and the bans are checked at login, the
  level limits when a character enters, the play options are the rules' settings. Changes made before the server is
  attached (OnModuleLoad) carry over to it, and they survive a saved game replacing the world.
- Only IP bans rest on what the server observes. The CD key (the client's player key) and the player name are what
  the client says; until keys are verified, CD key and name bans and the CD key allow list stop honest clients only.
- The NWNX chat script runs for every line the server sends out (players' chat, SpeakString, ActionSpeakString,
  NWNX_Chat_SendMessage); a skipped line reaches no player or listening creature. Hearing distances apply to players.
- NWNX_Feedback hides feedback messages, combat log lines (by kind) and the journal-updated line where the server
  sends them; SendMessageToPC counts as combat log kind 11. Which lines nwserver hides needs a player to measure and
  is not matched; whitelist mode follows the header (hidden unless the global state is set to FALSE).
- NWNX_Util's AddScript and AddNSSFile write to the NWNX folder whatever sAlias names. CleanResourceDirectory and
  RemoveNWNXResourceFile only ever delete inside the NWNX folder and refuse names that aren't plain resource names;
  OpenWinter servers have no custom resource directory file, so other aliases fail. GetUserDirectory is the folder the
  server writes to (a sandbox in development runs).
- Voice captions (PlayVoiceChat, battle cries) don't run the chat script; that path is unmeasured on nwserver.

`dotnet test tests/OpenWinter.Tests --filter FullyQualifiedName~NwnxPlayerGroupTests` covers the calls that need a
connected player; the parity suites `nwnx_administration`, `nwnx_util`, `nwnx_chat`, `nwnx_feedback`, `nwnx_dialog`,
`nwnx_rename` and `nwnx_player` measure the rest against nwserver with NWNX.
