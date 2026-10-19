using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>Doors, transitions, encounters, traps, area instancing and area queries (world package).</summary>
public class WorldTests
{
    readonly ITestOutputHelper _out;
    public WorldTests(ITestOutputHelper output) { _out = output; }

    static readonly object Gate = new();
    static ResMan? _rm;

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    /// <summary>The module's resources plus the compiled test scripts.</summary>
    static ResMan Resources()
    {
        lock (Gate)
        {
            if (_rm != null) return _rm;
            var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
            rm.Add(new DirectorySource(ScriptsDir()));
            return _rm = rm;
        }
    }

    sealed class RecordingOutput : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, object Data)> Notes = new();
        public readonly List<(uint Object, int Animation)> Animations = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) => Animations.Add((o.Id, animation));
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, subject?.Id ?? GameObject.InvalidId, data));
    }

    /// <summary>A world with scripts and only the named areas loaded (fast).</summary>
    (World World, RecordingOutput Output) AreaWorld(params string[] areas)
    {
        var world = new World(Resources());
        world.EnableScripts();
        world.Scripts!.Log = _out.WriteLine;
        world.Register(world.Module);
        world.Factions.Load(world.Resources);
        var loader = new AreaLoader(world);
        foreach (var a in areas) world.Areas.Add(loader.Load(a));
        var output = new RecordingOutput();
        world.Output = output;
        return (world, output);
    }

    /// <summary>The whole module, loaded with scripts (for transitions between areas).</summary>
    (World World, RecordingOutput Output) ModuleWorld()
    {
        var world = new World(Resources());
        world.EnableScripts();
        world.Scripts!.Log = _ => { };
        world.LoadModule();
        world.Scripts.Log = _out.WriteLine;
        var output = new RecordingOutput();
        world.Output = output;
        return (world, output);
    }

    static Creature MakePC(string tag = "WORLD_PC")
    {
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        return new Creature { Tag = tag, WalkRate = 2f, RunRate = 4f, Player = new PlayerSession { Connection = serverEnd } };
    }

    static void Steps(World world, float seconds, Func<bool>? until = null)
    {
        for (int i = 0; i < seconds * 30 && until?.Invoke() != true; i++) world.Step(1f / 30);
    }

    static Vector3? WalkablePointInside(Area area, Func<Vector3, bool> inside, IReadOnlyList<Vector3> polygon)
    {
        float minX = polygon.Min(v => v.X), maxX = polygon.Max(v => v.X), minY = polygon.Min(v => v.Y), maxY = polygon.Max(v => v.Y);
        var centre = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
        Vector3? best = null;
        for (float x = minX; x <= maxX; x += 0.5f)
            for (float y = minY; y <= maxY; y += 0.5f)
            {
                if (area.Walkmesh.HighestGround(x, y) is not { } z) continue;
                var p = new Vector3(x, y, z);
                if (!inside(p) || area.Walkmesh.GroundHeight(x, y, z) == null) continue;
                if (best == null || Vector2.Distance(new(x, y), centre) < Vector2.Distance(new(best.Value.X, best.Value.Y), centre)) best = p;
            }
        return best;
    }

    [DataFact]
    public void DoorOpensClosesAndLocksWithKey()
    {
        var (world, output) = AreaWorld("ss_town");
        var area = world.Areas[0];
        var door = area.ObjectsOf<Door>().First(d => area.Walkmesh.Triangles.Any(t => t.Owner == d.WalkmeshOwner));
        world.SetTag(door, "WORLD_DOOR");
        foreach (var evt in new[] { "OnOpen", "OnClosed", "OnLock", "OnUnlock", "OnFailToOpen" }) door.Scripts[evt] = "world_evt";
        world.Step(0.01f);

        // A point the closed door blocks: the middle of one of its footprint triangles.
        // The door's closed footprint is a box over the doorway; probe the ground under its middle,
        // treating anything up to the box's top as in the way.
        var box = area.Walkmesh.Triangles.Where(t => t.Owner == door.WalkmeshOwner).SelectMany(t => new[] { t.A, t.B, t.C }).ToList();
        var probe = new Vector3(box.Average(v => v.X), box.Average(v => v.Y), box.Min(v => v.Z));
        float reach = box.Max(v => v.Z) - probe.Z + 0.5f;
        bool Blocked() => area.Walkmesh.GroundHeight(probe.X, probe.Y, probe.Z, reach) == null;
        _out.WriteLine($"door {door} at {door.Position}, probe {probe}, blocked closed: {Blocked()}");
        Assert.False(door.IsOpen);
        Assert.True(Blocked());

        var c = new Creature { Tag = "WORLD_OPENER", WalkRate = 2f, RunRate = 4f };
        world.AddToArea(c, area, TransitionService.Destination(world, door).Position, 0);

        // Locked, key required, no key: the open fails and OnFailToOpen fires.
        world.Scripts!.Run("world_door", c.Id);
        var m = world.Module.Locals;
        Assert.Equal(1, m.Ints["locked"]);
        Assert.Equal(1, m.Ints["key_required"]);
        Assert.Equal("world_key", m.Strings["key_tag"]);
        Assert.Equal(250, m.Ints["unlock_dc"]);
        Assert.Equal(7, m.Ints["hardness"]);
        Assert.Equal(-1, m.Ints["hardness_creature"]);
        Assert.Equal(0, m.Ints["open_possible"]);
        Assert.Equal(0, m.Ints["unlock_possible"]);
        Steps(world, 5f, () => c.Actions.Count == 0);
        Assert.False(door.IsOpen);
        Assert.True(door.Locked);
        Assert.Equal(2, door.Locals.Ints["events"]); // OnLock (SetLocked fires it, like the original), then OnFailToOpen
        Assert.Equal(c.Id, door.Locals.Objects["clicking"]);
        Assert.True(Blocked());

        // With the key the door unlocks and opens; its footprint stops blocking.
        var key = new Item { Tag = "world_key" };
        world.Register(key);
        c.Inventory.Add(key);
        world.Scripts.Run("world_door", c.Id);
        Assert.Equal(1, m.Ints["open_possible"]);
        Steps(world, 5f, () => c.Actions.Count == 0);
        Assert.True(door.IsOpen);
        Assert.False(door.Locked);
        Assert.Equal(c.Id, door.Locals.Objects["unlocked"]);
        Assert.Equal(c.Id, door.Locals.Objects["opened"]);
        Assert.Equal(1, door.Locals.Ints["is_open"]);
        Assert.False(Blocked());
        Assert.Contains(output.Animations, a => a.Object == door.Id && a.Animation is 205 or 206);

        // Close and lock it again (lockable, key carried).
        world.Scripts.Run("world_doorclose", c.Id);
        Steps(world, 5f, () => c.Actions.Count == 0);
        Assert.False(door.IsOpen);
        Assert.True(door.Locked);
        Assert.Equal(c.Id, door.Locals.Objects["closed"]);
        Assert.Equal(c.Id, door.Locals.Objects["locked"]);
        Assert.True(Blocked());
        Assert.Contains(output.Animations, a => a.Object == door.Id && a.Animation == 204);
    }

    [DataFact]
    public void TransitionTriggerMovesPlayerToLinkedWaypoint()
    {
        var (world, _) = ModuleWorld();
        // ss_northfoots has a trigger linked to waypoint WP_Fh_2_ssfh in ss_foothills.
        var area = world.FindArea("ss_northfoots")!;
        var trigger = area.ObjectsOf<Trigger>().First(t => t.LinkedTo == "WP_Fh_2_ssfh");
        var waypoint = world.ByTag(trigger.LinkedTo).OfType<Waypoint>().Single();
        Assert.NotSame(area, waypoint.Area);

        world.Scripts!.Run("world_trans", trigger.Id);
        Assert.Equal(waypoint.Id, world.Module.Locals.Objects["target"]);

        var pc = MakePC();
        WalkInto(world, area, pc, trigger);
        _out.WriteLine($"pc now in {pc.Area?.ResRef} at {pc.Position}; waypoint {waypoint.Area?.ResRef} at {waypoint.Position}");
        Assert.Same(waypoint.Area, pc.Area);
        Assert.True(Vector2.Distance(new(pc.Position.X, pc.Position.Y), new(waypoint.Position.X, waypoint.Position.Y)) < 1f);

        // With an OnClick script the script decides: this one doesn't jump.
        trigger.Scripts["OnClick"] = "world_evt";
        var pc2 = MakePC("WORLD_PC2");
        WalkInto(world, area, pc2, trigger);
        Assert.Equal(pc2.Id, trigger.Locals.Objects["clicking"]);
        Assert.Same(area, pc2.Area);
    }

    void WalkInto(World world, Area area, Creature pc, Trigger trigger)
    {
        var inside = WalkablePointInside(area, trigger.Contains, trigger.Geometry)!.Value;
        // Start a few metres toward the middle of the area and walk in.
        var centre = new Vector3(area.Width * Area.TileSize / 2, area.Height * Area.TileSize / 2, 0);
        var dir = Vector3.Normalize(new Vector3(centre.X - inside.X, centre.Y - inside.Y, 0));
        var start = inside + dir * 4f;
        for (int i = 0; i < 20 && (area.Walkmesh.HighestGround(start.X, start.Y) == null || trigger.Contains(start)); i++) start += dir;
        start.Z = area.Walkmesh.HighestGround(start.X, start.Y) ?? inside.Z;
        world.AddToArea(pc, area, start, 0);
        pc.MoveDirection = -dir;
        Steps(world, 6f, () => pc.Area != area || trigger.Inside.Contains(pc.Id) && trigger.Script("OnClick").Length > 0);
        pc.MoveDirection = Vector3.Zero;
    }

    [DataFact]
    public void EncounterSpawnsWhenPlayerEnters()
    {
        var (world, _) = AreaWorld("ivy_furtherinto");
        var area = world.Areas[0];
        var enc = area.ObjectsOf<Encounter>().First(e => e.Tag == "NightbiterPickets");
        enc.Scripts["OnEntered"] = "world_evt";
        enc.Scripts["OnExhausted"] = "world_evt";

        world.Scripts!.Run("world_enc", enc.Id);
        var m = world.Module.Locals;
        Assert.Equal(1, m.Ints["enc_active"]);
        Assert.Equal(1, m.Ints["enc_difficulty"]);
        Assert.Equal(0, m.Ints["enc_spawns"]);
        Assert.Equal(-1, m.Ints["enc_max"]);

        // A non-player creature doesn't trigger a PlayerOnly encounter.
        var point = WalkablePointInside(area, p => EncounterSystem.Contains(enc, p), enc.Geometry)!.Value;
        var npc = new Creature { Tag = "WORLD_NPC" };
        world.AddToArea(npc, area, point, 0);
        world.Step(0.1f);
        Assert.Empty(area.ObjectsOf<Creature>().Where(x => x.SpawnedByEncounter == enc.Id));
        world.Destroy(npc);

        var pc = MakePC();
        world.AddToArea(pc, area, point, 0);
        world.Step(0.1f);
        var spawned = area.ObjectsOf<Creature>().Where(x => x.SpawnedByEncounter == enc.Id).ToList();
        _out.WriteLine($"spawned {spawned.Count}: {string.Join(", ", spawned.Select(s => $"{s.ResRef}@{s.Position}"))}");
        // The wave's size (docs/ENCOUNTERS.md): the budget for the player's party level and the difficulty, spent on the list.
        var probe = new EncounterState { MaxCreatures = enc.State.MaxCreatures, RecCreatures = enc.State.RecCreatures };
        probe.Creatures.AddRange(enc.State.Creatures);
        probe.Pool = EncounterSystem.Budget(EncounterSystem.PartyLevel(new[] { Math.Max(1, pc.TotalLevel) }) + EncounterSystem.DifficultyValue(world, enc.State.DifficultyIndex));
        int expected = EncounterSystem.ChooseEntries(probe, 0, new Random(1)).Count;
        Assert.Equal(expected, spawned.Count);
        Assert.All(spawned, s => Assert.Equal("nightbiterskirmi", s.ResRef));
        // Both at the spawn point, the second moved off it to keep personal space (PersonalSpace.FindSpot).
        Assert.All(spawned, s => Assert.True(Vector2.Distance(new(s.Position.X, s.Position.Y), new(46.05f, 26.87f)) < 1.5f));
        Assert.True(Vector3.Distance(spawned[0].Position, spawned[1].Position) >= PersonalSpace.CreatureSpace(world, spawned[0]) * 2 - 1e-4f);
        Assert.Equal(pc.Id, enc.Locals.Objects["entering"]);
        Assert.True(enc.State.Inside.Contains(pc.Id));

        // GetEncounterSpawnsCurrent counts respawns, not the first wave (measured).
        world.Scripts.Run("world_enc", enc.Id);
        Assert.Equal(0, m.Ints["enc_spawns"]);

        // Staying inside doesn't spawn a second wave; killing the wave fires OnExhausted.
        world.Step(0.1f);
        Assert.Equal(spawned.Count, area.ObjectsOf<Creature>().Count(x => x.SpawnedByEncounter == enc.Id));
        int events = enc.Locals.Ints["events"];
        foreach (var s in spawned) world.Destroy(s);
        world.Step(0.1f);
        Assert.Equal(events + 1, enc.Locals.Ints["events"]);
        // Exhausted, the encounter turns itself off.
        Assert.False(enc.Active);
    }

    /// <summary>The measured trigger and respawn rules (docs/ENCOUNTERS.md) on a module encounter.</summary>
    [DataFact]
    public void EncounterTriggerAndRespawnRules()
    {
        var (world, _) = AreaWorld("ivy_furtherinto");
        var area = world.Areas[0];
        var enc = area.ObjectsOf<Encounter>().First(e => e.Tag == "NightbiterPickets");
        var point = WalkablePointInside(area, p => EncounterSystem.Contains(enc, p), enc.Geometry)!.Value;
        int Spawned() => area.ObjectsOf<Creature>().Count(x => x.SpawnedByEncounter == enc.Id && !x.IsDead && !x.IsDestroyed);

        // PlayerOnly off: a hostile NPC sets it off, but only while a player is in the area.
        enc.State.PlayerOnly = false;
        enc.State.Faction = 1; // hostile
        var npc = new Creature { Tag = "WORLD_NPC", Faction = 2 }; // commoner
        world.AddToArea(npc, area, point, 0);
        world.Step(0.1f);
        Assert.Equal(0, Spawned());
        world.Destroy(npc);
        world.Step(0.1f);
        var pc = MakePC();
        world.AddToArea(pc, area, point + new Vector3(0, 0, 0), 0);
        world.Step(0.1f);
        int first = Spawned();
        Assert.True(first > 0);
        Assert.Equal(0, enc.State.SpawnsCurrent);

        // Continuous, reset after 2 s, two respawns: killed while the player stays, it respawns and counts each.
        enc.State.SpawnOption = 0;
        enc.State.Reset = true;
        enc.State.ResetTime = 2;
        enc.State.SpawnsMax = 2;
        for (int round = 1; round <= 3; round++)
        {
            // The respawn's party strength is measured round the last creature to step in: the wave appeared inside
            // the polygon here, so the player steps out and back in first.
            var stand = pc.Position;
            pc.Position = stand + new Vector3(60, 0, 0);
            world.Step(0.1f);
            pc.Position = stand;
            world.Step(0.1f);
            Assert.Equal(pc.Id, enc.State.LastEntered);
            foreach (var c in area.ObjectsOf<Creature>().Where(x => x.SpawnedByEncounter == enc.Id).ToList()) world.Destroy(c);
            world.Step(0.1f);
            Assert.False(enc.Active);
            Steps(world, 2.5f);
            if (round <= 2)
            {
                Assert.True(enc.Active);
                Assert.Equal(round, enc.State.SpawnsCurrent);
                Assert.Equal(first, Spawned());
            }
            else
            {
                Assert.False(enc.Active); // no respawns left
                Assert.Equal(0, Spawned());
            }
        }

        // An encounter of a faction that likes the player never spawns for her.
        enc.Active = true;
        enc.State.Triggered = false;
        enc.State.Faction = 2;
        Assert.False(world.System<EncounterSystem>().CanTrigger(enc, pc));
    }

    [DataFact]
    public void CreateCopyAndDestroyAreas()
    {
        var (world, _) = AreaWorld("ss_town");
        var town = world.Areas[0];
        world.Module.EntryArea = "ss_town";
        world.Scripts!.Run("world_area", world.Module.Id);
        var m = world.Module.Locals;
        Assert.Equal(1, m.Ints["before"]);
        Assert.Equal(2, m.Ints["after"]);
        Assert.StartsWith("nw_", m.Strings["created_resref"]);
        Assert.Equal("WORLD_TOWN2", m.Strings["created_tag"]);
        Assert.Equal("Second Town", m.Strings["created_name"]);
        Assert.NotEqual(m.Strings["created_resref"], m.Strings["copy_resref"]);
        Assert.Equal(town.Width, m.Ints["copy_width"]);
        Assert.Equal(0, m.Ints["bad_create"]);
        Assert.Equal(0, m.Ints["destroy_invalid"]);
        Assert.Equal(1, m.Ints["destroy_copy"]);
        // As in the original, the area goes once the script has finished.
        Assert.Equal(1, m.Ints["copy_valid"]);
        Assert.Equal(3, m.Ints["final"]);

        var created = world.Get<Area>(m.Objects["created"])!;
        Assert.Equal(town.Objects.Count, created.Objects.Count);
        Assert.Equal(town.Walkmesh.TriangleCount, created.Walkmesh.TriangleCount);
        Assert.NotNull(world.ByTag("WORLD_TOWN2").SingleOrDefault());

        // The module's entry area can't go; nor can an area with players in it.
        Assert.Equal(-1, AreaInstancing.Destroy(world, town));
        var pc = MakePC();
        world.AddToArea(pc, created, created.Objects.OfType<Waypoint>().First().Position, 0);
        Assert.Equal(-2, AreaInstancing.Destroy(world, created));
        world.Destroy(pc);
        int objects = created.Objects.Count;
        Assert.Equal(1, AreaInstancing.Destroy(world, created));
        Assert.True(created.IsDestroyed);
        Assert.DoesNotContain(created, world.Areas);
        _out.WriteLine($"destroyed instance with {objects} objects");

        // CopyArea keeps run-time changes: open a door in the source first.
        var door = town.ObjectsOf<Door>().First();
        DoorService.Open(world, door, null);
        var copy = AreaInstancing.Copy(world, town, "", "")!;
        var copiedDoor = copy.ObjectsOf<Door>().First(d => d.Position == door.Position);
        Assert.True(copiedDoor.IsOpen);
        Assert.NotEqual(door.Id, copiedDoor.Id);
        Assert.Equal(door.Locals.Strings.Count, copiedDoor.Locals.Strings.Count);
        Assert.NotSame(door.Locals, copiedDoor.Locals);
    }

    [DataFact]
    public void SurfaceMaterialAndGroundHeightOnRealArea()
    {
        var (world, _) = AreaWorld("ss_town");
        var area = world.Areas[0];
        var wp = area.ObjectsOf<Waypoint>().First(w => area.Walkmesh.GroundHeight(w.Position.X, w.Position.Y, w.Position.Z, 1f) != null);
        world.Module.Locals.Locations["probe"] = new Location(area.Id, wp.Position with { Z = wp.Position.Z + 0.3f }, 0);
        world.Scripts!.Run("world_ground", world.Module.Id);
        var m = world.Module.Locals;
        _out.WriteLine($"waypoint {wp.Tag} at {wp.Position}: surface {m.Ints["surface"]}, height {m.Floats["height"]}");
        Assert.True(m.Ints["surface"] > 0);
        Assert.Equal("1", world.Resources.TwoDA("surfacemat").Get(m.Ints["surface"], "Walk"));
        Assert.InRange(m.Floats["height"], wp.Position.Z - 1f, wp.Position.Z + 1f);
        Assert.Equal(-6f, m.Floats["bad_height"]);
        Assert.Equal(0, m.Ints["bad_surface"]);
        int x = (int)(wp.Position.X / 10), y = (int)(wp.Position.Y / 10);
        Assert.Equal(area.Tiles[y * area.Width + x].Id, m.Ints["tile_id"]);
        Assert.Equal(-1, m.Ints["bad_tile"]);
    }

    [DataFact]
    public void AreaSettingsTransformsTrapsAndExploration()
    {
        var (world, output) = AreaWorld("ss_town");
        var area = world.Areas[0];
        var wp = area.ObjectsOf<Waypoint>().First(w => area.Walkmesh.GroundHeight(w.Position.X + 2, w.Position.Y, w.Position.Z, 1f) != null
            && area.Walkmesh.GroundHeight(w.Position.X, w.Position.Y, w.Position.Z, 1f) != null);
        var pc = MakePC();
        world.AddToArea(pc, area, wp.Position, 0);
        world.Scripts!.Run("world_misc", pc.Id);
        var m = world.Module.Locals;

        Assert.Equal(0, m.Ints["interior"]);
        Assert.Equal(area.IsNatural ? 1 : 0, m.Ints["natural"]);
        Assert.Equal(-1, m.Ints["natural_bad"]);
        Assert.Equal(12, m.Ints["width"]);
        Assert.Equal(16, m.Ints["height"]);
        Assert.Equal("tcn01", m.Strings["tileset"]);
        Assert.Equal(2, m.Ints["weather"]);
        Assert.Equal(40, m.Ints["fog_sun"]);
        Assert.Equal(10, m.Ints["fog_moon"]);
        Assert.Equal(world.Scripts.Definitions.Int("SKYBOX_DESERT_CLEAR"), m.Ints["skybox"]);
        Assert.Equal(1, m.Ints["norest"]);
        Assert.Equal(1f, m.Floats["vt_default"]);
        Assert.Equal(1f, m.Floats["vt_old"]);
        Assert.Equal(2.5f, m.Floats["vt_new"]);
        Assert.Equal(1, m.Ints["vt_clear"]);
        Assert.Equal(0, m.Ints["vt_clear_again"]);
        Assert.Equal(138, m.Ints["music_day"]);
        Assert.Equal(7, m.Ints["music_day2"]);
        Assert.Equal(1, m.Ints["trapped"]);
        Assert.Equal(0, m.Ints["trap_type"]);
        Assert.Equal(0, m.Ints["trap_detected"]);
        Assert.Equal(1, m.Ints["trap_detected2"]);
        Assert.Equal(m.Objects["trap"], m.Objects["nearest_trap"]);
        Assert.Equal(GameObject.InvalidId, m.Objects["last_detected"]); // SetTrapDetectedBy isn't a find (trap_api det.last)
        Assert.Equal(0, m.Ints["explored_before"]);
        Assert.Equal(0, m.Ints["explored_set"]);
        Assert.Equal(1, m.Ints["explored_after"]);
        Assert.Equal(-1, m.Ints["explored_bad"]);
        Assert.Equal(0, m.Ints["start_valid"]);
        foreach (var kind in new[] { "weather", "area-fog", "area-skybox", "visual-transform", "visual-transform-clear", "area-audio", "trap-detected", "tile-explored" })
            Assert.Contains(output.Notes, n => n.Kind == kind);

        // Walking onto the trap springs it (one-shot: disarmed afterwards).
        var trap = world.Get<Trigger>(m.Objects["trap"])!;
        pc.MoveDirection = new Vector3(1, 0, 0);
        Steps(world, 3f, () => trap.TrapFlag == 0);
        pc.MoveDirection = Vector3.Zero;
        _out.WriteLine($"pc {wp.Position} -> {pc.Position}; trap at {trap.Position} [{string.Join(" ", trap.Geometry)}]");
        Assert.Equal(0, trap.TrapFlag);
        Assert.Equal(pc.Id, trap.Locals.Objects["entering"]);
        Assert.Contains(output.Notes, n => n.Kind == "trap-triggered");
    }
}
