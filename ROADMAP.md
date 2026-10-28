# Roadmap

**Goal:** a 1:1 replacement engine for Aurora-format games: any NWN module or
persistent world runs on it unchanged, built and maintained with the existing
toolset. On top of that: modern rendering, animation, UI and tooling the
original can't offer.

Compatibility means *all* engine behaviour, not just what one module uses:
every NWScript engine function (~1,190 in `nwscript.nss`), every module, area
and object event, the full rules system, and PW infrastructure.

The official campaign and existing persistent worlds are the test cases, not the boundary.

## Architecture

```
src/OpenWinter.Content   neutral content types (models, textures)       no NWN, no Godot
src/OpenWinter.Aurora    Aurora formats → Content (GFF r/w, 2DA, TLK, KEY/BIF, ERF, MDL, DDS/TGA/PLT)
src/OpenWinter.Game      authoritative simulation: world, areas, objects, scripts, rules. No Godot
src/OpenWinter.Net       protocol + transports (loopback, LiteNetLib UDP)
src/OpenWinter.Server    dedicated server (console; runs anywhere .NET 10 runs)
src/OpenWinter.Tools     developer diagnostics against local data
client/               Godot 4 C# client: rendering, animation, UI, input
tests/                xUnit (format round-trips; data tests run when config/local.json exists)
```

- The **server owns all game state**. Clients render what they're told and send input.
- **Single-player** runs the server in-process over a loopback connection: one code path.
- Clients never need the module file. Like the original, they get area layout and
  objects from the server and only need haks, the TLK and the base game locally.

## Milestones

### 1. Foundation ✅
- [x] C# solution; format library with GFF writer; unit tests
- [x] Server/client split, loopback + UDP transports, dedicated server
- [x] Whole-module load (40 areas, 3,879 objects in < 1 s)
- [x] Walkmesh (tiles + placeable PWK + door DWK) in pure C#
- [x] Rendering: tiles, placeables, doors, lights (tile main/source light colours), emitters (fountain/single), tile fade (black caps and edge plugs; as in the original nothing fades in play, the keyhole shows the player), merged-mesh index lists, TXI-aware alpha
- [x] Animation graph: stance state machine, speed blend spaces, upper-body gestures, full-body actions

### 2. Characters ✅
- [x] Part-based models (heads, bodies, phenotypes, genders, races)
- [x] PLT palette tinting (skin, hair, tattoo, metal, cloth, leather) for part-based and regular creatures
- [x] Equipment visuals: armour parts (incl. EE part numbers > 255), robes, cloaks, weapons, shields, helmets
- [x] Load characters from localvault BIC files (`--character name`)
- [x] Wings and tails (static for now)
- [ ] Animated wings/tails following the body's animation
- [x] Portraits in the UI
- [ ] Per-part armour colour overrides (EE), helmet hair hiding

### 3. NWScript ✅
- [x] NCS VM (all opcodes, STORE_STATE closures, engine types), tiered JIT to native code on a background thread
- [x] All 1,187 engine functions in `nwscript.nss` implemented (duplicate-registration guard + test)
- [x] Module/area/object events, heartbeats, DelayCommand, AssignCommand, action queues, user-defined events
- [x] Default AI and the official campaign run unmodified: all 13 campaign modules and 68 user modules run with zero script errors
- [x] NWNX: legacy ABIv2 bridge plus native implementations of the common plugins

### 4. Interaction ✅
- [x] Conversations with conditions, actions, tokens; journal; examine; map with exploration and pins
- [x] Inventory, equipping, containers/looting, body bags, stores, item properties, icons
- [x] Doors/placeables with locks, keys, traps, transitions; triggers; encounters
- [x] Click-to-move with pathfinding; right-click context menus; NUI windows; script camera, fades, cutscenes, targeting mode

### 5. Rules ✅
- [x] Abilities, classes, levels, feats, skills, saves, all 2DA-driven (module haks respected)
- [x] D20 combat: iterative attacks, finesse, two-weapon, criticals, sneak attack, concealment, AoO, special attacks, ammo
- [x] Effects, immunities, spells (slots, metamagic, SR, AoEs, projectiles), resting, death, corpses
- [x] Skills, stealth/detection, traps; character creation and level-up wizard with legal-character checks

### 6. Persistent worlds ✅
- [x] Server vault login, saving on logout and periodically; campaign DB and SQLite in the original layout
- [x] DM client: possession, creator palettes, DM tools, DM chat; parties, tells, PvP settings
- [x] Server settings file; content download (haks/TLK by SHA-1) for players missing them
- [x] Single-player save/load with full world snapshots (including pending scripts)
- [ ] Content delivery v2 ([docs/CONTENT_DELIVERY.md](docs/CONTENT_DELIVERY.md)): a static/CDN file service separate from the game server,
      per-resource content addressing with a shared cache, zstd compression, overrides/music/portraits/movies,
      signed manifests, play-while-downloading, and NWSync read/write

### 7. Performance
- [x] Tiered JIT with background compilation (no stalls on big scripts)
- [x] Simulation profiling (`run-module --profile`): a busy campaign chapter (547 creatures) uses ~6% of a core
- [ ] JIT phase 2: typed stack slots, inlined engine calls, on-disk cache
- [ ] Move non-script work off the simulation thread (networking, perception, persistence writes)
- [ ] Client: background model/texture decoding, instanced tiles, streaming for very large areas

### 8. UI overhaul ✅
- [x] UI kit: design tokens, OFL fonts, CC BY icons, global theme, glass panels, window frame ([docs/UI.md](docs/UI.md))
- [x] HUD: player/party/target frames, minimap, quest tracker, floating combat text, toasts, chat tabs, cursors, death screen
- [x] 1:1 grid inventory (server-side positions, capacity, moves), optional modern view; stores, loot, bags, item tooltips, examine
- [x] Character sheet, spellbook with preparation, character creation and level-up wizard
- [x] Cinematic dialogue, journal, zoomable map; action bar (3 pages), radial menu and abilities window: items, feats, skills and modes usable
- [x] Main menu, module browser, character select, save/load with thumbnails, loading screens, game menu, options with key rebinding
- [ ] Playtesting the official campaign
- [ ] NUI windows keep their NWN look; DM UI uses the theme but hasn't had a full redesign

### 8b. Classic UI mode
- [ ] A second, complete UI mode true to the original game (layout, look and behaviour, built from the
      game's own interface textures read at runtime), chosen from the main menu / Options; the classic UI is the default, the modern UI an option
  - [x] Foundation: interface models, layouts (.gui), bitmap fonts, generic controls, Options → Interface style, `--ui classic`, captures ([docs/CLASSIC_UI.md](docs/CLASSIC_UI.md))
  - [x] HUD (quickbar, party bar, compass, chat and talk line, action queue, radial) · inventory family (inventory, containers, store, examine, stack split) · sheets (character, spellbook and memorize, journal, maps, examine) · dialogs and menus (conversation, game menu, popups, death) · main menu and loading screen
  - [x] Character creation and level-up (cg_*.gui, [docs/CLASSIC_CHARGEN.md](docs/CLASSIC_CHARGEN.md))
  - [x] Player barter ([docs/BARTER.md](docs/BARTER.md))
  - [ ] Character select, the modern sub-pages still used (load/save, option pages, other modules, join server)
- [ ] NUI windows work unchanged in both modes, so PWs that use NUI need no changes

### 9. Known gaps
- [ ] Animated wings/tails; per-part armour colour overrides; helmet hair hiding
- [ ] Visual transforms on head/tail/wing/cloak scopes; tile animation loops; tree wind sway
- [x] Haste/slow AC changes; Defensive Roll; coup de grace; death attack paralysis; racial crit/sneak immunity
- [x] Game difficulty in combat and its default ([docs/DIFFICULTY.md](docs/DIFFICULTY.md))
- [x] Prestige spellcasting progression: slots follow the caster level, spells known the base class level (parity `prestige_casting`)
- [ ] Sorcerer/bard spell swap
- [x] Saves made by the original game load ([docs/SAVEGAME.md](docs/SAVEGAME.md))
- [x] Missing walls/floors/furniture in tiles (merged triangle lists) and tile fading (cut-away walls, caps, plugs)
- [ ] Interior lighting is darker than the original; roof gaps on some placeables

### 10. Native platform API; NWNX and Anvil as compatibility layers (planned, after parity)
Everything NWNX and Anvil offer becomes a first-class engine feature. NWNX and Anvil shrink to thin,
frozen adapters over those features, so an existing PW runs on day one. Its developers then port call by
call to the native API at their own pace and drop the adapters when done. The adapters are never removed.
- [ ] Native API: NWScript extension functions (shipped as an OpenWinter include) and a server-side C# API,
      designed as engine features rather than copies of NWNX's argument stack and string-keyed events
- [ ] Move the logic of the existing plugins (`src/OpenWinter.Game/Nwnx/Plugins`)
      into engine systems, leaving adapters; do this before adding more plugins
- [ ] NWNX completeness: every plugin and function in the `nwnx_*.nss` includes, with a coverage test against
      those declarations; adapters reproduce NWNX behaviour exactly, quirks included
- [ ] Anvil compatibility: stand-in `NWN.Anvil` / `NWN.Core` assemblies over the native API, so plugins using
      Anvil's high-level API load unmodified; code using `NWN.Native` (raw nwserver memory) can't be emulated
      and must be ported
- [ ] Migration tooling: a report of every NWNX/Anvil call a module or plugin uses, with its native equivalent;
      native and legacy calls mix freely in one module; optional deprecation hints; automatic rewrites for
      simple NWScript cases; a Roslyn analyzer with code fixes for Anvil C#
- [ ] Docs: native API reference and an NWNX/Anvil → native mapping table

### 11. Server extensions (planned, after parity)
OpenWinter servers can ship deep client changes, up to total conversions (first person, HD models, a different
setting), scoped to that server. Stock modules and the official campaign are never affected.
- [ ] Per-server isolation: a server's packages, scripts, assets, cache and settings mount on connect and
      unload on disconnect
- [ ] Server-side code: unrestricted C# on the server operator's hardware, using the milestone 10 API
- [ ] Client-side code: sandboxed scripting (MoonSharp Lua or WebAssembly) limited to an engine API (camera,
      input, movement prediction, UI, rendering hooks, messages to its own server); servers can never ship
      native DLLs or unsandboxed scripts to clients
- [ ] Packages: name, version, dependencies, scripts, Godot `.pck` assets and requested permissions; the join
      handshake downloads, verifies and caches them; players approve a server's extensions; a shared registry
      lets servers adopt each other's packages
- [ ] Engine seams: swappable camera, input and movement controllers; glTF models with PBR materials alongside
      MDL; a fully replaceable UI layer; free 3D movement; larger or streamed areas
- [ ] Until then: keep camera, input and movement code free of NWN-only assumptions where that costs little

#### Deferred extension notes: movement, interactions and labels

These are design notes for milestones 10/11, not new public functions or a package implementation.
Keep measured NWN behavior as the engine default. Existing NWScript functions and hak/2DA/TLK
resource overrides continue to work without installing a package.

- Move/attack feedback: allow a package to choose command classes that show a reticle, runtime model
  references, tint, scale and animation. Accepted destination/target and command cancellation stay
  server-authoritative; a cosmetic override must not report a rejected move as legal or reveal hidden targets.
- Placeable interactions: expose authored use-point selection (including PWK front points), approach
  distance, facing, actor animation/timing and object open/close presentation. Keep action queues,
  locks, reachability, use permission, inventory access and authoritative open state in engine systems.
  Reuse `ReplaceObjectAnimation` semantics; do not make visual hooks change conversation-abort rules.
- Object labels: allow visibility by object kind and trigger (hover, tooltip key, targeting), optional
  player/character/status lines, font/layout and reaction/health palettes, text/portrait/bubble templates.
  Preserve the player's Off mode and readability preferences. Use viewer-relative hostility and party/faction
  membership for stock name colors; keep it distinct from `SetObjectHiliteColor`'s model highlight. Health wording
  comes from `damagelevels.2da`/TLK; that table has no thresholds or color columns. Custom layouts should
  receive only the health category unless rules explicitly authorize exact HP. Account names must obey
  server identity/privacy policy, and hidden creatures must not leak through label callbacks.
- Scope and lifetime: explicit server/session defaults, per-object overrides and per-viewer overrides;
  deterministic precedence, reset-to-default operations, late-join snapshots and reliable changes.
  Unload on disconnect/module transfer and restore stock behavior; avoid process-global mutable settings.
- API shape: typed C# services plus NWScript wrappers over the same implementation. Adapt existing
  EE/NWNX calls (text bubbles, damage-level/name overrides and animation replacement) to these services
  when the package system is built; internal client notifications are not the promised public API.
- Acceptance checks: connect to two differently customized servers and then a stock module, including
  reconnects and area changes; verify isolation, cleanup, late join and differing per-player labels.
  Preserve default visual/rules parity tests alongside package-specific tests.
