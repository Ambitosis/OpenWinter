# Controls: the original's keys

Every key is a rebindable action in `client/Settings/Model/Bindings.cs` (`ActionCatalog`), listed in
Options → Controls with its default. The defaults are the original game's, so players' muscle memory works.

Movement clicks ray-pick walkable ground through rendered walls. A ray with no floor keeps a projected
movement attempt but shows the no-walk X; invalid destinations do not get a green command reticle.
Rapid clicks replace the route without interrupting the current locomotion cycle. A short click issues
its destination on release; holding for 0.2 s enters continuous mouse driving.
Ground clicks resolve the Walk modifier from the click event's own modifier flags, so an unmodified click runs even
if an earlier Shift release was missed. Always Walk inverts the held Walk binding; rebound walk keys and chords are
supported. Walk is a per-click choice: it does not toggle a persistent movement mode.
The orbit reaches exactly overhead (Insert or full upward tilt). Conversations keep the orbit attached
to the controlled character while they move; stationary conversation owners turn to follow them.

Mode toggles show the game's localized activation/deactivation message above the character in
both interfaces. Keen Sense (normally granted to elves) supplies active searching automatically:
Detect is omitted from their toggle menu, its key cannot disable it, and it does not force walking.
Defensive Casting has its own mode state and never changes the Detect key's state.

## Where the defaults come from

The original lists its rebindable actions in `keymap.2da` (rows `Yaw-` … `ToggleKeyhole`; dump it with
`dotnet run --project src/OpenWinter.Tools -c Release -- 2da keymap 300 --module <a .mod>`). The `Language0`
column is the default key as a number. The client writes no keymap file until a player changes a key, so
the numbers were decoded from public facts:

- **Letters.** `dialog.tlk` holds the key-name table the keymap screen shows (strrefs 10617–10683: Esc,
  Keyboard Enter, Print Screen, …, Space, A … Z). From Tab to Z the codes follow that table exactly:
  code = strref − 10579. So Tab 68, `[` 70, `]` 71, Space 78, A 79 … Z 104. Every letter default this gives
  matches something documented: I inventory, J journal, C character, B spells, M map, O options (the v74
  release notes: "the default 'O' key"), Y instant kill (v74: "default 'Y'"), G quick save (players' forum
  answers), W / S / A / D / Q / E driving (the original's own tutorial text, strref 49416).
- **Tab** is the tooltip key: the v79 release notes say so ("tooltip (TAB, by default)").
- **Numpad.** The radial rows (`RadialSW` 54 … `RadialNE` 62, with `RadialCancel` 58 in the middle and
  `ActivateOwnRadial` 53) only fit numpad 0–9 as 53–62. Code 64 (`CameraModeToggle`) continues the strref
  order down from Tab (Tab 68, `'` 67, Numpad Enter 66, `/` 65, Numpad * 64) and players confirm Num * toggles
  the camera mode.
- **Navigation keys.** The tutorial says the arrow keys rotate and zoom the camera (strref 49410); old
  guides list Home / End as zoom in / out full, Insert / Delete as camera up / down full and Page Up / Down
  as camera up / down; Print Screen is the screenshot key. The codes below 53 are not in strref order, so
  they were matched by these descriptions.

| Action (keymap.2da row) | Code | Key | Our action |
|---|---|---|---|
| Yaw- / Yaw+ | 40 / 41 | Left / Right | Rotate camera left / right |
| ZoomIn / ZoomOut | 42 / 43 | Up / Down | Zoom in / out (also Num + / Num −, ours) |
| Tilt+ / Tilt- | 44 / 45 | Page Up / Page Down | Camera up / down |
| ScreenCapture | 48 | Print Screen | Screenshot |
| PitchMin / PitchMax | 49 / 50 | Insert / Delete | Camera up full / down full |
| ZoomInFull / ZoomOutFull | 51 / 52 | Home / End | Zoom in / out full |
| ActivateOwnRadial | 53 | Num 0 | Radial menu on self |
| RadialSW, S, SE, W, Cancel, E, NW, N, NE | 54–62 | Num 1–9 | Radial menu wedge by direction; Num 5 back |
| CameraModeToggle | 64 | Num * | Toggle camera mode |
| Tooltip/Inspect | 68 | Tab | Tooltips (hold, double-tap to keep) |
| ToggleKeyhole | 68 | (Tab) | Toggle keyhole dissolve: no key (see below) |
| Previous / Next | 70 / 71 | [ / ] | Previous / next inventory page |
| Pause | 78 | Space | Pause |
| DriveModeTurnLeft / TurnRight | 79 / 82 | A / D | Turn left / right |
| DriveModeLeft / Right | 95 / 83 | Q / E | Strafe left / right |
| DriveModeForwardRun / Backward | 101 / 97 | W / S | Move forward / backward |
| DriveModeForward | 0 | none | (walk forward: Shift + W walks) |
| Spells | 80 | B | Spellbook |
| CharSheet | 81 | C | Character sheet |
| QuickSave | 85 | G | Quick save |
| ToggleGUI | 86 | H | Hide / show interface |
| Inventory | 87 | I | Inventory |
| Journal | 88 | J | Journal |
| Map | 91 | M | Map |
| Options | 93 | O | Options |
| PvP | 94 | P | Player list (PvP) |
| Rest | 96 | R | Rest |
| QuickChat | 100 | V | Quick chat |
| QuickGroupSet0–3 / Pick0–3 | none | none | Set / select quick group 1–4 |
| DMCreator, CMChooser, Instagib, ModuleDebugInfo, JumpToPoint, GetDebugInfo | 75, 76, 103, 102, 47, 46 | | DM and debug keys: not bound here |

OpenWinter's extra actions never take a key the original uses for something else (Ctrl + F-keys, for example,
are the original's third quickbar). The extras with default keys are Attack F, Combat stance X (X is also the
original's DM-only module debug key), Detect T and Chat Enter.

## Clicking containers

Clicking a container walks to the front use point authored in its PWK, stopping 0.8 m away (or at the
creature's larger minimum clearance). If the path fails or movement stalls briefly, the old 1.6 m horizontal
reach is accepted so furniture does not cause repeated stalled approaches. Other placeables keep their
1.6 m range. The player faces the container and plays the short `getmid` reach; the clicked use
action lasts half a second. Scripted use and `/loot` do not turn the player or play the reach.
The container model plays `close2open` into `open` when its open flag arrives, and `open2close` when the loot
window closes. The model clips come from the installed game data; the lid opens a little earlier than in the original.
Scenes `chestuse`, `chestbed` and `chesttable` capture the approach and opening on both engines.

Clicking the container currently being looted closes it. Any change in the player's position also
closes its inventory, even within interaction range; turning in place and inventory refreshes leave
it open. Both paths use the shared close service, including `OnClosed` / `GetLastClosedBy` and the lid
sound. (These two closing rules are not yet compared with the original's input handling.)
Ground pickup and container loot confirm successful acquisition with TLK 10468 (`Acquired Item`)
and the item's inventory placement sound. Rejected transfers and owned-grid rearrangements do not
emit acquisition feedback; a merged stack reports once. Both UI modes use the server acknowledgement,
and a dragged container item suppresses its speculative local placement sound to avoid double playback.

## What the keys do

- **Click feedback.** Ground orders show the original animated green `gui_walkto` marker; placeable/door/item
  approaches show it at the planned stopping point. Green clears when that particular move ends, fails or is
  replaced. Talking to an NPC creates no marker. Clicking a hostile creature shows `gui_hostile*` under the
  target until the attack ends; it follows the target. These markers use server-confirmed orders in both UI
  styles, independently of HUD selection. Keyboard steering, action cancellation and area changes clear them.

- **Orbit response.** Mouse dragging and camera turn keys apply the orbit angle directly, without angular
  easing or drift after release. Only the camera's arm length eases outward (zoom/collision recovery);
  collision pulls inward immediately. Chase-mode automatic turning retains its separate follow behaviour.
- **Camera modes** (`CameraMode`, numbered as `CAMERA_MODE_*`). Top down is the original's default: the
  camera stays where you turn it. Chase "automatically rotates to follow you as you move". Driving (the
  scripting name is stiff chase) stays right behind you at a fixed distance of 6 (the original does not let it
  zoom) and no flatter than 62° from straight down. Num * steps top down → chase → driving, the order the
  tutorial introduces them; Options → Gameplay sets it too, and SetCameraMode changes it for the session.
  Camera arrow keys remain separate from character driving. The chase cameras follow forward movement
  and clicks, while top down retains its yaw when the character turns.
- **Character driving.** W runs forward, S walks backward, Q/E strafe relative to the character's facing.
  A/D turn the character, including while stationary: a fresh press completes 30 degrees at the held-turn
  rate, then holding continues the turn. Rapid taps share that angular cap; input edges cannot add instant
  rotation. Q/E with A/D travel in a curve. The server owns direction, collision and speed; physical
  driving never uses camera yaw. The original `pauseturn` and `pausewalkl/r` animations
  supply the footwork. Releasing W/S/Q/E finishes the last step: the measured PC takes 1.6 m forward,
  0.8 m backward or a 0.8 m sidestep on a tap. Holding renews the step; a new direction replaces it.
  A/D release finishes any uncompleted part of the initial 30-degree turn while the footwork completes.
  Movement clips fade into each other using model transition times, and settle to idle over 0.1 s.
  Small speed-rounding changes during keyboard steering retain the current stride instead of
  repeatedly restarting the run clip; destination braking still blends down as speed falls.
  Visible facing catches up at about 360 degrees/second, separately from the slower held-key steering rate.
  Shift/Always walk select walking. The scripted capture/autopilot input uses a separate world-direction message.
- **Held left mouse.** After 0.2 seconds, the pointer becomes one of the installed 16-direction arrows
  (22.5-degree steps). The target is repicked on walkable ground while held. Nearby targets slow movement
  continuously below ordinary walking speed; farther targets accelerate toward running speed. Walking
  and running use separate clips with stride rates following actual speed. Arrow length is independent:
  the long arrow starts at about two metres, so a short arrow can accompany running. Releasing, losing
  focus, entering a blocking menu or moving over UI stops driving. Targeting and ordinary object clicks
  retain their own handling. The speed curve is fitted to the original's and not yet exact.
- **Footsteps.** Model `snd_footstep` events time contacts for walking, running, turning and strafing.
  The server sends the ground surface and effective footstep type (`SetFootstepType` override or
  `appearance.2da` FOOTSTEPTYPE). The client maps `surfacemat.2da` Sound families to the three variant
  columns in `footstepsounds.2da` and loads samples from installed resources. The none type is silent;
  sounds use the Effects3D bus and positional attenuation.
- **Previous / Next** are "Previous / Next Inventory Tab" in the original (strrefs 67745 / 67743), not
  target cycling: with the inventory open, [ and ] turn the backpack's page (the modern view steps through its
  filter tabs). The original has no target-cycling key, so none was added.
- **Tooltips.** Hold Tab for highlights and creature names. Your own character gets bright, even texture
  lighting with a subtle white edge glow on hover or while highlights are held. Placeables, doors and ground items only glow;
  they do not get floating name labels. A double tap within 200 ms (the original's default
  `tooltip-doubletap-threshold`) keeps the mode active until the next press. Mouse-hover names for
  non-creatures are also suppressed while this mode is active. Creature labels show the name above a
  localized injury description (`damagelevels.2da`/TLK), with separate colors for each. Names are red for
  hostiles, green for the player's party/faction (including self), blue otherwise; friendly reputation alone
  stays blue. Multiplayer also shows the player name above the character name. The text-only style uses the
  installed `fnt_default` face, in both UI modes. Options -> Interface -> Text bubble mode selects Off,
  Text only (the default), or Full (text, portrait, and bubble). Off hides every nameplate while highlights
  still work. Full reads `pnl_bubble*`, `gui_textbubble` and tiny portraits from installed resources/haks.
  Placeables have hover names outside Tab mode; doors never have nameplates. Labels are centered above the
  posed creature head, with a 12-pixel face / 14-pixel line pitch at default overhead-text size and 1x UI scale;
  the overhead-text size and UI scale preferences scale them.
- **ToggleKeyhole.** The original lists Tab for it as well, but Tab toggles keyholing only when the
  "keyholing with tooltip" setting (off by default) asks, so the action starts unbound. The key does not
  toggle keyholing yet; it says so.
- **Quick groups.** No default keys in the original. Our client selects one object at a time, so "set"
  remembers the selected object and "select" selects it again (`QuickGroups`).
- **Pause** sends the `pause` request. The server toggles the player pause bit (GetGamePauseState 2) for
  everyone:
  - in single player, always;
  - in multiplayer, for a Dungeon Master, or for players when the server allows it (`AllowPlayerPausing`, the
    original's `server.allow-player-pausing` / PauseAndPlay, off by default);
  - otherwise the player is told only a DM can pause.

  As in the original (`pausegame`):
  - every client's chat window says "PAUSED" and then "UNPAUSED" in red (255,1,1);
  - the classic portrait shows `po_pause`, an hourglass, with no banner (the modern interface keeps its banner);
  - the world stands still on screen, so animations, footsteps and placed or effect sounds stop, while the
    camera and interface still work.

  A single-player game also stands still while the game menu or options are open (Pause in menus), and while
  the window is in the background. "Pause in the background" is on by default, like the original's
  `game.auto-pause.on-focus-loss` (the default its settings schema gives). That background pause shows no
  chat line, which was not measured because the harness never gives the original focus.
- **Player list (P)** lists the other players with a Like / Dislike button each: disliking is mutual and
  lets you fight where the server's PvP setting allows (`player-list`, `pc-like`; [notify/player.md](notify/player.md)).
- **Quick chat (V)** opens the voice menu in either interface. The original W/E/D/S/X groups and second-letter
  keys select combat, exploration, tasks, social and feelings lines. Clicks work too. Escape closes the menu from any level; V and invalid letter keys also close it.
  A world click closes it. Menu keys do not move the player. The server chooses the character
  voice set and dispatches associate orders for Attack, Heal me, Guard me, Hold and Follow me. See [VOICES.md](VOICES.md).
- **Hide / show interface (H)** hides the whole HUD and its windows. Conversations stay visible.

## Bumping creatures

A perceived NPC yields only when it considers the mover friendly. Neutral Pavel in the
Prelude therefore stays put. Scripted walkers go around busy friends; direct keyboard
steering can push a stationary friend even with a queued wait. Default OnBlocked scripts
are not required. These distinctions are measured by the parity suite `coll_bump_flags` and the visual scene
`pavelbump`.

Planned paths go round busy or non-friendly creatures, also when one stands next to the destination
(`coll_goal`, `bimdoor`). A planning circle that holds the start or the destination shrinks to just
inside it instead of being dropped, so a stopping point a metre from a creature does not send the player
straight into it. Single creatures are passed along the tangents and round the arc, on the
left when the line runs through the centre. A destination inside such a creature's personal space
moves straight out of it to that distance, and an object's stopping point that one takes moves round
the object to where the range circle meets the creature's personal space (left first). Not copied:
the original sometimes still bumps a neutral creature 0.99 m away on the last step; OpenWinter arrives.

A bump leaves the NPC's facing and action queue alone. The server sends `creature-bump`
with the old/new positions; the client moves the visible model over a directional
`cwalkf/b/l/r` step under the held torch pose. Its duration comes from the loaded clip
(0.5 s for the measured humanoid). The blend between the step and the creature's other animations is not yet exact.

## Classic store shortcut (OpenWinter quality-of-life upgrade)

Shift + left-click buys a store item immediately without confirmation. Normal
left-click still asks. This deliberately extends the original game; see
[Quality-of-life upgrades](QUALITY_OF_LIFE.md) for the purchase and sound rules.
