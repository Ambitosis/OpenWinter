# OpenWinter

An open-source engine for Neverwinter Nights, written in C# (.NET 8) with a Godot 4.7 client.

## What OpenWinter is

OpenWinter is a complete, clean-room reimplementation of the engine behind Neverwinter Nights: Enhanced Edition.
That means the game server, the NWScript virtual machine, the D&D rules, persistence, networking and the client that
draws it all. Its developers have never seen the original engine's proprietary code. They work from public
documentation, the game's own data and scripts, black-box measurement of the original, and written behaviour
descriptions that contain no code, internal names or addresses. Behaviour is then checked against the original game
wherever it can be measured ([docs/METHODOLOGY.md](docs/METHODOLOGY.md)).

It reads the game's own data, so the official campaign, community modules, haks and persistent worlds run on it
unchanged, and they are still built with the original toolset.

Compatibility is the foundation, but no longer the ceiling. NWN:EE's engine is a closed binary: modules can only do
what its 1,187 script functions allow, and servers that want more patch `nwserver` in memory through NWNX. OpenWinter is
the whole engine as readable, modifiable C#. Anything the engine does, you can change, and anything it doesn't do,
you can add.

**OpenWinter contains no game assets.** It reads everything at run time from a copy of Neverwinter Nights:
Enhanced Edition that you own. No code from the original engine was used, and OpenWinter's developers never worked
from its code, disassembly or decompiled output.

## Download and play

### What you need

- Windows 10 or 11, 64-bit, with a graphics card that supports Vulkan or Direct3D 12.
- **Neverwinter Nights: Enhanced Edition**, installed from Steam, GOG or Beamdog. OpenWinter uses its data; you
  don't need to start it.

### Install and play

1. Download `OpenWinter-0.1.0-windows-x64.zip` from the [latest release](https://github.com/Ambitosis/openwinter/releases/latest).
2. Unzip it anywhere, for example `C:\Games\OpenWinter`.
3. Run `OpenWinter.exe`. It finds your NWN:EE installation by itself.
4. Choose **New Game**, pick a module, then pick or create a character.

Characters in your NWN `localvault` are listed but never changed. OpenWinter keeps its own settings, saves and new
characters in `%APPDATA%\Godot\app_userdata\OpenWinter` and never writes to your Neverwinter Nights folder.

OpenWinter starts with the classic interface, the original game's look. The redesigned modern interface is under
**Options → Interface**.

If OpenWinter can't find the game (a custom install folder, for example), create `config\local.json` next to
`OpenWinter.exe` with the folder that holds NWN:EE's `data` folder:

```json
{ "gameRoot": "D:/Games/Neverwinter Nights" }
```

### Host a server

1. Download `OpenWinter-Server-0.1.0-windows-x64.zip` from the same release and unzip it.
2. Run `openwinter-server.exe --module "<path to a .mod file or module folder>"`. It listens on UDP port 5121
   (`--port` changes it) and writes `server.json` with its settings on the first run; edit that file for the
   server name, PvP, difficulty, DM password and more. The server vault and databases are kept in `userdata`
   next to the server.
3. Players choose **Multiplayer**, enter the server's address (`host:5121`) and press **Connect**. Haks and TLKs
   they're missing download automatically.

Server and players must run the same OpenWinter version. The server needs NWN:EE's data too: it finds a local
installation the same way the client does, or reads `gameRoot` from `config\local.json` next to it.

### Reporting bugs

OpenWinter is an early pre-release, so expect rough edges. Please report problems on the
[issue tracker](https://github.com/Ambitosis/openwinter/issues) with:

- what you did, what happened, and what the original game does instead;
- the module and, if you can, a save game;
- the log: `%APPDATA%\Godot\app_userdata\OpenWinter\logs\godot.log`.

## Status and roadmap

The current release is **0.1.0**, an early pre-release ([CHANGELOG.md](CHANGELOG.md)). OpenWinter plays the official
campaign and community modules today, but it is not yet a drop-in replacement for every player or every server.
[ROADMAP.md](ROADMAP.md) has the full checklist.

### Works today

- **Formats:** GFF (read and write), 2DA, TLK, KEY/BIF, ERF/HAK, MDL, DDS/TGA/PLT, TXI and `.gui` layouts, with
  module haks respected throughout.
- **NWScript:** an NCS virtual machine with a tiered JIT, all 1,187 engine functions in `nwscript.nss`, and a
  built-in compiler for `ExecuteScriptChunk` / `CompileScript`. All 13 official campaign modules and 68 community
  modules load and run with zero script errors.
- **Rules:** abilities, classes, feats, skills, D20 combat, effects, spells, stealth, traps, resting, death,
  character creation and level-up, all driven by 2DA data.
- **World:** conversations, journal, inventory, stores and barter, containers, doors, triggers, encounters,
  pathfinding, cutscenes and NUI windows.
- **Persistent worlds:** a dedicated server, server vault, campaign DB and SQLite in the original layout, the DM
  client, parties and PvP, and content download for missing haks and TLKs.
- **Saves:** single-player save and load with full world snapshots, including pending scripts, and saves made by
  the original game.
- **Interface:** two UI modes. The **classic** UI, the default, rebuilds the original's interface from the game's
  own data. The **modern** UI is OpenWinter's redesign (Options → Interface, or `--ui modern`).
- **NWNX:** scripts that call NWNX run without NWNX installed. 15 plugins (about 230 functions) are implemented in
  the engine.

### In progress

- **Playtesting** the official campaign end to end, and fixing what it finds.
- **Classic UI:** character select and the screens that still use the modern style (load/save, option pages,
  module list, joining a server).
- **Visual gaps:** animated wings and tails, EE per-part armour colours, helmets hiding hair, tile animation loops,
  tree sway, and interiors that render darker than the original.
- **Rules gaps:** prestige classes adding spells known to the base caster class, and sorcerer/bard spell swaps.
- **Performance:** the second JIT phase (typed stack slots, inlined engine calls, an on-disk cache), moving
  networking, perception and saving off the simulation thread, and streaming very large areas.

### Planned

- **Content delivery v2:** a CDN-friendly content service with a shared cache, compression, signed manifests,
  play while downloading, and NWSync support ([docs/CONTENT_DELIVERY.md](docs/CONTENT_DELIVERY.md)).
- **Native platform API** with NWNX and Anvil as compatibility layers (ROADMAP §10).
- **Server extensions:** per-server packages, sandboxed client-side code and a shared package registry
  (ROADMAP §11). See [Built to be extended](#built-to-be-extended).

### How behaviour is verified

Behaviour is checked black-box against the real game. The parity lab runs 368 rule suites on the original
`nwserver` and on OpenWinter and diffs the results. The visual harness renders 577 authored scenes in `nwmain` and in
OpenWinter side by side, comparing screenshots, audio and timing. About 1,300 unit tests cover the engine itself. See
[docs/METHODOLOGY.md](docs/METHODOLOGY.md) and [parity/README.md](parity/README.md).

## Built to be extended

### What server owners control

With OpenWinter, the server owner controls the whole engine, not just the module that runs on it. The dedicated
server is an ordinary .NET 8 process with no graphics dependency, so it runs wherever .NET runs: a Windows box, a
Linux VPS or a container. Everything inside it is yours to configure, replace or rewrite: the rules, the timing of
the combat round, which systems run each tick, what is saved and how, and which messages go to players. Nothing is
reached through memory patches or hooks into a binary you can't see.

### What you can build today

The engine is organised so that common extensions are small, self-contained additions:

| To add | You write | It is picked up by |
| --- | --- | --- |
| A new game system (hunger and thirst, a living economy, weather, reputation, faction warfare) | A class implementing `IWorldSystem` with a `World` constructor | Automatic discovery; it is ticked every world step in the order you choose |
| A different combat model (alternative d20 rules, action points, a system that isn't D&D at all) | An `ICombatRules` implementation | `CombatSystem.Rules`; the round timing, targeting and animations keep working |
| New built-in NWScript functions | A declaration in `nwscript.nss` and a C# method marked `[NwFunc]` | Name binding at start-up; the built-in compiler and VM call it like any engine function |
| NWNX-style functions for existing PW scripts | A C# method marked `[NwnxFunc]` | The NWNX dispatcher; no NWNX install needed |
| New client actions and server messages (custom windows, minigames, new interactions) | A `[ClientRequest]` handler on the server, `IWorldOutput.Notify` events to clients, and the matching Godot UI | Automatic discovery on the server; the client shows the new events |

Because the server is plain C#, it can also talk directly to anything .NET can: your own database, web APIs, chat
bridges, metrics and admin tools, with no plugin layer in between.

Much of the game is already data rather than code. Rules read `baseitems.2da`, `feat.2da`, `ruleset.2da` and the
other tables at run time, so a hak that changes those tables changes OpenWinter's behaviour too, with no code at all.
Format parsing is kept apart from the simulation (`OpenWinter.Aurora` reads the files into the neutral types in
`OpenWinter.Content`), which keeps the door open to content beyond the Aurora formats.

### Mechanics NWN:EE can't do

Changes like these need a different engine, not a cleverer module:

- New combat models, character systems and rules that replace the D&D 3.x core rather than script around it.
- Server-side simulation that runs every tick with direct access to world state, not through heartbeat scripts.
- New object types, interactions and network messages, with a client that knows how to show them.
- Camera, movement and interface changes: first-person or over-the-shoulder views, a completely different UI,
  larger areas.
- Modern rendering and new content formats alongside MDL.

### Where it's going

Today, extending OpenWinter means changing the engine. A server that changes what players see or send needs those
players on the matching client build: connections check the protocol, engine version and a source fingerprint. Two
planned milestones ([ROADMAP](ROADMAP.md) §10–11) make extension a supported platform instead of a fork:

- **A native platform API, with NWNX and Anvil as compatibility layers.** Everything NWNX and Anvil offer becomes
  a first-class engine feature, reachable from NWScript (through an OpenWinter include) and from server-side C#.
  NWNX and Anvil become thin adapters over those features, so an existing PW runs on day one. Its developers can
  then port call by call to the native API, helped by a migration report, logged hints and a Roslyn analyzer.
- **Server extensions.** An OpenWinter server can ship deep client changes scoped to itself, up to a total
  conversion. Server-side code is unrestricted C#. Client-side code runs in a sandbox and ships as versioned
  packages that mount when you connect and unload when you leave, so the official campaign and stock modules are
  never affected, and a player can visit differently modded servers with one client.

## How it works

```
src/OpenWinter.Content   neutral content types (models, textures)       no NWN, no Godot
src/OpenWinter.Aurora    Aurora formats → Content
src/OpenWinter.Game      authoritative simulation: world, scripts, rules, AI, persistence, NWNX. No Godot
src/OpenWinter.Net       protocol and transports (loopback, LiteNetLib UDP)
src/OpenWinter.Server    dedicated server (console; runs anywhere .NET 8 runs)
src/OpenWinter.Tools     diagnostics, parity lab and visual harness drivers
client/                  Godot .NET client: rendering, animation, audio, modern / classic / NUI interfaces
tests/                   xUnit (data tests run when config/local.json exists)
parity/                  rule suites and visual scenes compared against the original game
```

- The **server owns all game state**. Clients render what they're told and send input.
- **Single player** runs the same server in-process over a loopback connection, so there is one code path.
- Like the original, **clients never read the module**. They get areas and objects from the server and only need
  the base game, haks and the TLK locally.

## Building from source

1. Install the .NET 8 SDK and Godot 4.7 **.NET** (`winget install GodotEngine.GodotEngine.Mono`).
2. Copy `config/local.example.json` to `config/local.json` and set your game, user and module paths.
3. `dotnet build OpenWinter.slnx`

Optional `config/local.json` keys:

| Key | Use |
| --- | --- |
| `devDisplay` | Monitor for dev windows (e.g. `"DISPLAY2"`). `tools/godot.sh` places the client there, and `tools/dev-display.ps1 -Move nwmain` moves an original-game window there. |
| `liveUserData` | Write to your real NWN user folder. Off by default: campaign databases, SQL and vault exports go to `out/sandbox-userdata`. |
| `nimTools` | Folder with the [neverwinter.nim](https://github.com/niv/neverwinter.nim) tools, used by some persistence and save-game tests. |
| `nwmainLock` | Lock file that serialises visual-harness runs of `nwmain` across checkouts (default: in the system temp folder). |

## Running

| Command | What |
| --- | --- |
| `tools/godot.sh` | Run the client; single player runs the server in-process |
| `tools/godot.sh -- --module <.mod/.nwm> --character <vault resref>` | Start a module with a character |
| `tools/godot.sh -- --area ss_town` | Start in a specific area |
| `tools/godot.sh -- --ui modern` | Use the modern interface (command-line captures use it unless `--ui classic` is given) |
| `dotnet run --project src/OpenWinter.Server -- --port 5121` | Dedicated server (writes `server.json` on first run; also `--module`, `--pvp`, `--oneparty 0|1`, `--dmpassword`, ...) |
| `tools/godot.sh -- --connect 127.0.0.1:5121` | Join a server |
| `tools/godot.sh -- --capture out/demo` | Scripted demo; saves screenshots and quits (`--capture-hud` and the other `--capture-*` flags are in `client/Main.cs`) |
| `tools/godot.sh -- --look "x,y,z,yaw,pitch,dist;..." --out dir` | Screenshots from fixed viewpoints |
| `tools/godot.sh -- --playtest /abs/out --module <.mod/.nwm> --character <vault resref> --minutes 25 --godmode` | Autopilot plays the module and writes `report.md` / `.html` / `.json` (bugs with screenshots and log excerpts) |
| `dotnet test` | Tests |
| `dotnet run --project src/OpenWinter.Tools -c Release -- <command>` | Data diagnostics (`model`, `area-textures`, `near`, `unimplemented`, `run-module --profile`, ...) |
| `dotnet run --project src/OpenWinter.Tools -c Release -- parity-run <suite>` | Run a rule suite on the original and on OpenWinter, then diff |
| `python tools/visual/visual.py run <scene>` | Render a scene in the original and in OpenWinter, side by side |

Controls follow the original's `keymap.2da`; see [docs/CONTROLS.md](docs/CONTROLS.md).

Multiplayer requires the same OpenWinter build on the server and every client. Connections check the protocol,
engine version and an automatically generated runtime-source fingerprint before login or content download. See
[docs/NETWORKING.md](docs/NETWORKING.md) for the compatibility policy.

## Documentation

| Doc | About |
| --- | --- |
| [ROADMAP.md](ROADMAP.md) | Milestones and status |
| [CONTRIBUTING.md](CONTRIBUTING.md) | How to contribute |
| [LEGAL.md](LEGAL.md) | Ownership, piracy, licences and trademarks |
| [docs/METHODOLOGY.md](docs/METHODOLOGY.md) | How formats and behaviour are established |
| [parity/README.md](parity/README.md) | Parity lab and visual harness |
| [docs/SCRIPTING.md](docs/SCRIPTING.md) | Implementing engine functions in the script VM |
| [docs/STOCK_AI.md](docs/STOCK_AI.md) | The stock creature and henchman AI: what is scripted, what the engine must match |
| [docs/UI.md](docs/UI.md), [docs/CLASSIC_UI.md](docs/CLASSIC_UI.md) | Modern UI kit and rules; the classic interface |
| [docs/CONTROLS.md](docs/CONTROLS.md) | Keys and mouse |
| [docs/NETWORKING.md](docs/NETWORKING.md) | Protocol and compatibility |
| [docs/NOTIFY.md](docs/NOTIFY.md) | Server-to-client notifications |
| [docs/SAVEGAME.md](docs/SAVEGAME.md) | Save games |
| [docs/CONTENT_DELIVERY.md](docs/CONTENT_DELIVERY.md) | Content delivery |
| [docs/EMITTERS.md](docs/EMITTERS.md) | Particle emitters |

The other files in [docs/](docs) describe individual game systems (combat, stealth, traps, parties, doors,
encounters, voices and more) and how OpenWinter matches the original's behaviour in each.

## License and legal

OpenWinter is free software, licensed under the [GNU General Public License v3.0](LICENSE).
Copyright © 2026 Ambitosis and the OpenWinter contributors.

**OpenWinter is not affiliated with or endorsed by Wizards of the Coast, Hasbro, Beamdog or BioWare.** It contains
no game data: you must own a legal copy of Neverwinter Nights: Enhanced Edition to play. We do not support or
condone pirated copies of the game. [LEGAL.md](LEGAL.md) covers who owns what, the third-party asset licences and
trademarks.
