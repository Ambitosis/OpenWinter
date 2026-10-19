# Contributing to OpenWinter

Thanks for helping. Bug reports, parity measurements and code are all welcome.

## Ground rules

- **Independent code only.** Don't submit code taken from the game's executables or from other engines
  (including NWNX). See [docs/METHODOLOGY.md](docs/METHODOLOGY.md).
- **No game assets.** Don't commit models, textures, tables, sounds or module content from the game, or anything
  extracted from them. Test fixtures you write yourself are fine.
- **Match the original.** OpenWinter aims to play exactly like Neverwinter Nights: Enhanced Edition. When you don't
  know how the original behaves, measure it with a parity suite or a visual scene ([parity/README.md](parity/README.md))
  rather than guess, and say in the pull request how you measured it.
- **Run unchanged content.** Modules, haks and stock scripts must work as they are. Fix the engine, never the content.

## Workflow

1. Fork the repository and create a branch from `main`.
2. Set up as described in the [README](README.md#setup).
3. Make your change. Keep format parsing in `src/OpenWinter.Aurora`, game rules in `src/OpenWinter.Game` (which
   never references Godot), and presentation in `client/`.
4. Check it:
   - `dotnet build OpenWinter.slnx` and `dotnet test` pass. Data tests need `config/local.json`.
   - For client changes, run the game or a capture (`--capture-*` flags in `client/Main.cs`) and look at the result.
   - For behaviour changes, add or update a parity suite or visual scene where you can.
5. Open a pull request covering what changed, how you verified it, and anything that still doesn't match the
   original.

## Style

- Follow the style of the surrounding code. C# uses the settings in `Directory.Build.props` (nullable enabled).
- Comments are short and say what the code does or why, including the original behaviour it matches.
- The repository is LF-only (`.gitattributes`). On Windows, make sure your editor and scripts keep LF line endings
  and UTF-8 encoding.

## Licence

By contributing, you agree that your contributions are licensed under the [GNU GPL v3.0](LICENSE).
