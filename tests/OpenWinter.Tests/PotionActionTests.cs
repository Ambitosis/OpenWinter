using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class PotionActionTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public readonly List<string> Messages = new();
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
            Tick(); Notes();
        }
        public void Tick(int count = 1) { for (int i = 0; i < count; i++) Server.Tick(1f / 30); }
        public void Request(string kind, object data) => Client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data) }.Write());
        public List<(string Kind, JsonElement Data)> Notes()
        {
            var notes = new List<(string, JsonElement)>();
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type == MessageType.Chat) { Messages.Add(ChatMessage.Read(reader).Text); continue; }
                if (reader.Type != MessageType.Notify) continue;
                var note = NotifyMessage.Read(reader);
                using var data = JsonDocument.Parse(note.Json);
                notes.Add((note.Kind, data.RootElement.Clone()));
            }
            return notes;
        }
    }

    [DataFact]
    public void PotionReadyPresentationEndsOnCompletionCancellationDeathAndFinalBottleRemoval()
    {
        foreach (int stack in new[] { 1, 2 })
            foreach (int stop in new[] { 0, 1, 2 })
            {
                var g = new Game();
                var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, stack, "potion")!;
                g.Tick(); g.Notes();
                g.Request("item-use", new { item = potion.Id });
                g.Tick(15);
                Assert.False(g.Pc.InCombat);
                Assert.True(Assert.Single(g.Notes(), n => n.Kind == "potion-ready").Data.GetProperty("active").GetBoolean());
                if (stop == 1) ActionQueues.Clear(g.World, g.Pc);
                if (stop == 2) g.Pc.IsDead = true;
                g.Tick(90);
                Assert.False(Assert.Single(g.Notes(), n => n.Kind == "potion-ready").Data.GetProperty("active").GetBoolean());
                Assert.Empty(g.Pc.Actions);
                Assert.Equal(stop != 0 ? stack : stack - 1, potion.IsDestroyed ? 0 : potion.StackSize);
            }
    }

    [DataFact]
    public void InventoryPotionQueueUsesMeasuredPhasesAndPublishesConsumption() => CheckQueue("item-use");

    [DataFact]
    public void HotbarPotionQueueUsesMeasuredPhasesAndPublishesConsumption() => CheckQueue("use-item");

    static void CheckQueue(string request)
    {
        var g = new Game();
        var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 3, "potion")!;
        g.Tick(); g.Notes();
        for (int i = 0; i < 3; i++) g.Request(request, new { item = potion.Id, prop = 0 });
        g.Tick(49);
        Assert.Equal(3, potion.StackSize);
        Assert.Equal(3, g.Pc.Actions.Count);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "spell-conjure");
        g.Tick(7);
        Assert.Equal(2, potion.StackSize);
        var first = g.Notes();
        var snapshot = Assert.Single(first, n => n.Kind == "inventory-open").Data;
        Assert.Equal(2, Assert.Single(snapshot.GetProperty("items").EnumerateArray()).GetProperty("stack").GetInt32());
        Assert.Contains(g.Messages, m => m.Contains("uses Potion of Cure Light Wounds"));
        Assert.Equal(32, g.Pc.LastSpell!.SpellId);
        Assert.Equal(potion.Id, g.Pc.LastSpell.CastItem);
        g.Tick(68);
        Assert.Equal(2, potion.StackSize); // second release is4.25s, not the old2.0s
        g.Tick(9);
        Assert.Equal(1, potion.StackSize);
        g.Tick(73);
        Assert.False(potion.IsDestroyed); // final impact still exposes the item
        g.Tick(18);
        Assert.True(potion.IsDestroyed);
        Assert.Empty(g.Pc.Actions);
        Assert.Contains(g.Notes(), n => n.Kind == "inventory-open" && n.Data.GetProperty("items").GetArrayLength() == 0);
    }

    [DataFact]
    public void DamageDuringPotionPreparationAndDrinkDoesNotInterruptConsumption()
    {
        foreach (int damageTick in new[] { 15, 39 })
        {
            var g = new Game();
            g.Pc.MaxHitPoints = g.Pc.CurrentHitPoints = 1000;
            var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 2, "potion")!;
            g.Request("item-use", new { item = potion.Id });
            g.Tick(damageTick);
            // DC above any natural d20 roll makes an erroneous concentration check fail.
            g.Pc.CurrentHitPoints -= 100;
            g.Tick(56 - damageTick);
            Assert.Equal(1, potion.StackSize);
            Assert.Equal(32, g.Pc.LastSpell!.SpellId);
            Assert.DoesNotContain(g.Notes(), n => n.Kind == "spell-interrupted");
        }
    }

    [DataFact]
    public void EarlyPotionClearPreservesBottleAndCommittedClearRetainsAction()
    {
        var g = new Game();
        var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 3, "potion")!;
        g.Tick(); g.Notes();
        g.Request("item-use", new { item = potion.Id });
        g.Tick(22);
        ActionQueues.Clear(g.World, g.Pc);
        g.Tick(60);
        Assert.Equal(3, potion.StackSize);
        Assert.Null(g.Pc.LastSpell);
        g.Request("item-use", new { item = potion.Id });
        g.Request("item-use", new { item = potion.Id });
        g.Tick(38);
        ActionQueues.Clear(g.World, g.Pc);
        Assert.Single(g.Pc.Actions);
        g.Request("item-use", new { item = potion.Id });
        g.Tick(28);
        Assert.Equal(2, potion.StackSize);
        Assert.Equal(2, g.Pc.Actions.Count);
        g.Tick(72);
        Assert.Equal(1, potion.StackSize);
        g.Tick(25);
        Assert.Empty(g.Pc.Actions);
    }
}
