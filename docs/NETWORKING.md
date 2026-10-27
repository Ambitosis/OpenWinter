# Multiplayer compatibility

OpenWinter clients connect to OpenWinter dedicated servers over LiteNetLib UDP (default port 5121). Single player
uses an in-process loopback connection. This is OpenWinter's own protocol. The client can also join NWN:EE servers
that run the NWNX_WSNetLayer plugin ([below](#joining-nwnee-servers-wsnetlayer)); the original UDP protocol is not
implemented.

## Version check

Remote connections require an exact match of:

- `NetworkVersion.CurrentProtocol`: the gameplay wire protocol revision (currently 3).
- The engine version from the OpenWinter.Net assembly's build metadata.
- A SHA-256 fingerprint of the engine's runtime source and build files.

The dedicated server prints its version and shortened fingerprint at startup. A rejected client's error screen
shows its version and the server's version, with a button to return to the main menu. Install the same build on
both machines and reconnect. There is no automatic updater or switch to bypass the check.

`build/NetworkBuild.targets` generates the fingerprint into OpenWinter.Net, shared by the dedicated server and
Godot client. It includes C# source and project files from Aurora, Content, Game, Net, Server and the client,
plus the root build properties/targets, network build target and Godot project settings. Changes to local,
uncommitted runtime source count. Tests, tools, documentation, game data and generated build outputs do not.
Text line endings and relative path separators are normalized, and paths are sorted ordinally. Identical source
has the same identity across checkout locations and Debug/Release builds; timestamps and commit IDs do not
affect it. Rebuild and distribute the complete client/server output together; mixing DLLs from different builds
is unsupported. The fingerprint identifies source compatibility, not executable authenticity.

## Handshake ordering

The connection request carries a small, bounded envelope (`openwinter/handshake/1`) containing the three version
fields. The server rejects missing, malformed or mismatched identities through LiteNetLib's rejection payload,
before raising `NetServerHost.Connected` or creating a GameServer session. The rejection includes the server
identity, independent of gameplay message serialization.

After acceptance, the server sends its identity as its first reliable ordered packet. The client validates it
before sending any queued gameplay messages, including Hello. Gameplay bytes are never parsed as such until
the handshake succeeds. Handshake failure clears queued input and stops the connection; a handshake that never
completes times out. Leaving the error screen disposes the transport and returns to the menu in either UI style.

The earlier `openwinter/1` transport cannot negotiate versions: servers reject it, and clients explain a rejection
by such a server. Both endpoints must run the same build.

Keep the envelope stable when changing gameplay messages; increment `CurrentProtocol` for incompatible wire
changes. The generated source fingerprint also catches changes when a protocol bump is accidentally omitted.
Allowing different builds of a compatible protocol would be an explicit future relaxation of this policy.

## Testing

`NetworkVersionTests` uses real localhost UDP sockets: matching peers, queued login, protocol/build/engine
mismatches, malformed and legacy handshakes, client-side verification, cancellation, and two players entering
the same world. The latter needs configured game data; the transport cases do not.

Two players on a real dedicated server: `python tools/multiplayer/mp.py run mp_sync` ([MULTIPLAYER_HARNESS.md](MULTIPLAYER_HARNESS.md))
records what each client shows of the other and of the world and compares it with the server.

For UI verification, run the client with `--connect host:port --capture-connection-failure <directory>` against
a mismatched test server. It captures the actual failure, returns to the menu and captures the menu; add
`--ui classic` to verify that style. Launch it through `tools/godot.sh` as usual.

## Module presentation resources

Single-player enables the same server content catalogue as remote play. Alongside haks
and custom TLKs, the server announces `ContentKind.ModuleResources` (protocol 3): a
hash-verified ERF containing only supported client resource types from the module.
The client does not open the `.mod` or read area state, scripts, dialogs, or templates.
Models, textures, tilesets/walkmeshes, UI/font assets, soundsets/audio, and 2DAs are
allowlisted in `ModuleClientResources.IsClientResource`; unknown types remain excluded.
Runtime sources and development overrides are not implicitly exported.

Packages are built deterministically in `PersistenceSystem.UserRoot/cache/content`
(the configured sandbox write root in development). Clients download to their existing
Godot content cache. Lookup is announced haks, module presentation package, then base
resources. An unchanged package reuses its hash; changing module resources creates a
new package. Dedicated servers with `ServeContent=false` still deliberately disable
all automatic content distribution. With content serving on, the module's own 2DAs are
shipped to every client (they are needed for presentation), so their contents are not private.

Single player also runs with `ServeContent = true`, so every launch re-hashes the module's haks and custom TLK,
rebuilds and hashes the presentation package, and the first launch of a module version streams the package over
the loopback connection. The in-process client could load the server's package (or the module's resources)
directly instead.

A new Welcome interrupts an old download, disposes incomplete files, discards held and pending old-module packets,
and replaces scene and audio resources. The `ModuleCleared` event closes modern, NUI and dialog windows and pending
targeting without sending old close callbacks into the new server world. Classic panels detach and rebuild after
the next area loads. Verified installation raises `ClientSession.ContentChanged`. Retired resource sources are
disposed after one frame, once old widgets and scene nodes finish deferred removal; session exit disposes any that
remain. This supports switching modules on the server (`StartNewModule`) but does not implement it.

Testing: `ModuleClientResourcesTests` and `ContentSyncTests` cover loose and archive modules, lookup precedence,
server-only exclusions, deterministic hashes and cache reuse, and interrupted transfers. The headless client probe
`tools/godot.sh --headless -- --verify-module-content <directory>` checks the client session's Welcome, download,
area and audio replacement, including cutscene, pause and rest recovery when a replacement Welcome discards queued
old-module notices. The visual scenes `modcontenta` and `modcontentb` show a module-only model changing colour
under the same resource name, without haks, on both engines (`parity/visual/scenes/modcontenta/README.md`); material
brightness still differs.

## Joining NWN:EE servers (WSNetLayer)

[NWNX_WSNetLayer](https://github.com/niv/NWNX_WSNetLayer) is an NWNX plugin that exposes NWN:EE's message layer over
websockets. A server that loads it can be joined from the OpenWinter client:

    tools/godot.sh -- --connect-nwn ws://host:port --name <player name> [--character <server-vault resref>]

From the main menu, Multiplayer joins one too: tick "NWN:EE server (WSNetLayer)" or type a `ws://` or `wss://`
address. Without `--character` the server's character list is shown. The server needs nothing beyond the plugin:
no scripts, settings or other plugins.

- Use `wss://` when the server puts the plugin behind a TLS proxy, as its README asks.
- The client logs in with the CD key from the player's `cdkey.ini` (read only). Only the public part and an MD5
  answer to the server's challenge are sent. `--nwn-key generate` makes a throwaway key for test servers that can't
  reach the master server.
- Only NWN:EE 8193.37.17 is supported, the version the plugin and its message definitions cover.

`src/OpenWinter.Net.Nwn` has three parts:

- **Codec.** It reads and writes the plugin's encoding, which puts a type byte before every field. Most of it is
  generated by `tools/nwnproto` from the message definitions in
  [nwn-message.py](https://github.com/niv/nwn-message.py) (GPL-3.0); the types that library codes by hand are written
  by hand in `Messages/Custom`. Where recorded traffic differs from those definitions, the generator corrects them
  (`apply_corrections`): NUI windows, binds, events and layouts carry JSON fields (type byte, length, text) rather
  than length-prefixed blobs, and a NUI bind entry from the server ends after its value, while one from the client
  carries an array index after it (the client's NuiBinds is its own message, `NuiClientBinds`). A body bag's creature is an object id,
  not a dword. The player's level-up information has one starting package (a dword) before its
  multiclass level, not two.
- **Session.** It handles login, keep-alives and echoes.
- **Bridge.** `NwnBridgeConnection` is an `IConnection` that turns NWN messages into the OpenWinter messages the client
  already understands, and the client's requests into NWN input. NWN sends a walking creature's path once; the bridge
  moves the creature along it at its own rate.

Payloads that both the OpenWinter server and the bridge send are records in `OpenWinter.Net/Notices.cs`.

Supported so far:

- login and choosing a server-vault character
- the area and its static objects
- creatures with appearance and equipment, placeables, doors (open or closed) and ground items
- chat in both directions, explored map tiles, and the server's feedback lines (items and gold gained or lost,
  locked doors, script messages)
- click-to-move walking, keyboard driving and walking towards a held mouse button; other creatures' walks
- opening and closing doors, using placeables and picking up ground items
- script-built NUI windows: opening, closing, binds and group layouts, with clicks and edited values sent back (an
  edited value goes back whole, with the row whose array element changed or -1, which the server's watch event
  reports as its array index)
- the player's abilities (`Bridge/NwnAbilities*.cs`): the quickbar the server keeps with the character (edits are
  stored on the server), the spellbook (prepared and known spells, slots and uses left, preparing and emptying
  slots), casting at a creature, an object or the ground (spontaneous casts too), feats and their uses, skills,
  items' cast-spell properties (potions, wands, scrolls), combat and stealth/detect modes, resting, cancelling a
  queued action, the radial menu's use of all of these, and the character sheet (abilities, saves, base attack,
  armour class, hit points, skills, feats, experience) as the server sends it; spells' conjure and cast effects.
  Using an item's power is the abilities' (an item they haven't seen is left to the inventory), with the items as
  the inventory knows them; the action queue goes into the combat bridge's HUD
- conversations: the speaker's line, name, portrait and voice-over, the answers, picking one, Esc and the end
  choice, the lines in the chat window, and one-liners spoken over the talker. The server sends text as written or
  as a talk table reference, with the creature its tokens describe, and leaves the tokens (names, race, class,
  gender words, custom tokens) to the client; the bridge fills them in. Text for a female character comes from the
  female talk tables (dialogf.tlk and the custom table's "f" file) where the language has them; English has none.
- talking: the Talk command (radial menu, quickbar) asks the server for a conversation; a click on a creature is
  an attack when the creature is hostile to the player, otherwise a use, and the server picks the default action.
- the journal: opening its window opens the quest screen, and the server sends the whole journal (it sends it only
  then); closing the window closes the quest screen. The server's journal-updated message prints the journal line
  and flashes the button.
- cutscene mode and fades; Esc in cutscene mode sends the cutscene's cancel, also while a conversation is shown,
  which it leaves open (docs/CONVERSATION_ENDINGS.md)
- the player's items (`NwnInventory`, `NwnItems`): the backpack, bags and equipment with icons, stacks, charges,
  identified state, names, weight and gold; equipping, unequipping, dropping, splitting, using and moving items, into
  and out of bags; examining items; placeable containers and body bags (open, take, take all, put, close); stores
  (every category, buying, selling, identifying, the store's prices and gold); barter between players (asking starts
  it for both at once, as the server does; offers of items and gold, locking, accepting, cancelling)
- combat: attacking (the attack command, and a click on a hostile creature), each attack's swing, weapon sounds,
  the target's reaction and the damage floating over it; the combat log (attack rolls, damage by type, kills,
  initiative, saving throws, spell resistance, damage immunity, resistance and reduction, immunities, healing,
  experience); hit points, temporary hit points and effect icons in the HUD, the selected target's health and
  attitude, health bars over creatures, hostile creatures coloured as such; dead and dying creatures lying down and
  corpses fading; the death panel (also shown to the living when a script asks, and closed when the server says)
  and respawn; battle music; impact visual effects and lasting ones on creatures. The HUD's experience comes from
  the server's character sheet updates; until one arrives the bar is empty.
- animations: a creature's animation code becomes the animation OpenWinter's server plays for the same
  ANIMATION_* constant (script animations, emotes, sitting, talking, conjuring and casting); a looping one plays
  until the server sends the next code, as it does when the script's time is up. A knockdown lays the creature
  down; placeables switch on and off, open and close, and doors and placeables show their destroyed state. The
  client's emotes go to the server as the animation input, which plays them for everyone. The looping custom
  animations (ANIMATION_LOOPING_CUSTOM1-57) play the model's customN clips, a module's hak ones included
  (customNstart, customNlp for the time asked, customNend); a model without CUSTOM3-11 clips plays the engine's own
  casting, knockdown or sitting animation of that number. ReplaceObjectAnimation and ReplaceObjectTexture: an object
  update carries the object's whole list of replacements, and the bridge passes on each change.
- the camera (`NwnCamera`): SetCameraFacing (the direction comes in radians, the pitch in degrees from straight down,
  the distance in metres, each only when the script gave it; the transition is the script's CAMERA_TRANSITION_TYPE_*,
  and the camera turns at that many degrees a second), StoreCameraFacing and RestoreCameraFacing (the server also
  sends these when cutscene mode begins and ends), SetCameraMode, SetCameraHeight, LockCameraPitch, LockCameraDistance
  and LockCameraDirection, SetCameraLimits (pitch and distance), SetCameraFlags and AttachCamera.
- spell projectiles fly when the server sends them, for the flight time it gives (spells.2da model, path and sound)
- visual effects at a place, beams (lasting or instant), projectile-style effects, lasting effects on placeables
  and doors (with removal), areas of effect (vfx_persistent.2da), visual transforms and highlight colours
- sounds: PlaySound (a message the definitions lack, 0x09 0x07: the object and the sound's resref), voice chat and
  death cries from the creature's voice set, sound objects (placed with the area, played, stopped, moved and
  turned up or down), and the area's music, its delay, battle track and ambience
- levelling up (`NwnProgression`): an NWN client works out a level's options itself, so the bridge does with
  OpenWinter's own rules (`NwnRules`, a world without a module on the client's game data and the server's haks). The
  server's player information (classes, base abilities, feats, known spells, skill ranks, unspent points, associates,
  whether a level is due) is kept as an OpenWinter creature for each creature the player has controlled: the server
  describes a creature in full when control of it begins (possessing a creature and returning), except its ability
  scores, which come only when they change. Bonus feats from items and effects are left out, since enforce legal
  characters doesn't count them as prerequisites. Opening the level-up window asks the server which classes it allows
  (by classes.2da row) and shows those; the server doesn't answer while it keeps the window shut (a possessed
  creature). Taking the level sends the choices as nwmain does: the class, the raised ability every fourth level, the
  hit points the level gives (in full to the third level, then rolled, the figure the classic summary showed), the
  ranks added to each raised skill (one rank of a cross-class skill for two points), the points left, the chosen
  feats and those the class grants at the level (the server adds only the feats it is sent), the spells learnt by
  spell level, the associates and the class's domains or school. The server confirms or refuses it; another level
  waits until the server's update shows the last one. The server applies what it is sent without checking it, and
  enforce legal characters checks the character at its next login: levels taken through the bridge pass (fighter,
  wizard, sorcerer, rogue with a cross-class skill, cleric then wizard, to fifth level, and after possession). A
  level's skill list is flagged in a 32-bit field, so skills past the 32nd row aren't raised yet. A sorcerer's or
  bard's exchange of a known spell isn't offered.
- creating characters (`NwnCharacterCreation`): the creation window's races, classes, portraits and checks come from
  the rules too, and the new character goes to the server as a character file for its vault, as nwmain's New button
  sends it; the server logs the player in with it or says why not. A new character's file is about 5 KiB, more than
  the server takes in one message (below), so the bridge leaves out the fields that are zero or empty, the unranked
  skills at the end of the skill lists and, if still needed, the prepared spells. Most first-level characters fit; a
  wizard's spellbook does not, and the window says so. The server's copy has every field again (body parts and all)
  and passes enforce legal characters. The server says nothing about whether it takes new characters, so the
  character list always offers one.
- walking: with WSNetLayer, the server crashes inside NWNX_Events on the first click-to-move walk from a websocket
  client while the module subscribes `NWNX_ON_INPUT_WALK_TO_WAYPOINT_*`, and on the first keyboard drive while it
  subscribes `NWNX_ON_INPUT_KEYBOARD_*` or `NWNX_ON_INPUT_TOGGLE_PAUSE_*` (measured with NWNX:EE master and with a
  fork of it, with only Core, Events and WSNetLayer loaded too; the subscribed script never runs). Other input events
  (attack, cast, drop, examine) don't trigger it, and the game's own client walks and drives over UDP on the same
  server without a crash. It is a server-side fault the client can't avoid: such servers can't be walked on through
  WSNetLayer yet.
- message size: WSNetLayer takes at most 4088 bytes of a message from a client and a bigger one stops the server
  (measured, with or without compression or fragments). The session never sends a message over 4064 bytes (it notes
  it in `NwnSession.Unsent` instead), and long chat lines go in pieces of up to 2000 characters.

The server shows the client only the panels an NWN client has open: the equipment and the shown backpack page while
the inventory is open, one container or bag at a time, and a store's shown category and page. OpenWinter's windows
list everything, so the bridge keeps the inventory open and shows the server each backpack page in turn, then each
bag (only while no container is open, since opening a bag closes it), and each store category and page, and looks
again after every change the server confirms; what a look doesn't see again is gone. A page's answer comes with the
server's next update, so the pages are shown one at a time. An inventory the server closes stays closed until the
player opens it, and a new area closes the open container, store and barter. Item tooltips (category, damage,
armour, properties, size, slots) come from the item's appearance and the client's 2DAs.

The server names neither swing clips nor weapon sounds; the bridge picks them from what the attacker holds, as the
client does on its own server. NWSync is still to come.

What the server accepts, as measured against it:

- A cast names the class by its place in the character's class list and the spell's spells.2da TargetType as its
  target type; the server refuses a cast with any other target type. A special ability is cast with its own
  message at the caster level it was given; a polymorph form's ability as an ordinary cast with class 255.
- The player state's mode bits are detect 0x1, stealth 0x2, parry 0x4, power attack 0x8, improved power attack
  0x10, flurry of blows 0x20, counterspell 0x200, expertise 0x2000, improved expertise 0x4000, defensive casting
  0x8000, dirty fighting 0x10000 and defensive stance 0x40000 (detect and stealth also set 0x80). Rapid shot's isn't
  known (the server refuses it without a bow), so it is toggled whenever the client asks.
- The character sheet is filled for the panel the client has active and only that one: the main panel (0) brings
  abilities, saves, base attack, combat figures and experience, the skills panel (1) each skill's total, and
  switching back to a panel brings all its figures again. While the client polls an open sheet the bridge switches
  between the two every few seconds, and sends the sheet only in answer to those polls.
- The client sets an item quickbar button with the item's id and property field, then a flag and, for a
  main/off-hand pair, the off-hand item's id; not the full item description the server's own item buttons carry
  (`Messages/Custom/GuiQuickbarItemButton.cs`). Without the flag WSNetLayer logs the message as short ("desync at
  stream pos 4: expected BOOL"). A button is sent only when the player changes it, never back to the server after
  the server set it.

Testing:

- `NwnCodecConformanceTests` decodes frames encoded by nwn-message.py and re-encodes them to the same bytes.
- `NwnSessionTests` covers the login against a scripted server; `NwnBridgeTests` covers the translation.
- `tools/nwnws/nwnws.py` runs a sandboxed Linux nwserver with NWNX and the plugin in WSL. It can't open outbound
  connections, so it never contacts the master server. `--nwnx CREATURE,...` loads more plugins, for a test module
  (built with `parity-build`) whose scripts call them.
- `openwinter-tools nwn-probe`, `nwn-dump` and `nwn-bridge` record and inspect sessions against it. `nwn-bridge`
  can also say a line, use or talk to objects, answer conversations, open the journal, drive with the keyboard,
  click NUI elements, attack, respawn, send any client request, play emotes and record (`--say`, `--use`,
  `--talk`, `--replies`, `--journal`, `--drive`, `--nui-click`, `--attack`, `--respawn-at`,
  `--request kind=json|...`, `--emote`, `--record`), and run a script of client requests as another player
  (`--player`; `--do "use:Chest;req:container-take {\"item\":\"@c:Arrow\"}"`, items named from the last
  inventory, container or store contents); it prints the animations it passes on. `nwn-dump` reads 2DAs with the
  haks the recorded server named, since some fields' presence depends on them.
- The client's `--capture-nwn-dialog` talks to a creature, answers each line with a number key and takes a shot of
  every line, then opens the journal.
- `parity/suites/bridge_inventory` builds a test module with a chest, a body bag, a store opened by using a
  barrel and a chair to a second area with another chest; the entering player is given weapons, armour, stacks, a
  wand, an unidentified ring, a bag and gold. The client's `--capture-nwn-inventory dir` opens the inventory, equips
  a weapon, loots the chest, buys, sells and examines, with a shot after each step.
- The visual scene `bridgecombat` (`visual-build bridgecombat`) is a fight to run on that server: goblins attack,
  effects, spells and saves, dying, death and respawn, each announced in chat. The client's `--capture-nwn-combat`
  follows it and takes shots; `NwnBridgeCombatTests` replays frames recorded from it.
- The client's `--capture-nwn-actions` takes shots of the quickbar, character sheet and spellbook, then of a mode, a
  cast, a feat and a rest.
- The visual scene `bridgefx` (parity/visual/scenes) runs animations, conditions, placeable and door states, visual
  effects, spells, sounds and music by script, announcing each step in chat; `openwinter-tools visual-build
  bridgefx` makes its module for `nwnws.py start --module`. The client's `--capture-nwn-fx` takes shots along its
  timeline, and `NwnBridgePresentationTests` replays frames recorded from it.
- `parity/suites/bridge_progression` builds a test module that gives the entering player the experience for fifth
  level; saying "possess" possesses a goblin and "unpossess" returns (start the server with `--nwnx PLAYER`), and
  "dodgering" equips a ring granting Dodge. `nwn-bridge --levelup CLASS,...` takes a level in each class with its
  recommended choices (`CLASS+sN` also raises skill N), and `nwn-bridge URL - --new-character NAME[,CLASS]` creates a
  character; with the server's `--elc 1`, logging in again checks the result. The client's `--capture-nwn-levelup
  dir [--levelup-class N] [--levelups N]` takes levels in the level-up window (the classic one with `--ui classic`)
  and `--capture-nwn-chargen dir` creates a character from the character list, with a shot at each step.
- `parity/suites/bridge_nui` builds a test module whose window has a text field bound to a single value and a list
  bound to an array, both watched; its event script reports each watch (index and value) in chat, so
  `nwn-bridge --request 'nui-bind={"token":1,"bind":"rows","value":[...],"index":1}'` shows what the server made of
  an edit.
