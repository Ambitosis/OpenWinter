using System.Numerics;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
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
        public string Text(uint strRef, byte gender) => strRef switch
        {
            10468 => "Acquired Item: <CUSTOM0>",
            8296 => "This object is locked.",
            _ => $"#{strRef}",
        };
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
        // Strafing right (the server then plays its strafe-right animation and follows the reported positions).
        Assert.Equal((byte)9, NwnBridgeConnection.DriveFlags(walking: true, forward: 0, right: 1));
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

    [Fact]
    public void WhileDrivingTheServersTrailingEchoesAreIgnoredButATeleportIsNot()
    {
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListAddCreature { ObjectId = 7, Position = (20, 20, 0) },
                new GameObjUpdateObjListAppearanceCreature { ObjectId = 7, Flags = UpdateCreatureAppearanceFlags.Race },
            },
        });
        mirror.Control(7);
        mirror.Driving = true;
        mirror.Predict(7, new Vector3(20, 22, 0), MathF.PI / 2, MovementMode.Walk);
        output.Clear();
        GameObjUpdateMessages.GameObjUpdateObjList Echo(float y) => new()
        {
            Entries =
            {
                new GameObjUpdateObjListUpdateCreature
                {
                    ObjectId = 7, Flags = UpdateFlag.ObjectPositionFlag | UpdateFlag.ObjectAnimationFlag, Position = (20, y, 0),
                    AnimTypeData = new GameObjUpdateAnimationMessages.AnimTypeData { AnimType = 2 },
                },
            },
        };
        // As recorded while walking forward: the server's position trails the reported one by 0.1-0.3 m.
        mirror.ObjectList(Echo(21.8f));
        Assert.Empty(output);
        Assert.True(mirror.TryPose(7, out var at, out _, out _, out _));
        Assert.Equal(new Vector3(20, 22, 0), at);
        // A jump far from where the client has it is taken.
        mirror.ObjectList(Echo(35));
        Assert.Equal(new Vector3(20, 35, 0), UpdateMessage.Read(Assert.Single(output)).Position);
        // Once released, the server's position is the creature's again.
        output.Clear();
        mirror.Driving = false;
        mirror.ObjectList(Echo(35.3f));
        Assert.Equal(new Vector3(20, 35.3f, 0), UpdateMessage.Read(Assert.Single(output)).Position);
    }

    [Fact]
    public void FeedbackIsWrittenToChatWithTheTalkTableLine()
    {
        // As recorded: picking up a longsword, then clicking a locked door.
        var chat = new List<ChatMessage>();
        void Feed(object payload, long tag) => NwnFeedback.ToClient(
            new ClientSideMessageFeedbackMessages.ClientSideMessageFeedback { Payload = new NwnTagged(tag, payload) }, new Data(),
            m => chat.Add(ChatMessage.Read(new MessageReader(m))));
        Feed(new ClientSideMessageFeedbackMessages.FeedbackItemReceived
        {
            Item = new ClientSideMessageFeedbackMessages.ItemData { Identified = true, Name = new NwnTagged(1, NwnLocString.FromStrRef(166)) },
        }, 50);
        Feed(new ClientSideMessageFeedbackMessages.FeedbackObjectLocked { LockedObject = 3 }, 13);
        Feed(new ClientSideMessageFeedbackMessages.FeedbackPaused(), 205);
        Assert.Equal(["Acquired Item: #166", "This object is locked."], chat.Select(c => c.Text));
        Assert.All(chat, c => Assert.Equal(ChatChannel.Server, c.Channel));
    }

    [Fact]
    public void WhileDrivingAPositionOffTheDrivenPathIsTaken()
    {
        // As recorded: running north-east from (23.3, 23.9), the server first trails the client along its path,
        // then stops the creature against an obstacle while the client's position runs on.
        var (mirror, output) = Mirror();
        mirror.Area(Area());
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListAddCreature { ObjectId = 7, Position = (23.3f, 23.9f, 0) },
                new GameObjUpdateObjListAppearanceCreature { ObjectId = 7, Flags = UpdateCreatureAppearanceFlags.Race },
            },
        });
        mirror.Control(7);
        mirror.Driving = true;
        GameObjUpdateMessages.GameObjUpdateObjList Echo(float x, float y) => new()
        {
            Entries =
            {
                new GameObjUpdateObjListUpdateCreature
                {
                    ObjectId = 7, Flags = UpdateFlag.ObjectPositionFlag | UpdateFlag.ObjectAnimationFlag, Position = (x, y, 0),
                    AnimTypeData = new GameObjUpdateAnimationMessages.AnimTypeData { AnimType = 4 },
                },
            },
        };
        foreach (var (x, y) in new[] { (24.29f, 24.89f), (24.59f, 25.19f), (24.88f, 25.48f), (25.18f, 25.78f) })
            mirror.Predict(7, new Vector3(x, y, 0), MathF.PI / 4, MovementMode.Run);
        output.Clear();
        mirror.ObjectList(Echo(25.11f, 25.80f));
        Assert.Empty(output);
        foreach (var (x, y) in new[] { (25.47f, 26.07f), (25.77f, 26.37f), (26.06f, 26.66f), (26.35f, 26.95f) })
            mirror.Predict(7, new Vector3(x, y, 0), MathF.PI / 4, MovementMode.Run);
        output.Clear();
        mirror.ObjectList(Echo(24.97f, 26.72f));
        Assert.Equal(new Vector3(24.97f, 26.72f, 0), UpdateMessage.Read(Assert.Single(output)).Position);
    }

    static NwnMessage Decode(string hex, NwnMessage into)
    {
        into.Read(new NwnWsReader(Convert.FromHexString(hex), new NwnSessionContext(_ => 0, (_, _, _) => "")));
        return into;
    }

    [Fact]
    public void NuiWindowsAndBindsReachTheClient()
    {
        // As recorded: a script-built window, then a bind. JSON travels as its type byte, a length and the text.
        var open = Decode("0803000000" + "0e0400000077696e64" + "0801000000" + "10110000007b22726f6f74223a7b7d2c2266223a317d",
            new NuiMessages.NuiCreateWindow());
        var bind = Decode("0801000000" + "0803000000" + "0e0c00000077696e646f775f7469746c65" + "10020000002222", new NuiMessages.NuiBinds());
        var notes = new List<NotifyMessage>();
        foreach (var m in new[] { open, bind, new NuiMessages.NuiDestroyWindow { Token = 3 } })
            Assert.True(NwnNui.ToClient(m, b => notes.Add(NotifyMessage.Read(new MessageReader(b)))));
        Assert.Equal(["nui-open", "nui-bind", "nui-close"], notes.Select(n => n.Kind));
        Assert.Equal("""{"token":3,"id":"wind","resref":"","window":{"root":{},"f":1}}""", notes[0].Json);
        Assert.Equal("""{"token":3,"bind":"window_title","value":""}""", notes[1].Json);
        Assert.Equal("""{"token":3}""", notes[2].Json);
    }

    [Fact]
    public void NuiClicksGoBackAsTheEventTheServerReads()
    {
        // The encoding the server acted on: a click on a window's button, with no payload.
        var events = NwnNui.FromClient("nui-event", """{"token":3,"type":"click","element":"ok","index":-1,"payload":null}""");
        var w = new NwnWsWriter(new NwnSessionContext(_ => 0, (_, _, _) => ""));
        events!.Write(w);
        Assert.Equal("0801000000" + "0803000000" + "0e05000000636c69636b" + "0e020000006f6b" + "08ffffffff" + "10040000006e756c6c",
            Convert.ToHexString(w.ToArray()).ToLowerInvariant());
        var edit = NwnNui.FromClient("nui-bind", """{"token":3,"bind":"name","value":"Ann"}""");
        Assert.Equal("\"Ann\"", Assert.Single(Assert.IsType<NuiMessages.NuiClientBinds>(edit).Binds).Value);
        Assert.Null(NwnNui.FromClient("area-ready", "{}"));
    }

    [Fact]
    public void NuiBindEditsGoBackWithTheArrayIndexTheServerReads()
    {
        // Recorded edits the server took whole, with no desync, its watch event reporting the index sent
        // (parity/suites/bridge_nui): a single value with -1, then an array with row 0.
        string Encode(string json)
        {
            var w = new NwnWsWriter(new NwnSessionContext(_ => 0, (_, _, _) => ""));
            NwnNui.FromClient("nui-bind", json)!.Write(w);
            return Convert.ToHexString(w.ToArray());
        }
        Assert.Equal("0801000000" + "0801000000" + "0E040000006E616D65" + "1005000000226F6E6522" + "08FFFFFFFF",
            Encode("""{"token":1,"bind":"name","value":"one"}"""));
        Assert.Equal("0801000000" + "0801000000" + "0E04000000726F7773" + "100F0000005B22723261222C2262222C2263225D" + "0800000000",
            Encode("""{"token":1,"bind":"rows","value":["r2a","b","c"],"index":0}"""));

        // A recorded client frame decodes with its index; the server's own bind entries still end at the value.
        var read = Decode("0801000000" + "0801000000" + "0E04000000726F7773" + "100600000022656C656D22" + "0801000000",
            Assert.IsType<NuiMessages.NuiClientBinds>(NwnMessageRegistry.Create(0x37, 0x04, NwnDirection.ClientToServer)));
        var entry = Assert.Single(((NuiMessages.NuiClientBinds)read).Binds);
        Assert.Equal(("rows", "\"elem\"", 1), (entry.Bind, entry.Value, entry.ArrayIndex));
        Assert.IsType<NuiMessages.NuiBinds>(NwnMessageRegistry.Create(0x37, 0x04, NwnDirection.ServerToClient));
    }
}
