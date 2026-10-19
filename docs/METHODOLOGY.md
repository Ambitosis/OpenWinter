# Methodology

OpenWinter is a clean-room reimplementation. This page covers where its knowledge of file formats and game
behaviour comes from, and what the repository may and may not contain.

## Rules

1. **Independent code.** OpenWinter's code is written for OpenWinter. Code is never copied from the game's
   executables or from other engines, including NWNX. OpenWinter's developers do not read the original's code,
   disassembly or decompiled output.
2. **No game assets.** The repository holds no models, textures, tables, sounds or module content from the game.
   All of it is read at run time from an installation the player owns, and `.gitignore` blocks the common asset
   extensions. Test fixtures (text models, small 2DA tables, NWScript) are written for this project.
3. **Behaviour is measured, not guessed.** When the original's behaviour is unknown, it is measured from the
   outside: a parity suite on the original `nwserver`, or a visual scene on the original `nwmain`. The result
   becomes a test, and the measured numbers go into the code comment or the doc for that system.
   A written behaviour description (below) is a lead, not evidence: it is confirmed this way before it is relied
   on, and where a measurement and a description disagree, the measurement wins. The few rules that could not be
   measured are marked "not measured" where they are implemented.
4. **Aurora support stays separate.** Format parsing lives in `src/OpenWinter.Aurora`. `src/OpenWinter.Content`
   and the client's rendering and animation code work on neutral content types and never parse Aurora formats
   directly.

## Sources for game behaviour

- **Public documentation:** the NWScript function documentation in `nwscript.nss`, the toolset documentation, the
  game manual, and the D&D 3rd edition rules the game implements.
- **The stock scripts:** much of the game's behaviour lives in its own NWScript (AI, spells, conversations). It runs
  unchanged on OpenWinter's VM, so the engine only has to provide what those scripts expect.
- **The game's data:** 2DA tables, `ruleset.2da`, TLK strings and `.gui` layouts are read at run time, so modules
  and haks that change them change OpenWinter's behaviour too.
- **Black-box measurement:** the parity lab ([parity/README.md](../parity/README.md)) runs the same NWScript suite
  on the original server and on OpenWinter and diffs the logs. The visual harness renders the same authored scene
  in the original client and in OpenWinter and compares screenshots, audio and timing. Both run the original in
  sandboxed user folders.
- **Written behaviour descriptions:** prose descriptions of the original server's observable behaviour (rules,
  numbers, the order of events), written by a separate party who analysed the original server program. They
  contain no code, internal names, addresses or copied tables, and OpenWinter's developers see nothing else from
  that work.

## Sources for format support

| Area | Source |
| --- | --- |
| KEY/BIF, ERF/HAK/MOD, GFF, 2DA, resource type ids | The file format descriptions BioWare published for the Aurora toolset community (2002–2003), as widely restated in public documentation. |
| Binary MDL layout (model, node, mesh, skin and animation headers; controller keys) | Public community descriptions of the compiled model format, checked field by field against installed game files. The skin bone table (node indices in depth-first order) was confirmed empirically. |
| MDL trimesh vertex index lists, specular colour, `tilefade` values | Named in public community format descriptions. Their meaning was worked out from the geometry of installed tile models with the `model-bounds` and `area-tiles` diagnostics. |
| ASCII MDL, PWK | The text format written by the game's model tools. |
| BioWare DDS header | Public format notes, confirmed against installed textures. |
| MDL light and emitter nodes, emitter flags, controller ids, render/update/blend modes | Public community format descriptions and modding documentation, tallied over installed models with the `emitters` and `emitter-survey` diagnostics, and checked on the original with the project's own text test model (visual scene `emitcal`; see [EMITTERS.md](EMITTERS.md)). |
| TLK V3.0, TXI keywords, tile light naming (`ml1`/`sl1`) | Public format descriptions, verified against installed files. |
| Game rules data (appearance, placeables, surfacemat, ...) | Read at run time from the player's 2DA files; nothing is hard-coded from them. |

## Third-party code and assets

- Linked into the build: Godot (MIT), LiteNetLib (MIT), Microsoft.Data.Sqlite and SQLitePCLRaw (MIT) with SQLite
  (public domain), and xUnit (Apache-2.0, tests only).
- Bundled in `client/Assets`: the Cinzel, Inter and EB Garamond fonts under the SIL Open Font License 1.1 (licence
  texts beside the font files), and interface icons from game-icons.net under CC BY 3.0 (authors in
  `client/Assets/Icons/CREDITS.md`).
- The original mouse cursors, portraits, icons and interface art are read at run time from the player's game data,
  so haks that replace them are honoured.
