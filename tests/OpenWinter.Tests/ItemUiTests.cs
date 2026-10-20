using System.Text.Json;
using System.Text.RegularExpressions;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>The item details the client's tooltips, inventory, store and loot windows are built from.</summary>
public class ItemUiTests
{
    readonly ITestOutputHelper _out;
    public ItemUiTests(ITestOutputHelper output) { _out = output; }

    static (World, GameServer, IConnection, Creature) Login()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.ControlObject) me = ControlObjectMessage.Read(r).ObjectId;
        }
        return (world, server, client, world.Get<Creature>(me)!);
    }

    static List<(string Kind, JsonElement Data)> Notifies(IConnection client)
    {
        var list = new List<(string, JsonElement)>();
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type != MessageType.Notify) continue;
            var n = NotifyMessage.Read(r);
            list.Add((n.Kind, JsonDocument.Parse(n.Json).RootElement));
        }
        return list;
    }

    static JsonElement Inventory(GameServer server, IConnection client)
    {
        Notifies(client);
        client.Send(new ClientRequestMessage { Kind = "inventory" }.Write());
        server.Tick(1f / 30);
        return Notifies(client).Last(n => n.Kind == "inventory-open").Data;
    }

    [DataFact]
    public void InventoryItemsCarryTooltipDetails()
    {
        var (world, server, client, pc) = Login();
        var inv = Inventory(server, client);
        Assert.True(inv.GetProperty("normalLoad").GetInt32() > 0);
        Assert.True(inv.GetProperty("weight").GetInt32() > 0);
        var items = inv.GetProperty("items").EnumerateArray().Concat(inv.GetProperty("equipped").EnumerateArray()).ToList();
        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            var info = item.GetProperty("info");
            _out.WriteLine($"{item.GetProperty("name")}: {info.GetProperty("type")} [{info.GetProperty("category")}] dmg {info.GetProperty("damage")} ac {info.GetProperty("ac")} " +
                $"wt {info.GetProperty("weight")} value {info.GetProperty("value")} props {info.GetProperty("properties").GetArrayLength()} reqs {info.GetProperty("requirements").GetArrayLength()}");
            Assert.False(string.IsNullOrEmpty(info.GetProperty("type").GetString()));
            Assert.True(info.GetProperty("weight").GetInt32() >= 0);
        }
        // Every weapon shows its damage dice.
        foreach (var weapon in items.Where(i => i.GetProperty("info").GetProperty("category").GetString() == "weapon"))
            Assert.Matches(new Regex(@"^\d+d\d+"), weapon.GetProperty("info").GetProperty("damage").GetString()!);
    }

    [DataFact]
    public void CategoriesFollowTheBaseItemTable()
    {
        var world = new World(TestData.Module);
        var inv = world.Inventory;
        Assert.Equal("weapon", inv.Category(1));    // longsword
        Assert.Equal("weapon", inv.Category(8));    // longbow
        Assert.Equal("armor", inv.Category(16));
        Assert.Equal("shield", inv.Category(56));
        Assert.Equal("apparel", inv.Category(17));  // helmet
        Assert.Equal("apparel", inv.Category(36));  // gloves (listed with melee weapons)
        Assert.Equal("apparel", inv.Category(80));  // cloak
        Assert.Equal("jewelry", inv.Category(52));  // ring
        Assert.Equal("jewelry", inv.Category(19));  // amulet
        Assert.Equal("ammo", inv.Category(20));     // arrow
        Assert.Equal("consumable", inv.Category(49)); // potion
        Assert.Equal("consumable", inv.Category(75)); // scroll
        Assert.Equal("consumable", inv.Category(46)); // wand
        Assert.Equal("container", inv.Category(66)); // large box
        Assert.Equal("misc", inv.Category(24));
    }

    [DataFact]
    public void SplitMakesASecondStackInTheSameBag()
    {
        var (world, server, client, pc) = Login();
        var item = world.Inventory.Items(pc).First(i => InventorySystem.SlotOf(pc, i) < 0 && world.Inventory.Holder(i) == pc);
        item.StackSize = 5;
        int before = world.Inventory.Items(pc).Count();
        client.Send(new ClientRequestMessage { Kind = "item-split", Json = $"{{\"item\":{item.Id},\"count\":2}}" }.Write());
        server.Tick(1f / 30);
        Assert.Equal(3, item.StackSize);
        Assert.Equal(before + 1, world.Inventory.Items(pc).Count());
        Assert.Contains(world.Inventory.Items(pc), i => i != item && i.ResRef == item.ResRef && i.StackSize == 2);
        // A split can't take the whole stack.
        client.Send(new ClientRequestMessage { Kind = "item-split", Json = $"{{\"item\":{item.Id},\"count\":3}}" }.Write());
        server.Tick(1f / 30);
        Assert.Equal(3, item.StackSize);
    }

    [DataFact]
    public void StoreViewOffersPricesForThePlayersItems()
    {
        var (world, server, client, pc) = Login();
        var store = world.AllObjects.OfType<Store>().FirstOrDefault();
        if (store == null) { _out.WriteLine("module has no store"); return; }
        Notifies(client);
        world.Inventory.OpenStore(store, pc, 0, 0);
        var view = Notifies(client).Last(n => n.Kind == "store-open").Data;
        Assert.Equal(pc.Gold, view.GetProperty("playerGold").GetInt32());
        var offers = view.GetProperty("offers").EnumerateArray().ToList();
        int unequipped = world.Inventory.Items(pc).Count(i => InventorySystem.SlotOf(pc, i) < 0);
        Assert.Equal(unequipped, offers.Count);
        foreach (var o in offers)
        {
            var item = world.Get<Item>(o.GetProperty("id").GetUInt32())!;
            Assert.Equal(world.Inventory.BuyPrice(store, item, pc), o.GetProperty("price").GetInt32());
        }
        Assert.All(view.GetProperty("items").EnumerateArray(), i => Assert.True(i.TryGetProperty("info", out _)));
    }

    [DataFact]
    public void ExamineDescribesCreaturesWithRaceAndClass()
    {
        var (world, server, client, pc) = Login();
        var json = JsonSerializer.Serialize(OpenWinter.Game.Requests.Examine.Describe(world, pc, pc));
        var d = JsonDocument.Parse(json).RootElement;
        _out.WriteLine(d.GetProperty("subtitle").GetString());
        Assert.False(string.IsNullOrEmpty(d.GetProperty("subtitle").GetString()));
    }

    [DataFact]
    public void GoldDropRevalidatesBalanceAndCanBePickedUpAgain()
    {
        var (world, server, client, pc) = Login();
        pc.Gold = 123;
        var cancelled = new DropGoldAction { Count = 50 };
        Assert.Equal(ActionStatus.InProgress, cancelled.Step(world, pc, 0.1f));
        cancelled.Cancel(world, pc);
        Assert.Equal(123, pc.Gold);
        var invalid = new DropGoldAction { Count = 124 };
        Assert.Equal(ActionStatus.Failed, invalid.Step(world, pc, 2));
        Assert.Equal(123, pc.Gold);
        var drop = new DropGoldAction { Count = 50 };
        Assert.Equal(ActionStatus.Done, drop.Step(world, pc, 2));
        Assert.Equal(73, pc.Gold);
        var coins = world.AllObjects.OfType<Item>().Single(i => i.BaseItem == ItemRules.BaseItemGold && i.Area == pc.Area && i.StackSize == 50);
        world.Inventory.MoveTo(coins, pc);
        Assert.Equal(123, pc.Gold);
    }

    [DataFact]
    public void CompletedDropSendsInventoryWithoutTheDroppedItem()
    {
        var (world, server, client, pc) = Login();
        var item = world.Inventory.CreateOn("nw_it_mpotion001", pc, 1, "UI_DROP_TEST")!;
        Notifies(client);
        var action = new PutDownItemAction { ItemId = item.Id };
        Assert.Equal(ActionStatus.InProgress, action.Step(world, pc, 0.05f));
        Assert.DoesNotContain(Notifies(client), n => n.Kind == "inventory-open");
        // This test checks completion feedback; player reach/hold timing is covered separately.
        Assert.Equal(ActionStatus.Done, action.Step(world, pc, PutDownItemAction.PlayerDuration));
        var inventory = Notifies(client).Last(n => n.Kind == "inventory-open").Data;
        Assert.DoesNotContain(inventory.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetUInt32() == item.Id);
        Assert.Equal(pc.Area, item.Area);
    }

    /// <summary>
    /// The original lays a put-down item at facing 0 whichever way its dropper faces, and draws it with its own
    /// model, so the spawn carries the item's parts and colours (parity/visual, scenes grounddrop and grounditems).
    /// </summary>
    [DataFact]
    public void PutDownItemFacesEastAndSpawnsWithItsModelParts()
    {
        var (world, server, client, pc) = Login();
        var sword = world.Inventory.CreateOn("nw_wswls001", pc, 1, "UI_GROUND_MODEL")!;
        pc.Facing = MathF.PI / 2;
        server.Tick(1f / 30);
        while (client.TryReceive(out _)) { }
        var action = new PutDownItemAction { ItemId = sword.Id };
        action.Step(world, pc, 0.05f);
        Assert.Equal(ActionStatus.Done, action.Step(world, pc, PutDownItemAction.PlayerDuration));
        Assert.Equal(pc.Area, sword.Area);
        Assert.Equal(0f, sword.Facing);
        server.Tick(1f / 30);
        SpawnMessage? spawn = null;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.Spawn && SpawnMessage.Read(r) is { } s && s.ObjectId == sword.Id) spawn = s;
        }
        Assert.NotNull(spawn);
        Assert.Equal(ObjectKind.Item, spawn!.Kind);
        var look = Assert.Single(spawn.Appearance.Equipment);
        Assert.Equal(1, look.BaseItem);
        Assert.Equal(new ushort[] { 61, 11, 11 }, look.ModelParts);
        Assert.Equal(6, look.Colors.Length);
    }

    [DataFact]
    public void ArmourIconsUseInstalledPartsAndItemColors()
    {
        var world = new World(TestData.Module);
        var armor = world.Items.FromBlueprint("nw_aarcl004")!;
        var first = world.Inventory.Icon(armor);
        Assert.NotEmpty(first);
        Assert.All(first, icon => Assert.StartsWith("iconplt:ipm_", icon));
        armor.Template.SetByte("Cloth1Color", 10);
        var second = world.Inventory.Icon(armor);
        Assert.NotEqual(string.Join(',', first), string.Join(',', second));
        var viewer = new Creature { Gender = 1 };
        Assert.All(world.Inventory.Icon(armor, viewer), icon => Assert.StartsWith("iconplt:ipf_", icon));
    }

    [DataFact]
    public void ArmorPlacementSoundUsesPhysicalArmorWithMagicAndIdentificationChanges()
    {
        var resources = new OpenWinter.Aurora.Resources.ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var expected = new[] { "it_armorleather", "it_armorleather", "it_armorchain", "it_armorchain",
            "it_armorplate", "it_armorplate", "it_armorplate", "it_armorleather", "it_materialcloth" };
        for (int i = 1; i <= 9; i++)
        {
            var armor = world.Items.FromBlueprint($"nw_aarcl00{i}")!;
            Assert.Equal(expected[i - 1], world.Inventory.Rules.InventorySound(armor));
        }
        var cloth = world.Items.FromBlueprint("nw_cloth001")!;
        Assert.Equal("it_materialcloth", world.Inventory.Rules.InventorySound(cloth));
        var studded = world.Items.FromBlueprint("nw_aarcl002")!;
        studded.Properties.Add(new OpenWinter.Game.Scripting.ItemProperty
        { Type = ItemRules.PropAcBonus, CostTableValue = 5, DurationType = ItemRules.DurationPermanent });
        foreach (bool identified in new[] { true, false })
        {
            studded.Identified = identified;
            var info = JsonSerializer.SerializeToElement(world.Inventory.ItemInfo(studded));
            Assert.Equal(identified ? 8 : 3, info.GetProperty("ac").GetInt32());
            Assert.Equal("it_armorleather", info.GetProperty("inventorySound").GetString());
        }
        var sword = world.Items.FromBlueprint("nw_wswls001")!;
        Assert.Equal("it_bladelarge", world.Inventory.Rules.InventorySound(sword));
    }

    [DataFact]
    public void ArmorUiRequirementsAgreeWithEquipRulesAcrossPhysicalWeights()
    {
        var resources = new OpenWinter.Aurora.Resources.ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var viewer = new Creature();
        foreach (var (blueprint, feat) in new[] { ("nw_cloth001", -1), ("nw_aarcl002", 3),
                     ("nw_aarcl003", 4), ("nw_aarcl007", 2) })
        {
            var item = world.Items.FromBlueprint(blueprint)!;
            item.Properties.Add(new OpenWinter.Game.Scripting.ItemProperty
            { Type = ItemRules.PropAcBonus, CostTableValue = 5, DurationType = ItemRules.DurationPermanent });
            foreach (bool identified in new[] { false, true })
                foreach (bool trained in new[] { false, true })
                {
                    item.Identified = identified;
                    viewer.Feats.Clear();
                    if (trained && feat >= 0) viewer.Feats.Add(feat);
                    var info = JsonSerializer.SerializeToElement(world.Inventory.ItemInfo(item, viewer));
                    var requirements = info.GetProperty("requirements").EnumerateArray().ToArray();
                    Assert.Equal(feat >= 0 ? 1 : 0, requirements.Length);
                    bool usable = requirements.All(r => r.GetProperty("met").GetBoolean());
                    Assert.Equal(feat < 0 || trained, usable);
                    Assert.Equal(world.Inventory.HasProficiency(viewer, item), usable);
                }
        }
    }

}
