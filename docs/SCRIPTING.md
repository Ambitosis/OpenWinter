# Implementing engine functions

The server runs compiled NWScript (`.ncs`) in `OpenWinter.Game.Scripting.ScriptVM`. Every
function declared in `nwscript.nss` (about 1,190) needs an implementation that behaves
like the original engine, because any module or persistent world may call it.

## Ground rules

- **Original work only.** Work from `nwscript.nss` (its comments document each function), the
  game's 2DA tables and data files, observed behaviour, and public format docs. Never
  copy code from other engines (xoreos is GPL; NWNX source is not to be copied either).
  No game assets in the repo.
- **Match the original.** Same return values for invalid input (usually 0, "", OBJECT_INVALID,
  or an invalid engine structure), same defaults, same quirks where modules depend on them.
- **Server only.** Nothing in `OpenWinter.Game` may reference Godot.

## Adding a function

Create or extend a file in `src/OpenWinter.Game/Scripting/Functions/`. One static class per
area, methods tagged with the nwscript name:

```csharp
static class ExampleFunctions
{
    // int GetHitDice(object oCreature)
    [NwFunc("GetHitDice")] static object? HitDice(Call c) => c.Obj<Creature>(0)?.TotalLevel ?? 0;
}
```

`Call` gives typed arguments in declaration order (`c.I(0)`, `c.F(1)`, `c.S(2)`, `c.O(3)`,
`c.Obj(3)`, `c.Obj<Creature>(3)`, `c.V(i)`, `c.L(i)` location, `c.E(i)` effect, `c.IP(i)`,
`c.T(i)` talent, `c.Ev(i)` event, `c.J(i)` json, `c.Q(i)` sqlquery, `c.A(i)` action closure),
plus `c.Self` (OBJECT_SELF), `c.World`, `c.Host`, and `c.Event` (current event data).
Compiled scripts always pass every argument (the compiler fills in defaults).

Return: `int`/`bool` for int, `float`, `string`, `uint` object id (use `GameObject.InvalidId`),
`Vector3`, `Location`, or the engine types in `Scripting/EngineTypes.cs`
(`Effect`, `ScriptEvent`, `Talent`, `ItemProperty`, `SqlQuery`, `Cassowary`, `JsonValue`).
Return `null` for void.

Constants: `c.Host.Definitions.Int("OBJECT_TYPE_CREATURE")` reads values from nwscript.nss.
2DA tables: `c.World.Resources.TwoDA("classes")` (cached).

## Where state lives

- Object classes (`Creature`, `Placeable`, `Door`, `Item`, `Area`, ...) are `partial`. Add
  new fields in **your own file** (e.g. `Objects/Door.Locks.cs`) rather than editing
  `Objects.cs`.
- `World` is partial too. Per-tick logic goes in a class implementing `IWorldSystem` with
  a constructor taking `World`; it is discovered and ticked automatically.
  Find it with `world.System<MySystem>()`.
- Things clients need to show (visual effects, sounds, music, door animations, journal,
  camera, NUI, ...) go through `world.Output?.Notify(kind, subject, data, player)`. Pick a
  short kebab-case `kind` and a small anonymous object for `data`; document new kinds in
  `docs/NOTIFY.md`. The client side is wired up separately.
- Events: `world.RunEvent(obj, "ScriptOnNotice", new EventContext().Set("Perceived", id))`
  runs an object's event script. Event getters read `c.Event.Obj("Key")` etc.
- Delays: `c.Host.Schedule(seconds, situation, selfId)` for script closures,
  `world.After(seconds, () => ...)` for engine code.

## Testing

Write a small `.nss` in `tests/scripts/`, compile it with the official compiler, and
assert on the results in an xUnit test (see `tests/OpenWinter.Tests/ScriptTests.cs`):

```bash
cd tests/scripts
"<neverwinter.nim>/nwn_script_comp.exe" --root "<NWN install>" -c my_test.nss
```

Commit both the `.nss` and the `.ncs`. Run `dotnet test`. `dotnet run --project
src/OpenWinter.Tools -- run-module 60` runs the configured module and lists which
unimplemented functions it calls and any script errors.

Data tests need `config/local.json`; when running from another checkout, point
`OPENWINTER_CONFIG` at it.
