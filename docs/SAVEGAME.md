# Save games

Single-player save, load, quick save and autosave (`src/OpenWinter.Game/SaveGames/`). A save
restores the *whole* simulation: every area and object with its original object id, local
variables, effects, inventories, factions and reputation, the journal, the clock, and the
script state in flight (DelayCommand / AssignCommand closures, action queues with
ActionDoCommand, engine timers), then resumes exactly where it was.

## Layout on disk

Saves live under `PersistenceSystem.UserRoot` (the dev sandbox `out/sandbox-userdata` unless
live user data is enabled; tests use a temp folder), mirroring NWN:

```
saves/
  000000 - QUICKSAVE/        quick save slot (overwritten)
  000001 - AUTOSAVE/         DoSinglePlayerAutoSave slot (overwritten)
  000002 - My Save/          manual saves: next free number; saving under an existing name reuses its slot
    savegame.sav             ERF, file type "SAV " (V1.0; V1.1 if a resref is longer than 16)
    savename.txt             the save's display name (Windows-1252)
    <character>.bic          each player character (BIC with current state, position, ObjectId)
    screen.tga               optional thumbnail sent by the client (uncompressed 24-bit TGA, 256x160)
```

A save is written to `saves/.tmp-<guid>/` and moved into place; an existing slot is renamed
aside first and deleted only after the move succeeded, so a failed save never damages the old one.

## savegame.sav

| resource | type | contents |
| --- | --- | --- |
| `module` | IFO | `module.ifo` with the current date/hour (`Mod_Start*`), `Mod_Area_list` (current areas, with `ObjectId`), module locals (`VarTable`) and event scripts |
| `<area>` | ARE | each area's ARE with its current tag, name, `Tile_List` (SetTile changes), scripts and locals |
| `<area>` | GIT | the area's objects in their *current* state, in the usual lists (`Creature List`, `Door List`, `Placeable List`, `WaypointList`, `TriggerList`, `SoundList`, `Encounter List`, `StoreList`, `List` for items on the ground, `AreaEffectList`), each struct with `ObjectId` and its state (`VarTable`, `EffectList`, `ActionList`, `Transition`, in the original's layout: see "Object state" below) |
| `module` | JRL | the module's journal (copied) |
| `playerlist` | IFO | `Mod_PlayerList`: the player characters as BIC structs |
| `saveinfo` | GFF ("SINF") | listing data: `Name`, `Kind`, `SavedAt` (ISO 8601), `Module`, `AreaName`, `AreaResRef`, `PlayerName`, `GameTime` |
| `module` | SQ3 | the module database (an "SQL3" zstd buffer), as the original's saves hold it |
| `openwinter` | GFF ("ELYS") | the engine snapshot: `Format` (int), `Engine` (build id), `Encoding` = `json+gzip`, `RawSize`, `Snapshot` (VOID: gzip of the JSON below) |

The NWN-shaped resources are written for tools and for fidelity with the original layout.
**OpenWinter restores from the `openwinter` snapshot**, which carries everything those formats have no
field for. The ERF writer is `OpenWinter.Aurora.Formats.Erf` (BioWare's published ERF layout).

## The engine snapshot

`WorldSnapshot.Capture` serialises the world by reflection (`SnapshotWriter` / `SnapshotReader`),
so every field a package adds to an object, system or action is saved without per-field code.

This includes quickbar edits and their item references. Character BIC exports also update `QBList`, preserving
untouched unsupported entries; OpenWinter-only actions and item UUID bindings use the extensions in [UI.md](UI.md).

```
{ "format": 1, "engine": <assembly MVID>, "time", "nextId", "timerSeq",
  "players": [ { "obj", "player", "vaultFile" } ],
  "world":   { World fields: clock, factions, limits, areas, tag index, module, pause state, ... },
  "systems": [ { "type": <IWorldSystem full name>, "fields": { ... } } ],
  "host":    { "seq", "params", "scheduled": [ { "at", "seq", "self", "s": <ScriptSituation> } ] },
  "timers":  [ { "at", "seq", "do": <delegate> } ],
  "objects": [ { "k": key, "id", "t": type, "reg": registered?, "resref"/"src" (areas), "f": { fields } } ],
  "extra":   { "moduleDb", "characterDbs": { key: GFF "SQLite" struct }, "tempTemplates": [...] } }
```

Value encoding:

- primitives as JSON values (enums as numbers; NaN/Infinity as strings), strings as strings;
  `Vector3`/`Vector2` as arrays; script stack cells as `I` or `[I, ref]`
- classes and structs as objects of their instance fields (all access levels, base classes
  included); `"$t"` names the runtime type when it differs from the declared one
- lists, sets, stacks and queues as arrays; dictionaries as `[[key, value], ...]`; a non-default
  string comparer (case-insensitive keys) is kept as `{"$cmp": name, "$v": [...]}`
- game objects as `{"$obj": key}` (the object id for registered objects, `u<n>` for unregistered
  ones, which are saved too); engine services (`World`, systems, `ScriptHost`, `ResMan`, factories,
  the output) as `{"$svc": name}`, bound to the new world's instances on load
- an instance referenced more than once gets `"$id"`; later references are `{"$ref": id}`
- `GffStruct` as `{"$gff": base64 GFF}`, `JsonNode` as `{"$json": text}`, compiled scripts as
  `{"$prog": resref}` (re-resolved on load), delegates as `{"$del": [{type, name, params, target}]}`
- not saved (host resources, see `SnapshotTypes`): player connections, SQLite handles,
  walkmeshes and pathfinders, NUI windows, 2DA/TLK caches, random generators, and delegates
  outside closures (event subscriptions). Fields holding them keep the new world's values.

Values are read into existing instances where possible (the new world's systems, collections made
by field initializers), so unsaved fields keep working values and collections keep their comparers.
New instances are made with a constructor when one can be called (parameterless, or with optional
or engine-service parameters).

DelayCommand / AssignCommand closures are `ScriptSituation`s (script resref, resume offset, saved
globals and locals), so stack values of every type (ints, strings, objects, locations, effects,
item properties, json, ...) survive. Action queues are lists of `GameAction` objects saved field by
field. Engine timers (`World.After`) are C# closures: their compiler-generated capture classes are
saved like any object and the delegate is rebound to the same method by name.
Engine timers survive a reload unless marked non-persistent, in which case they are omitted from
the snapshot. Summon arrival is the one such event, as in the original: a summoned creature that has
not yet arrived loses its arrival on load, while its master's effect and remaining expiry survive.
Saving without loading still lets the event run.

## Saving

`SaveGameService.Save(world, name, kind, screenshotTga?, overwriteFolder?)` must run between world
steps (it refuses while a script is running). Client requests and `DoSinglePlayerAutoSave` are
deferred with `GameServer.Defer` to the end of the server tick. The player cannot save during a
conversation (as in NWN); a script autosave can. Save games are enabled by
`ServerOptions.SaveGames` (the single-player client sets it; servers leave it off, so
`DoSinglePlayerAutoSave` does nothing there, like the original).

## Loading

`SaveGameService.Load(current, folder)` builds a fresh world from the same module (sharing the
resources; `World.LoadModule` runs before scripts are enabled, so OnModuleLoad, OnSpawn and OnEnter
do not fire, as in NWN), then `WorldSnapshot.Apply`:

1. creates every saved object with its saved id (module areas reuse the freshly loaded area so its
   walkmesh is kept; the module object is reused) and replaces the object registry;
2. fills world fields, systems, the script scheduler, timers and objects from the snapshot, in the
   order they were written;
3. rebuilds the walkmesh of areas whose tiles changed (SetTile) or that were instanced
   (CreateArea / CopyArea), syncs door walkmesh state, restores TEMP: templates, the module
   database and character databases.

`SaveGameRequests.LoadInto(server, folder)` then calls `GameServer.SwapWorld`: each connected
player takes over the saved character with the same player name (else the first unclaimed one) and
is sent its area as on a transition; open NUI windows are closed and the journal is resent. No
module or area enter events run.

## Client

`client/UI/SaveLoadPanel.cs`: **O** opens Save / Load (saves listed with name, area and date; type
a name and Save, or select one and Load), **Ctrl+F5** quick save, **Ctrl+F9** quick load. Requests
and notifies are listed in [notify/savegame.md](notify/savegame.md).

## Tests

`tests/OpenWinter.Tests/SaveGameTests.cs` runs the configured module, changes locals, items, effects,
doors, deaths, the journal, tiles, factions, an instanced area, pending DelayCommands, an action
queue with ActionDoCommand and a delayed DestroyObject (`tests/scripts/sg_setup.nss`), saves, loads
through the server, checks that a snapshot of the loaded world equals the saved one field for field,
then runs on and checks the closures fire with their captured values. Others cover saving
mid-combat and mid-spell, slot naming and quick load, and an ERF byte-for-byte round trip.

Parity suites compare against the original's dedicated server, which saves and reloads its own archive
(see [parity/README.md](../parity/README.md#native-savereload-parity-windows)):

- `summon_save`: temporary summon ownership, remaining duration and expiry. The original's dedicated-server Load
  reruns OnModuleLoad while keeping saved DelayCommands, so the suite's setup guards against running twice. This
  does not establish single-player load callbacks or HUD restoration.
- `associate_health_save` against the uninterrupted `associate_health_control`: injured and dead familiars,
  companions and summoned creatures keep their feat uses, hit points, locals and object references, corpse
  ownership and summon effect expiry. `summon_death` measures the death-event timing of summoned creatures.
- `curse_mask_save` against the uninterrupted `curse_mask_control`: curse suppression is the one measured exception
  to keeping an effect's applied state. On reload the original reevaluates a curse's penalties against the immunity
  present in the restored world, even immunity that landed after the curse; expired immunity no longer suppresses
  it. The effect's integers are unchanged, IgnoreEffectImmunity survives, and derived hit points follow
  Constitution. Only curse penalties are reevaluated; other effect side effects are not replayed.

## The original's save games

A save made by the original game can be loaded like OpenWinter's own: copy its folder (`NNNNNN - Name`, holding
`<module>.sav`) into OpenWinter's saves folder (never read from the real NWN user folder). The Save/Load window and
the main menu list it beside OpenWinter's saves; `SaveGameService.Load` hands it to `NativeSaveGame.Load`.
The format below was measured with the visual scene `nativesave`, which makes a save in a sandboxed run of the
original game.

The original's save folder:

| file | contents |
| --- | --- |
| `<module>.sav` | ERF of type **MOD**: the module itself (a .mod module's every resource: scripts, dialogs, blueprints) with the resources below replaced or added |
| `module.ifo` | `Mod_IsSaveGame` 1, the clock (`Mod_Start*`), `Mod_Area_list` (each area's `ObjectId`), the module's `VarTable`, `Mod_PlayerList` (each player: the BIC fields plus `ObjectId`, `AreaId`, `VarTable`, `EffectList`, `ActionList`, `Transition`, reputation and map data), `EventQueue` (pending DelayCommands), `Mod_NextObjId0` |
| `<area>.git` | every object as it is, in the usual lists, each with its `ObjectId` and its state (`VarTable`, `EffectList` on creatures, `ActionList`, `Transition`); the area's variables in the GIT's own `VarTable` |
| `<area>.are` | the area (twice: the module's and the current one) |
| `repute.fac` | the factions' current reputations |
| `module.sq3` | the module database: an "SQL3" compressed buffer (zstd) of the SQLite file |
| `player.bic`, `savenfo.txt`, `module_uuid.txt`, `portrait.tga` | beside the archive: the player's character, a label, the module's `Mod_UUID`, the portrait |

Object ids: the module is 0, areas and objects keep the ids they had (areas from 1), players count down from
0x7FFFFFFF, OBJECT_INVALID is 0x7F000000. OpenWinter builds the world from the archive as a module that runs no
load, enter or spawn events, maps the saved ids to its own and translates every reference it reads: object and
location variables, effect creators and object parameters, transition targets and saved script stacks.

What is carried over:
- the clock, the module's, areas' and objects' variables (every type: json locals are type 7, a struct holding the
  text in `JSON`; locations type 5), factions, the module database;
- every object in its saved state and place: creatures (hit points, inventories, equipment), doors (open, locked),
  placeables (contents), stores (gold, stock), items on the ground, waypoints, triggers (transition targets),
  encounters and sounds;
- players: position, area, gold, experience, inventory, equipment, hit points, variables, effects and the journal
  (the original keeps a quest's state in the player's locals `NW_JOURNAL_ENTRY` / `NW_JOURNAL_DATE` /
  `NW_JOURNAL_TIME` + tag);
- effects, from the measured effect table (below): those the original rebuilds from equipment (`SkipOnLoad`) are
  rebuilt by OpenWinter the same way; temporary effects keep the time they had left;
- the action queue: ActionWait and ActionMoveToPoint (other native actions are skipped and reported);
- pending DelayCommands: an `EventQueue` entry holds the script's code (the .ncs without its 13-byte header), the
  instruction it resumes at (an offset into that code, as OpenWinter's VM counts) and the stack it captured (cell
  types 3 int, 4 float, 5 string, 6 object, 16 + n the nth engine structure). OpenWinter runs the same bytecode, so
  the command runs at its saved calendar time in the loaded world (nativesave: a `DelayCommand(90.0, ...)` fires).

Not carried over yet: other native action types (DoCommand, conversations in progress, attacks: skipped), engine
structures on a saved stack other than locations and effects (they start as their defaults), saved events other than
script commands, the player's map exploration, personal reputations (`PersonalRepList`), polymorph and other effect
types not in the table, area-of-effect objects. Campaign (.nwm) saves were not measured: their archive may not hold
the module's resources, so OpenWinter layers it over the running module's. Which events the original's
single-player load runs (OnClientEnter, OnModuleLoad) is not measured yet; OpenWinter runs none, as for its own
saves. The visual scene `loadevents` makes an autosave and an uninterrupted control for that check (a 20 s wait, a
move, a 60 s temporary enemy and a 40 s DelayCommand), but loading it through the original's Load Game window cannot
yet be driven by the harness.

Saving in the original's format is not implemented. OpenWinter's saves carry the original's resources for tools
(and each object's state in the original's layout), but the original was not tried on them.

## Object state, ObjectToJson and the campaign database

`ObjectSerializer` writes objects as ObjectToJson, StoreCampaignObject and SqlBindObject do, and reads them back
(JsonToObject, RetrieveCampaignObject, SqlGetObject). Measured on nwserver with the parity suites `persist_objects`
(every object type, with and without the state, through all three) and `persist_effects` (80 effect constructors,
links and modifiers, action queues, transitions, the combined area format):
- Types: creatures (UTC/BIC), items (UTI), placeables (UTP), doors (UTD), waypoints (UTW), triggers (UTT), stores
  (UTM), encounters (UTE), sound objects (UTS) and areas (CAF: `{ARE, GIT}`; JsonToObject makes a new "nw_N" area
  with everything in it). StoreCampaignObject and SqlBindObject refuse areas; TemplateToJson of an area resref as
  RESTYPE_CAF gives null.
- The state (`bSaveObjectState`): `VarTable` on every type, `EffectList` on creatures and items only, `ActionList`
  and `Transition` (`TargetObjectId`, `TargetTag`) on every type. Without it those fields are absent, except on items
  (always carried) and areas (the combined format always has it). JsonToObject with bLoadObjectState reads them
  (effects are applied again for the time they had left); a door or trigger's SetTransitionTarget comes back only
  with the state.
- An `EffectList` entry: the original's own effect number (`Type`; the table is in `NativeEffects`: AC increase 48,
  ability increase 36, state effects 8 with the state code as the first integer, ...), `SubType` = subtype | duration
  type, `Duration`, the calendar day and millisecond it expires (`ExpireDay` / `ExpireTime`), creator, spell (-1 for
  none), caster level (-1 outside a spell), `CustomTag`, `IgnoreImmunity`, eight integers in GetEffectInteger's order
  (interval effects: the interval in ms and the next tick's day and time), four floats, six strings, four objects, two
  vectors. OpenWinter adds `OWEffect` (the effect's exact state) and reads it first; the original's entries map
  through the table.
- An `ActionList` entry: `ActionId` (the original's numbers: wait 30, move to point 1, speak 14, animation 6,
  do-command 37, jump 48), `GroupActionId`, `NumParams` and `Paramaters` (sic). OpenWinter adds `OWAction`.
- The campaign database stores objects as vartype 'O' with compressed 1: a "CPDB" compressed buffer. Compressed
  buffers (`nwbufcompress`, campaign objects, character databases "SQL3", module.sq3) are zstd: magic, version 3,
  algorithm 2, size, then a sub-header version 1, dictionary id 0 and one zstd frame. OpenWinter reads and writes
  them (ZstdSharp, MIT); uncompressed and zlib buffers still read.

## Limits

- Engine-timer closures are rebound by method name; a save made by a different engine build may drop a pending
  timer whose lambda moved (logged as a warning). Fields an older or newer build doesn't have are skipped with a
  warning (test `SaveFromAnEarlierBuildStillLoads`).
- A conversation in progress at an autosave is restored and shown to the player again (`ConversationService.Reshow`);
  what the original does then was not measured.
- Random number generators are not saved, as in the original (its save has no generator state: neither module.ifo
  nor the GITs carry one), so rolls after a load differ from an uninterrupted run.
- A save of another module, loaded from inside a game, is restored into its own module (the module path it recorded);
  later saves record that module.
- A load that fails is reported to the player and logged with its whole exception; the game goes on.
- Saves are large (about 11 MB for a large persistent-world module; the NWN-shaped resources are most of it).
