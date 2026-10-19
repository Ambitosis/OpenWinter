using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>The classic party bar's portrait hover text shows the area's name: the HUD feed carries it.</summary>
public sealed class HudFeedAreaTests
{
    [DataFact]
    public void HudFeedReportsThePlayersAreaName()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        world.Register(world.Module);
        var area = new Area { ResRef = "hudprobe", Width = 4, Height = 4, Name = new LocString("Probe Hall") };
        world.Register(area); world.Areas.Add(area);
        var server = new GameServer(world, new ServerOptions { StartArea = area.ResRef, DefaultAppearance = 1, Vault = VaultMode.Local });
        var pair = LoopbackConnection.CreatePair();
        server.Accept(pair.Server);
        pair.Client.Send(new HelloMessage { PlayerName = "Hud tester" }.Write());
        server.Tick(1f / 30);
        var pc = server.Sessions.Single().Creature!;
        world.Jump(pc, new Location(area.Id, new Vector3(20, 15, 0), 0));
        for (int i = 0; i < 15; i++) server.Tick(1f / 30);

        string? reported = null;
        while (pair.Client.TryReceive(out var bytes))
        {
            var r = new MessageReader(bytes);
            if (r.Type != MessageType.Notify) continue;
            var n = NotifyMessage.Read(r);
            if (n.Kind != "hud") continue;
            using var json = JsonDocument.Parse(n.Json);
            reported = json.RootElement.GetProperty("self").GetProperty("area").GetString();
        }
        Assert.Equal("Probe Hall", reported);
    }
}
