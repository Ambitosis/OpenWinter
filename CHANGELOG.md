# Changelog

OpenWinter follows [semantic versioning](https://semver.org). Until 1.0, minor versions may change behaviour,
save formats and the network protocol; clients and servers must run the same build to connect.

## 0.2.0 (2026-10-28)

Clients and servers must both be 0.2.0 to connect.

- **Joining NWN:EE servers:** the client joins NWN:EE servers that run the
  [NWNX_WSNetLayer](https://github.com/niv/NWNX_WSNetLayer) plugin (Multiplayer → "NWN:EE server (WSNetLayer)", or a
  `ws://` / `wss://` address) and plays there: walking and driving, doors and placeables, conversations, the journal
  and cutscenes, inventory, containers, stores and barter, combat, the quickbar, spellbook and casting, feats,
  skills, modes and resting, character creation and level-up, script-built NUI windows, camera functions and custom
  animations, server feedback, the player list and parties, and the server's resource, texture and talk table
  overrides. What isn't passed on yet is listed in [docs/NETWORKING.md](docs/NETWORKING.md#joining-nwnee-servers-wsnetlayer).
- **NWNX:** all 633 functions of NWNX:EE's script includes, up from about 230, including NWNX_Events with its
  dispatch lists and whitelists, Creature, Object (Serialize and Deserialize), Player, Effect, Damage, Item, Store,
  Area, Feat, Race, NoStack, Tweaks, ELC and SQL (SQLite, MySQL and PostgreSQL). HTTPClient and WebHook send real
  requests; features that reach outside the server need a dedicated server and their own setting.
  [docs/NWNX_DIAGNOSTICS.md](docs/NWNX_DIAGNOSTICS.md) lists the events that are never raised.
- **Module shaders:** MTR materials with custom GLSL shaders, worn parts, the post-process pass, water and script
  shader uniforms, run from the game's own shader data ([docs/SHADERS.md](docs/SHADERS.md)).
- **Linux server:** a self-contained `linux-x64` server download alongside the Windows one. Haks, TLKs and modules
  are found whatever the case of their file names.
- **Formats:** E1.0 archives (resource hashes and compressed resources).
- **Security:** file-format readers, compressed buffers, campaign databases and save snapshots are hardened against
  malformed input and fuzz-tested. Names from servers and archives stay inside their folders and downloads inside
  the cache. The server checks message sizes, caps a client's backlog and its connections, drops connections that
  never log in, and refuses input while a character may not act.
- **NWScript:** JIT-compiled scripts agree with the interpreter (differential tests with random programs), and
  faults inside the VM become script errors.
- **Matched to the original:**
  - Area transition triggers light up and take the transition cursor under the pointer, but only when they lead
    somewhere.
  - When a player's target dies, the attack turns to the nearest enemy in reach; otherwise it ends.
  - The sun and moon light areas from a fixed direction, up in the north-east, at every hour (they used to arc
    across the sky), so shading, shadows and `lightAreaDiffuseDirection` match; SetTime's new hour reaches players
    at once.
  - Prestige classes raise the base class's spell slots but not its spells known; LevelUpHenchman picks spells
    from the package's list only.
  - Esc in cutscene mode cancels the cutscene, not the conversation.
  - Camera script functions, with their transitions and limits.
  - Disabled and forced GUI panels, and the built-in GUI events.
  - NUI windows use the original's text face, margins and padding.
  - Tiles no longer fade in play; camera collision is off while the keyhole shows the player.
  - Body parts with no texture, robe pieces off the skeleton and linked emitter ribbons.
  - Weapon finesse by creature size, a cap on personal map pins, and examining only what the player can perceive.
- An installed client or server with a `config/local.json` keeps its user data in `userdata` beside it.
- A server given game data older than it needs says so at startup instead of crashing on its first tick.
- NuGet updates, including LiteNetLib 2.1.5 and Microsoft.Data.Sqlite 10.0.14.

Known gaps: a WSNetLayer server whose module subscribes NWNX_Events' walk-to-waypoint, keyboard or pause input
events crashes on a websocket client's first walk or drive, a server-side fault the client can't avoid
([docs/NETWORKING.md](docs/NETWORKING.md#joining-nwnee-servers-wsnetlayer)). Players strafe around melee targets more
often than in the original ([#67](https://github.com/Ambitosis/OpenWinter/issues/67)).

## 0.1.1 (2026-10-20)

A maintenance release: no gameplay changes. Clients and servers must both be 0.1.1 to connect.

- **.NET 10:** OpenWinter now targets .NET 10 (LTS, supported to November 2028); .NET 8 support ends in November
  2026. Building from source needs the .NET 10 SDK (pinned by `global.json`). Release downloads bundle the runtime,
  so players and server hosts need nothing new.
- **CI:** builds, tests and a formatting check on Windows and Linux for every change; packaged test builds from
  the `dev` branch; release zips built by CI with `SHA256SUMS.txt`.
- **Community:** code of conduct, security policy with private vulnerability reporting, issue forms (bug report,
  differs from the original, feature request) and Discussions.

## 0.1.0 (2026-10-19)

First public release.

- **Formats:** GFF (read and write), 2DA, TLK, KEY/BIF, ERF/HAK, MDL, DDS/TGA/PLT, TXI and `.gui` layouts, with
  module haks respected throughout.
- **NWScript:** NCS virtual machine with a tiered JIT, all 1,187 engine functions, and a built-in compiler for
  `ExecuteScriptChunk` / `CompileScript`. The 13 official campaign modules and 68 community modules run with zero
  script errors.
- **Rules:** abilities, classes, feats, skills, D20 combat, effects, spells, stealth, traps, resting, death,
  character creation and level-up, all driven by 2DA data.
- **World:** conversations, journal, inventory, stores and barter, containers, doors, triggers, encounters,
  pathfinding, cutscenes and NUI windows.
- **Persistent worlds:** dedicated server, server vault, campaign DB and SQLite, DM client, parties and PvP, and
  content download for missing haks and TLKs.
- **Saves:** single-player save and load with full world snapshots; saves made by the original game load too.
- **Interface:** the classic UI (default), rebuilt from the game's own interface data, and an optional modern UI.
  Character select, load/save, the option pages, the module list and joining a server still use the modern style.
- **NWNX:** 15 plugins (about 230 functions) implemented in the engine, so NWNX scripts run without NWNX.
- **Network protocol:** 3.

Known gaps are listed in the README's [Status and roadmap](README.md#status-and-roadmap) and in
[ROADMAP.md](ROADMAP.md).
