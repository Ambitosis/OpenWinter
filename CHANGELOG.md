# Changelog

OpenWinter follows [semantic versioning](https://semver.org). Until 1.0, minor versions may change behaviour,
save formats and the network protocol; clients and servers must run the same build to connect.

## Unreleased

- **Linux server:** a self-contained `linux-x64` server download alongside the Windows one.
- Haks, TLKs and modules are found whatever the case of their file names, as Linux file systems are case-sensitive.
- An installed client or server with a `config/local.json` keeps its user data in `userdata` beside it.
- A server given game data older than it needs says so at startup instead of crashing on its first tick.
- NuGet updates, including LiteNetLib 2.1.
- The sun and moon light areas from the original's fixed direction, up in the north-east, at every hour (they
  used to arc across the sky), so shading, shadows and `lightAreaDiffuseDirection` match; SetTime's new hour reaches
  players at once.

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
