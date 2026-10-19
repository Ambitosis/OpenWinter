using System.Diagnostics;
using System.Numerics;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class ServerTests
{
    readonly ITestOutputHelper _out;
    public ServerTests(ITestOutputHelper output) { _out = output; }

    [DataFact]
    public void LoadsWholeModuleAndPlayerCanWalk()
    {
        var sw = Stopwatch.StartNew();
        var world = new World(TestData.Module);
        world.LoadModule();
        _out.WriteLine($"module loaded: {world.Areas.Count} areas, {world.AllObjects.Count()} objects in {sw.ElapsedMilliseconds} ms");
        Assert.True(world.Areas.Count >= 30);

        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);

        var types = new List<MessageType>();
        uint controlled = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            types.Add(r.Type);
            if (r.Type == MessageType.ControlObject) controlled = ControlObjectMessage.Read(r).ObjectId;
        }
        Assert.Equal(MessageType.Welcome, types[0]);
        Assert.Equal(MessageType.LoadArea, types[1]);
        Assert.True(types.Count(t => t == MessageType.Spawn) > 600);
        Assert.NotEqual(0u, controlled);

        var me = world.Get<Creature>(controlled)!;
        var start = me.Position;
        client.Send(new MoveInputMessage { Direction = new Vector3(1, 0, 0) }.Write());
        for (int i = 0; i < 60; i++) server.Tick(1f / 30);
        _out.WriteLine($"moved from {start} to {me.Position}, speed {me.Speed}");
        Assert.True(Vector3.Distance(start, me.Position) > 1f);
    }

    [DataFact]
    public void GodDevCommandKeepsThePlayerAlive()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DevCommands = true });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint controlled = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.ControlObject) controlled = ControlObjectMessage.Read(r).ObjectId;
        }
        var me = world.Get<Creature>(controlled)!;
        me.CurrentHitPoints = 1;
        client.Send(new ChatSendMessage { Text = "/god" }.Write());
        server.Tick(1f / 30);
        Assert.True(me.IsImmortal);
        Assert.Equal(OpenWinter.Game.Rules.CreatureRules.MaxHitPoints(world, me), me.CurrentHitPoints);
        client.Send(new ChatSendMessage { Text = "/kill" }.Write());
        server.Tick(1f / 30);
        Assert.False(me.IsDead);
        client.Send(new ChatSendMessage { Text = "/god off" }.Write());
        server.Tick(1f / 30);
        Assert.False(me.IsImmortal);
    }
}
