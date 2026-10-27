using System.IO.Compression;
using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// The camera script functions, animation replacements and custom animations through the NWN bridge, from frames
/// recorded on nwserver running the camanim scene (parity/visual/scenes/camanim): the player and an actor (0xA).
/// Each frame is named by its time in the recording.
/// </summary>
public class NwnBridgeCameraTests
{
    const uint Actor = 0xA, Player = 0x7FFFFFFF;

    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 1000;
        public string TwoDA(string table, int row, string column) => (table, row, column) switch
        {
            ("baseitems", 16, "ModelType") => "3",
            ("baseitems", _, "ModelType") => "2",
            ("classes", _, _) => "****",
            _ => "",
        };
        public string Text(uint strRef, byte gender) => $"#{strRef}";
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    static readonly Lazy<List<(long Ms, byte[] Frame)>> Recording = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nwn-bridge-camera.jsonl.gz");
        using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        var frames = new List<(long, byte[])>();
        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var row = JsonDocument.Parse(line).RootElement;
            frames.Add((row.GetProperty("ms").GetInt64(), Convert.FromHexString(row.GetProperty("hex").GetString()!)));
        }
        return frames;
    });

    /// <summary>The area and its creatures as the bridge's connection dispatches them; then each later frame on demand.</summary>
    sealed class Bridge
    {
        readonly Data _data = new();
        readonly NwnSessionContext _context;
        readonly NwnWorldMirror _world;
        readonly NwnPresentation _presentation;
        readonly List<byte[]> _output = [];

        public Bridge()
        {
            _context = new NwnSessionContext(_data.TwoDARowCount, _data.TwoDA);
            _world = new NwnWorldMirror(_data, _output.Add);
            _presentation = new NwnPresentation(_data, _world, _output.Add);
            foreach (var ms in new long[] { 2247, 2431, 2617 }) Feed(ms);
            // The server's control message, as the connection hands it on.
            _world.Control(Player);
            _output.Clear();
        }

        public List<MessageReader> Feed(long ms)
        {
            int before = _output.Count;
            foreach (var (_, frame) in Recording.Value.Where(f => f.Ms == ms))
            {
                var m = NwnMessageRegistry.Create(frame[1], frame[2], NwnDirection.ServerToClient)!;
                m.Read(new NwnWsReader(frame.AsMemory(3), _context));
                switch (m)
                {
                    case AreaMessages.AreaClientArea area:
                        _world.Area(area);
                        _presentation.Area(area);
                        break;
                    case GameObjUpdateMessages.GameObjUpdateObjList list:
                        _presentation.ObjectList(list, _world.ObjectList);
                        break;
                    default:
                        if (!NwnCamera.ToClient(m, _world.Controlled, _output.Add)) _presentation.ToClient(m);
                        break;
                }
            }
            return _output.Skip(before).Select(b => new MessageReader(b)).ToList();
        }
    }

    static List<(string Kind, uint Subject, JsonElement Json)> Notices(List<MessageReader> output) =>
        output.Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read)
            .Select(n => (n.Kind, n.Subject, JsonDocument.Parse(n.Json).RootElement)).ToList();

    static JsonElement Camera(Bridge bridge, long ms) => Assert.Single(Notices(bridge.Feed(ms)), n => n.Kind == "camera").Json;

    static (float Direction, float Distance, float Pitch, int Transition) Facing(JsonElement j)
    {
        Assert.Equal("facing", j.GetProperty("op").GetString());
        return (MathF.Round(j.GetProperty("direction").GetSingle(), 3), j.GetProperty("distance").GetSingle(), j.GetProperty("pitch").GetSingle(),
            j.GetProperty("transition").GetInt32());
    }

    [Fact]
    public void SetCameraFacingArrivesInTheScriptsUnits()
    {
        var bridge = new Bridge();
        // SetCameraFacing(90.0, 10.0, 45.0, SNAP): the wire's radians become the script's degrees again.
        Assert.Equal((90f, 10f, 45f, 0), Facing(Camera(bridge, 5122)));
        // A part the script left at -1 isn't sent, and stays -1.
        Assert.Equal((0f, -1f, -1f, 0), Facing(Camera(bridge, 7992)));
        Assert.Equal((-1f, 6f, -1f, 0), Facing(Camera(bridge, 10860)));
        Assert.Equal((-1f, -1f, 70f, 0), Facing(Camera(bridge, 13725)));
        // The transition is CAMERA_TRANSITION_TYPE_*: MEDIUM, FAST, SLOW, CRAWL.
        Assert.Equal((270f, 12f, 50f, 40), Facing(Camera(bridge, 18514)));
        Assert.Equal((90f, 12f, 50f, 70), Facing(Camera(bridge, 24250)));
        Assert.Equal((270f, 6f, 80f, 20), Facing(Camera(bridge, 29040)));
        Assert.Equal((90f, 12f, 50f, 2), Facing(Camera(bridge, 35733)));
        Assert.Equal((200f, 20f, 30f, 0), Facing(Camera(bridge, 53892)));
    }

    [Fact]
    public void TheOtherCameraFunctionsReachTheClientAsOpenWintersServerSendsThem()
    {
        var bridge = new Bridge();
        Assert.Equal(3f, Camera(bridge, 44323).GetProperty("height").GetSingle());
        Assert.Equal(0f, Camera(bridge, 47201).GetProperty("height").GetSingle());
        Assert.Equal(-1f, Camera(bridge, 50060).GetProperty("height").GetSingle());

        var store = Assert.Single(Notices(bridge.Feed(52932)));
        Assert.Equal(("camera-store", Player), (store.Kind, store.Subject));
        Assert.Equal("restore", Camera(bridge, 56767).GetProperty("op").GetString());

        // SetCameraMode: CAMERA_MODE_* as it is.
        Assert.Equal([0, 2, 1], new long[] { 59634, 60581, 61539 }.Select(ms => Camera(bridge, ms).GetProperty("mode").GetInt32()));

        var locks = new long[] { 62498, 62976, 63455, 63920, 64405, 64886 }
            .Select(ms => Assert.Single(Notices(bridge.Feed(ms)), n => n.Kind == "camera-lock").Json)
            .Select(j => (j.GetProperty("which").GetString(), j.GetProperty("locked").GetBoolean()));
        Assert.Equal([("pitch", true), ("pitch", false), ("distance", true), ("distance", false), ("direction", true), ("direction", false)], locks);

        var limits = Camera(bridge, 65365);
        Assert.Equal((20f, 60f, 4f, 15f), (limits.GetProperty("minPitch").GetSingle(), limits.GetProperty("maxPitch").GetSingle(),
            limits.GetProperty("minDistance").GetSingle(), limits.GetProperty("maxDistance").GetSingle()));
        Assert.Equal(-1f, Camera(bridge, 66311).GetProperty("maxDistance").GetSingle());

        // CAMERA_FLAG_DISABLE_ZOOM | CAMERA_FLAG_DISABLE_TURN, then none.
        Assert.Equal(80, Camera(bridge, 67257).GetProperty("flags").GetInt32());
        Assert.Equal(0, Camera(bridge, 67737).GetProperty("flags").GetInt32());

        // AttachCamera to the actor, then back: the server names the player itself, the client none.
        var attach = Camera(bridge, 68219);
        Assert.Equal(("attach", Actor, true), (attach.GetProperty("op").GetString(), attach.GetProperty("target").GetUInt32(), attach.GetProperty("findClearView").GetBoolean()));
        Assert.Equal(GameObjUpdateMessages.InvalidObjectId, Camera(bridge, 70604).GetProperty("target").GetUInt32());
    }

    [Fact]
    public void CutsceneModeStoresTheCameraAndPutsItBack()
    {
        var bridge = new Bridge();
        Assert.Contains(Notices(bridge.Feed(120750)), n => n.Kind == "camera-store");
        Assert.Equal((180f, 8f, 30f, 0), Facing(Camera(bridge, 121663)));
        Assert.Equal("restore", Camera(bridge, 124406).GetProperty("op").GetString());
    }

    [Fact]
    public void AnimationReplacementsArriveAsEachCallDid()
    {
        var bridge = new Bridge();
        // Every update carries the whole list; the client hears each change.
        (uint, string, string) Swap(long ms)
        {
            var n = Assert.Single(Notices(bridge.Feed(ms)), n => n.Kind == "object-animation");
            return (n.Subject, n.Json.GetProperty("old").GetString()!, n.Json.GetProperty("new").GetString()!);
        }
        Assert.Equal((Actor, "pause1", "custom3lp"), Swap(73037));
        Assert.Equal((Actor, "walk", "run"), Swap(75915));
        // ReplaceObjectAnimation(o, "pause1", ""): it drops out of the list.
        Assert.Equal((Actor, "pause1", ""), Swap(76861));
        Assert.Equal((Actor, "walk", "walk"), Swap(79744));
        Assert.Equal((Actor, "walk", ""), Swap(80692));
    }

    [Fact]
    public void CustomAnimationsPlayAsTheirCustomClips()
    {
        var bridge = new Bridge();
        PlayAnimationMessage Played(long ms) =>
            Assert.Single(bridge.Feed(ms).Where(r => r.Type == MessageType.PlayAnimation).Select(PlayAnimationMessage.Read), a => a.ObjectId == Actor);
        // ANIMATION_LOOPING_CUSTOM3 (code 101) and CUSTOM6 (104) loop until the server stands the actor up.
        var custom3 = Played(82612);
        Assert.Equal(("custom3", false), (custom3.Animation, custom3.Gesture));
        Assert.True(custom3.Duration > 1000);
        Assert.Equal("pause1", Played(88367).Animation);
        Assert.Equal("custom6", Played(92177).Animation);
        Assert.Equal("pause1", Played(97876).Animation);
        Assert.Equal("custom4", Played(101677).Animation);
        Assert.Equal("pause1", Played(107391).Animation);
        Assert.Equal("custom1", Played(111231).Animation);
    }

    [Fact]
    public void LoopingCustomAnimationsAreNamedAsTheModelsCustomClips()
    {
        // ANIMATION_LOOPING_CUSTOM3, CUSTOM6, CUSTOM20 and CUSTOM21 on OpenWinter's own server.
        Assert.Equal(["custom3", "custom6", "custom20", "custom21"], new[] { 23, 26, 40, 43 }.Select(a => OpenWinter.Game.Animations.Name(a).Name));
        // A model without custom6 clips plays the engine's own self-cast for CUSTOM6 (camanim scene); CUSTOM12 has no stand-in.
        Assert.Equal("castself", OpenWinter.Game.Animations.CustomFallback("custom6"));
        Assert.Null(OpenWinter.Game.Animations.CustomFallback("custom12"));
    }
}
