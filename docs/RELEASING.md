# Branches, CI and releases

## Branches

| Branch | What it holds |
| --- | --- |
| `dev` | New work. Every push is built, tested and packaged as a test build. Pull requests target this branch. |
| `main` | The latest tested state. It only moves forward to a `dev` commit that has passed CI and been played. |
| `v*` tags | Releases, always on `main`. |

## CI

`.github/workflows/ci.yml` runs on every push to `main` and `dev` and on every pull request, on Windows and
Linux:

- `dotnet format whitespace OpenWinter.slnx --verify-no-changes`: whitespace and layout per `.editorconfig`.
- `dotnet build OpenWinter.slnx -c Release`, including the Godot client (the Godot .NET SDK package is enough to
  compile it).
- `dotnet test`. Tests that need the game's data are skipped, since CI has no copy of the game; run them locally
  with `config/local.json` before merging.

Parity suites and visual scenes run the original game and are not part of CI.

Actions are pinned to commit SHAs and Godot downloads to their published SHA-512; Dependabot proposes weekly
updates for actions and NuGet packages (`.github/dependabot.yml`). The Godot .NET SDK is left out: it must match
the editor and export templates, so `GODOT_VERSION`, the checksums and `Godot.NET.Sdk` change together.

## Test builds

`.github/workflows/build.yml` packages every push to `dev` with Godot and its export templates, using
`build/package.py`. The zips are versioned `<version>-dev.<run number>` and attached to the workflow run under
**Actions → Build** (signed-in GitHub users can download them for 30 days). A test client only connects to a
server from the same build.

## Releasing

1. On `dev`: set `VersionPrefix` in `Directory.Build.props`, the matching `application/file_version` and
   `product_version` (`<version>.0`) in `client/export_presets.cfg` (packaging refuses a mismatch), the download
   names and current release in `README.md`, and add the version's entry to `CHANGELOG.md` (`## <version> (<date>)`).
2. Wait for CI and the test build to pass, and play the test build.
3. Fast-forward `main` and tag it:

   ```
   git switch main
   git merge --ff-only dev
   git tag v<version>
   git push origin main v<version>
   ```

4. The tag's build checks that it matches `VersionPrefix`, packages the client and server, and creates a draft
   pre-release with the zips, `SHA256SUMS.txt` and the CHANGELOG entry as notes. Review it and publish.

To package locally: `python build/package.py` (needs Godot 4.7 .NET with its Windows export templates; set `GODOT`
to the console executable if it isn't the winget install). Output goes to `out/dist`.
