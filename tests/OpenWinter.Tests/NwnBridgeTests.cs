using System.Numerics;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using UpdateFlag = OpenWinter.Net.Nwn.Messages.GameObjUpdateSharedMessages.GameObjUpdateType;

namespace OpenWinter.Tests;

/// <summary>How the NWN bridge turns NWN:EE world messages into OpenWinter server messages.</summary>
public class NwnBridgeTests
{
    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => "";
        public string Text(uint strRef, byte gender) => $"#{strRef}";
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    static (NwnWorldMirror, List<MessageReader>) Mirror()
    {
        var output = new List<MessageReader>();
        return (new NwnWorldMirror(new Data(), m => output.Add(new MessageReader(m))), output);
    }

    static AreaMessages.AreaClientArea Area(uint id = 5) => new()
    {
        ObjectId = id, Resref = "test", DisplayName = "Test", Width = 4, Height = 2, Tileset = "tds01",
        Tiles = Enumerable.Range(0, 8).Select(i => new AreaMessages.Tile { Id = i }).ToList(), Location = (20, 20, 0),
    };

    [Fact]
    public void AnAreaLoadsWithItsTilesAndStaticObjects()
    {
        var (mirror, output) = Mirror();
        var area = Area();
        area.StaticPlaceables.Add(new AreaMessages.Placeable { ObjectId = 9, Appearance = 65, Position = (1, 2, 0), Orientation = (0, 1, 0) });
        mirror.Area(area);
        var load = LoadAreaMessage.Read(output.First(m => m.Type == MessageType.LoadArea));
        Assert.Equal(("test", "Test", 4, 2, 8, 1u), (load.ResRef, load.Name, load.Width, load.Height, load.Tiles.Length, load.LoadToken));
        var statue = SpawnMessage.Read(output.First(m => m.Type == MessageType.Spawn));
        Assert.Equal((65, ObjectFlags.Static), (statue.Appearance.Type, statue.Flags));
        Assert.Equal(MathF.PI / 2, statue.Facing, 4);
        Assert.Equal(MessageType.Notify, output[^1].Type);
    }

    [Fact]
    public void CreaturesSpawnWithTheirAppearance()
    {
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        output.Clear();
        var parts = Enumerable.Range(0, 19).Select(i => (ushort)(100 + i)).ToList();
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListAddCreature { ObjectId = 7, Position = (3, 4, 0), Orientation = (-1, 0, 0) },
                new GameObjUpdateObjListAppearanceCreature
                {
                    ObjectId = 7, Flags = UpdateCreatureAppearanceFlags.All, Race = 86, Gender = 1, HeadVariation = 3,
                    Name = new NwnTagged(1, new OriginalCreatureName { First = NwnLocString.FromText("Goblin"), Last = NwnLocString.FromText("") }),
                    PartInfo = new CreaturePartInfo { Parts = parts },
                    EquippedItems =
                    [
                        new NwnTagged('A', new EquippedItemAdd
                        {
                            Slot = 16,
                            Item = new ItemAppearanceAndProps
                            {
                                BaseItemId = 1, ModelData = new NwnTagged(2, new CompositeModel { Part1 = 61, Part2 = 11, Part3 = 21 }),
                                Properties = [new ItemPropertyEntry { Name = 44, Param1 = 2, CostTableValue = 3 }],
                            },
                        }),
                    ],
                },
            },
        });
        var spawn = SpawnMessage.Read(Assert.Single(output, m => m.Type == MessageType.Spawn));
        Assert.Equal(("Goblin", 86, (byte)1), (spawn.Name, spawn.Appearance.Type, spawn.Appearance.Gender));
        Assert.Equal(MathF.PI, MathF.Abs(spawn.Facing), 4);
        // Head first, then neck (NWN part 9), torso (7), pelvis (6) ... left foot (1).
        Assert.Equal(new ushort[] { 3, 109, 107, 106, 108, 114, 115, 112, 113, 110, 111, 116, 117, 105, 104, 102, 103, 100, 101 }, spawn.Appearance.BodyParts);
        var sword = Assert.Single(spawn.Appearance.Equipment);
        Assert.Equal((16, 1, 2, 3), (sword.Slot, sword.BaseItem, (int)sword.LightColor, (int)sword.LightLevel));
        Assert.Equal(new ushort[] { 61, 11, 21 }, sword.ModelParts);
    }

    [Fact]
    public void WalkedPathsBecomeMovementTheClientCanFollow()
    {
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListAddCreature { ObjectId = 7, Position = (0, 0, 0) },
                new GameObjUpdateObjListAppearanceCreature { ObjectId = 7, Flags = UpdateCreatureAppearanceFlags.Race },
            },
        });
        output.Clear();
        // A bearing of 0 faces +Y; the path runs 4 m along +X at the walk rate (2 m/s).
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListUpdateCreature
                {
                    ObjectId = 7, Flags = UpdateFlag.ObjectPositionFlag | UpdateFlag.ObjectOrientationFlag | UpdateFlag.ObjectAnimationFlag,
                    Position = (0, 0, 0), Orientation = new OrientationUpdate { Bearing = 0 },
                    AnimTypeData = new GameObjUpdateAnimationMessages.AnimTypeData { AnimType = 2 },
                    Waypoints = new CreatureWaypointUpdate { NumWaypoints = 1, Waypoints = [(4, 0)] },
                },
            },
        });
        var start = UpdateMessage.Read(Assert.Single(output));
        Assert.Equal((MovementMode.Walk, 0f), (start.Mode, start.Position.X));
        Assert.Equal(MathF.PI / 2, start.Facing, 4);
        output.Clear();
        mirror.Tick(1f);
        var half = UpdateMessage.Read(Assert.Single(output));
        Assert.Equal(new Vector3(2, 0, 0), half.Position);
        Assert.Equal(0f, half.Facing, 4);
        mirror.Tick(1.5f);
        var end = UpdateMessage.Read(output[^1]);
        Assert.Equal((new Vector3(4, 0, 0), MovementMode.Stand), (end.Position, end.Mode));
    }

    [Fact]
    public void ExploredTilesArriveAsTheMapMask()
    {
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        output.Clear();
        // Two bytes for eight tiles: bit i of the stream is tile i, lowest bit first.
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries = { new GameObjUpdateObjListPlayerInfo { ObjectId = 7, Automap = new AutoMapUpdate { Bitstream = true, RawBytes = [0b1000_0101] } } },
        });
        var notice = NotifyMessage.Read(Assert.Single(output));
        Assert.Equal("area-map", notice.Kind);
        Assert.Contains("\"explored\":\"10100001\"", notice.Json);
    }

    static GameObjUpdateObjListAddDoor Door(uint id, ushort anim) => new()
    {
        ObjectId = id, GenericType = 5, OriginalName = false, StrName = "Door", Anim = anim,
    };

    [Fact]
    public void DoorsAreOpenOrClosedFromTheirAnimationState()
    {
        // As recorded from nwserver: a closed door's state is 22, an opened one's 51.
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        output.Clear();
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList { Entries = { Door(7, 22), Door(4, 51) } });
        var spawns = output.Where(m => m.Type == MessageType.Spawn).Select(m => SpawnMessage.Read(m)).ToDictionary(s => s.ObjectId);
        Assert.Equal(0u, (uint)(spawns[7].Flags & ObjectFlags.Open));
        Assert.Equal(ObjectFlags.Open, spawns[4].Flags & ObjectFlags.Open);
        output.Clear();
        // The server closes door 4: an update with only the animation.
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries = { new ObjListUpdateDoor { ObjectType = NwnExternalTypes.ObjectType.Door, ObjectId = 4, Flags = 4, Animation = 22 } },
        });
        var update = UpdateMessage.Read(Assert.Single(output));
        Assert.Equal((4u, (ObjectFlags)0), (update.ObjectId, update.Flags & ObjectFlags.Open));
    }

    [Fact]
    public void ClickingADoorOpensOrClosesIt()
    {
        var (mirror, _) = Mirror();
        mirror.Area(Area());
        var transition = Door(9, 51);
        transition.Clickable = true;
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList { Entries = { Door(7, 22), Door(4, 51), transition } });
        // As recorded: 21 opens a closed door, 22 closes an open one.
        Assert.Equal((7u, (ushort)21), (mirror.DoorRequest(7)!.Door, mirror.DoorRequest(7)!.Animation));
        Assert.Equal((ushort)22, mirror.DoorRequest(4, use: true)!.Animation);
        // An open transition is travelled through on a plain use, closed from the radial menu.
        Assert.Null(mirror.DoorRequest(9, use: true));
        Assert.Equal((ushort)22, mirror.DoorRequest(9)!.Animation);
        Assert.Null(mirror.DoorRequest(5));
    }

    [Fact]
    public void KeyboardDrivingSendsTheBearingAndGaitTheServerExpects()
    {
        // As recorded: facing north is bearing 900 (the server then reports 0 from +Y); running sets flag 2.
        Assert.Equal((ushort)900, NwnBridgeConnection.Bearing(MathF.PI / 2));
        Assert.Equal((ushort)0, NwnBridgeConnection.Bearing(0));
        Assert.Equal((ushort)2700, NwnBridgeConnection.Bearing(-MathF.PI / 2));
        Assert.Equal(((byte)1, (byte)3), (NwnBridgeConnection.DriveFlags(walking: true), NwnBridgeConnection.DriveFlags(walking: false)));
    }

    [Fact]
    public void GroundItemsSpawnWithTheirTalkTableName()
    {
        // As recorded: a longsword on the ground, named by strref 166, made useable by its first update.
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        output.Clear();
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListAddItem
                {
                    ObjectId = 36,
                    Item = new ItemAppearanceAndProps
                    {
                        BaseItemId = 1, ModelData = new NwnTagged(2, new CompositeModel { Part1 = 11, Part2 = 11, Part3 = 11 }),
                        Name = new NwnTagged(1, new OriginalItemName { Name = NwnLocString.FromStrRef(166, 0) }),
                    },
                },
                new ObjListUpdateItem { ObjectId = 36, Position = (20, 22, 0), Useable = true },
            },
        });
        var spawn = SpawnMessage.Read(Assert.Single(output));
        Assert.Equal((ObjectKind.Item, "#166", 1, new System.Numerics.Vector3(20, 22, 0)), (spawn.Kind, spawn.Name, spawn.Appearance.Type, spawn.Position));
        Assert.Equal(ObjectFlags.Useable | ObjectFlags.Selectable, spawn.Flags);
        Assert.Equal(ObjectKind.Item, mirror.KindOf(36));
    }
}
