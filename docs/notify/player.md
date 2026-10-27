# Notify kinds: player package

Sent with `IWorldOutput.Notify(kind, subject, data, player)`. "Player" means the
notify goes only to that player's client; "area" means everyone in the subject's
area; "all" means every connected player (`World.NotifyAll`).

| kind | to | subject | data fields | meaning |
| --- | --- | --- | --- | --- |
| `command-reticle` | player | the controlled creature | `kind` = `move` / `attack` with `target`, `x`, `y`, `z` (area coordinates), or `clear` | Feedback for a clicked order. Move marks the planned stopping point until its move action ends; attack follows the target until that attack ends. Replaces previous feedback; talking creates none. |
| `camera` | player | the PC | `op` = `facing` (`direction`, `distance`, `pitch`, `transition`; -1 = keep current), `restore`, `mode` (`mode` CAMERA_MODE_*), `height` (`height`, 0 = racial default), `limits` (`minPitch`, `maxPitch`, `minDistance`, `maxDistance`; -1 = client config), `flags` (`flags` CAMERA_FLAG_*), `attach` (`target` object id or OBJECT_INVALID, `findClearView`), `move-rate` (`rate`) | Camera control (SetCameraFacing, RestoreCameraFacing, SetCameraMode, SetCameraHeight, SetCameraLimits, SetCameraFlags, AttachCamera, SetCutsceneCameraMoveRate) |
| `cutscene` | player | the PC | `enabled`, `leftClick` | SetCutsceneMode: hide GUI / block input (left-click world interaction if `leftClick`) |
| `fade` | player | the PC | `op` = `from-black` / `to-black` (with `speed`, FADE_SPEED_* per frame), `stop`, `black` | Screen fades |
| `gui-panel` | player | the PC | `panel` (GUI_PANEL_*) | PopUpGUIPanel: the client opens the panel even when it is disabled |
| `gui-death` | player | the PC | `respawn`, `waitForHelp`, `help` (text) | PopUpDeathGUIPanel |
| `panel-flash` | player | the PC | `button` (PANEL_BUTTON_*), `flash` | SetPanelButtonFlash |
| `gui-disable` | player | the PC | `panel`, `disabled`, `target` | SetGuiPanelDisabled: the client closes the panel if open and keeps it shut, answering the player's attempts with a `gui-event` request (GUIEVENT_DISABLED_PANEL_ATTEMPT_OPEN); see docs/UI.md "Panels scripts control" |
| `post-string` | player | the PC | `text`, `x`, `y`, `anchor` (SCREEN_ANCHOR_*), `life` (s), `rgba`, `rgba2` (0xRRGGBBAA), `id`, `font` | PostString overlay text |
| `targeting-mode` | player | the PC | `types` (OBJECT_TYPE_* mask, 0 = cancel), `cursor`, `badCursor`, `shape`, `sizeX`, `sizeY`, `flags`, `range`, `spell`, `feat` | EnterTargetingMode (+ SetEnterTargetingModeData); reply with `PlayerEvents.TargetSelected` |
| `audio-stream` | player | the PC | `op` = `start` (`stream`, `resref`, `looping`, `fade`, `seek`, `volume`), `stop` (`stream`, `fade`), `pause` (`stream`, `paused`, `fade`), `volume` (`stream`, `volume`, `fade`), `seek` (`stream`, `seconds`) | Audio streams |
| `boot` | player | the PC | `reason` | BootPC / NWNX DeletePlayerCharacter; the connection is closed right after |
| `vibrate` | player | the PC | `motor`, `strength` (0-1), `seconds` | Vibrate |
| `achievement` | player | the PC | `id`, `last`, `current`, `max` | UnlockAchievement |
| `pc-like` | player (both) | the PC | `other` (other PC id), `like` | SetPCLike / SetPCDislike, the player list's like / dislike |
| `player-list` | player | the PC | `players` (the other players): array of `id`, `name`, `party` (in your party), `leader` (leads her party), `partySize` (players in her party), `dislike` (you dislike her: Attitude), `dislikedBy` (she dislikes you: Reaction), `invitedMe`, `invitedThem` (open invitations); `leader` (you lead your party), `inParty` (a party of more than one), `oneParty` (One Party Only), `invitedBy` (who invited you, 0 = nobody), `dm`, `pvp` (the PvP setting where you are: 0 none, 1 party, 2 full), `pvpText` (dialog.tlk 8018-8020) | Answer to `player-list`, and sent to every player whenever a party, an invitation, a like or the players change (the player list, P; [PARTY.md](../PARTY.md)) |
| `party-invitation` | player | the invited PC | `from` (the inviting player), `name`, `open` (false: answered, expired or withdrawn), `text` (dialog.tlk 66776 with the name) | A party invitation to show (party_invitation.gui) or to close |
| `voice-chat` | area | speaking creature | `id` (VOICE_CHAT_*), `soundset`, `sound` (resolved SSF WAV resref) | PlayVoiceChat and quickchat; positional playback on Voices, localized text travels through normal speech |
| `mouse-cursor` | area | the object | `cursor` (MOUSECURSOR_*, -1 = default) | SetObjectMouseCursor |
| `ui-discovery` | area | the object | `mask` (OBJECT_UI_DISCOVERY_*) | SetObjectUiDiscoveryMask |
| `text-bubble` | area | the object | `mode` (OBJECT_UI_TEXT_BUBBLE_OVERRIDE_*), `text` | SetObjectTextBubbleOverride |
| `journal` | player | the PC | `tag`, `state`, `name`, `text`, `end`, `priority`, `picture`, `xp`, `day`, `time` (ms of day), `removed` = false; or `tag`, `removed` = true | Journal quest added/updated or removed |
| `nui-open` | player | the PC | `token`, `id`, `resref` (NuiCreateFromResRef) or `window` (NUI json) | Open a NUI window |
| `nui-close` | player | the PC | `token` | Close a NUI window (NuiDestroy or replaced by a window with the same id) |
| `nui-bind` | player | the PC | `token`, `bind`, `value` (json) | Bind value set by the server |
| `nui-group` | player | the PC | `token`, `element`, `layout` (json) | NuiSetGroupLayout |
| `portal` | player | the PC | `address`, `password`, `waypoint`, `seamless` | ActivatePortal |
| `start-module` | all | each PC | `module` | StartNewModule (server must load `World.PendingModule`) |
| `end-game` | all | each PC | `movie` | EndGame |
| `tlk-override` | all | each PC | `strref`, `text` ("" = removed) | SetTlkOverride |
| `pause` | all | each PC | `state` (GetGamePauseState bits: 1 timestop, 2 player pause) | SetGameActivePause |
| `nwnx-player` | player (or all) | the PC | `op` = `quickbar` (`slot`, `type`, `resref`, `label`), `floating-text` (`creature`, `text`), `placeable-usable` (`placeable`, `usable`), `name-override` (`target`, `name`, `playerNameState`), `hilite-color` (`target`, `color`), `mouse-cursor` (`target`, `cursor`), `visual-transform` (`target`, `transform`, `value`), `sound` (`sound`, `target`), `vfx` (`effect`, `target` or `x`,`y`,`z`), `vfx-loop` (`effect`, `target`), `music` (`day`, `track`), `music-play` (`play`), `battle-music` (`track`), `battle-music-play` (`play`), `timing-bar` (`seconds`, `type`), `timing-bar-stop`, `always-walk` (`walk`), `character-sheet`, `inventory` (`target`, `open`), `examine` (`target`), `tlk-override` (`strref`, `text`), `dm` (`enabled`), `exploration` (`area`, `data`), `visibility` (`target`, `value`) | Per-player client tweaks from the NWNX_Player / Rename / Visibility plugins |
| `creature-override` | area | the creature | `walkAnimation`, `damageLevel` or `limbo` | NWNX_Creature visual overrides |
| `area-setting` | area | the area | `setting` (`dayNightCycle`, `sunMoon`), `value`, `type` | NWNX_Area settings visible to clients |

Client to server (wired by the integrator): NUI events go to
`NuiService.HandleEvent(player, token, type, element, arrayIndex, payload)` and
client-side bind edits to `NuiService.ClientSetBind`; GUI events to
`PlayerEvents.GuiEvent`; target selections to `PlayerEvents.TargetSelected`.

The client NUI renderer (`client/UI/Nui/`) sends `nui-event` {token, type, element,
index, payload} for: `click` (buttons; list rows carry their row in `index`),
`mousedown` / `mouseup` (tagged elements; payload `{"mouse_btn": 0 left / 1 middle /
2 right, "mouse_pos": {"x","y"}}` relative to the element), `mousescroll` (payload
`{"mouse_scroll": {"x","y"}}`, y = +1 up), and window events with element `_window_`:
`open`, `close` (user closed it; the client drops the window), `focus`, `blur`. It
sends `nui-bind` {token, bind, value} when the user edits a bound value (whole array
for list rows), and for a bound `geometry` (after placement and after moving or
resizing) and bound `collapsed`.

The keys add these requests (`Requests/PlayerListRequests.cs`): `pause` toggles the player pause (bit 2 of
GetGamePauseState, answered with `pause` to everyone) in a single-player game only; `player-list` asks for the
`player-list` notify; `pc-like` {`other`, `like`} likes or dislikes another player, both ways, like SetPCLike /
SetPCDislike. Party management ([PARTY.md](../PARTY.md)): `party-invite` {`other`}, `party-answer` {`from`, `answer` = `accept` /
`reject` / `ignore`}, `party-leave`, `party-kick` {`other`} and `party-leader` {`other`} (Transfer Leadership).

The `voice-chat` client request accepts `{id}` only for the original selectable quickchat slots.
The speaker is the authenticated player; clients cannot choose another creature, soundset, or sound resource.
Death/internal grunts cannot be requested through this menu. Script PlayVoiceChat supports all 49 slots.
