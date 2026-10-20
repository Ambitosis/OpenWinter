using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>ActivatePortal (docs/CLASSIC_UI.md "Portal prompt"): the server-info answer a portal prompt checks for, and
/// arriving at the portal's waypoint.</summary>
public sealed class PortalTests
{
    static (World World, GameServer Server) NewServer(ServerOptions options)
    {
        var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources); world.Register(world.Module); world.EnableScripts();
        var area = new Area { ResRef = "mp", Width = 8, Height = 8 };
        world.Register(area); world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(80, 0, 0), new(80, 80, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(80, 80, 0), new(0, 80, 0), 4, true);
        options.StartArea = "mp";
        options.Vault = VaultMode.Local;
        return (world, new GameServer(world, options));
    }

    static List<NotifyMessage> Notes(LoopbackConnection c)
    {
        var notes = new List<NotifyMessage>();
        while (c.TryReceive(out var data))
        {
            var r = new MessageReader(data);
            if (r.Type == MessageType.Notify) notes.Add(NotifyMessage.Read(r));
        }
        return notes;
    }

    [DataFact]
    public void ServerInfoAnswersAConnectionThatHasNotLoggedIn()
    {
        var (_, server) = NewServer(new ServerOptions { ServerName = "Test Server", MaxPlayers = 8 });
        // One player in the game.
        var player = LoopbackConnection.CreatePair();
        server.Accept(player.Server);
        player.Client.Send(new HelloMessage { PlayerName = "A" }.Write());
        server.Tick(.01f);

        // A portal prompt's check: no Hello, only the request.
        var probe = LoopbackConnection.CreatePair();
        server.Accept(probe.Server);
        probe.Client.Send(new ClientRequestMessage { Kind = "server-info" }.Write());
        server.Tick(.01f);

        var info = Assert.Single(Notes(probe.Client), n => n.Kind == "server-info");
        var d = JsonDocument.Parse(info.Json).RootElement;
        Assert.Equal("Test Server", d.GetProperty("name").GetString());
        Assert.Equal(1, d.GetProperty("players").GetInt32());
        Assert.Equal(8, d.GetProperty("maxPlayers").GetInt32());
        Assert.False(d.GetProperty("password").GetBoolean());
        Assert.True(d.GetProperty("localCharacters").GetBoolean()); // this test server is a local vault
        Assert.Equal(1, d.GetProperty("minLevel").GetInt32());
        Assert.Equal(40, d.GetProperty("maxLevel").GetInt32());
        // Asking did not log anyone in.
        Assert.Single(server.Sessions, s => s.Creature != null);
    }

    [DataTheory]
    [InlineData("WP_Portal", true)]
    [InlineData("wp_portal", false)] // the tag is case sensitive
    [InlineData("", false)]
    public void APortalsWaypointPlacesTheArrivingPlayer(string waypoint, bool there)
    {
        var (world, server) = NewServer(new ServerOptions());
        var area = world.Areas.Single();
        world.AddToArea(new Waypoint { Tag = "WP_Portal" }, area, new System.Numerics.Vector3(30, 30, 0), 0);
        var c = LoopbackConnection.CreatePair();
        server.Accept(c.Server);
        c.Client.Send(new HelloMessage { PlayerName = "A", PortalWaypoint = waypoint }.Write());
        server.Tick(.01f);
        var pc = Assert.Single(server.Sessions, s => s.Creature != null).Creature!;
        Assert.Equal(there, System.Numerics.Vector2.Distance(new(pc.Position.X, pc.Position.Y), new(30, 30)) < 0.5f);
    }
}
