using System.Numerics;
using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Society;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class CreatureVisibilityTests
{
    static (World World, Area Area) Setup()
    {
        var world = new World(TestData.Module);
        var area = new Area(); world.Register(area); world.Areas.Add(area);
        return (world, area);
    }
    static Creature Add(World world, Area area, Vector3 position, int faction = 1)
    {
        var c = new Creature { RulesInitialised = true, CurrentHitPoints = 10, MaxHitPoints = 10, Faction = faction };
        world.AddToArea(c, area, position, 0, exact: true, enterEvent: false);
        return c;
    }
    static IConnection Connect(GameServer server, Creature pc)
    {
        var (client, endpoint) = LoopbackConnection.CreatePair();
        pc.Player = server.Accept(endpoint);
        pc.Player.Creature = pc; pc.Player.Area = pc.Area;
        return client;
    }

    [DataFact]
    public void PlayersInitializePromptlyWhileNpcScheduleStaysFourSeconds()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, default, 0);
        var npc = Add(world, area, new(2, 0, 0));
        Connect(new GameServer(world), pc);
        world.Step(.05f);
        Assert.Contains(npc.Id, pc.Society.Seen);
        Assert.Equal(area.Id, pc.Society.PerceptionArea);
        Assert.Empty(npc.Society.Seen);
        for (int i = 0; i < 38; i++) world.Step(.1f);
        Assert.Empty(npc.Society.Seen);
        world.Step(.3f);
        Assert.Contains(pc.Id, npc.Society.Seen);
        npc.Position = new(200, 0, 0);
        world.Step(PerceptionSystem.PlayerInterval + .01f);
        Assert.False(pc.Society.Seen.Contains(npc.Id), $"pc={pc.Position} npc={npc.Position} range={pc.Society.SightRange} now={world.Time} next={pc.Society.NextPerception}");
        Assert.Contains(pc.Id, npc.Society.Seen);
    }

    [DataFact]
    public void PlayerSightBoundaryDoesNotRetainPreviouslyHeardTarget()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, default, 0);
        var target = Add(world, area, new(19, 0, 0));
        Connect(new GameServer(world), pc);
        // Pin the installed base row used by the native fixture: TestData's module can override it.
        pc.Society.RangesRow = 12; pc.Society.SightRange = 35; pc.Society.HearingRange = 20;
        var perception = world.System<PerceptionSystem>();
        perception.Update(pc);
        Assert.Contains(target.Id, pc.Society.Heard);
        target.Position = new(21, 0, 0); perception.Update(pc);
        Assert.Contains(target.Id, pc.Society.Seen);
        Assert.Contains(target.Id, pc.Society.Heard);
        target.Position = new(35, 0, 0); perception.Update(pc);
        Assert.DoesNotContain(target.Id, pc.Society.Seen);
        Assert.DoesNotContain(target.Id, pc.Society.Heard);
    }

    [DataFact]
    public void StationaryPlayerDoorChangesDoNotUseNpcSightCache()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, new(0, 0, 0), 0);
        var target = Add(world, area, new(6, 0, 0));
        Connect(new GameServer(world), pc);
        var door = new Door(); world.AddToArea(door, area, new(3, 0, 0), 0, exact: true, enterEvent: false);
        area.Walkmesh.Add(new(3, -3, 0), new(3, 3, 0), new(3, 3, 4), 2, false, (int)door.Id);
        area.Walkmesh.Add(new(3, -3, 0), new(3, 3, 4), new(3, -3, 4), 2, false, (int)door.Id);
        var perception = world.System<PerceptionSystem>();
        perception.Update(target); // primes symmetric NPC cache with the closed door
        world.Step(.05f);
        Assert.DoesNotContain(target.Id, pc.Society.Seen);
        Assert.Contains(target.Id, pc.Society.Heard);
        Assert.Equal(1, perception.PlayerSightQueries);
        door.IsOpen = true;
        world.Step(.11f);
        Assert.Contains(target.Id, pc.Society.Seen);
        Assert.Equal(2, perception.PlayerSightQueries);
        door.IsOpen = false;
        world.Step(.11f);
        Assert.DoesNotContain(target.Id, pc.Society.Seen);
        Assert.Contains(target.Id, pc.Society.Heard);
        Assert.Equal(3, perception.PlayerSightQueries);
    }

    [DataFact]
    public void StationaryPlayerReusesSightAcrossUpdatesAndCachePruning()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, default, 0);
        var target = Add(world, area, new(6, 0, 0));
        Connect(new GameServer(world), pc);
        var perception = world.System<PerceptionSystem>();
        for (int i = 0; i < 120; i++)
        {
            world.MarkChanged(target); // non-geometric actor updates must not flush sight
            world.Step(.11f);
            Assert.Contains(target.Id, pc.Society.Seen);
        }
        Assert.Equal(1, perception.PlayerSightQueries);
        pc.Position += new Vector3(.01f, 0, 0);
        world.Step(.11f);
        Assert.Equal(2, perception.PlayerSightQueries);
        target.Position += new Vector3(.01f, 0, 0);
        world.Step(.11f);
        Assert.Equal(3, perception.PlayerSightQueries);
    }

    [DataFact]
    public void PlayerSightInvalidatesGeometryEvenWhenTriangleCountIsUnchanged()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, default, 0);
        var target = Add(world, area, new(6, 0, 0));
        Connect(new GameServer(world), pc);
        var perception = world.System<PerceptionSystem>();
        void Wall(float y)
        {
            area.Walkmesh.Clear();
            area.Walkmesh.Add(new(3, y - 3, 0), new(3, y + 3, 0), new(3, y + 3, 4), 2, false);
            area.Walkmesh.Add(new(3, y - 3, 0), new(3, y + 3, 4), new(3, y - 3, 4), 2, false);
        }
        Wall(20);
        perception.Update(pc);
        Assert.Contains(target.Id, pc.Society.Seen);
        Wall(0);
        perception.Update(pc);
        Assert.DoesNotContain(target.Id, pc.Society.Seen);
        Wall(20);
        perception.Update(pc);
        Assert.Contains(target.Id, pc.Society.Seen);
        Assert.Equal(3, perception.PlayerSightQueries);
    }

    [DataFact]
    public void PlayerSightInvalidatesBlockerLifecycleAndAreaButNotOtherAreas()
    {
        var (world, area) = Setup();
        var other = new Area(); world.Register(other); world.Areas.Add(other);
        var pc = Add(world, area, default, 0);
        var target = Add(world, area, new(6, 0, 0));
        Connect(new GameServer(world), pc);
        var perception = world.System<PerceptionSystem>();
        perception.Update(pc);
        var door = new Door();
        world.AddToArea(door, other, new(3, 0, 0), 0, exact: true, enterEvent: false);
        door.IsOpen = true; world.MarkChanged(door);
        perception.Update(pc);
        Assert.Equal(1, perception.PlayerSightQueries);
        world.AddToArea(door, area, door.Position, 0, exact: true, enterEvent: false);
        area.Walkmesh.Add(new(3, -3, 0), new(3, 3, 0), new(3, 3, 4), 2, false, (int)door.Id);
        area.Walkmesh.Add(new(3, -3, 0), new(3, 3, 4), new(3, -3, 4), 2, false, (int)door.Id);
        door.IsOpen = false;
        perception.Update(pc);
        Assert.DoesNotContain(target.Id, pc.Society.Seen);
        world.Destroy(door);
        perception.Update(pc);
        Assert.Contains(target.Id, pc.Society.Seen);
        Assert.Equal(3, perception.PlayerSightQueries);

        var placeable = new Placeable();
        world.AddToArea(placeable, area, new(3, 0, 0), 0, exact: true, enterEvent: false);
        perception.Update(pc);
        placeable.Position += Vector3.UnitY; world.MarkChanged(placeable);
        perception.Update(pc);
        world.AddToArea(placeable, other, placeable.Position, 0, exact: true, enterEvent: false);
        perception.Update(pc); // invalidate its previous area too
        Assert.Equal(6, perception.PlayerSightQueries);

        // Both endpoints retain their coordinates, but a different area's geometry applies.
        other.Walkmesh.Add(new(3, -3, 0), new(3, 3, 0), new(3, 3, 4), 2, false);
        other.Walkmesh.Add(new(3, -3, 0), new(3, 3, 4), new(3, -3, 4), 2, false);
        world.AddToArea(pc, other, pc.Position, 0, exact: true, enterEvent: false);
        world.AddToArea(target, other, target.Position, 0, exact: true, enterEvent: false);
        perception.Update(pc);
        Assert.DoesNotContain(target.Id, pc.Society.Seen);
        Assert.Equal(7, perception.PlayerSightQueries);
    }

    [DataFact]
    public void SameAreaReentryResendsIdenticalPresentationForRecreatedViews()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, default, 0);
        var npc = Add(world, area, new(2, 0, 0));
        var server = new GameServer(world);
        var client = Connect(server, pc);
        pc.Player!.Known.Add(npc.Id);
        var feed = world.System<CreatureVisibilityFeed>();
        var perception = world.System<PerceptionSystem>();
        perception.Update(pc); feed.Step(.11f);
        double scheduled = pc.Society.NextPerception = world.Time + .07;
        while (client.TryReceive(out _)) { }
        server.EnterArea(pc.Player, area, pc.Position, 0, alreadyPlaced: true);
        Assert.Equal(area.Id, pc.Society.PerceptionArea);
        Assert.Equal(scheduled, pc.Society.NextPerception);
        while (client.TryReceive(out _)) { }
        perception.Update(pc); feed.Step(.11f);
        Assert.True(client.TryReceive(out var bytes));
        var message = NotifyMessage.Read(new MessageReader(bytes));
        Assert.Equal("creature-visibility", message.Kind);
        using var doc = JsonDocument.Parse(message.Json);
        Assert.True(doc.RootElement.GetProperty("reset").GetBoolean());
        Assert.True(doc.RootElement.GetProperty(npc.Id.ToString()).GetProperty("seen").GetBoolean());
    }

    [DataFact]
    public void FeedIsPrivateInitializedDeltaAndClearsUnmeasuredCategories()
    {
        var (world, area) = Setup();
        var pc = Add(world, area, default, 0);
        var other = Add(world, area, new(200, 0, 0), 0);
        var npc = Add(world, area, new(2, 0, 0));
        var unknown = Add(world, area, new(3, 0, 0));
        var server = new GameServer(world);
        var client = Connect(server, pc);
        var otherClient = Connect(server, other);
        pc.Player!.Known.UnionWith([pc.Id, npc.Id]);
        other.Player!.Known.Add(npc.Id);
        var feed = new CreatureVisibilityFeed(world);
        JsonElement Read(IConnection connection)
        {
            Assert.True(connection.TryReceive(out var bytes));
            var note = NotifyMessage.Read(new MessageReader(bytes));
            Assert.Equal("creature-visibility", note.Kind);
            return JsonDocument.Parse(note.Json).RootElement;
        }
        feed.Step(.11f);
        var initial = Read(client);
        Assert.True(initial.GetProperty("reset").GetBoolean());
        Assert.Equal(JsonValueKind.Null, initial.GetProperty(npc.Id.ToString()).ValueKind);
        Read(otherClient);
        world.System<PerceptionSystem>().Update(pc);
        world.System<PerceptionSystem>().Update(other);
        feed.Step(.11f);
        var near = Read(client);
        Assert.True(near.GetProperty(npc.Id.ToString()).GetProperty("seen").GetBoolean());
        Assert.False(near.TryGetProperty(unknown.Id.ToString(), out _));
        Assert.False(Read(otherClient).GetProperty(npc.Id.ToString()).GetProperty("seen").GetBoolean(), $"other={other.Position} npc={npc.Position} range={other.Society.SightRange}");
        feed.Step(.11f); Assert.False(client.TryReceive(out _)); Assert.False(otherClient.TryReceive(out _));
        npc.IsDead = true; feed.Step(.11f);
        Assert.Equal(JsonValueKind.Null, Read(client).GetProperty(npc.Id.ToString()).ValueKind);
        Read(otherClient);
        npc.IsDead = false; npc.Master = pc.Id;
        Assert.Null(CreatureVisibilityFeed.Read(world, pc, npc));
        npc.Master = GameObject.InvalidId;
        pc.InCutscene = true;
        Assert.Null(CreatureVisibilityFeed.Read(world, pc, npc));
        pc.InCutscene = false;
        var second = new Area(); world.Register(second);
        world.AddToArea(pc, second, default, 0, exact: true, enterEvent: false);
        feed.Step(.11f);
        Assert.True(Read(client).GetProperty("reset").GetBoolean());
        Assert.Null(CreatureVisibilityFeed.Read(world, pc, npc));
    }
}
