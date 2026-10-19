using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class PlayerActionQueueTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public readonly CreatureClass Cleric;
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            var area = new Area { ResRef = "queueprobe", Width = 4, Height = 4 };
            World.Register(area); World.Areas.Add(area);
            Server = new GameServer(World, new ServerOptions { StartArea = area.ResRef, DefaultAppearance = 1, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Queue tester" }.Write());
            Tick(); Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(area.Id, new Vector3(20, 15, 0), 0));
            foreach (var item in World.Inventory.AllItems(Pc).ToList()) World.Destroy(item);
            Pc.Classes.Clear(); Cleric = new CreatureClass { Class = 2, Level = 15 }; Pc.Classes.Add(Cleric);
            for (int i = 0; i < 6; i++) Pc.Abilities[i] = 18;
            CreatureRules.EnsureInit(World, Pc); CreatureRules.SyncHitPoints(World, Pc);
            Cleric.Memorized[1].Clear();
            Cleric.Memorized[1].AddRange([(50, 0, true), (6, 0, true), (50, 0, true)]);
            Tick(); Notes();
        }
        public void Tick(int n = 1) { for (int i = 0; i < n; i++) Server.Tick(1f / 30); }
        public void Request(string kind, object data)
        {
            Client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data) }.Write()); Tick();
        }
        public void Cast(int spell) => Request("cast-spell", new { spell, @class = 2, meta = 255, target = Pc.Id });
        public List<(string Kind, JsonElement Data)> Notes()
        {
            var notes = new List<(string, JsonElement)>();
            while (Client.TryReceive(out var bytes))
            {
                var r = new MessageReader(bytes);
                if (r.Type != MessageType.Notify) continue;
                var n = NotifyMessage.Read(r);
                using var json = JsonDocument.Parse(n.Json);
                notes.Add((n.Kind, json.RootElement.Clone()));
            }
            return notes;
        }
        public JsonElement[] Queue()
        {
            Tick(9);
            return Notes().Last(n => n.Kind == "hud").Data.GetProperty("self").GetProperty("queue").EnumerateArray().ToArray();
        }
    }

    [DataFact]
    public void SpellCommandsAppendAndMiddleCancellationPreservesNeighborsAndUnspentSlot()
    {
        var g = new Game();
        g.Cast(50); g.Cast(6); g.Cast(50);
        Assert.Equal(3, g.Pc.Actions.Count);
        var queue = g.Queue();
        Assert.Equal(new[] { 50, 6, 50 }, queue.Select(a => a.GetProperty("spell").GetInt32()));
        Assert.Equal(3, queue.Select(a => a.GetProperty("id").GetUInt32()).Distinct().Count());
        g.Request("clear-actions", new { id = queue[1].GetProperty("id").GetUInt32() });
        Assert.Equal(2, g.Pc.Actions.Count);
        g.Tick(330);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(new[] { false, true, false }, g.Cleric.Memorized[1].Select(s => s.Ready));
    }

    [DataFact]
    public void CurrentCancellationLeavesNextSpellAndStaleIdentityCannotCancelItsReplacement()
    {
        var g = new Game();
        g.Cast(50); g.Cast(6);
        var queue = g.Queue();
        uint old = queue[0].GetProperty("id").GetUInt32();
        g.Request("clear-actions", new { id = old });
        Assert.Single(g.Pc.Actions);
        g.Request("clear-actions", new { id = old });
        Assert.Single(g.Pc.Actions);
        g.Tick(330);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(new[] { true, false, true }, g.Cleric.Memorized[1].Select(s => s.Ready));
        var ready = g.Notes().Where(n => n.Kind == "spell-ready").ToArray();
        Assert.Contains(ready, n => n.Data.GetProperty("active").GetBoolean());
        Assert.False(ready.Last().Data.GetProperty("active").GetBoolean());
    }

    [DataFact]
    public void InventoryAndAbilityPotionRequestsShareQueueIconsAndIndividualCancellation()
    {
        var g = new Game();
        var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 3, "queuepotion")!;
        g.Request("item-use", new { item = potion.Id });
        g.Request("use-item", new { item = potion.Id, prop = 0 });
        g.Request("item-use", new { item = potion.Id });
        var queue = g.Queue();
        Assert.Equal(3, queue.Length);
        Assert.All(queue, a => {
            Assert.Equal(19, a.GetProperty("action").GetInt32());
            Assert.Equal(32, a.GetProperty("spell").GetInt32());
            Assert.False(string.IsNullOrEmpty(a.GetProperty("icon").GetString()));
        });
        g.Request("clear-actions", new { id = queue[1].GetProperty("id").GetUInt32() });
        g.Tick(210);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(1, potion.StackSize);
    }

    [DataFact]
    public void ClickingCommittedCurrentSpellKeepsItsRecoveryAndQueuedSuccessor()
    {
        var g = new Game();
        g.Cast(50); g.Cast(6);
        for (int tick = 0; tick < 180 && g.Cleric.Memorized[1][0].Ready; tick++) g.Tick();
        Assert.False(g.Cleric.Memorized[1][0].Ready);
        var current = g.Pc.Actions[0];
        var queue = g.Queue();
        g.Request("clear-actions", new { id = queue[0].GetProperty("id").GetUInt32() });
        Assert.Equal(2, g.Pc.Actions.Count);
        Assert.Same(current, g.Pc.Actions[0]);
        g.Tick(330);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(new[] { false, false, true }, g.Cleric.Memorized[1].Select(s => s.Ready));
    }

    [DataFact]
    public void TurnUndeadRequestsQueueAndCancellingPreparationLeavesUsesForSuccessor()
    {
        var g = new Game();
        g.Pc.Feats.Add(NwRules.FeatTurnUndead);
        int before = CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead);
        g.Request("use-feat", new { feat = 294, target = g.Pc.Id });
        g.Request("use-feat", new { feat = 294, target = g.Pc.Id });
        var queue = g.Queue();
        Assert.Equal(2, queue.Length);
        Assert.Equal(before, CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead));
        g.Request("clear-actions", new { id = queue[0].GetProperty("id").GetUInt32() });
        Assert.Single(g.Pc.Actions);
        Assert.Equal(before, CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead));
        g.Tick(300);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(before - 1, CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead));
        Assert.Single(g.Notes(), n => n.Kind == "spell-release");
    }

    [DataFact]
    public void ClickingCommittedTurnUndeadRetainsItsReleaseAndNextQueuedUse()
    {
        var g = new Game();
        g.Pc.Feats.Add(NwRules.FeatTurnUndead);
        int before = CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead);
        g.Request("use-feat", new { feat = 294, target = g.Pc.Id });
        g.Request("use-feat", new { feat = 294, target = g.Pc.Id });
        var queue = g.Queue();
        for (int tick = 0; tick < 90 && CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead) == before; tick++) g.Tick();
        Assert.Equal(before - 1, CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead));
        var current = g.Pc.Actions[0];
        g.Request("clear-actions", new { id = queue[0].GetProperty("id").GetUInt32() });
        Assert.Equal(2, g.Pc.Actions.Count);
        Assert.Same(current, g.Pc.Actions[0]);
        g.Tick(300);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(before - 2, CreatureRules.RemainingFeatUses(g.World, g.Pc, NwRules.FeatTurnUndead));
        Assert.Equal(2, g.Notes().Count(n => n.Kind == "spell-release"));
    }

    [DataFact]
    public void MixedSpellAndPotionTasksPreserveOrderAndSpendOnlyWhenCurrent()
    {
        var g = new Game();
        var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 2, "mixedqueue")!;
        g.Cast(50);
        g.Request("item-use", new { item = potion.Id });
        g.Cast(6);
        Assert.Equal(new[] { 4, 19, 4 }, g.Pc.Actions.Select(a => a.ActionType));
        Assert.Equal(2, potion.StackSize);
        Assert.True(g.Cleric.Memorized[1][1].Ready);
        g.Tick(600);
        Assert.Empty(g.Pc.Actions);
        Assert.Equal(1, potion.StackSize);
        Assert.Equal(new[] { false, false, true }, g.Cleric.Memorized[1].Select(s => s.Ready));
    }

    [DataFact]
    public void AbilityTaskCanReplaceIndefiniteAttackWithoutDiscardingOtherTasks()
    {
        var g = new Game();
        var attack = new OpenWinter.Game.Combat.AttackAction { Target = g.Pc.Id };
        var pending = new WaitAction { Remaining = 1 };
        g.Pc.Actions.Add(attack); g.Pc.Actions.Add(pending);
        g.Cast(6);
        Assert.Equal(2, g.Pc.Actions.Count);
        Assert.Same(pending, g.Pc.Actions[0]);
        Assert.IsType<OpenWinter.Game.Spells.CastSpellAction>(g.Pc.Actions[1]);
        g.Pc.Commandable = false;
        g.Cast(50);
        Assert.Equal(2, g.Pc.Actions.Count);
    }

    [DataFact]
    public void FinishedActionIdentityAndInvalidIndexDoNotClearPendingQueue()
    {
        var g = new Game();
        g.Cast(50); g.Cast(6);
        uint old = g.Queue()[0].GetProperty("id").GetUInt32();
        g.Tick(100);
        Assert.Single(g.Pc.Actions);
        g.Request("clear-actions", new { id = old });
        g.Request("clear-actions", new { index = 50 });
        Assert.Single(g.Pc.Actions);
        g.Request("clear-actions", new { index = -1 });
        Assert.Empty(g.Pc.Actions);
        Assert.True(g.Cleric.Memorized[1][1].Ready);
    }
}
