using System.Diagnostics;
using OpenWinter.Game;
using OpenWinter.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Real localhost UDP, including rejection before a GameServer session can be accepted.</summary>
public class NetworkVersionTests
{
    static void Until(Func<bool> done, Action poll, int milliseconds = 5000)
    {
        var clock = Stopwatch.StartNew();
        while (!done() && clock.ElapsedMilliseconds < milliseconds) { poll(); Thread.Sleep(2); }
        Assert.True(done(), "Timed out waiting for the UDP handshake.");
    }

    static NetDataWriter Handshake(NetworkVersion v)
    {
        var w = new NetDataWriter();
        w.Put(NetworkVersion.HandshakeKey); w.Put(v.Protocol); w.Put(v.Engine); w.Put(v.Build);
        return w;
    }

    [Fact]
    public void BuildHasAnAutomaticallyGeneratedIdentity()
    {
        Assert.True(NetworkVersion.Current.Valid);
        Assert.Equal(64, NetworkVersion.Current.Build.Length);
        Assert.NotEqual(new string('0', 64), NetworkVersion.Current.Build);
    }

    [Fact]
    public void MatchingPeersReleaseQueuedGameplayOnlyAfterHandshake()
    {
        using var server = new NetServerHost(0);
        NetConnection? accepted = null;
        server.Connected += c => accepted = c;
        using var client = new NetClientConnection("127.0.0.1", server.Port);
        var hello = new HelloMessage { PlayerName = "Alice" }.Write();
        client.Send(hello); // queued before any polling or acceptance
        byte[]? received = null;
        Until(() => received != null, () =>
        {
            server.Poll();
            if (accepted != null && accepted.TryReceive(out var message)) accepted.Send(message);
            if (client.TryReceive(out var reply)) received = reply;
        });
        Assert.Equal(hello, received);
        Assert.True(client.IsConnected);
        Assert.Null(client.FailureReason);
    }

    [Fact]
    public void ConnectionsBeyondTheLimitAreRefused()
    {
        using var server = new NetServerHost(0) { MaxConnections = 1 };
        int accepted = 0;
        server.Connected += _ => accepted++;
        using var first = new NetClientConnection("127.0.0.1", server.Port);
        Until(() => first.Joined, () => { server.Poll(); first.TryReceive(out _); });
        using var second = new NetClientConnection("127.0.0.1", server.Port);
        Until(() => second.FailureReason != null, () => { server.Poll(); first.TryReceive(out _); second.TryReceive(out _); });
        Assert.Equal(1, accepted);
        Assert.True(first.IsConnected);
        Assert.False(second.Joined);
    }

    [Fact]
    public void OversizedMessagesEndTheConnection()
    {
        using var server = new NetServerHost(0) { MaxMessageBytes = 1000 };
        NetConnection? accepted = null;
        server.Connected += c => accepted = c;
        using var client = new NetClientConnection("127.0.0.1", server.Port);
        client.Send(new HelloMessage { PlayerName = "Small" }.Write());
        client.Send(new CharacterChoiceMessage { Bic = new byte[5000] }.Write());
        byte[]? first = null;
        Until(() => accepted is { IsConnected: false }, () =>
        {
            server.Poll();
            client.TryReceive(out _);
            if (first == null && accepted != null && accepted.TryReceive(out var m)) first = m;
        });
        if (first == null) accepted!.TryReceive(out first);
        Assert.NotNull(first);
        Assert.Equal((byte)MessageType.Hello, first![0]);
        Assert.False(accepted!.TryReceive(out _));
    }

    [Theory]
    [InlineData("protocol")]
    [InlineData("build")]
    [InlineData("engine")]
    public void MismatchesNeverReachTheGameServer(string difference)
    {
        var current = NetworkVersion.Current;
        var remote = difference switch
        {
            "protocol" => current with { Protocol = current.Protocol + 1 },
            "build" => current with { Build = new string('a', 64) },
            _ => current with { Engine = "99.0.0" },
        };
        using var server = new NetServerHost(0, remote);
        int accepted = 0;
        server.Connected += _ => accepted++;
        using var client = new NetClientConnection("127.0.0.1", server.Port);
        client.Send(new HelloMessage { PlayerName = "Must not log in" }.Write());
        Until(() => client.FailureReason != null, () => { server.Poll(); Assert.False(client.TryReceive(out _)); });
        Assert.False(client.IsConnected);
        Assert.Equal(0, accepted);
        Assert.Contains(current.Display, client.FailureReason);
        Assert.Contains(remote.Display, client.FailureReason);
        Assert.Contains(difference == "protocol" ? "protocol mismatch" : "build mismatch", client.FailureReason);
        client.Send(new HelloMessage().Write()); // no revival/queueing after rejection
        Assert.False(client.TryReceive(out _));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("truncated")]
    [InlineData("oversized")]
    [InlineData("missing-build")]
    public void UnversionedAndMalformedClientsAreRejected(string kind)
    {
        using var server = new NetServerHost(0);
        int accepted = 0;
        server.Connected += _ => accepted++;
        var listener = new EventBasedNetListener();
        var raw = new NetManager(listener) { AutoRecycle = true };
        bool rejected = false;
        listener.PeerDisconnectedEvent += (_, info) => rejected = info.Reason == DisconnectReason.ConnectionRejected;
        var payload = new NetDataWriter();
        if (kind == "legacy") payload.Put("openwinter/1");
        if (kind == "truncated") { payload.Put(NetworkVersion.HandshakeKey); payload.Put((byte)2); }
        if (kind == "oversized") payload.Put(new string('x', 300));
        if (kind == "missing-build") payload = Handshake(NetworkVersion.Current with { Build = "" });
        try
        {
            Assert.True(raw.Start());
            raw.Connect("127.0.0.1", server.Port, payload);
            Until(() => rejected, () => { server.Poll(); raw.PollEvents(); });
            Assert.Equal(0, accepted);
        }
        finally { raw.Stop(); }
    }

    [Fact]
    public void NewClientExplainsRejectionByLegacyServer()
    {
        var listener = new EventBasedNetListener();
        var legacy = new NetManager(listener) { AutoRecycle = true };
        listener.ConnectionRequestEvent += r => r.AcceptIfKey("openwinter/1");
        try
        {
            Assert.True(legacy.Start(0));
            using var client = new NetClientConnection("127.0.0.1", legacy.LocalPort);
            Until(() => client.FailureReason != null, () => { legacy.PollEvents(); client.TryReceive(out _); });
            Assert.False(client.IsConnected);
            Assert.Contains("older OpenWinter build", client.FailureReason);
        }
        finally { legacy.Stop(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientChecksServerIdentityBeforeSendingHello(bool malformed)
    {
        var listener = new EventBasedNetListener();
        var raw = new NetManager(listener) { AutoRecycle = true };
        int gameplay = 0;
        listener.ConnectionRequestEvent += r => r.Accept();
        listener.PeerConnectedEvent += p => p.Send(malformed
            ? new WelcomeMessage { ModuleName = "No handshake" }.Write()
            : Handshake(NetworkVersion.Current with { Build = new string('b', 64) }).CopyData(), DeliveryMethod.ReliableOrdered);
        listener.NetworkReceiveEvent += (_, _, _, _) => gameplay++;
        try
        {
            Assert.True(raw.Start(0));
            using var client = new NetClientConnection("127.0.0.1", raw.LocalPort);
            client.Send(new HelloMessage().Write());
            Until(() => client.FailureReason != null, () => { raw.PollEvents(); Assert.False(client.TryReceive(out _)); });
            Assert.False(client.IsConnected);
            Assert.Equal(0, gameplay);
        }
        finally { raw.Stop(); }
    }

    [Fact]
    public void ServerThatNeverSendsItsIdentityTimesOutWithoutReceivingGameplay()
    {
        var listener = new EventBasedNetListener();
        var raw = new NetManager(listener) { AutoRecycle = true };
        int gameplay = 0;
        listener.ConnectionRequestEvent += r => r.Accept();
        listener.NetworkReceiveEvent += (_, _, _, _) => gameplay++;
        try
        {
            Assert.True(raw.Start(0));
            using var client = new NetClientConnection("127.0.0.1", raw.LocalPort);
            client.Send(new HelloMessage().Write());
            Until(() => client.FailureReason != null, () => { raw.PollEvents(); Assert.False(client.TryReceive(out _)); }, 12000);
            Assert.False(client.IsConnected);
            Assert.Contains("did not complete the version handshake", client.FailureReason);
            Assert.Equal(0, gameplay);
        }
        finally { raw.Stop(); }
    }

    [Fact]
    public void ServerClosingAJoinedConnectionIsADisconnectNotAJoinFailure()
    {
        using var server = new NetServerHost(0);
        NetConnection? accepted = null;
        server.Connected += c => accepted = c;
        using var client = new NetClientConnection("127.0.0.1", server.Port);
        client.Send(new HelloMessage { PlayerName = "Alice" }.Write());
        bool hello = false;
        Until(() => hello, () => { server.Poll(); client.TryReceive(out _); hello |= accepted != null && accepted.TryReceive(out _); });
        Assert.True(client.Joined);
        // A parting message (e.g. a boot notice) that is still unread when the server hangs up.
        var parting = new HelloMessage { PlayerName = "Goodbye" }.Write();
        accepted!.Send(parting);
        Thread.Sleep(200);
        accepted.Disconnect();
        var received = new List<byte[]>();
        Until(() => client.FailureReason != null, () =>
        {
            server.Poll();
            while (client.TryReceive(out var m)) received.Add(m);
        });
        while (client.TryReceive(out var m)) received.Add(m);
        Assert.Equal("The server closed the connection.", client.FailureReason);
        Assert.False(client.IsConnected);
        Assert.Contains(received, m => m.SequenceEqual(parting));
    }

    [Fact]
    public void DisconnectWhileConnectingDoesNotReportConnectedForever()
    {
        using var server = new NetServerHost(0);
        using var client = new NetClientConnection("127.0.0.1", server.Port);
        client.Send(new HelloMessage().Write());
        client.Disconnect();
        Assert.False(client.IsConnected);
        Assert.Null(client.FailureReason); // leaving voluntarily is not an error
        Assert.False(client.TryReceive(out _));
    }

    [DataFact]
    public void TwoMatchingUdpPlayersEnterTheSameWorld()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var game = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        using var server = new NetServerHost(0);
        server.Connected += c => game.Accept(c);
        using var alice = new NetClientConnection("127.0.0.1", server.Port);
        using var bob = new NetClientConnection("127.0.0.1", server.Port);
        alice.Send(new HelloMessage { PlayerName = "Alice" }.Write());
        bob.Send(new HelloMessage { PlayerName = "Bob" }.Write());
        uint a = 0, b = 0;
        var seenA = new HashSet<uint>();
        var seenB = new HashSet<uint>();
        void Drain(NetClientConnection c, ref uint me, HashSet<uint> seen)
        {
            while (c.TryReceive(out var data))
            {
                var r = new MessageReader(data);
                if (r.Type == MessageType.ControlObject) me = ControlObjectMessage.Read(r).ObjectId;
                if (r.Type == MessageType.Spawn) seen.Add(SpawnMessage.Read(r).ObjectId);
            }
        }
        Until(() => a != 0 && b != 0 && seenA.Contains(b) && seenB.Contains(a), () =>
        {
            server.Poll(); game.Tick(1f / 30);
            Drain(alice, ref a, seenA); Drain(bob, ref b, seenB);
        });
        Assert.NotEqual(a, b);
        Assert.Equal(2, game.Sessions.Count());
    }
}
