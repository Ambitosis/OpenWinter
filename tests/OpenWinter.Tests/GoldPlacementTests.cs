using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class GoldPlacementTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame();
            World = new World(resources);
            World.Register(World.Module);
            Area = new Area { ResRef = "goldfloor", Width = 4, Height = 4 };
            World.Register(Area);
            World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = Area.ResRef, DefaultAppearance = 6, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair();
            Client = pair.Client;
            Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Gold tester" }.Write());
            Tick();
            Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(Area.Id, new Vector3(20, 15, 0), 90));
            Pc.Gold = 198;
        }
        public void Tick(int frames = 1) { for (int i = 0; i < frames; i++) Server.Tick(1f / 30); }
        public void Drop(Vector3 point, uint? area = null, int count = 50) => Send(JsonSerializer.Serialize(new
        { count, area = area ?? Area.Id, position = new[] { point.X, point.Y, point.Z } }));
        public void Send(string json)
        {
            Client.Send(new ClientRequestMessage { Kind = "drop-gold", Json = json }.Write());
            Tick();
        }
        public List<NotifyMessage> Notes()
        {
            var notes = new List<NotifyMessage>();
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type == MessageType.Notify) notes.Add(NotifyMessage.Read(reader));
            }
            return notes;
        }
        public Item[] Piles => Area.Objects.OfType<Item>().Where(i => i.BaseItem == 76).ToArray();
    }

    [DataFact]
    public void AuthenticatedDropRunsToPointAndSpendsOnlyOnCompletion()
    {
        var g = new Game();
        var destination = new Vector3(27, 15, 0);
        g.Drop(destination);
        Assert.IsType<DropGoldAction>(Assert.Single(g.Pc.Actions));
        g.Tick(15);
        Assert.Equal(198, g.Pc.Gold);
        Assert.Empty(g.Piles);
        Assert.True(g.Pc.Position.X > 20);
        Assert.False(g.Pc.WalkRequested);
        g.Tick(150);
        var pile = Assert.Single(g.Piles);
        Assert.Equal(50, pile.StackSize);
        Assert.Equal(0, pile.FacingDegrees);
        Assert.Equal(148, g.Pc.Gold);
        Assert.InRange(Vector3.Distance(pile.Position, destination), 0, 0.01f);
        Assert.InRange(Vector3.Distance(g.Pc.Position, destination), 0.99f, 1.01f);
    }

    [DataFact]
    public void NearbyDropFacesThePointWithoutWalkingAndWaitsForTheGoldGesture()
    {
        var g = new Game();
        var start = g.Pc.Position;
        var destination = start + new Vector3(0.3873f, 0.2693f, 0);
        g.Drop(destination);
        g.Tick(40);
        Assert.Equal(start, g.Pc.Position);
        Assert.InRange(g.Pc.FacingDegrees, 34, 36);
        Assert.Equal(198, g.Pc.Gold);
        Assert.Empty(g.Piles);
        g.Tick(8);
        Assert.Equal(148, g.Pc.Gold);
        Assert.Equal(destination, Assert.Single(g.Piles).Position);
    }

    [DataFact]
    public void CancellationStopsApproachWithoutSpendingOrCreatingGold()
    {
        var g = new Game();
        g.Drop(new Vector3(35, 15, 0));
        g.Tick(10);
        ActionQueues.Clear(g.World, g.Pc);
        var stopped = g.Pc.Position;
        g.Tick(150);
        Assert.Equal(198, g.Pc.Gold);
        Assert.Empty(g.Piles);
        Assert.InRange(Vector3.Distance(stopped, g.Pc.Position), 0, 0.01f);
    }

    [DataFact]
    public void DropRevalidatesBalanceAndOriginalAreaWhileApproaching()
    {
        foreach (bool changeArea in new[] { false, true })
        {
            var g = new Game();
            g.Drop(new Vector3(35, 15, 0));
            g.Tick(10);
            if (changeArea)
            {
                var area = new Area { ResRef = "other", Width = 4, Height = 4 };
                g.World.Register(area);
                g.World.Areas.Add(area);
                g.World.Jump(g.Pc, new Location(area.Id, new Vector3(10, 10, 0), 0));
            }
            else g.Pc.Gold = 10;
            g.Tick(150);
            Assert.Equal(changeArea ? 198 : 10, g.Pc.Gold);
            Assert.Empty(g.Piles);
            Assert.DoesNotContain(g.World.AllObjects.OfType<Item>(), i => i.BaseItem == 76);
            Assert.Equal(Vector3.Zero, g.Pc.MoveDirection);
        }
    }

    [DataFact]
    public void InvalidAndStaleDestinationsNeverQueueGoldSpending()
    {
        var g = new Game();
        g.Drop(new Vector3(25, 15, 0), g.Area.Id + 1);
        g.Drop(new Vector3(100, 100, 0));
        foreach (string position in new[] { "[]", "[1,2]", "[1,2,3,4]", "[1,2,1e50]", "[1,2,\"bad\"]", "null" })
            g.Send($"{{\"count\":50,\"area\":{g.Area.Id},\"position\":{position}}}");
        Assert.Empty(g.Pc.Actions);
        g.Tick(150);
        Assert.Equal(198, g.Pc.Gold);
        Assert.Empty(g.Piles);
    }

    [DataFact]
    public void DropShowsTheMarkerBendsAndPlaysTheCoinSoundAsAnItemDrop()
    {
        var g = new Game();
        g.Notes();
        g.Drop(new Vector3(27, 15, 0));
        g.Tick(2);
        var early = g.Notes();
        Assert.Contains(early, n => n.Kind == "command-reticle" && n.Json.Contains("\"move\""));
        Assert.DoesNotContain(early, n => n.Kind == "object-use");
        g.Tick(200);
        var notes = g.Notes();
        Assert.Single(notes, n => n.Kind == "object-use" && n.Json.Contains("getlow"));
        var placed = Assert.Single(notes, n => n.Kind == "item-placed");
        Assert.Contains("it_coins", placed.Json);
        Assert.Contains(notes, n => n.Kind == "command-reticle" && n.Json.Contains("clear"));
    }

    [DataFact]
    public void PickingUpGoldPlaysTheCoinSound()
    {
        var g = new Game();
        g.Drop(g.Pc.Position + new Vector3(0.5f, 0, 0));
        g.Tick(60);
        var pile = Assert.Single(g.Piles);
        g.Notes();
        g.Client.Send(new ClientRequestMessage { Kind = "pickup", Json = JsonSerializer.Serialize(new { item = pile.Id }) }.Write());
        g.Tick(80);
        Assert.Equal(198, g.Pc.Gold);
        var acquired = Assert.Single(g.Notes(), n => n.Kind == "item-acquired");
        Assert.Contains("it_coins", acquired.Json);
    }
}
