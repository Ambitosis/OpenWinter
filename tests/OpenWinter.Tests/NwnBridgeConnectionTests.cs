using System.Collections.Concurrent;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;

namespace OpenWinter.Tests;

/// <summary>The bridge connection as a whole, logged in to a scripted server: what reaches the client and the server.</summary>
public class NwnBridgeConnectionTests
{
    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => "";
        public readonly Dictionary<uint, string> Overrides = [];
        public string Text(uint strRef, byte gender) => Overrides.GetValueOrDefault(strRef, "");
        public void SetText(uint strRef, string text) => Overrides[strRef] = text;
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    sealed class Transport : INwnTransport
    {
        public readonly ConcurrentQueue<byte[]> ToClient = new();
        public readonly List<byte[]> FromClient = [];
        public bool IsOpen => true;
        public string? CloseReason => null;
        public void Send(byte[] frame) => FromClient.Add(frame);
        public bool TryReceive(out byte[] frame) => ToClient.TryDequeue(out frame!);
        public void Close(string reason) { }
        public void Dispose() { }
    }

    static readonly NwnSessionContext Context = new(_ => 0, (_, _, _) => "");

    /// <summary>A bridge logged in to the scripted server.</summary>
    static (NwnBridgeConnection, Transport) Joined(Data? data = null, Action<string>? log = null)
    {
        var t = new Transport();
        var bridge = new NwnBridgeConnection(new NwnBridgeOptions(new Uri("ws://test"), NwnCdKey.Generate(new Random(1))) { Connect = _ => t, Log = log }, data ?? new Data());
        bridge.Send(new HelloMessage { PlayerName = "probe", AreaReadyHandshake = true }.Write());
        for (int i = 0; i < 20 && !bridge.Joined; i++)
        {
            Drain(bridge);
            if (t.FromClient.Count == 0) continue;
            NwnPacket? reply = NwnPacket.Parse(t.FromClient[^1]) switch
            {
                GnhiPacket => new GnhiPacket(),
                BnesPacket => new BnerPacket(0, "Test"),
                BnxiPacket => new BnxrPacket { ModuleName = "Test" },
                BndsPacket => new BndrPacket("details", "a module", "8193"),
                BncsPacket => new BncrPacket(BncrPacket.Accepted, NwnAuthResult.Ok, [], [2], [3]),
                BnvsPacket => new BnvrPacket(true, NwnAuthResult.Ok, 1, 8193, 37, 17),
                _ => null,
            };
            if (reply != null) t.ToClient.Enqueue(reply.ToBytes());
        }
        Assert.True(bridge.Joined);
        Drain(bridge);
        t.FromClient.Clear();
        return (bridge, t);
    }

    static List<byte[]> Drain(NwnBridgeConnection bridge)
    {
        var got = new List<byte[]>();
        while (bridge.TryReceive(out var m)) got.Add(m);
        return got;
    }

    static byte[] Frame(NwnMessage m)
    {
        var w = new NwnWsWriter(Context);
        m.Write(w);
        return [(byte)'P', m.Major, m.Minor, .. w.ToArray()];
    }

    static AreaMessages.AreaClientArea Area() => new()
    {
        ObjectId = 5, Resref = "test", DisplayName = "Test", Width = 1, Height = 1, Tileset = "tds01",
        Tiles = [new AreaMessages.Tile { Id = 0 }], Location = (5, 5, 0),
    };

    static GameObjUpdateMessages.GameObjUpdateObjList Creature() => new()
    {
        Entries =
        {
            new GameObjUpdateObjListAddCreature { ObjectId = 7, Position = (3, 4, 0), Orientation = (-1, 0, 0) },
            new GameObjUpdateObjListAppearanceCreature { ObjectId = 7, Flags = UpdateCreatureAppearanceFlags.Race, Race = 6 },
        },
    };

    [Fact]
    public void AnObjectListReachesTheClientOnce()
    {
        var (bridge, t) = Joined();
        t.ToClient.Enqueue(Frame(Area()));
        Drain(bridge);
        t.ToClient.Enqueue(Frame(Creature()));
        var got = Drain(bridge).Select(b => new MessageReader(b).Type).ToList();

        // What the world mirror alone makes of the same list.
        var alone = new List<MessageType>();
        var mirror = new NwnWorldMirror(new Data(), b => alone.Add(new MessageReader(b).Type));
        mirror.Area(Area());
        alone.Clear();
        mirror.ObjectList(Creature());

        Assert.Equal(1, got.Count(m => m == MessageType.Spawn));
        foreach (var type in new[] { MessageType.Spawn, MessageType.Appearance, MessageType.Update })
            Assert.Equal(alone.Count(m => m == type), got.Count(m => m == type));
    }

    [Fact]
    public void ALongChatLineGoesInPiecesTheServerTakes()
    {
        var (bridge, t) = Joined();
        var words = string.Join(" ", Enumerable.Repeat("word", 1500));
        bridge.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = words }.Write());
        var sent = t.FromClient.Where(f => f[1] == ChatMessages.ChatTalkC2S.MajorId).ToList();
        Assert.Equal(4, sent.Count);
        Assert.All(sent, f => Assert.InRange(f.Length, 1, NwnSession.MaxClientFrameBytes));
        var said = sent.Select(f =>
        {
            var m = new ChatMessages.ChatTalkC2S();
            m.Read(new NwnWsReader(f.AsMemory(3), Context));
            return m.Message;
        });
        Assert.Equal(words, string.Join(" ", said));
    }

    [Fact]
    public void AMessageTooLargeForTheServerIsNotSent()
    {
        var (bridge, t) = Joined();
        // A NUI value of 5000 characters: WSNetLayer stops the server on a client message over about 4 KiB.
        var value = new string('x', 5000);
        bridge.Send(new ClientRequestMessage { Kind = "nui-bind", Json = $$"""{"token":1,"bind":"text","value":"{{value}}"}""" }.Write());
        Assert.Empty(t.FromClient);
        Assert.Contains("NuiClientBinds", Assert.Single(bridge.Session!.Unsent));
        bridge.Send(new ClientRequestMessage { Kind = "nui-bind", Json = """{"token":1,"bind":"text","value":"short"}""" }.Write());
        Assert.Single(t.FromClient);
    }

    static List<NotifyMessage> Notices(IEnumerable<byte[]> got) =>
        got.Select(b => new MessageReader(b)).Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).ToList();

    [Fact]
    public void TextureAndResourceOverridesReachTheClientAsTheServerSendsThem()
    {
        var (bridge, t) = Joined();
        // SetTextureOverride travels as an override of RESTYPE_TEX (2001); NWNX_Player_SetResManOverride as its own type.
        t.ToClient.Enqueue(Frame(new ResmanMessages.ResmanOverride { Action = ResmanMessages.ResmanOverrideAction.Store, Restype = 2001, Name = "gui_loadwell", NewName = "gui_replace01" }));
        t.ToClient.Enqueue(Frame(new ResmanMessages.ResmanOverride { Action = ResmanMessages.ResmanOverrideAction.Remove, Restype = 2002, Name = "c_spidgiant" }));
        var notices = Notices(Drain(bridge));

        Assert.Equal("""{"old":"gui_loadwell","new":"gui_replace01"}""", Assert.Single(notices, n => n.Kind == "texture-override").Json);
        Assert.Equal("""{"op":"resman-override","type":2002,"from":"c_spidgiant","to":""}""", Assert.Single(notices, n => n.Kind == "nwnx-player").Json);
        Assert.Empty(bridge.Unused);
    }

    [Fact]
    public void TalkTableOverridesChangeTheBridgesTextAndTheClients()
    {
        var data = new Data();
        var (bridge, t) = Joined(data);
        t.ToClient.Enqueue(Frame(new ResmanMessages.ResmanTlkOverrideList { Overrides = [(85561u, "Damage Immunity Decreased"), (16777300u, "Custom")] }));
        t.ToClient.Enqueue(Frame(new ResmanMessages.ResmanTlkOverride { Strref = 85561, Value = "" }));
        var notices = Notices(Drain(bridge)).Where(n => n.Kind == "tlk-override").Select(n => n.Json).ToList();

        Assert.Equal(["""{"strref":85561,"text":"Damage Immunity Decreased"}""", """{"strref":16777300,"text":"Custom"}""", """{"strref":85561,"text":""}"""], notices);
        Assert.Equal("Custom", data.Text(16777300, 0));
        Assert.Equal("", data.Overrides[85561]);
    }

    [Fact]
    public void MessagesNothingUsesAreCountedAndLoggedOnce()
    {
        var logged = new List<string>();
        var (bridge, t) = Joined(log: logged.Add);
        t.ToClient.Enqueue(Frame(new LoginMessages.LoginGetWaypoint()));
        t.ToClient.Enqueue(Frame(new LoginMessages.LoginGetWaypoint()));
        t.ToClient.Enqueue(Frame(new LoginMessages.LoginConfirm()));
        Drain(bridge);

        Assert.Equal(2, bridge.Unused["LoginGetWaypoint"]);
        Assert.False(bridge.Unused.ContainsKey("LoginConfirm"));
        Assert.Contains("LoginGetWaypoint", Assert.Single(logged));
    }
}
