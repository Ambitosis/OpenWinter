# Multiplayer harness

Two players on one server, side by side on the dev monitor, driven by a scene's scripts, with a record of what each
client shows of the other and of the world, in a form that can be compared with the server and with each other.

```
python tools/multiplayer/mp.py run mp_sync                      # OpenWinter: openwinter-server + two clients, then compare
python tools/multiplayer/mp.py compare mp_sync                  # the comparison again, on the last run
python tools/multiplayer/mp.py run mp_sync --client-args "--ui classic"
python tools/multiplayer/mp.py run mp_barter --silent               # --silent: Godot's Dummy audio driver, nothing reaches the speakers
python tools/multiplayer/mp.py run mp_sync --only nwn --native-framebuffer   # the original (see "The original" below)
```

Output: `out/multiplayer/<scene>/openwinter/{server,MPA,MPB}/` and `report.md` beside them.

## How it works

- **Scenes** are visual-harness scenes (`parity/visual/scenes/<name>/`, built by `visual-build`) with a
  `"multiplayer"` block naming the players and their characters:
  `"multiplayer": { "players": [{ "name": "MPA", "character": "fighter" }, { "name": "MPB", "character": "aluviandarkstar" }] }`.
  Characters come from the game's pregenerated `data/lcv` (read only). `"extraAreas"` adds more generated areas
  (`[{ "resref", "tag", "tileset", "tile", "size", "onEnter" }]`) for area transitions.
- **The scene's own scripts drive both players** (`parity/visual/include/mp_inc.nss`), so the same scene runs on the
  original's nwserver and on OpenWinter's server. `vis_main` (area OnEnter) waits until every player is in, then runs
  a chain of phases. Players are told apart by their login name (`MPPlayer("MPA")`, GetPCPlayerName).
  - `MPShot(name)`: a checkpoint. Every player's script-visible state is logged as `VISUAL|note|<name>|...` (area,
    position, facing, hit points, experience, party leader, current action, combat), then `VISUAL|shot|<name>`.
  - `MPNote(text)`: any other reading (`notes.txt`).
  - `MPDo(player, "verb|args")`: input for one player's client, so the client-to-server path is exercised too
    (OpenWinter only): `key|<input action>|down|up|tap` (the keyboard bindings: `move_forward`, `walk` ...),
    `moveto|x,y|run|walk` (a click on the ground), `click|<tag>` (a left click on an object), `chat|<channel>|text`,
    `request|<kind>|<json>` (any client request; `"@tag"` in the JSON becomes that object's id: the server resolves
    any object with that tag, items in inventories too, and the client the objects it was sent).
  - `MPDone()`: the end.
- **OpenWinter server** (`openwinter-server --harness <dir>`, `src/OpenWinter.Server/MultiplayerHarness.cs`): on each
  `VISUAL|shot` it writes its state to `checkpoints.jsonl` at the end of the tick (every creature, door, ground item and
  usable placeable in the players' areas: position, facing, movement mode and speed, hit points and the HP bar row,
  current action, effects, lasting visual effects, faction; per player: area, experience, gold, party, inventory and
  the objects the server has told that client about) and sends every client the notify `mp-checkpoint`. It samples
  creatures and doors every 0.2 s (`samples.jsonl`), forwards `VISUAL|do` as `mp-do` to that player's client, and on
  `VISUAL|done` tells the clients (`mp-done`) and stops 3 s later. Nothing in it changes the simulation.
- **OpenWinter clients** (`--mp-trace <dir> --mp-character <file>`, `client/Gameplay/MultiplayerTrace.cs`): pick that
  server-vault character as soon as the list arrives, then record what they show: `samples.jsonl` every 0.2 s (each
  creature's last position from the server and its drawn position, facing, animation: locomotion, stance, one-shot
  clip; HP bar; door states), `checkpoints.jsonl` at each `mp-checkpoint` (every creature, door, item and placeable
  shown, with HP bar, reaction, lasting visuals the server announced and the ones actually drawn; the party roster and
  HUD party; the chat lines and floating text since the last checkpoint) plus a JPEG of the window, and `events.jsonl`
  (every chat line, notify and animation as it arrives). `mp-do` is played through the real input paths.
- **Isolation**: the server and each client get their own `config/local.json` (through `OPENWINTER_CONFIG`) whose
  user folder and write sandbox are under the run folder; each client also gets its own `APPDATA`, so Godot's
  `user://` (settings, player key, downloaded content) is its own. The characters go into the server's sandboxed
  `servervault/<player name>/`. Nothing is written outside `out/multiplayer`.
- **Windows**: both clients open side by side on `devDisplay`, each half its width (`--size`, default 944x708),
  without taking focus.

## The comparison (`tools/multiplayer/compare.py`)

For each checkpoint and each client:
- objects the server has in the player's area that the client doesn't show (and whether the server ever sent them),
  and objects the client shows that the server doesn't have there;
- per object: the last position the server sent (0.35 m, plus 0.15 s of travel for movers) and the drawn position
  (0.75 m plus 0.3 s of travel) against the server's, facing (12 degrees, still objects), door and placeable state,
  death, HP bar row, the lasting visual effects told and drawn, and the animation against the movement mode (a model
  standing still while the server has it moving faster than 0.2 m/s, or the reverse). A creature the server turned
  or moved in the 0.6 s before the checkpoint (a turn, a combat sidestep) is compared by its sent position only:
  clients show such changes a moment later;
- the party roster the client was told against the server's party;
- then the two clients against each other (positions, open, dead, HP bar, visuals, animation of movers);
- the chat lines and floating text each client got since the previous checkpoint are listed (the expected sets come
  from measurements of the original, below).

From the samples: for each client and each creature the server had moving, the mean and largest distance between
the client's view (sent and drawn) and the server's at the same moment, and the time lag that fits best.

## The scenes

- `mp_barter`: MPA and MPB barter through their own clients (refusal, offers, locks, the trade, a cancel); see
  [BARTER.md](BARTER.md).
- `mp_sync`: MPA (Junta Khan, level 15 fighter) and MPB (Aluvian Darkstar, level 1 wizard). Walk and run, a click on
  the ground from MPA's client, MPB opens and closes a door, MPA drops a longsword and MPB picks it up, chat lines
  (talk, shout, a server message to MPA, floating text to MPA's faction and to MPA only, party chat from MPA's client)
  before and after MPB joins MPA's party (AddToParty), a goblin fights MPA and dies (HP bars, animations, combat lines,
  the party's XP split), MPB casts Barkskin on herself, MPB jumps to another area while MPA opens the door, drops a
  dagger, starts to glow and a hurt goblin appears, then comes back; MPA walks with the keyboard from her client, MPB
  casts Magic Missile at the goblin, MPA salutes.
  MPB's client then quits (OpenWinter only) and MPA must lose her.
- `mp_delivery` (one player, a visual scene): who gets a line, by distance: speech by volume, floating text with and
  without bBroadcastToFaction (a commoner, the player, henchmen at 5, 25 and 35 m), fights between two creatures that
  aren't hers at 4 to 45 m, a creature casting near and far. Run it with
  `python tools/visual/visual.py run mp_delivery --native-framebuffer --silent` and compare the two `chat.txt`.
- `mp_join` (one player, a visual scene): the joining line and the PvP line on entering areas with each
  PlayerVsPlayer value.
- `mp_party` (OpenWinter): party management between the two players through their own clients (MPDo
  `ui|invite|<object id>`, `ui|accept`, `ui|reject`, `ui|leave`, `ui|kick|<id>`, `ui|leader|<id>`, `ui|dislike|<id>`,
  `ui|list`: the player list's and the invitation window's own actions): the invitation window, joining, the party's
  kill experience, the leadership, a refused kick, kicking, a rejection, AddToParty, leaving, a dislike and the open
  list ([PARTY.md](PARTY.md)). Client checkpoints also record the player list (`playerList`) and whether the invitation
  window is open.
- `mp_partyui` (one original client, interactive): the classic player list and the self radial in multiplayer.

## The original

`--only nwn` (`tools/multiplayer/nwn.py`) starts a sandboxed nwserver (`out/multiplayer/<scene>/nwn/server/user`) and
one nwmain per player (`.../nwn/<player>/user`), holding the shared nwmain lock for the whole run.

- **Several nwmain instances run at once.** Each sandbox's `-userdirectory` keeps its settings, logs and screenshots
  apart.
- **Player names and the server vault:** the client's player name is `[client.identity] name` in its sandbox
  `settings.tml` (the profile's "Player Name"). With `[server.vault] by-player-name = true` in the server's
  `settings.tml` the server lists `servervault/<player name>/*.bic`, and the character selection screen opens with
  that player's character already selected.
- **Joining:** `+connect <address>` is accepted on the command line but always answers "Could not translate
  address.", so the harness uses Direct Connect: `[ui.multiplayer.direct-connect] last-address` prefills the address,
  and the harness clicks Multiplayer, Direct Connect, presses Enter (OK), then clicks Play (window captures of each
  step: `<player>/login_*.png`). Enter does not press Play. The original reads the real pointer's place for a click,
  so the harness parks the pointer on the button for 0.25 s, posts the button to the window (no focus needed) and
  puts the pointer back; it does this on the dev monitor only and refuses anywhere else.
- **CD key:** started outside Steam the client has no CD key and asks for one when joining. `SteamAppId=704450` in
  its environment attaches it to the running Steam client. A second client with the same account's key is refused
  ("Your CD Key is already in use on that server"), and nwserver offers no setting round it. **Two original clients
  on one server need a second licence**: `"nwnClientKeys": {"MPB": "<path of an nwncdkey.ini>"}` in
  `config/local.json` copies that key into the second client's sandbox and starts it without Steam. Without one the
  original is measured with a single client (`mp_delivery`, `mp_join`), and what another *player* is told stays
  unmeasured.
- **Interactive mode:** `MP_NWN_INTERACTIVE=1` starts the server and clients and then runs commands appended to
  `out/multiplayer/<scene>/nwn/cmd.txt` (`MPA click 0.5,0.6`, `MPA rclick 0.5,0.6` (right button), `MPA login`,
  `MPA key 13`, `MPA text abc`, `MPA shot name`, `MPA nshot name`, `quit`), with a window capture after each. It is
  useful for working out a new scene's input steps.
- **Scene input for the original:** the watcher takes `VISUAL|click|<player>|fx,fy` (a real-pointer click as the
  login uses, dev monitor only), `VISUAL|key|<player>|<vk>` and `VISUAL|text|<player>|<characters>` from the server's
  script log, so a scene's own timeline can press the original's buttons (`mp_portal`). A scene can fix the server's
  port for both engines (`"multiplayer": { "port": 5951 }`) when its scripts must name the server, and a player with
  `"dm": true` starts nwmain as the DM client (`-dmc`) against nwserver's `-dmpassword` (`mp_journaldm`).
- **One Party Only** is nwserver's default (`server.one-party-only = true`), and OpenWinter's server follows it. A
  scene's `"multiplayer": { "oneParty": false }` turns it off for both harnesses (nwserver's settings.tml and
  openwinter-server's server.json); `mp_sync` and `mp_party` do.

## Who is told what (measured with one original client)

| Line | The original (mp_delivery, mp_join) and OpenWinter |
|---|---|
| SpeakString / player talk | players within 20 m (heard at 20, not 25) |
| Whisper | within 3 m (2.5 yes, 3.5 no) |
| Shout | heard 50 and 65 m away; OpenWinter sends it to the whole server |
| FloatingTextStringOnCreature, bBroadcastToFaction | the creature's faction (her party, her henchmen) within 30 m; a commoner's reaches nobody |
| ... without bBroadcastToFaction | only the creature's own player |
| Attack and damage lines | the players involved and the master of an associate involved; not a bystander (a fight with no associate of hers near prints nothing, seen or blinded; her henchman attacking or attacked prints its lines) |
| Spell casting | a bystander 5 m away: "Bandit casting unknown spell", "Bandit casts unknown spell"; nothing 45 m away. Spellcraft decides the name; "casting" reaches 35 m, "casts" 28 m ([PARTY.md](PARTY.md)) |
| Joining | "MPA has joined as a player.." (TLK 6610 plus a full stop), seen by the joining player. OpenWinter sends it to everyone, and the leaving line (6612) to the others (not measured) |
| Entering an area | "[Server] You are now in a Party PVP area." every time; "No PVP" where PlayerVsPlayer is 0; never above the server's setting |

The range limits apply to player chat too (talk and whisper from a client). Only NPC speech was measured; the
original uses the same TALKVOLUME rules for both as far as the scripting interface shows.

## The OpenWinter sync audit (mp_sync)

`mp_sync` passes with no differences between either client and the server or between the two clients, in both the
modern and classic interfaces (27 checkpoints). What it covers:
- MPA walking and running seen by MPB: mean distance between the drawn position and the server's 0.03-0.08 m (largest
  0.4 m, while starting or stopping), the best-fitting lag 100 ms; MPB's model walks and runs when the server's does.
- The door, the dropped and picked-up sword, the dropped dagger, the goblins' HP bars (temporary hit points
  included), their deaths, the Barkskin and red glow visuals (told and drawn), the party roster on both clients.
- The late view: what changed while MPB was in another area (door, dagger, glow, a hurt goblin) is all shown to her
  when she comes back, and her Barkskin to MPA.
- MPA's keyboard walk and click from her own client, MPB's quit (MPA loses her at once).
- Chat: MPB sees MPA's attack and damage lines and the kill line, but not MPA's initiative roll, server messages
  or floating text meant for MPA only; party chat reaches both, talk and shout reach both.

## Not matched yet, or not measured

- **What another player is told** needs a second licence for the second original client (above). In particular:
  combat lines between party members (OpenWinter shows another member's fight only when one of her associates is in
  it), the initiative roll, spell casting lines, damage from spells (EffectDamage lines go to the players involved
  only), the party's kill XP split between players of different levels (OpenWinter: MPA level 15 got 2, MPB level 1
  got 10 for the goblin), the "has joined the party" lines (dialog.tlk 6454) on AddToParty, and the leaving line.
- **Party invitations** ([PARTY.md](PARTY.md)): what another player sees of them is not measured.
- **Combat cadence against an immortal creature**: in mp_delivery's fights OpenWinter's bandit attacked an immortal
  1 HP goblin about 20 times in 9 s; the original's bandit attacked twice. This is a combat matter, not a network one.
- The single-player join line names "Player" (OpenWinter's default player name); the original's sandbox has none.
