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

public sealed class InventoryPublicationTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public readonly Placeable Chest;
        public readonly List<string> Chat = new();
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            var area = new Area { ResRef = "inventorypub", Width = 4, Height = 4 };
            World.Register(area); World.Areas.Add(area);
            Server = new GameServer(World, new ServerOptions { StartArea = area.ResRef, DefaultAppearance = 1, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Inventory tester" }.Write());
            Tick(); Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(area.Id, new Vector3(20, 15, 0), 0));
            foreach (var item in World.Inventory.AllItems(Pc).ToList()) World.Destroy(item);
            Chest = new Placeable { HasInventory = true, Useable = true, CurrentHitPoints = 50, HitPoints = 50 };
            World.AddToArea(Chest, area, Pc.Position + Vector3.UnitX, 0);
            World.System<ContainerService>().Show(Pc, Chest);
            Tick(); Notes();
        }
        public Item Give(GameObject holder, int type, string resref, int count = 1)
        {
            var item = new Item { BaseItem = type, ResRef = resref, Tag = resref, StackSize = count };
            World.Register(item);
            return World.Inventory.MoveTo(item, holder)!;
        }
        public void Tick(int count = 1) { for (int i = 0; i < count; i++) Server.Tick(1f / 30); }
        public void Request(string kind, object data) => Client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data) }.Write());
        public List<(string Kind, JsonElement Data)> Notes()
        {
            Chat.Clear();
            var notes = new List<(string, JsonElement)>();
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type == MessageType.Chat) { Chat.Add(ChatMessage.Read(reader).Text); continue; }
                if (reader.Type != MessageType.Notify) continue;
                var note = NotifyMessage.Read(reader);
                using var data = JsonDocument.Parse(note.Json);
                notes.Add((note.Kind, data.RootElement.Clone()));
            }
            return notes;
        }
        public JsonElement Inventory() => Assert.Single(Notes(), n => n.Kind == "inventory-open").Data;
    }

    [DataFact]
    public void LootAcquisitionReportsNameAndPlacementSoundOnceIncludingMergedStacks()
    {
        var g = new Game();
        var arrows = g.Give(g.Pc, 20, "arrows", 10);
        arrows.Name = new LocString("Arrows");
        var loot = g.Give(g.Chest, 20, "arrows", 8);
        loot.Name = new LocString("Arrows");
        g.Tick(); g.Notes();
        g.Request("container-take", new { item = loot.Id });
        g.Tick();
        var acquired = Assert.Single(g.Notes(), n => n.Kind == "item-acquired").Data;
        Assert.Equal(new[] { "Acquired Item: Arrows" }, g.Chat);
        Assert.Equal(arrows.Id, acquired.GetProperty("item").GetUInt32());
        Assert.Equal(g.World.Inventory.Rules.InventorySound(arrows), acquired.GetProperty("sound").GetString());
        Assert.False(string.IsNullOrEmpty(acquired.GetProperty("sound").GetString()));
        Assert.True(loot.IsDestroyed);
        Assert.Equal(18, arrows.StackSize);

        // An owned-grid move and a rejected duplicate loot request must not report acquisition.
        g.Request("item-move", new { item = arrows.Id, x = 3, y = 0 });
        g.Request("container-take", new { item = loot.Id });
        g.Tick();
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "item-acquired");
        Assert.Empty(g.Chat);
    }

    [DataFact]
    public void GroundPickupReportsOnlyWhenThePickupCompletes()
    {
        var g = new Game();
        var item = new Item { BaseItem = 49, Name = new LocString("Test potion"), StackSize = 1 };
        g.World.AddToArea(item, g.Pc.Area!, g.Pc.Position, 0);
        g.Pc.Actions.Add(new PickUpItemAction { ItemId = item.Id });
        g.Tick();
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "item-acquired");
        Assert.Empty(g.Chat);
        g.Tick(45);
        Assert.Single(g.Notes(), n => n.Kind == "item-acquired");
        Assert.Equal(new[] { "Acquired Item: Test potion" }, g.Chat);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
    }

    [DataFact]
    public void LootClickUsesFirstFreeCellAndDragUsesExplicitCellWithLiveSnapshots()
    {
        var g = new Game();
        var occupied = g.Give(g.Pc, 49, "occupied");
        var click = g.Give(g.Chest, 49, "click");
        var drag = g.Give(g.Chest, 49, "drag");
        g.Tick(); g.Notes();
        g.Request("container-take", new { item = click.Id });
        g.Tick();
        var notes = g.Notes();
        var inventory = Assert.Single(notes, n => n.Kind == "inventory-open").Data;
        var contents = Assert.Single(notes, n => n.Kind == "container-open").Data;
        Assert.Equal((0, 0), (occupied.InvX, occupied.InvY));
        Assert.Equal((1, 0), (click.InvX, click.InvY));
        Assert.Contains(inventory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == click.Id);
        Assert.DoesNotContain(contents.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == click.Id);
        g.Request("container-take", new { item = drag.Id, x = 7, y = 3 });
        g.Tick();
        inventory = g.Inventory();
        var moved = Assert.Single(inventory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == drag.Id);
        Assert.Equal(7, moved.GetProperty("x").GetInt32());
        Assert.Equal(3, moved.GetProperty("y").GetInt32());
        Assert.Same(g.Pc, g.World.Inventory.Owner(drag));
    }

    [DataFact]
    public void ArrowMergeAndScriptDestructionPublishFinalContentsOnce()
    {
        var g = new Game();
        var arrows = g.Give(g.Pc, 20, "arrows", 10);
        var loot = g.Give(g.Chest, 20, "arrows", 8);
        var potion = g.Give(g.Pc, 49, "potion");
        g.Tick(); g.Notes();
        g.Request("container-take", new { item = loot.Id });
        g.Tick();
        var inventory = g.Inventory();
        var stack = Assert.Single(inventory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == arrows.Id);
        Assert.Equal(18, stack.GetProperty("stack").GetInt32());
        Assert.True(loot.IsDestroyed);
        // Later server-side changes do not depend on another client inventory request.
        g.World.After(0.2f, () => { g.World.Destroy(potion); arrows.StackSize = 15; g.World.Inventory.Changed(arrows); g.World.Inventory.Changed(arrows); });
        g.Tick(5);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "inventory-open");
        g.Tick(3);
        inventory = g.Inventory();
        Assert.DoesNotContain(inventory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == potion.Id);
        Assert.Equal(new[] { 15 }, inventory.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("stack").GetInt32()).Order().ToArray());
        g.Tick(3);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "inventory-open");
    }

    [DataFact]
    public void NestedBagMutationUpdatesOwnerAndRejectedLootDoesNotLeakOtherContainers()
    {
        var g = new Game();
        var bag = g.Give(g.Pc, 66, "bag");
        var item = g.Give(bag, 49, "potion");
        var other = new Placeable { HasInventory = true };
        g.World.AddToArea(other, g.Pc.Area!, g.Pc.Position, 0);
        var hidden = g.Give(other, 49, "hidden");
        g.Tick(); g.Notes();
        g.Request("container-take", new { item = hidden.Id });
        g.Tick();
        Assert.Same(other, g.World.Inventory.Owner(hidden));
        Assert.DoesNotContain(g.Notes(), n => n.Kind is "inventory-open" or "container-open");
        g.World.Destroy(item);
        g.Tick();
        var inventory = g.Inventory();
        Assert.DoesNotContain(inventory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == item.Id);
        Assert.Contains(inventory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetUInt32() == bag.Id);
    }
}
