using System.Collections.Concurrent;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>The WSNetLayer login exchange and session housekeeping, against a scripted server.</summary>
public class NwnSessionTests
{
    /// <summary>Test vectors from nwn-message.py's CDKey for a generated key and the challenge bytes 0..15.</summary>
    [Fact]
    public void CdKeyMatchesTheReference()
    {
        var key = NwnCdKey.Parse("JWNJH-YUX3D-CHY3W-KF43Y-YT3TV-UKYX9-WWKVX");
        Assert.Equal("WJYXDH3K", key.PublicKey);
        Assert.Equal("JWNJH-YUX3D-CHY3W-KF43Y-YT3TV-UKYX9-WWKVX", key.FullKey);
        var challenge = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        Assert.Equal("WJYXDH3K56189b97a631af9f0b3d4a5f61d888f3", System.Text.Encoding.ASCII.GetString(key.ChallengeResponse(challenge)));
        Assert.Equal("560ec9449092d367ca5efb706cd4e707",
            System.Text.Encoding.ASCII.GetString(NwnCdKey.Md5Hex([.. "secret"u8, .. challenge])));
        Assert.Throws<FormatException>(() => NwnCdKey.Parse("JWNJH-YUX3D-CHY3W-KF43Y-YT3TV-UKYX9-WWKVU"));

        var generated = NwnCdKey.Generate(new Random(4));
        Assert.Equal(generated.PublicKey, NwnCdKey.Parse(generated.FullKey).PublicKey);
    }

    /// <summary>Both ends of an in-memory link; the test plays the server.</summary>
    sealed class FakeTransport : INwnTransport
    {
        public readonly ConcurrentQueue<byte[]> ToClient = new();
        public readonly List<byte[]> FromClient = [];
        public bool IsOpen { get; set; } = true;
        public string? CloseReason { get; set; }
        public void Send(byte[] frame) => FromClient.Add(frame);
        public bool TryReceive(out byte[] frame) => ToClient.TryDequeue(out frame!);
        public void Close(string reason)
        {
            CloseReason ??= reason;
            IsOpen = false;
        }
        public void Dispose() { }
        public void Reply(NwnPacket packet) => ToClient.Enqueue(packet.ToBytes());
        public NwnPacket LastPacket => NwnPacket.Parse(FromClient[^1])!;
    }

    static NwnSessionContext Context() => new(_ => 0, (_, row, _) => row.ToString());

    static readonly NwnCdKey Key = NwnCdKey.Parse("JWNJH-YUX3D-CHY3W-KF43Y-YT3TV-UKYX9-WWKVX");

    /// <summary>Runs the login up to the server's challenge, checking each request the client makes.</summary>
    static (NwnSession, FakeTransport) ToChallenge(NwnLogin login)
    {
        var t = new FakeTransport();
        var s = new NwnSession(t, login, Context());
        var got = new List<NwnMessage>();
        s.Pump(got);
        Assert.IsType<GnhiPacket>(t.LastPacket);
        t.Reply(new GnhiPacket());
        s.Pump(got);
        Assert.IsType<BnesPacket>(t.LastPacket);
        t.Reply(new BnerPacket(0, "Test Server"));
        s.Pump(got);
        var bnxi = Assert.IsType<BnxiPacket>(t.LastPacket);
        Assert.Equal(login.PlayerName, bnxi.PlayerName);
        Assert.Equal(Key.PublicKey, bnxi.PublicKey);
        Assert.Equal(("8193", "37", "17"), (bnxi.BuildNumber, bnxi.BuildRevision, bnxi.BuildPostfix));
        t.Reply(new BnxrPacket { ModuleName = "Prelude", Elc = true, NwSyncUrl = "http://sync", NwSyncPrimaryManifest = "abc" });
        s.Pump(got);
        Assert.IsType<BndsPacket>(t.LastPacket);
        t.Reply(new BndrPacket("details", "a module", "8193"));
        s.Pump(got);
        var bncs = Assert.IsType<BncsPacket>(t.LastPacket);
        Assert.Equal((8193u, 37, 17), (bncs.VersionNumber, bncs.PatchRevision, bncs.PatchPostfix));
        Assert.Equal(Key.PublicKey, bncs.CdKey);
        Assert.Equal("Test Server", s.SessionInfo!.SessionName);
        Assert.Equal("http://sync", s.Settings!.NwSyncUrl);
        return (s, t);
    }

    [Fact]
    public void LogsInAnsweringTheChallenges()
    {
        var (s, t) = ToChallenge(new NwnLogin("Tester", Key, Password: "pw"));
        var keyChallenge = new byte[] { 1, 2, 3 };
        var gameChallenge = new byte[] { 9, 8 };
        t.Reply(new BncrPacket(BncrPacket.PasswordRequired, NwnAuthResult.Ok, gameChallenge, keyChallenge, [7]));
        s.Pump([]);
        var bnvs = Assert.IsType<BnvsPacket>(t.LastPacket);
        Assert.Equal(Key.ChallengeResponse(keyChallenge), bnvs.CdKeyResponse);
        Assert.Equal(NwnCdKey.Md5Hex([.. "pw"u8, .. gameChallenge]), bnvs.GamePasswordResponse);
        Assert.Equal(NwnCdKey.Md5Hex([7]), bnvs.MasterPasswordResponse);
        // The private part of the key never goes out as text.
        Assert.DoesNotContain(t.FromClient, f => System.Text.Encoding.ASCII.GetString(f).Contains(Key.FullKey.Replace("-", "")[..20]));

        t.Reply(new BnvrPacket(true, NwnAuthResult.Ok, 1, 8193, 37, 17));
        s.Pump([]);
        Assert.Equal(NwnSessionState.Ready, s.State);
        Assert.Equal(NwnVersion.Supported, s.ServerVersion);
    }

    [Fact]
    public void RefusalsAndOtherVersionsFailTheLogin()
    {
        var (s, t) = ToChallenge(new NwnLogin("Tester", Key));
        t.Reply(new BncrPacket(BncrPacket.Rejected, NwnAuthResult.Banned, [], [], []));
        s.Pump([]);
        Assert.Equal(NwnSessionState.Failed, s.State);
        Assert.Equal(NwnAuthResult.Banned, s.Rejection);

        (s, t) = ToChallenge(new NwnLogin("Tester", Key));
        t.Reply(new BncrPacket(BncrPacket.PasswordRequired, NwnAuthResult.Ok, [1], [2], [3]));
        s.Pump([]);
        Assert.Equal(NwnSessionState.Failed, s.State);
        Assert.Contains("password", s.Failure);

        (s, t) = ToChallenge(new NwnLogin("Tester", Key));
        t.Reply(new BncrPacket(BncrPacket.Accepted, NwnAuthResult.Ok, [], [2], [3]));
        s.Pump([]);
        t.Reply(new BnvrPacket(true, NwnAuthResult.Ok, 1, 8193, 37, 18));
        s.Pump([]);
        Assert.Equal(NwnSessionState.Failed, s.State);
        Assert.Contains("8193.37.18", s.Failure);
    }

    [Fact]
    public void AnswersKeepAlivesAndEchoesAndDecodesMessages()
    {
        var (s, t) = ToChallenge(new NwnLogin("Tester", Key));
        t.Reply(new BncrPacket(BncrPacket.Accepted, NwnAuthResult.Ok, [], [2], [3]));
        s.Pump([]);
        t.Reply(new BnvrPacket(true, NwnAuthResult.Ok, 1, 8193, 37, 17));
        s.Pump([]);

        t.Reply(new BnlmPacket(5121, 4, 77));
        s.Pump([]);
        Assert.Equal(new BnlrPacket(5121, 4, 77), t.LastPacket);

        var w = new NwnWsWriter(s.Context);
        new DeviceMessages.DeviceEchoRequest { Data = [5, 6] }.Write(w);
        t.ToClient.Enqueue([(byte)'P', 0x36, 0x02, .. w.ToArray()]);
        var got = new List<NwnMessage>();
        s.Pump(got);
        Assert.IsType<DeviceMessages.DeviceEchoRequest>(Assert.Single(got));
        var reply = t.FromClient[^1];
        Assert.Equal(new byte[] { (byte)'p', 0x36, 0x03 }, reply[..3]);
        var echoed = new DeviceMessages.DeviceEchoResponse();
        echoed.Read(new NwnWsReader(reply.AsMemory(3), s.Context));
        Assert.Equal(new byte[] { 5, 6 }, echoed.Data);

        t.ToClient.Enqueue([(byte)'P', 0x7E, 0x7E]);
        s.Pump(got);
        Assert.Contains("unknown message", Assert.Single(s.DecodeErrors).Error);
        Assert.Equal(NwnSessionState.Ready, s.State);
    }

    [Fact]
    public void APumpStopsAfterTheModuleRunningMessageSoItsHaksApplyToTheRest()
    {
        var (s, t) = ToChallenge(new NwnLogin("Tester", Key));
        t.Reply(new BncrPacket(BncrPacket.Accepted, NwnAuthResult.Ok, [], [2], [3]));
        s.Pump([]);
        t.Reply(new BnvrPacket(true, NwnAuthResult.Ok, 1, 8193, 37, 17));
        s.Pump([]);

        byte[] Frame(NwnMessage m)
        {
            var w = new NwnWsWriter(s.Context);
            m.Write(w);
            return [(byte)'P', m.Major, m.Minor, .. w.ToArray()];
        }
        t.ToClient.Enqueue(Frame(new ServerStatusMessages.ServerStatusModuleRunning()));
        t.ToClient.Enqueue(Frame(new DeviceMessages.DeviceEchoRequest { Data = [1] }));
        var got = new List<NwnMessage>();
        s.Pump(got);
        Assert.IsType<ServerStatusMessages.ServerStatusModuleRunning>(Assert.Single(got));
        s.Pump(got);
        Assert.IsType<DeviceMessages.DeviceEchoRequest>(got[1]);
    }

    [Fact]
    public void ProtocolFlagsAreAddedToTheUri()
    {
        Assert.Equal("ws://localhost:5121/?proto_flags=0x100", NwnWebSocketTransport.WithProtocolFlags(new Uri("ws://localhost:5121")).ToString());
        Assert.Equal("wss://pw.example/nwn?a=1&proto_flags=0x100",
            NwnWebSocketTransport.WithProtocolFlags(new Uri("wss://pw.example/nwn?a=1")).ToString());
    }
}
