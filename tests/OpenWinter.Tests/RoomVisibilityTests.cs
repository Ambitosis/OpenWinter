using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class RoomVisibilityTests
{
    static (World World, Area Area, ResMan Resources) Academy()
    {
        var resources = ResMan.ForModule(TestData.Paths, Path.Combine(TestData.Config!.GameRoot, "data/nwm/Prelude.nwm"));
        var world = new World(resources);
        var area = new AreaLoader(world).Load("m0q1b"); world.Areas.Add(area);
        return (world, area, resources);
    }
    static Door WestDoor(Area area) => area.Objects.OfType<Door>().Single(d => Vector3.Distance(d.Position, new(40, 15, 0)) < .1f);
    static string Mask(World world, Area area, Vector3 position) => string.Concat(RoomVisibility.For(world, area)!.VisibleTiles(area, position).Select(v => v ? '1' : '0'));
    const string Closed = "0000100000/0000100000/0000110000/1111100000/1111100000/1111110000/0100110000/0000000000/0000000000/0000000000/";
    const string Open = "0000100000/0111100000/0111110000/1111100000/1111100000/1111110000/0100110000/0000000000/0000000000/0000000000/";

    [DataFact]
    public void AcademyMatchesAllNativeTilesAndRespondsToDoorsAndAppearance()
    {
        var (world, area, resources) = Academy(); using var dispose = resources;
        var position = new Vector3(45, 15, 0); var door = WestDoor(area);
        Assert.Equal(Closed.Replace("/", ""), Mask(world, area, position));
        // The extra northern tile is visible through the installed portcullis BlockSight=0.
        Assert.True(RoomVisibility.For(world, area)!.VisibleTiles(area, position)[61]);
        door.IsOpen = true; // Direct restored/script state need not emit ObjectChanged.
        Assert.Equal(Open.Replace("/", ""), Mask(world, area, position));
        door.IsOpen = false;
        Assert.Equal(Closed.Replace("/", ""), Mask(world, area, position));
        door.GenericType = 2;
        Assert.Equal(Open.Replace("/", ""), Mask(world, area, position));
        door.GenericType = 0;
        Assert.Equal(Closed.Replace("/", ""), Mask(world, area, position));
        door.IsOpen = true;
        var first = RoomVisibility.For(world, area)!.VisibleTiles(area, position);
        Assert.Same(first, RoomVisibility.For(world, area)!.VisibleTiles(area, position));
    }

    [DataFact]
    public void DiscoveryPersistsButPerceptionDoesNotAndSnapshotsResendIt()
    {
        var (world, area, resources) = Academy(); using var dispose = resources;
        var pc = new Creature { RulesInitialised = true, CurrentHitPoints = 10, MaxHitPoints = 10, Faction = 0 };
        var npc = new Creature { RulesInitialised = true, CurrentHitPoints = 10, MaxHitPoints = 10, Faction = 1 };
        world.AddToArea(pc, area, new(45, 15, 0), 0, exact: true, enterEvent: false);
        world.AddToArea(npc, area, new(35, 15, 0), 0, exact: true, enterEvent: false);
        var server = new GameServer(world); var (client, endpoint) = LoopbackConnection.CreatePair();
        pc.Player = server.Accept(endpoint); pc.Player.Creature = pc; pc.Player.Area = area;
        pc.Society.RangesRow = 12; pc.Society.SightRange = 35; pc.Society.HearingRange = 20;
        var perception = world.System<PerceptionSystem>(); var exploration = world.System<MapExploration>();
        perception.Update(pc); exploration.Sync(pc);
        Assert.DoesNotContain(npc.Id, pc.Society.Heard); Assert.DoesNotContain(npc.Id, pc.Society.Seen);
        Assert.False(AreaSettings.Of(world, area).Explored[pc.Id][13]);
        var door = WestDoor(area); DoorService.Open(world, door, door);
        perception.Update(pc); exploration.Sync(pc);
        Assert.Contains(npc.Id, pc.Society.Heard); Assert.Contains(npc.Id, pc.Society.Seen);
        Assert.True(AreaSettings.Of(world, area).Explored[pc.Id][13]);
        DoorService.Close(world, door, door); perception.Update(pc); exploration.Sync(pc);
        Assert.DoesNotContain(npc.Id, pc.Society.Heard); Assert.DoesNotContain(npc.Id, pc.Society.Seen);
        Assert.True(AreaSettings.Of(world, area).Explored[pc.Id][13]);
        while (client.TryReceive(out _)) { }
        server.EnterArea(pc.Player, area, pc.Position, pc.Facing, alreadyPlaced: true);
        var snapshots = new List<NotifyMessage>();
        while (client.TryReceive(out var bytes)) { var reader = new MessageReader(bytes); if (reader.Type == MessageType.Notify) snapshots.Add(NotifyMessage.Read(reader)); }
        var snapshot = Assert.Single(snapshots, n => n.Kind == "area-map");
        using var json = System.Text.Json.JsonDocument.Parse(snapshot.Json);
        Assert.Equal('1', json.RootElement.GetProperty("explored").GetString()![13]);
    }

    [DataFact]
    public void StoreroomCrateHalfIsRevealedWithItsWalkableHalf()
    {
        // The Academy's storeroom (Storage 2 1x2): tic01_c07_01 at (8,4) is crates on Nonwalk floor, tic01_c06_01 at
        // (9,4) the walkable half. Native explores both, with the room's other four tiles (scene storeroom).
        var resources = ResMan.ForModule(TestData.Paths, Path.Combine(TestData.Config!.GameRoot, "data/nwm/Prelude.nwm"));
        using var dispose = resources;
        var world = new World(resources);
        var area = new AreaLoader(world).Load("m0q1d"); world.Areas.Add(area);
        var native = new string('0', 12 * 4) + "000000001100" + "000000001100" + "000000001100" + new string('0', 12 * 5);
        Assert.Equal(native, Mask(world, area, new(95, 47.3f, 0)));
        Assert.Equal(native, Mask(world, area, new(91, 45.5f, 0)));
        Assert.Equal(native, Mask(world, area, new(96, 51.5f, 0)));
    }

    [DataFact]
    public void NonwalkableFloorJoinsRoomsButWallTopsDoNot()
    {
        var world = new World(TestData.Module);
        var area = new Area { Width = 3, Height = 1, Tiles = new TileInfo[3] };
        area.Are.SetDWord("Flags", 1); world.Register(area); world.Areas.Add(area);
        void Quad(float x0, float x1, float z, int surface, bool walkable)
        {
            area.Walkmesh.Add(new(x0, 0, z), new(x1, 0, z), new(x1, 10, z), surface, walkable);
            area.Walkmesh.Add(new(x0, 0, z), new(x1, 10, z), new(x0, 10, z), surface, walkable);
        }
        Quad(0, 10, 0, 7, false);   // Nonwalk floor (WalkCheck 1): crates fill it, but it is still floor
        Quad(10, 20, 0, 4, true);   // walkable stone
        Quad(20, 30, 0, 2, false);  // Obscuring (WalkCheck 0): a wall's top
        var mask = RoomVisibility.For(world, area)!.VisibleTiles(area, new(15, 5, 0));
        Assert.Equal([true, true, false], mask);
    }

    [DataFact]
    public void OpenInteriorUsesEightTileStepsAndLooseDoorDoesNotSealIt()
    {
        var world = new World(TestData.Module);
        var area = new Area { Width = 16, Height = 16, Tiles = new TileInfo[256] };
        area.Are.SetDWord("Flags", 1); world.Register(area); world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(160, 0, 0), new(160, 160, 0), 1, true);
        area.Walkmesh.Add(new(0, 0, 0), new(160, 160, 0), new(0, 160, 0), 1, true);
        var door = new Door { Position = new(80, 75, 0), GenericType = 0 }; world.AddToArea(door, area, door.Position, 0, exact: true, enterEvent: false);
        area.Walkmesh.Add(new(80, 74, 0), new(80, 76, 0), new(80, 76, 3), 1, false, (int)door.Id);
        area.Walkmesh.Add(new(80, 74, 0), new(80, 76, 3), new(80, 74, 3), 1, false, (int)door.Id);
        var rooms = RoomVisibility.For(world, area)!;
        var mask = rooms.VisibleTiles(area, new(75, 75, 0));
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) Assert.Equal(Math.Abs(x - 7) + Math.Abs(y - 7) <= 8, mask[y * 16 + x]);
        Assert.Same(mask, rooms.VisibleTiles(area, new(79.8f, 75, 0)));
        Assert.True(rooms.VisibleTiles(area, new(80.2f, 75, 0))[15 * 16 + 8]);
        // A replaced walkmesh invalidates both static openings and the per-origin cached mask.
        area.Walkmesh.Clear();
        area.Walkmesh.Add(new(70, 70, 0), new(80, 70, 0), new(80, 80, 0), 1, true);
        area.Walkmesh.Add(new(70, 70, 0), new(80, 80, 0), new(70, 80, 0), 1, true);
        Assert.Single(RoomVisibility.For(world, area)!.TilesIn(area, new(75, 75, 0)));
    }

    [DataFact]
    public void CachedDoorListFollowsDoorsComingGoingOpeningAndChangingAppearance()
    {
        var world = new World(TestData.Module);
        var area = new Area { Width = 16, Height = 16, Tiles = new TileInfo[256] };
        area.Are.SetDWord("Flags", 1); world.Register(area); world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(160, 0, 0), new(160, 160, 0), 1, true);
        area.Walkmesh.Add(new(0, 0, 0), new(160, 160, 0), new(0, 160, 0), 1, true);
        // A door whose leaf runs the whole x = 80 seam seals the east half while it is shut.
        void Slab(Door d)
        {
            area.Walkmesh.Add(new(80, 0, 0), new(80, 160, 0), new(80, 160, 3), 1, false, (int)d.Id);
            area.Walkmesh.Add(new(80, 0, 0), new(80, 160, 3), new(80, 0, 3), 1, false, (int)d.Id);
        }
        var origin = new Vector3(75, 75, 0);
        bool EastVisible() => RoomVisibility.For(world, area)!.VisibleTiles(area, origin)[7 * 16 + 8];
        var door = new Door { GenericType = 0 };
        world.AddToArea(door, area, new(80, 80, 0), 0, exact: true, enterEvent: false);
        Slab(door);
        Assert.False(EastVisible());
        var doors = area.Doors;
        Assert.Same(doors, area.Doors);
        var placeable = new Placeable();
        world.AddToArea(placeable, area, new(20, 20, 0), 0, exact: true, enterEvent: false);
        Assert.Same(doors, area.Doors); // other objects leave the door list alone

        DoorService.Open(world, door, door);
        Assert.True(EastVisible());
        DoorService.Close(world, door, door);
        Assert.False(EastVisible());
        door.GenericType = 2; // BlockSight 0
        Assert.True(EastVisible());
        door.GenericType = 0;
        Assert.False(EastVisible());

        // Destroying the door takes it out of the list: its leftover footprint no longer seals anything.
        world.Destroy(door);
        Assert.NotSame(doors, area.Doors);
        Assert.Empty(area.Doors);
        Assert.True(EastVisible());

        // A door registered with its footprint but not yet placed is not watched; placing it shut seals the half.
        var late = new Door { GenericType = 0, IsOpen = true };
        world.Register(late);
        Slab(late);
        Assert.True(EastVisible());
        late.IsOpen = false;
        world.AddToArea(late, area, new(80, 80, 0), 0, exact: true, enterEvent: false);
        Assert.Same(late, Assert.Single(area.Doors));
        Assert.False(EastVisible());
        late.IsOpen = true;
        Assert.True(EastVisible());
    }
}
