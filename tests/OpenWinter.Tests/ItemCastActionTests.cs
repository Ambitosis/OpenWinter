using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ItemCastActionTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public readonly List<string> Messages = new();
        public Game(Action<ResMan>? configure = null)
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            configure?.Invoke(resources);
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
            Pc.Classes.Clear(); Pc.Classes.Add(new CreatureClass { Class = 10, Level = 15 });
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
    public void BothPotionKindsPublishSelfTargetAndIgnoreRequestedOtherTarget()
    {
        foreach (int kind in new[] { 49, 104 })
            foreach (string route in new[] { "item-use", "use-item" })
            {
                var g = new Game();
                var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 2, "potion")!;
                potion.BaseItem = kind;
                var other = new Creature();
                g.World.Register(other);
                g.World.Jump(other, new Location(g.Pc.Area!.Id, g.Pc.Position + Vector3.UnitX, 0));
                g.Tick(3);
                g.Request("abilities", new { }); g.Tick();
                var power = g.Notes().Last(n => n.Kind == "abilities").Data.GetProperty("items")
                    .EnumerateArray().Single(x => x.GetProperty("item").GetUInt32() == potion.Id)
                    .GetProperty("props").EnumerateArray().First();
                Assert.Equal(AbilityRequests.TargetSelf, power.GetProperty("targetType").GetInt32());
                g.Request(route, new { item = potion.Id, prop = 0, target = other.Id });
                g.Tick(120);
                Assert.Equal(g.Pc.Id, g.Pc.LastSpell!.Target);
                Assert.Equal(1, potion.StackSize);
                Assert.Empty(g.Pc.Actions);
            }
    }

    [DataFact]
    public void BothItemRequestRoutesUseScrollPreparationAndPreserveCommittedReading()
    {
        foreach (string route in new[] { "item-use", "use-item" })
        {
            var g = new Game();
            var scroll = g.World.Inventory.CreateOn("nw_it_sparscr104", g.Pc, 2, "scroll")!;
            g.Request(route, new { item = scroll.Id, prop = 0, target = g.Pc.Id });
            g.Tick(15);
            ActionQueues.Clear(g.World, g.Pc);
            Assert.Single(g.Pc.Actions); // Native item_use_clear: reading is already committed.
            g.Tick(56);
            Assert.Equal(2, scroll.StackSize);
            Assert.Null(g.Pc.LastSpell);
            Assert.DoesNotContain(g.Notes(), n => n.Kind == "spell-conjure");
            g.Tick(10);
            Assert.Equal(1, scroll.StackSize);
            Assert.Equal(102, g.Pc.LastSpell!.SpellId);
            Assert.Single(g.Pc.Actions);
            g.Tick(30);
            Assert.Empty(g.Pc.Actions);
        }
    }

    [DataFact]
    public void InventoryScrollQueueWaitsSixSecondsAndKeepsFinalItemForHalfSecond()
    {
        var g = new Game();
        var scroll = g.World.Inventory.CreateOn("nw_it_sparscr104", g.Pc, 3, "scroll")!;
        for (int i = 0; i < 3; i++) g.Request("item-use", new { item = scroll.Id });
        g.Tick(80); Assert.Equal(2, scroll.StackSize);
        g.Tick(150); Assert.Equal(2, scroll.StackSize); // Before the second2.5s release at8.5.
        g.Tick(30); Assert.Equal(1, scroll.StackSize);
        g.Tick(180); Assert.False(scroll.IsDestroyed); // Final impact14.5, destruction15.0.
        g.Tick(20); Assert.True(scroll.IsDestroyed);
        Assert.Empty(g.Pc.Actions);
        Assert.Contains(g.Notes(), n => n.Kind == "inventory-open" && n.Data.GetProperty("items").GetArrayLength() == 0);
    }

    [DataFact]
    public void RepeatedScrollInputKeepsEveryQueuedActivationOnBothRoutes()
    {
        foreach (string route in new[] { "item-use", "use-item" })
        {
            var g = new Game();
            var scroll = g.World.Inventory.CreateOn("x1_it_sparscr103", g.Pc, 5, "scroll")!;
            for (int i = 0; i < 3; i++)
            {
                g.Request(route, new { item = scroll.Id, prop = 0 });
                g.Tick(6); // Actual-player native F7 control spaces presses by .2 seconds.
            }
            Assert.Equal(3, g.Pc.Actions.Count);
            Assert.Equal(5, scroll.StackSize);
            g.Tick(480);
            Assert.Equal(2, scroll.StackSize);
            Assert.Equal(417, g.Pc.LastSpell!.SpellId);
            Assert.Empty(g.Pc.Actions);
        }
    }

    [DataFact]
    public void ChargedItemReleaseSpendsBeforeRecoveryAndLastChargeRemovalEndsAction()
    {
        foreach (string route in new[] { "item-use", "use-item" })
        {
            var g = new Game();
            var wand = g.World.Inventory.CreateOn("nw_wmgwn004", g.Pc, 1, "wand")!;
            g.World.Inventory.SetCharges(wand, 1);
            g.Request(route, new { item = wand.Id, prop = 0, target = g.Pc.Id });
            g.Tick(10); Assert.Equal(1, wand.Charges);
            g.Tick(10); Assert.Equal(0, wand.Charges);
            Assert.False(wand.IsDestroyed);
            Assert.Single(g.Pc.Actions);
            Assert.Equal(107, g.Pc.LastSpell!.SpellId);
            g.Tick(20);
            Assert.True(wand.IsDestroyed);
            Assert.Empty(g.Pc.Actions);
        }
    }

    [DataFact]
    public void InventoryActivationUsesRestrictionsAllowedByThePropertyTable()
    {
        foreach (var (res, type, subtype, allowed) in new[]
        {
            ("nw_it_sparscr104", 63, 2, false), // Scroll class restriction applies.
            ("nw_it_sparscr104", 64, 5, true),  // Native AddItemProperty rejects these table-disallowed properties.
            ("nw_it_sparscr104", 62, 5, true),
            ("nw_it_mpotion001", 63, 2, true), // Potion's column disallows all three.
            ("nw_it_mpotion001", 64, 5, true),
            ("nw_it_mpotion001", 62, 5, true),
            ("nw_wmgwn004", 63, 2, false),
            ("nw_wmgwn004", 64, 5, false),
            ("nw_wmgwn004", 62, 5, false),
        })
            foreach (string route in res is "nw_it_mpotion001" or "nw_it_sparscr104" ? new[] { "item-use", "use-item" } : new[] { "item-use" })
            {
                var g = new Game(); g.Pc.GoodEvil = 100; g.Pc.Skills[19] = 0;
                Assert.NotEqual(5, g.Pc.Race);
                var item = g.World.Inventory.CreateOn(res, g.Pc, 2, "limited")!;
                item.Properties.RemoveAll(p => p.Type is >= 62 and <= 65);
                if (g.World.Inventory.Rules.AllowsProperty(item.BaseItem, type))
                    item.Properties.Add(new ItemProperty { Type = type, SubType = subtype, DurationType = 2, Useable = false });
                g.Tick(3); // Acquisition invalidates derived stats; settle before the concentration snapshot.
                if (item.BaseItem is 49 or 75)
                {
                    g.Request("abilities", new { }); g.Tick();
                    var description = g.Notes().Last(n => n.Kind == "abilities").Data.GetProperty("items")
                        .EnumerateArray().Single(x => x.GetProperty("item").GetUInt32() == item.Id);
                    Assert.Equal(allowed, description.GetProperty("usable").GetBoolean());
                }
                int stack = item.StackSize, charges = item.Charges;
                int property = item.Properties.FindIndex(p => p.Type == ItemRules.PropCastSpell);
                g.Request(route, new { item = item.Id, prop = property, target = g.Pc.Id });
                g.Tick(120);
                Assert.True(allowed == (g.Pc.LastSpell != null), $"{res} restriction {type}:{subtype}; stack={item.StackSize}, actions={g.Pc.Actions.Count}, notes={string.Join(';', g.Notes().Select(x => x.Kind))}, chat={string.Join(';', g.Messages)}");
                Assert.Equal(allowed ? stack - 1 : stack, item.StackSize);
                Assert.Equal(charges, item.Charges); // Rejected wands never spend a charge.
                Assert.Empty(g.Pc.Actions);
            }
    }

    static void CustomBase(ResMan resources, int row, int sourceRow, params (string Column, string Value)[] values)
    {
        // Clone installed data only in memory, before any cached lookup in the new world.
        var table = TwoDA.Parse(resources.Read("baseitems", ResType.TwoDA)!);
        var text = new System.Text.StringBuilder("2DA V2.0\n\n").AppendLine(string.Join(" ", table.Columns));
        for (int index = 0; index < Math.Max(table.RowCount, row + 1); index++)
        {
            var cells = table.Columns.Select(c => table.Get(index == row ? sourceRow : index, c)).ToArray();
            if (index == row) foreach (var (column, value) in values) cells[table.ColumnIndex(column)] = value;
            text.Append(index).Append(' ').AppendLine(string.Join(" ", cells.Select(s => string.IsNullOrEmpty(s) ? "****" : s.Contains(' ') ? "\"" + s + "\"" : s)));
        }
        var custom = new OpenWinter.Game.Persistence.MemorySource("authored consumable classification");
        custom.Set(ResKey.Of("baseitems", ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(text.ToString()));
        resources.AddFirst(custom);
    }

    [DataFact]
    public void CraftedAndCustomItemsUseOrdinaryPreparationAndSixSecondRounds()
    {
        foreach (int baseItem in new[] { 105, 106, 200 })
        {
            var g = new Game(r => CustomBase(r, 200, 49));
            var item = g.World.Inventory.CreateOn("nw_it_sparscr104", g.Pc, 3, "custom")!;
            item.BaseItem = baseItem;
            var power = item.Properties.Single(p => p.Type == ItemRules.PropCastSpell);
            if (baseItem == 106) { power.CostTableValue = 6; item.Charges = 3; item.StackSize = 1; }
            int Remaining() => baseItem == 106 ? item.Charges : item.StackSize;
            int property = item.Properties.IndexOf(power);
            g.Tick(3);
            g.Request("item-use", new { item = item.Id, prop = property });
            g.Request("item-use", new { item = item.Id, prop = property });
            g.Tick(24); Assert.Equal(2, Remaining()); // .5s release, not potion1.75 or scroll2.5.
            g.Tick(120); Assert.Equal(2, Remaining());
            g.Tick(70); Assert.Equal(1, Remaining()); // Second release waits for the six-second round.
        }
    }

    [DataFact]
    public void CustomEquipmentSlotsAndActualPotionRestrictionsControlActivation()
    {
        var g = new Game(r => CustomBase(r, 200, 46, ("EquipableSlots", "0x00010")));
        var wand = g.World.Inventory.CreateOn("nw_wmgwn004", g.Pc, 1, "custom")!;
        wand.BaseItem = 200;
        var skills = g.World.System<OpenWinter.Game.Skills.SkillSystem>();
        Assert.False(skills.CanActivateSpellItem(g.Pc, wand));
        g.Pc.Equipment[EquipSlot.RightHand] = wand;
        Assert.True(skills.CanActivateSpellItem(g.Pc, wand));

        var changed = new Game(r => CustomBase(r, 49, 49, ("PropColumn", "10")));
        changed.Pc.Skills[19] = 0;
        var potion = changed.World.Inventory.CreateOn("nw_it_mpotion001", changed.Pc, 2, "restricted")!;
        Assert.True(changed.World.Inventory.Rules.AllowsProperty(49, 63));
        potion.Properties.Add(new ItemProperty { Type = 63, SubType = 2, DurationType = 2 });
        changed.Tick(3);
        changed.Request("abilities", new { }); changed.Tick();
        var metadata = changed.Notes().Last(n => n.Kind == "abilities").Data.GetProperty("items")
            .EnumerateArray().Single(x => x.GetProperty("item").GetUInt32() == potion.Id);
        Assert.False(metadata.GetProperty("usable").GetBoolean());
        changed.Request("item-use", new { item = potion.Id, prop = 0 });
        changed.Request("use-item", new { item = potion.Id, prop = 0 });
        changed.Tick(90);
        Assert.Null(changed.Pc.LastSpell);
        Assert.Equal(2, potion.StackSize);
    }

    [DataFact]
    public void PersonalScrollPowerUsesSelfWithoutRequestingAnItemTarget()
    {
        var g = new Game();
        var shield = g.World.Inventory.CreateOn("x1_it_sparscr103", g.Pc, 2, "shield")!;
        var armor = g.World.Inventory.CreateOn("nw_it_sparscr104", g.Pc, 2, "armor")!;
        g.Tick(3);
        g.Request("abilities", new { }); g.Tick();
        var items = g.Notes().Last(n => n.Kind == "abilities").Data.GetProperty("items").EnumerateArray().ToArray();
        JsonElement Power(Item item) => items.Single(x => x.GetProperty("item").GetUInt32() == item.Id)
            .GetProperty("props").EnumerateArray().First();
        Assert.Equal(AbilityRequests.TargetSelf, Power(shield).GetProperty("targetType").GetInt32());
        Assert.NotEqual(AbilityRequests.TargetSelf, Power(armor).GetProperty("targetType").GetInt32());
        g.Request("use-item", new { item = shield.Id, prop = 0 });
        g.Tick(120);
        Assert.Equal(417, g.Pc.LastSpell!.SpellId);
        Assert.Equal(1, shield.StackSize);
        Assert.Equal(2, armor.StackSize);
    }

    [DataFact]
    public void BothRequestRoutesRejectBackpackStaffUntilItIsEquipped()
    {
        foreach (string route in new[] { "item-use", "use-item" })
        {
            var g = new Game();
            var staff = g.World.Inventory.CreateOn("nw_wmgst004", g.Pc, 1, "staff")!;
            staff.Properties.RemoveAll(p => p.Type == ItemRules.PropCastSpell && p.SubType != 167);
            int charges = staff.Charges;
            int property = staff.Properties.FindIndex(p => p.Type == ItemRules.PropCastSpell && p.SubType == 167);
            g.Request(route, new { item = staff.Id, prop = property, target = g.Pc.Id });
            g.Tick(120);
            Assert.Null(g.Pc.LastSpell); Assert.Equal(charges, staff.Charges); Assert.Empty(g.Pc.Actions);
            Assert.True(g.World.Inventory.Equip(g.Pc, staff, 4));
            g.Request(route, new { item = staff.Id, prop = property, target = g.Pc.Id });
            g.Tick(60);
            Assert.Equal(102, g.Pc.LastSpell!.SpellId);
            Assert.Equal(charges - 1, staff.Charges);
        }
    }
}
