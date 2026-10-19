using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

public class PathfindingTests
{
    [DataFact]
    public void RouteToADoorEndsBesideItWhenItsCellIsOnTheFarSide()
    {
        // Found by the playtest autopilot (Chapter 1, City Core: Apse): the door back to the Hall of Justice sits
        // at the edge of the area, so its nearest open ground is the strip behind it, which can't be reached from
        // inside. The search failed outright and walking up to the door got stuck against the walls.
        var module = Path.Combine(TestData.Config!.GameRoot, "data", "nwm", "Chapter1.nwm");
        if (!File.Exists(module)) return;
        var world = new World(ResMan.ForModule(TestData.Paths, module));
        world.LoadModule();
        var door = world.ByTag("M1Q1E_M1Q1F").OfType<Door>().Single();
        var area = door.Area!;
        // Where a player lands coming through the linked door, and the middle of the room.
        var landing = TransitionService.Destination(world, door).Position;
        foreach (var from in new[] { landing, new Vector3(area.Width * OpenWinter.Game.Areas.Area.TileSize / 2, area.Height * OpenWinter.Game.Areas.Area.TileSize / 2, 0) })
        {
            var path = area.Pathfinder.FindPath(from, door.Position);
            Assert.NotNull(path);
            var end = path![^1];
            Assert.True(Vector2.Distance(new Vector2(end.X, end.Y), new Vector2(door.Position.X, door.Position.Y)) < OpenWinter.Game.Areas.Pathfinder.NearEnough);
        }
    }

    [DataFact]
    public void PlayerSlidesRoundAnUnwalkableSliverOnTheRoute()
    {
        // Found by the playtest autopilot (Prelude, Training Halls): after chasing a caged training dummy the player
        // stopped beside the tip of a thin unwalkable triangle that the route between two open grid cells grazed,
        // and every move from there was blocked for the rest of the run.
        var module = Path.Combine(TestData.Config!.GameRoot, "data", "nwm", "Prelude.nwm");
        if (!File.Exists(module)) return;
        var world = new World(ResMan.ForModule(TestData.Paths, module));
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "m0q1b" });
        var (client, serverEnd) = OpenWinter.Net.LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new OpenWinter.Net.HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new OpenWinter.Net.MessageReader(m);
            if (r.Type == OpenWinter.Net.MessageType.ControlObject) me = OpenWinter.Net.ControlObjectMessage.Read(r).ObjectId;
        }
        var pc = world.Get<Creature>(me)!;
        world.Jump(pc, new Location(pc.Area!.Id, new Vector3(48.8f, 58.2f, 0), 225));
        var start = pc.Position;
        var jaroo = world.ByTag("M1Q0BJaroo").First();
        client.Send(new OpenWinter.Net.ActionRequestMessage { Action = OpenWinter.Net.ActionKind.Talk, Target = jaroo.Id }.Write());
        for (int i = 0; i < 120; i++) { server.Tick(1f / 30); while (client.TryReceive(out _)) { } }
        Assert.True(Vector3.Distance(start, pc.Position) > 3f, $"stuck at {pc.Position}");
    }

    [DataFact]
    public void PlayerInsideAPlaceableFootprintCanWalkOut()
    {
        // Found by the playtest autopilot (Prelude, Graduation Chamber): the player ended up standing inside a desk's
        // footprint; every step from there counted as blocked, so the way back to the hallway was never reachable.
        var module = Path.Combine(TestData.Config!.GameRoot, "data", "nwm", "Prelude.nwm");
        if (!File.Exists(module)) return;
        var world = new World(ResMan.ForModule(TestData.Paths, module));
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "m0q1c" });
        var (client, serverEnd) = OpenWinter.Net.LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new OpenWinter.Net.HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new OpenWinter.Net.MessageReader(m);
            if (r.Type == OpenWinter.Net.MessageType.ControlObject) me = OpenWinter.Net.ControlObjectMessage.Read(r).ObjectId;
        }
        var pc = world.Get<Creature>(me)!;
        var area = pc.Area!;
        var spot = new Vector3(32.1f, 32.8f, 0);
        Assert.Null(area.Walkmesh.GroundHeight(spot.X, spot.Y, 0, 0.9f)); // inside a footprint
        // Put it there directly (a jump moves creatures out of footprints; landing spots and scripts may not).
        pc.Position = spot;
        var open = new Vector3(36f, 32.8f, 0);
        Assert.NotNull(area.Walkmesh.GroundHeight(open.X, open.Y, 0, 0.9f));
        client.Send(new OpenWinter.Net.ActionRequestMessage { Action = OpenWinter.Net.ActionKind.MoveTo, Position = open }.Write());
        for (int i = 0; i < 60; i++) { server.Tick(1f / 30); while (client.TryReceive(out _)) { } }
        Assert.True(Vector3.Distance(spot, pc.Position) > 2f, $"stuck at {pc.Position}");
    }

    [DataFact]
    public void PlayerOffTheWalkmeshStepsBackOntoIt()
    {
        // Found by the playtest autopilot (Chapter 1, the Moonstone Mask): the player stood at (9.8, 24.4), where no
        // walkable ground is left (a doorway), and every step was refused; 41 orders failed in a row.
        var module = Path.Combine(TestData.Config!.GameRoot, "data", "nwm", "Chapter1.nwm");
        if (!File.Exists(module)) return;
        var world = new World(ResMan.ForModule(TestData.Paths, module));
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "map_m1s2a" });
        var (client, serverEnd) = OpenWinter.Net.LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new OpenWinter.Net.HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new OpenWinter.Net.MessageReader(m);
            if (r.Type == OpenWinter.Net.MessageType.ControlObject) me = OpenWinter.Net.ControlObjectMessage.Read(r).ObjectId;
        }
        var pc = world.Get<Creature>(me)!;
        var area = pc.Area!;
        var spot = new Vector3(9.8f, 24.4f, 0);
        Assert.Null(area.Walkmesh.HighestGround(spot.X, spot.Y));
        world.Jump(pc, new Location(area.Id, spot, 0));
        var barkeep = world.ByTag("M1S2ABarkeep").First();
        client.Send(new OpenWinter.Net.ActionRequestMessage { Action = OpenWinter.Net.ActionKind.MoveTo, Position = barkeep.Position }.Write());
        for (int i = 0; i < 60; i++) { server.Tick(1f / 30); while (client.TryReceive(out _)) { } }
        Assert.True(Vector3.Distance(spot, pc.Position) > 3f, $"stuck at {pc.Position}");
    }
}
