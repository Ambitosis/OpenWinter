using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class GroundInventoryPlacementTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public readonly List<(string Kind, JsonElement Data)> Notes = new();
        public readonly List<string> Chat = new();
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            Area = new Area { ResRef = "groundinventory", Width = 4, Height = 4 };
            World.Register(Area); World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = Area.ResRef, DefaultAppearance = 6, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Ground inventory tester" }.Write());
            Tick(); Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(Area.Id, new Vector3(20, 15, 0), 90));
            foreach (var item in World.Inventory.AllItems(Pc).ToList()) World.Destroy(item);
            Clear();
        }
        public Item Item(bool ground = false, int type = 49, string name = "Test potion", int count = 1)
        {
            var item = new Item { BaseItem = type, Name = new LocString(name), ResRef = name, Tag = name, StackSize = count };
            if (ground) World.AddToArea(item, Area, Pc.Position + new Vector3(6, 0, 0), 0);
            else { World.Register(item); World.Inventory.MoveTo(item, Pc); }
            Tick(); Clear();
            return item;
        }
        public void Tick(int frames = 1)
        {
            for (int i = 0; i < frames; i++) Server.Tick(1f / 30);
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type == MessageType.Chat) Chat.Add(ChatMessage.Read(reader).Text);
                else if (reader.Type == MessageType.Notify)
                {
                    var note = NotifyMessage.Read(reader);
                    using var doc = JsonDocument.Parse(note.Json);
                    Notes.Add((note.Kind, doc.RootElement.Clone()));
                }
            }
        }
        public void Clear() { Notes.Clear(); Chat.Clear(); }
        public void Request(string kind, object data)
        {
            Client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data) }.Write()); Tick();
        }
        public void Drop(Item item, Vector3 point, uint? area = null) => Request("drop", new
        { item = item.Id, area = area ?? Area.Id, position = new[] { point.X, point.Y, point.Z } });
    }

    [DataFact]
    public void PlayerGroundTransfersWaitForMeasuredReachAndLoopDuration()
    {
        foreach (bool pickup in new[] { false, true })
        {
            var g = new Game(); var item = g.Item(ground: pickup);
            if (pickup)
            {
                item.Position = g.Pc.Position + new Vector3(.5f, 0, 0);
                g.Client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = item.Id }.Write());
                g.Tick();
            }
            else g.Request("drop", new { item = item.Id });
            var reach = Assert.Single(g.Notes, n => n.Kind == "object-use");
            Assert.Equal(1.5f, reach.Data.GetProperty("duration").GetSingle());
            g.Tick(40);
            Assert.Equal(!pickup, g.World.Inventory.Owner(item) == g.Pc);
            Assert.DoesNotContain(g.Notes, n => n.Kind is "item-acquired" or "item-placed");
            g.Tick(8);
            Assert.Equal(pickup, g.World.Inventory.Owner(item) == g.Pc);
            Assert.Single(g.Notes, n => n.Kind == (pickup ? "item-acquired" : "item-placed"));
        }
    }

    [DataFact]
    public void TerrainDropApproachesThenAnimatesAndPublishesLossAndSoundOnlyOnCompletion()
    {
        var g = new Game(); var item = g.Item();
        var destination = g.Pc.Position + new Vector3(7, 0, 0);
        g.Drop(item, destination);
        g.Tick(12);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.True(g.Pc.Position.X > 20);
        Assert.DoesNotContain(g.Notes, n => n.Kind is "object-use" or "item-placed");
        Assert.Empty(g.Chat);
        g.Tick(150);
        Assert.Null(g.World.Inventory.Owner(item));
        Assert.InRange(Vector3.Distance(destination, item.Position), 0, .01f);
        Assert.InRange(Vector3.Distance(destination, g.Pc.Position), .99f, 1.01f);
        Assert.Equal("getlow", Assert.Single(g.Notes, n => n.Kind == "object-use").Data.GetProperty("animation").GetString());
        var placement = Assert.Single(g.Notes, n => n.Kind == "item-placed").Data;
        Assert.Equal(g.World.Inventory.Rules.InventorySound(item), placement.GetProperty("sound").GetString());
        Assert.Equal(new[] { "Lost Item: Test potion" }, g.Chat);
    }

    [DataFact]
    public void CancelledOrInvalidTerrainDropKeepsOwnershipAndDoesNotReportLoss()
    {
        var g = new Game(); var item = g.Item();
        g.Drop(item, new Vector3(100, 100, 0));
        g.Drop(item, new Vector3(25, 15, 0), g.Area.Id + 1);
        Assert.Empty(g.Pc.Actions);
        g.Drop(item, new Vector3(35, 15, 0));
        g.Tick(10); ActionQueues.Clear(g.World, g.Pc);
        var stop = g.Pc.Position;
        g.Tick(150);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.Equal(stop, g.Pc.Position);
        Assert.DoesNotContain(g.Notes, n => n.Kind == "item-placed");
        Assert.Empty(g.Chat);
    }

    [DataFact]
    public void DefaultWorldUseRunsThePickupActionAndAcknowledgesAcquisition()
    {
        var g = new Game(); var item = g.Item(ground: true);
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = item.Id }.Write());
        g.Tick();
        Assert.IsType<PickUpItemAction>(Assert.Single(g.Pc.Actions));
        g.Tick(150);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.Single(g.Notes, n => n.Kind == "object-use");
        Assert.Single(g.Notes, n => n.Kind == "item-acquired");
        Assert.Equal(new[] { "Acquired Item: Test potion" }, g.Chat);
    }

    [DataFact]
    public void GroundCarryMetadataDoesNotAcquireOrWalkAndChosenCellIsPreserved()
    {
        var g = new Game(); var item = g.Item(ground: true);
        var start = g.Pc.Position;
        g.Request("ground-item-carry", new { item = item.Id });
        var metadata = Assert.Single(g.Notes, n => n.Kind == "ground-item-carry").Data;
        Assert.Equal(item.Id, metadata.GetProperty("id").GetUInt32());
        Assert.True(metadata.GetProperty("info").GetProperty("width").GetInt32() > 0);
        g.Tick(45);
        Assert.Equal(start, g.Pc.Position);
        Assert.Empty(g.Pc.Actions);
        Assert.Null(g.World.Inventory.Owner(item));
        Assert.Empty(g.Chat);
        g.Request("pickup", new { item = item.Id, x = 7, y = 3 });
        g.Tick(150);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.Equal((7, 3), (item.InvX, item.InvY));
        Assert.Single(g.Notes, n => n.Kind == "item-acquired");
    }

    [DataFact]
    public void GroundPickupRejectsInvalidCellsFallsBackFromOccupiedCellsAndMergesStacks()
    {
        var g = new Game();
        var occupied = g.Item(name: "Other potion");
        var ground = g.Item(ground: true);
        g.Request("pickup", new { item = ground.Id, x = 100, y = 0 });
        g.Tick(150);
        Assert.Null(g.World.Inventory.Owner(ground));
        Assert.DoesNotContain(g.Notes, n => n.Kind == "item-acquired");
        g.Request("pickup", new { item = ground.Id, x = occupied.InvX, y = occupied.InvY });
        g.Tick(150);
        Assert.Same(g.Pc, g.World.Inventory.Owner(ground));
        Assert.NotEqual((occupied.InvX, occupied.InvY), (ground.InvX, ground.InvY));
        Assert.Single(g.Notes, n => n.Kind == "item-acquired");
        var arrows = g.Item(type: 20, name: "Arrows", count: 10);
        var incoming = g.Item(ground: true, type: 20, name: "Arrows", count: 5);
        g.Request("pickup", new { item = incoming.Id, x = arrows.InvX, y = arrows.InvY });
        g.Tick(150);
        Assert.Equal(15, arrows.StackSize);
        Assert.True(incoming.IsDestroyed);
        Assert.Equal(arrows.Id, Assert.Single(g.Notes, n => n.Kind == "item-acquired").Data.GetProperty("item").GetUInt32());
    }

    [DataFact]
    public void PaperdollSlotKeepsExplicitOffhandAndAcquisitionBoundary()
    {
        var g = new Game();
        g.Pc.Size = 3;
        g.Pc.Feats.Add(46); // simple weapon proficiency
        var item = g.World.Inventory.CreateOn("nw_wswdg001", g.Pc, 1, "GROUND")!;
        g.World.Inventory.Drop(item, g.Pc.Location, g.Pc); g.Tick(); g.Clear();
        g.Request("equip-ground", new { item = item.Id, slot = 5 });
        for (int frame = 0; frame < 55 && g.World.Inventory.Owner(item) == null; frame++) g.Tick();
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.DoesNotContain(item, g.Pc.Equipment.Values);
        g.Tick();
        Assert.Same(item, g.Pc.Equipment[EquipSlot.LeftHand]);
        Assert.False(g.Pc.Equipment.ContainsKey(EquipSlot.RightHand));
        Assert.Single(g.Notes, n => n.Kind == "item-acquired");
    }

    [DataFact]
    public void ContainerDepositReportsLossOnceAndInternalMovesStaySilent()
    {
        var g = new Game(); var item = g.Item();
        var chest = new Placeable { HasInventory = true, Useable = true };
        g.World.AddToArea(chest, g.Area, g.Pc.Position, 0);
        g.World.System<ContainerService>().Show(g.Pc, chest); g.Tick(); g.Clear();
        g.Request("item-move", new { item = item.Id, x = 3, y = 2 });
        Assert.Empty(g.Chat);
        g.Request("container-put", new { item = item.Id });
        Assert.Same(chest, g.World.Inventory.Owner(item));
        Assert.Equal(new[] { "Lost Item: Test potion" }, g.Chat);
        g.Clear();
        g.Request("container-put", new { item = item.Id });
        Assert.Empty(g.Chat);
    }

    [DataFact]
    public void FinalPotionConsumptionGoesWithoutLostItem()
    {
        // The item stays script-visible for half a second, then goes; the original prints no "Lost Item" for a used-up
        // potion (visual scenes castlines2, castlines4).
        var g = new Game();
        var item = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 1, "CONSUME")!;
        var property = item.Properties.First(p => p.Type == ItemRules.PropCastSpell);
        g.Tick(); g.Clear();
        Assert.True(g.World.Inventory.UseItemProperty(g.Pc, item, property, g.Pc.Id, g.Pc.Location, true));
        g.Tick(5);
        Assert.DoesNotContain(g.Chat, m => m.StartsWith("Lost Item:"));
        Assert.False(item.IsDestroyed);
        g.Tick(25);
        Assert.True(item.IsDestroyed);
        g.Tick(30);
        Assert.DoesNotContain(g.Chat, m => m.StartsWith("Lost Item:"));
    }

    // parity/visual scene dropchain: the original stays bent through queued put-downs and stands once after the last.
    static List<JsonElement> Reaches(Game g) => g.Notes.Where(n => n.Kind == "object-use").Select(n => n.Data).ToList();
    static bool Chain(JsonElement reach) => reach.GetProperty("chain").GetBoolean();

    [DataFact]
    public void QueuedPutDownsMarkTheReachAsChained()
    {
        var g = new Game();
        var items = Enumerable.Range(0, 3).Select(i => g.Item(name: "Chain potion " + i)).ToList();
        // Requested together, as dragging several items out in one moment does.
        foreach (var item in items)
            g.Client.Send(new ClientRequestMessage { Kind = "drop", Json = JsonSerializer.Serialize(new { item = item.Id }) }.Write());
        g.Tick(5 * 30);
        var reaches = Reaches(g);
        Assert.Equal(new[] { true, true, false }, reaches.Select(Chain));
        Assert.All(reaches, r => Assert.Equal(PutDownItemAction.PlayerDuration, r.GetProperty("duration").GetSingle(), 3));
        Assert.All(items, item => Assert.NotNull(item.Area));
    }

    [DataFact]
    public void PutDownQueuedDuringABendChainsItAndACancelEndsIt()
    {
        var g = new Game();
        var first = g.Item(name: "Chain potion a");
        var second = g.Item(name: "Chain potion b");
        g.Request("drop", new { item = first.Id });
        g.Tick(15);
        g.Request("drop", new { item = second.Id });
        g.Tick(3 * 30);
        var reaches = Reaches(g);
        // The first reach, its upgrade for the rest of its time, then the second.
        Assert.Equal(new[] { false, true, false }, reaches.Select(Chain));
        Assert.InRange(reaches[1].GetProperty("duration").GetSingle(), .8f, 1.0f);
        Assert.NotNull(second.Area);

        g.Clear();
        var third = g.Item(name: "Chain potion c");
        g.Request("drop", new { item = third.Id });
        g.Tick(10);
        ActionQueues.Clear(g.World, g.Pc);
        g.Tick();
        reaches = Reaches(g);
        Assert.Equal(2, reaches.Count);
        Assert.True(reaches[1].GetProperty("cancelled").GetBoolean());
        Assert.Null(third.Area);
    }
}
