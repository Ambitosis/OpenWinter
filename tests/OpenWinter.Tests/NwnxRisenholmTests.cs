using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>NWNX_Risenholm functions that need a player or the network, and NWNX_Tweaks' arithmetic.</summary>
public class NwnxRisenholmTests
{
    sealed class NullConnection : IConnection
    {
        public int Id => 0;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    sealed class Output : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, uint To)> Notes = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, subject?.Id ?? GameObject.InvalidId, player?.Id ?? GameObject.InvalidId));
    }

    sealed record Setup(World World, NwnxRegistry Registry, Output Out, Area Area, Creature Pc, Dictionary<string, string> Env);

    static Setup Make(bool scripts = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        if (scripts) world.EnableScripts();
        world.Register(world.Module);
        world.Factions.Load(rm);
        world.System<PersistenceSystem>().RootOverride = Path.Combine(Path.GetTempPath(), "openwinter-tests-risenholm", Guid.NewGuid().ToString("N"));
        var output = new Output();
        world.Output = output;
        var area = new Area { ResRef = "rh_test", Tag = "rh_test", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        var pc = Player(world, area, "pc", 10);
        var env = new Dictionary<string, string>();
        var reg = world.System<NwnxRegistry>();
        reg.Log = _ => { };
        reg.EnvironmentVariable = name => env.GetValueOrDefault(name);
        return new Setup(world, reg, output, area, pc, env);
    }

    static Creature Player(World world, Area area, string name, float x)
    {
        var pc = new Creature { Tag = name, Faction = FactionTable.PlayerFaction, CurrentHitPoints = 20, MaxHitPoints = 20 };
        pc.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = name };
        world.AddToArea(pc, area, new Vector3(x, 10, 0), 0);
        return pc;
    }

    static NwnxValue? Call(NwnxRegistry r, string function, params NwnxValue[] pushed)
    {
        foreach (var v in pushed) r.Stack.Push(v);
        Assert.True(r.Call("NWNX_Risenholm", function));
        return r.Stack.Count > 0 ? r.Stack.Pop() : null;
    }

    static NwnxValue O(GameObject o) => NwnxValue.Object(o.Id);
    static NwnxValue I(int i) => NwnxValue.Int(i);
    static NwnxValue S(string s) => NwnxValue.String(s);

    [DataFact]
    public void LikeStatusChangesOnlyThePlayerListAndCanBeOneSided()
    {
        var s = Make();
        var other = Player(s.World, s.Area, "other", 14);
        // Pushed as the header does: reciprocal, attitude, target, source.
        Call(s.Registry, "SetPCLikeStatus", I(0), I(0), O(other), O(s.Pc));
        Assert.Contains(other.Id, s.Pc.Dislikes);
        Assert.DoesNotContain(s.Pc.Id, other.Dislikes);
        Call(s.Registry, "SetPCLikeStatus", I(1), I(0), O(other), O(s.Pc));
        Assert.Contains(s.Pc.Id, other.Dislikes);
        Call(s.Registry, "SetPCLikeStatus", I(1), I(1), O(other), O(s.Pc));
        Assert.Empty(s.Pc.Dislikes);
        Assert.Empty(other.Dislikes);
    }

    [DataFact]
    public void ClientFacingCallsReachOnlyThatPlayer()
    {
        var s = Make();
        var npc = new Creature { Tag = "npc", CurrentHitPoints = 5, MaxHitPoints = 5 };
        s.World.AddToArea(npc, s.Area, new Vector3(12, 10, 0), 0);
        Assert.Equal(0, Call(s.Registry, "SetResourceOverride", S("pmh151"), S("pmh0"), I(2002), O(npc))?.Value);
        Assert.Equal(1, Call(s.Registry, "SetResourceOverride", S("pmh151"), S("pmh0"), I(2002), O(s.Pc))?.Value);
        Assert.Contains(s.Out.Notes, n => n.Kind == "resource-override" && n.To == s.Pc.Id);
        Call(s.Registry, "ForceExamineWindow", O(npc), O(s.Pc));
        Assert.Contains(s.Out.Notes, n => n.Kind == "examine" && n.Subject == npc.Id && n.To == s.Pc.Id);
        Call(s.Registry, "StartLevelUp", O(s.Pc));
        Assert.Contains(s.Out.Notes, n => n.Kind == "levelup-open" && n.To == s.Pc.Id);
    }

    [DataFact]
    public void DamageLevelBaseShiftsTheLiveRowsOnly()
    {
        var s = Make();
        Assert.Equal(0, CreatureLabelFeed.DamageLevel(s.World, s.Pc));
        Call(s.Registry, "SetDamageLevelBase", I(30), O(s.Pc));
        Assert.Equal(30, CreatureLabelFeed.DamageLevel(s.World, s.Pc));
        s.Pc.CurrentHitPoints = 4;
        Assert.Equal(34, CreatureLabelFeed.DamageLevel(s.World, s.Pc));
        Call(s.Registry, "SetDamageLevelBase", I(0), O(s.Pc));
        Assert.Equal(4, CreatureLabelFeed.DamageLevel(s.World, s.Pc));
    }

    [DataFact]
    public void ShutdownFileIsFoundOnceAndDeleted()
    {
        var s = Make();
        var root = s.World.System<PersistenceSystem>().UserRoot;
        Directory.CreateDirectory(root);
        Assert.Equal(0, Call(s.Registry, "CheckForShutdownFile")?.Value);
        File.WriteAllText(Path.Combine(root, "shutdown"), "");
        Assert.Equal(1, Call(s.Registry, "CheckForShutdownFile")?.Value);
        Assert.False(File.Exists(Path.Combine(root, "shutdown")));
        Assert.Equal(0, Call(s.Registry, "CheckForShutdownFile")?.Value);
    }

    [DataFact]
    public void ExecuteCommandReturnsWhatTheProgramPrinted()
    {
        var s = Make();
        // Pushed last argument first, the command last.
        var output = OperatingSystem.IsWindows()
            ? Call(s.Registry, "ExecuteCommand", S(""), S(""), S(""), S("echo hi"), S("/c"), S("/d"), S("cmd.exe"))
            : Call(s.Registry, "ExecuteCommand", S(""), S(""), S(""), S(""), S(""), S("hi"), S("/bin/echo"));
        Assert.Equal("hi", (output?.Value as string)?.Trim());
        Assert.Equal("", Call(s.Registry, "ExecuteCommand", S(""), S(""), S(""), S(""), S(""), S(""), S("no-such-program-anywhere"))?.Value);
    }

    [DataFact]
    public void RpcListenerRunsTheNamedScriptAndAnswersWithItsResponse()
    {
        var s = Make(scripts: true);
        var source = "void main() { NWNXCall(\"NWNX_Risenholm\", \"GetRpcRequest\"); string s = NWNXPopString();"
            + " NWNXPushString(\"{\\\"got\\\":\" + s + \"}\"); NWNXCall(\"NWNX_Risenholm\", \"SetRpcResponse\"); }";
        var ncs = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(source, "pw_rpc_echo", s.World.Scripts!.Definitions, _ => null, out var error);
        Assert.True(ncs != null, error);
        s.World.System<PersistenceSystem>().SetTemplate("pw_rpc_echo", ResType.Ncs, ncs);
        var silent = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile("void main() { }", "pw_rpc_mute", s.World.Scripts.Definitions, _ => null, out _);
        s.World.System<PersistenceSystem>().SetTemplate("pw_rpc_mute", ResType.Ncs, silent);

        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        const string Secret = "0123456789abcdef0123";
        s.Env["NWNX_RISENHOLM_RPC_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        s.Env["NWNX_RISENHOLM_RPC_SECRET"] = Secret;
        var rpc = s.World.System<RisenholmRpc>();
        rpc.Step(0);
        try
        {
            Assert.Equal(port, rpc.Port);
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            (int Status, string Body) Post(string path, string body, string? secret)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body) };
                if (secret != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                var task = http.SendAsync(request);
                // The script runs on the game thread, at a server step.
                for (int i = 0; i < 500 && !task.IsCompleted; i++) { s.Registry.Step(0.01f); Thread.Sleep(10); }
                var response = task.Result;
                return ((int)response.StatusCode, response.Content.ReadAsStringAsync().Result);
            }
            Assert.Equal((200, "{\"got\":[1,2]}"), Post("/rpc/echo", "[1,2]", Secret));
            Assert.Equal(401, Post("/rpc/echo", "[1,2]", "wrong-secret-wrong-secret").Status);
            Assert.Equal(401, Post("/rpc/echo", "[1,2]", null).Status);
            Assert.Equal(404, Post("/rpc/nothing", "", Secret).Status);
            Assert.Equal(404, Post("/rpc/Bad-Name", "", Secret).Status);
            Assert.Equal(500, Post("/rpc/mute", "", Secret).Status);
            // Outside a request there is nothing to read.
            Assert.Equal("", Call(s.Registry, "GetRpcRequest")?.Value);
        }
        finally { rpc.Stop(); }
    }

    [Theory]
    [InlineData("0x1A", 26)]
    [InlineData("010", 8)]
    [InlineData("08", 0)]
    [InlineData("-0x10", -16)]
    [InlineData("  0x10", 16)]
    [InlineData("+017", 15)]
    [InlineData("0b11", 3)]
    [InlineData("0x", 0)]
    [InlineData("1e3", 1)]
    [InlineData("0xFFFFFFFF", 0)]
    [InlineData("abc", 0)]
    public void StringToIntWithAutomaticBase(string text, int expected) => Assert.Equal(expected, NwnxTweaks.StringToIntAuto(text));
}
