using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Skills;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// NWNX_Area, NWNX_Tileset, NWNX_Encounter, NWNX_Reveal and NWNX_Visibility behaviour that the NWNX parity suites
/// can't reach (players, the server's sending, files) or that is cheaper to pin here.
/// </summary>
public class NwnxAreaTests
{
    const int Hostile = 1, Commoner = 2;

    sealed class Flat
    {
        public required World World;
        public required Area Area;
        public NwnxRegistry Registry => World.System<NwnxRegistry>();
        public SkillSystem Skills => World.System<SkillSystem>();

        public Creature Spawn(float x, float y, int faction = Commoner, bool pc = false)
        {
            var c = new Creature { Faction = faction, Race = 6, CurrentHitPoints = 20, MaxHitPoints = 20, PerceptionRange = 9 };
            if (pc) c.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, PlayerName = "p", Creature = c };
            World.AddToArea(c, Area, new Vector3(x, y, 0), 0);
            CreatureRules.EnsureInit(World, c);
            return c;
        }

        public void Tick(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.1f) World.Step(0.1f);
        }

        /// <summary>Calls an NWNX function with its arguments in declaration order (pushed last first, like the headers).</summary>
        public NwnxStack Call(string plugin, string function, params NwnxValue[] args)
        {
            for (int i = args.Length - 1; i >= 0; i--) Registry.Stack.Push(args[i]);
            Assert.True(Registry.Call(plugin, function));
            return Registry.Stack;
        }
    }

    static NwnxValue O(GameObject? o) => NwnxValue.Object(o?.Id ?? GameObject.InvalidId);
    static NwnxValue I(int v) => NwnxValue.Int(v);
    static NwnxValue F(float v) => NwnxValue.Float(v);
    static NwnxValue S(string v) => NwnxValue.String(v);

    /// <summary>A flat 4 x 4 tile area on the base game's data (tileset tde01, tile 4: open floor).</summary>
    static Flat FlatArea(int width = 4, int height = 4)
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        world.Factions.Load(rm);
        world.Factions.SetFactionReputation(Hostile, Commoner, 0);
        world.Factions.SetFactionReputation(Commoner, Hostile, 0);
        var area = new Area { ResRef = "nwnx_test", Tag = "nwnx_test", Width = width, Height = height, Tileset = "tde01" };
        area.Tiles = Enumerable.Range(0, width * height).Select(_ => new TileInfo { Id = 4, AnimLoops = 7 }).ToArray();
        world.Register(area);
        world.Areas.Add(area);
        WalkmeshBuilder.Rebuild(world, area);
        world.System<SkillSystem>().StealthDie = sides => Math.Min(10, sides);
        return new Flat { World = world, Area = area };
    }

    static Creature Hider(Flat f, float x, float y)
    {
        var h = f.Spawn(x, y, Hostile);
        h.Skills[SkillIds.Hide] = 60;
        h.Skills[SkillIds.MoveSilently] = 60;
        f.Skills.SetActionMode(h, SkillIds.ModeStealth, true);
        return h;
    }

    [DataFact]
    public void RevealLetsAPlayerSeeAHiderUntilItStealthsOutOfSight()
    {
        var f = FlatArea();
        var pc = f.Spawn(10, 10, Commoner, pc: true);
        var npc = f.Spawn(10, 14, Commoner);
        var hider = Hider(f, 13, 10);
        f.Tick(5);
        Assert.DoesNotContain(hider.Id, pc.Society.Seen);

        f.Call("Reveal", "RevealTo", O(hider), O(pc), I(SkillSystem.RevealSeen));
        f.Call("Reveal", "RevealTo", O(hider), O(npc), I(SkillSystem.RevealSeen));
        f.Tick(5);
        Assert.Contains(hider.Id, pc.Society.Seen);
        Assert.DoesNotContain(hider.Id, pc.Society.Heard); // seen, not heard: Move Silently still beats Listen
        Assert.DoesNotContain(hider.Id, npc.Society.Seen); // NPC observers ignore reveals (nwnx_reveal)

        // Out of range the player loses it; stealthing again there ends the reveal.
        hider.Position = new Vector3(38, 38, 0);
        f.Tick(5);
        Assert.DoesNotContain(hider.Id, pc.Society.Seen);
        f.Skills.SetActionMode(hider, SkillIds.ModeStealth, false);
        f.Skills.SetActionMode(hider, SkillIds.ModeStealth, true);
        hider.Position = new Vector3(13, 10, 0);
        f.Tick(5);
        Assert.DoesNotContain(hider.Id, pc.Society.Seen);
        Assert.Null(f.Skills.RevealMethod(pc, hider));

        f.Call("Reveal", "RevealTo", O(hider), O(pc), I(SkillSystem.RevealHeard));
        f.Tick(5);
        Assert.Contains(hider.Id, pc.Society.Heard);
        Assert.DoesNotContain(hider.Id, pc.Society.Seen);
    }

    [DataFact]
    public void VisibilityOverridesDecideWhatTheServerSendsEachPlayer()
    {
        var f = FlatArea();
        var server = new GameServer(f.World);
        var pc = f.Spawn(10, 10, Commoner, pc: true);
        var (_, endpoint) = LoopbackConnection.CreatePair();
        pc.Player = server.Accept(endpoint);
        pc.Player.Creature = pc;
        pc.Player.Area = f.Area;
        var other = f.Spawn(30, 30, Commoner, pc: true);
        var npc = f.Spawn(12, 10, Commoner);
        var hider = Hider(f, 10, 13);
        void Tick(float seconds) { for (float t = 0; t < seconds; t += 1f / 30) server.Tick(1f / 30); }
        Tick(1);
        Assert.Contains(npc.Id, pc.Player.Known);
        Assert.True(GameServer.Perceives(f.World, pc, npc));
        Assert.False(GameServer.Perceives(f.World, pc, hider));

        f.Call("Visibility", "SetVisibilityOverride", O(null), O(npc), I(VisibilityOverrides.HiddenAlways));
        Assert.False(GameServer.Perceives(f.World, pc, npc));
        Tick(0.2f);
        Assert.DoesNotContain(npc.Id, pc.Player.Known);
        // The player's own override wins over the global one; another player's doesn't count.
        f.Call("Visibility", "SetVisibilityOverride", O(other), O(npc), I(VisibilityOverrides.Visible));
        Assert.False(GameServer.Perceives(f.World, pc, npc));
        f.Call("Visibility", "SetVisibilityOverride", O(pc), O(npc), I(VisibilityOverrides.Visible));
        Assert.True(GameServer.Perceives(f.World, pc, npc));
        Tick(0.2f);
        Assert.Contains(npc.Id, pc.Player.Known);
        Assert.Equal(VisibilityOverrides.Visible, f.Call("Visibility", "GetVisibilityOverride", O(pc), O(npc)).Pop()!.Value.Value);

        // Always visible shows a hider nobody perceives; DM-only hides from players.
        f.Call("Visibility", "SetVisibilityOverride", O(pc), O(hider), I(VisibilityOverrides.AlwaysVisible));
        Assert.True(GameServer.Perceives(f.World, pc, hider));
        f.Call("Visibility", "SetVisibilityOverride", O(pc), O(npc), I(VisibilityOverrides.DMOnly));
        Assert.False(GameServer.Perceives(f.World, pc, npc));
        f.Call("Visibility", "SetVisibilityOverride", O(pc), O(npc), I(VisibilityOverrides.Default));
        f.Call("Visibility", "SetVisibilityOverride", O(null), O(npc), I(VisibilityOverrides.Default));
        Assert.True(GameServer.Perceives(f.World, pc, npc));
        Assert.Equal(-1, f.Call("Visibility", "GetVisibilityOverride", O(null), O(npc)).Pop()!.Value.Value);
        // A player never loses their own character.
        f.Call("Visibility", "SetVisibilityOverride", O(null), O(pc), I(VisibilityOverrides.HiddenAlways));
        Assert.True(GameServer.Perceives(f.World, pc, pc));
    }

    [DataFact]
    public void RotatingANonSquareAreaSwapsItsSizeAndMovesTilesAndObjects()
    {
        var f = FlatArea(width: 3, height: 2);
        f.Area.Tiles[1].Id = 121; // tile (1, 0)
        f.Area.Tiles[1].Orientation = 1;
        var wp = new Waypoint { Tag = "wp" };
        f.World.AddToArea(wp, f.Area, new Vector3(5, 6, 0), MathF.PI / 6); // 30 degrees
        var trigger = new Trigger { Geometry = [new(1, 1, 0), new(3, 1, 0), new(3, 3, 0)] };
        f.World.AddToArea(trigger, f.Area, new Vector3(2, 2, 0), 0);

        f.Call("Area", "RotateArea", O(f.Area), I(1));
        Assert.Equal((2, 3), (f.Area.Width, f.Area.Height));
        // (x, y) -> (y, W - x) for a 30 m wide area; tile (1, 0) -> (0, 1) in the 2-wide grid.
        Assert.Equal(new Vector3(6, 25, 0), wp.Position);
        Assert.Equal(121, f.Area.Tiles[2].Id);
        Assert.Equal(0, f.Area.Tiles[2].Orientation);
        Assert.Equal(new Vector3(1, 29, 0), trigger.Geometry[0]);
        Assert.True(trigger.Contains(new Vector3(1.5f, 27.5f, 0)));
        // The original's facing arithmetic: acos(|sin 30|) = 60.
        Assert.Equal(60f, wp.FacingDegrees, 0.01f);
        Assert.NotNull(f.Area.Walkmesh.HighestGround(15, 25));
    }

    [DataFact]
    public void AreaEntriesAndExitsAreRecordedWhenTheirEventsRun()
    {
        var f = FlatArea();
        var c = f.Spawn(5, 5);
        Assert.Equal(GameObject.InvalidId, f.Call("Area", "GetLastEntered", O(f.Area)).Pop()!.Value.Value);
        f.World.Step(0.05f);
        Assert.Equal(c.Id, f.Call("Area", "GetLastEntered", O(f.Area)).Pop()!.Value.Value);
        f.World.Destroy(c);
        f.World.Step(0.05f);
        // The creature that left is gone, so it reads as OBJECT_INVALID.
        Assert.Equal(c.Id, f.Area.LastLeft);
        Assert.Equal(GameObject.InvalidId, f.Call("Area", "GetLastLeft", O(f.Area)).Pop()!.Value.Value);
    }

    [DataFact]
    public void ExportedAreaFilesLoadBackWithoutExcludedOrFilteredObjects()
    {
        var f = FlatArea();
        var root = Path.Combine(Path.GetTempPath(), "openwinter-nwnx-export-" + Guid.NewGuid().ToString("N"));
        f.World.System<PersistenceSystem>().RootOverride = root;
        try
        {
            var keep = new Waypoint { Tag = "KEEP" };
            var skip = new Waypoint { Tag = "SKIP" };
            f.World.AddToArea(keep, f.Area, new Vector3(5, 5, 0), 0);
            f.World.AddToArea(skip, f.Area, new Vector3(6, 6, 0), 0);
            f.Spawn(8, 8);
            f.Area.Locals.Ints["areavar"] = 9;
            f.Call("Area", "SetAreaSpotModifier", O(f.Area), I(4));
            f.Call("Area", "AddObjectToExclusionList", O(skip));
            Assert.Equal(1, f.Call("Area", "ExportARE", O(f.Area), S("nwexport"), S("Exported"), S("EXPTAG"), S("NWNX")).Pop()!.Value.Value);
            Assert.Equal(1, f.Call("Area", "ExportGIT", O(f.Area), S("nwexport"), I(1), I(1), I(1), S("NWNX")).Pop()!.Value.Value);
            // A file name over 16 characters fails the argument check: nothing is returned.
            Assert.Equal(0, f.Call("Area", "ExportARE", O(f.Area), S("abcdefghijklmnopq"), S(""), S(""), S("NWNX")).Count);

            var are = Gff.Read(File.ReadAllBytes(Path.Combine(root, "nwnx", "nwexport.are")));
            var git = Gff.Read(File.ReadAllBytes(Path.Combine(root, "nwnx", "nwexport.git")));
            Assert.False(git.Has("Creature List"));
            var loaded = new AreaLoader(f.World).Load("nwexport", are, git);
            Assert.Equal("EXPTAG", loaded.Tag);
            Assert.Equal(4, (int)loaded.Are.GetInt("ModSpotCheck"));
            Assert.Equal(16, loaded.Tiles.Length);
            Assert.Equal(9, loaded.Locals.Ints["areavar"]);
            Assert.Equal(["KEEP"], loaded.ObjectsOf<Waypoint>().Select(w => w.Tag).ToArray());
            Assert.Empty(loaded.ObjectsOf<Creature>());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [DataFact]
    public void TileOverridesRewriteTheAreaDataTheyAreMappedTo()
    {
        var f = FlatArea();
        f.Call("Tileset", "CreateTileOverride", S("ovr"), S("ttr01"), I(2), I(1));
        f.Call("Tileset", "SetOverrideTileData", S("ovr"), I(1), I(35), I(2), I(1), I(5), I(6), I(7), I(8), I(1), I(0), I(1));
        f.Call("Tileset", "SetAreaTileOverride", S("myarea"), S("ovr"));
        var are = new GffStruct().SetResRef("Tileset", "tde01").SetInt("Width", 4).SetInt("Height", 4);
        f.World.System<TileOverrides>().Apply("MYAREA", are);
        Assert.Equal("ttr01", are.GetString("Tileset"));
        Assert.Equal(2, (int)are.GetInt("Width"));
        var tiles = are.GetList("Tile_List");
        Assert.Equal(2, tiles.Count);
        Assert.Equal(0, (int)tiles[0].GetInt("Tile_ID"));
        Assert.Equal(0, (int)tiles[0].GetInt("Tile_AnimLoop1"));
        Assert.Equal(35, (int)tiles[1].GetInt("Tile_ID"));
        Assert.Equal(2, (int)tiles[1].GetInt("Tile_Orientation"));
        Assert.Equal(7, (int)tiles[1].GetInt("Tile_SrcLight1"));
        Assert.Equal(1, (int)tiles[1].GetInt("Tile_AnimLoop3"));

        // Removing the mapping leaves areas alone.
        f.Call("Tileset", "SetAreaTileOverride", S("myarea"), S(""));
        var plain = new GffStruct().SetResRef("Tileset", "tde01");
        f.World.System<TileOverrides>().Apply("myarea", plain);
        Assert.Equal("tde01", plain.GetString("Tileset"));
    }

    [DataFact]
    public void EncounterCreatureListIsIndexedFromItsLastEntry()
    {
        var f = FlatArea();
        var e = new Encounter();
        e.State.Creatures.AddRange([new("first", 1, false), new("second", 2, false), new("third", 3, true)]);
        f.World.AddToArea(e, f.Area, new Vector3(5, 5, 0), 0);
        var r = f.Call("Encounter", "GetEncounterCreatureByIndex", O(e), I(0));
        Assert.Equal(0, r.Pop()!.Value.Value); // already used
        Assert.Equal(1, r.Pop()!.Value.Value); // unique
        Assert.Equal(3f, r.Pop()!.Value.Value);
        Assert.Equal("third", r.Pop()!.Value.Value);
        f.Call("Encounter", "SetEncounterCreatureByIndex", O(e), I(2), S("kobold"), F(1.5f), I(1), I(1));
        Assert.Equal("kobold", e.State.Creatures[0].ResRef);
        Assert.Contains(0, e.State.UsedUnique);
        // A negative index fails the argument check: nothing is returned.
        Assert.Equal(0, f.Call("Encounter", "GetEncounterCreatureByIndex", O(e), I(-1)).Count);
    }

    [DataFact]
    public void DefaultDiscoveryMasksApplyToArrivalsByAreaThenGlobally()
    {
        var f = FlatArea();
        var before = f.Spawn(5, 5);
        f.Call("Area", "SetDefaultObjectUiDiscoveryMask", O(f.Area), I(1), I(2), I(0));
        f.Call("Area", "SetDefaultObjectUiDiscoveryMask", O(null), I(1 | 2), I(8), I(0));
        var after = f.Spawn(6, 6);
        Assert.Equal(-1, before.UiDiscoveryMask);
        Assert.Equal(2, after.UiDiscoveryMask);
        f.Call("Area", "SetDefaultObjectUiDiscoveryMask", O(f.Area), I(1), I(-1), I(1));
        Assert.Equal(8, before.UiDiscoveryMask);
        Assert.Equal(8, after.UiDiscoveryMask);
    }

    [DataFact]
    public void DirectLinesReportTerrainPlaceablesAndCreaturesInTheWay()
    {
        var f = FlatArea();
        int Line(float sx, float sy, float ex, float ey) =>
            (int)f.Call("Area", "TestDirectLine", O(f.Area), F(sx), F(sy), F(ex), F(ey), F(0.5f), F(1.8f), I(0)).Pop()!.Value.Value!;
        Assert.Equal(1, Line(5, 5, 35, 5));
        Assert.Equal(-1, Line(5, 5, 45, 5)); // off the area's ground
        var walker = f.Spawn(5, 15);
        Assert.Equal(1, Line(5, 15, 35, 15)); // the creature at the start is the walker
        f.Spawn(20, 15.5f);
        Assert.Equal(-3, Line(5, 15, 35, 15));
        // A placeable's footprint across the line.
        int owner = 4242;
        f.Area.Walkmesh.SetPlaceableOwner(owner);
        f.Area.Walkmesh.Add(new(19, 24, 0), new(21, 24, 0), new(21, 26, 0), 0, false, owner);
        f.Area.Walkmesh.Add(new(19, 24, 0), new(21, 26, 0), new(19, 26, 0), 0, false, owner);
        Assert.Equal(-2, Line(5, 25, 35, 25));
        Assert.Equal(walker.Area, f.Area);
    }

    [DataFact]
    public void PathChecksReuseTheAreaGraphUntilTheGroundChanges()
    {
        var f = FlatArea();
        Assert.True(TilePathCheck.Exists(f.Area, new(5, 5, 0), new(35, 35, 0), 7));
        Assert.False(TilePathCheck.Exists(f.Area, new(5, 5, 0), new(35, 35, 0), 6));
        Assert.True(TilePathCheck.Exists(f.Area, new(5, 5, 0), new(6, 6, 0), 1));
        // Taking a middle row of tiles' ground away cuts the area in two.
        for (int x = 0; x < 4; x++) f.Area.Tiles[4 + x].Id = 9999;
        WalkmeshBuilder.Rebuild(f.World, f.Area);
        Assert.False(TilePathCheck.Exists(f.Area, new(5, 5, 0), new(35, 35, 0), 16));
        Assert.True(TilePathCheck.Exists(f.Area, new(5, 25, 0), new(35, 35, 0), 16));
    }

    [DataFact]
    public void VisibilityOverridesEndWithTheirObjectsButSurviveAPickUp()
    {
        var f = FlatArea();
        var overrides = f.World.System<VisibilityOverrides>();
        var pc = f.Spawn(10, 10, Commoner, pc: true);
        var item = new Item { BaseItem = 49 };
        f.World.AddToArea(item, f.Area, new Vector3(12, 10, 0), 0);
        overrides.Set(pc.Id, item.Id, VisibilityOverrides.HiddenAlways);
        f.World.LiftFromArea(item);
        Assert.Equal(VisibilityOverrides.HiddenAlways, overrides.Get(pc.Id, item.Id));
        var npc = f.Spawn(14, 10);
        overrides.Set(pc.Id, npc.Id, VisibilityOverrides.HiddenAlways);
        f.World.Destroy(pc);
        Assert.False(overrides.Any);
    }
}
