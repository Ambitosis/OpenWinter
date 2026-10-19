# Changelog

OpenWinter follows [semantic versioning](https://semver.org). Until 1.0, minor versions may change behaviour,
save formats and the network protocol; clients and servers must run the same build to connect.

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
