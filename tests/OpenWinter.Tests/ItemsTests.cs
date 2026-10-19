using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class ItemsTests
{
    readonly ITestOutputHelper _out;
    public ItemsTests(ITestOutputHelper output) { _out = output; }

    sealed class RecordingOutput : IWorldOutput
    {
        public readonly List<(string Kind, GameObject? Subject, object Data, Creature? Player)> Notifies = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) => Notifies.Add((kind, subject, data, player));
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    sealed class Fixture
    {
        public required World World;
        public required Area Area;
        public required Creature Pc;
        public required Placeable Chest;
        public required RecordingOutput Output;
        public readonly List<string> Log = new();
        public LocalVariables M => World.Module.Locals;
        public InventorySystem Inv => World.Inventory;
        public void Run(string script) => World.Scripts!.Run(script, Pc.Id);
        public void Tick(int n = 1) { for (int i = 0; i < n; i++) World.Step(0.1f); }
        public T Obj<T>(string local) where T : GameObject => World.Get<T>(M.Objects[local])!;
    }

    static Fixture Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        var output = new RecordingOutput();
        world.Output = output;
        world.EnableScripts();
        world.Register(world.Module);
        world.Module.Scripts["Mod_OnAcquirItem"] = "items_acq";
        world.Module.Scripts["Mod_OnUnAqreItem"] = "items_unacq";
        world.Module.Scripts["Mod_OnPlrEqItm"] = "items_equ";
        world.Module.Scripts["Mod_OnPlrUnEqItm"] = "items_unequ";
        world.Module.Scripts["Mod_OnActvtItem"] = "items_act";
        var area = new Area { ResRef = "test_area", Tag = "test_area" };
        world.Register(area);
        world.Areas.Add(area);
        var pc = new Creature { Tag = "pc" };
        // A player: the equip events are only for players.
        pc.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = pc };
        foreach (int feat in new[] { 2, 3, 4, 32, 45, 46 }) pc.Feats.Add(feat); // armour, shield and weapon proficiencies
        world.AddToArea(pc, area, new Vector3(10, 10, 0), 0);
        var chest = new Placeable { Tag = "test_chest", HasInventory = true, Useable = true };
        chest.Scripts["OnInvDisturbed"] = "items_dist";
        world.AddToArea(chest, area, new Vector3(11, 10, 0), 0);
        var f = new Fixture { World = world, Area = area, Pc = pc, Chest = chest, Output = output };
        world.Scripts!.Log = f.Log.Add;
        return f;
    }

    [DataFact]
    public void CreateStackCopyAndEvents()
    {
        var f = Make();
        f.Run("items_basic");
        foreach (var l in f.Log) _out.WriteLine(l);
        var m = f.M;

        Assert.Equal(1, m.Ints["merged"]);
        Assert.Equal(15, m.Ints["stack"]);
        Assert.Equal(1, m.Ints["possessor"]);
        Assert.Equal(1, m.Ints["tag"]);
        Assert.Equal(1, m.Ints["bytag"]);
        Assert.Equal(1, m.Ints["swstack"]);
        Assert.Equal(99, m.Ints["arrowmax"]);
        Assert.Equal(30, m.Ints["value"]); // longsword: BaseCost 15 x ItemMultiplier 2
        Assert.Equal(1, m.Ints["base"]);
        Assert.Equal(256, m.Ints["baseinvalid"]);
        Assert.Equal(1, m.Ints["ranged"]);
        Assert.Equal(1, m.Ints["gem_in_bag"]);
        Assert.Equal(1, m.Ints["gem_bag"]);
        Assert.Equal(110, m.Ints["hasinv"]);
        Assert.Equal(5, m.Ints["count"]); // arrows, sword, bow, bag, then the potion inside the bag
        Assert.Equal(1, m.Ints["bagcount"]);
        Assert.Equal(1, m.Ints["bag_in_bag"]); // created but left nowhere, as on the original server
        Assert.Equal(70, m.Ints["gold"]);
        Assert.Equal(11010, m.Ints["flags"]);
        Assert.Equal(1, m.Ints["copy_in_chest"]);
        Assert.Equal(7, m.Ints["copy_var"]);
        Assert.Equal(0, m.Ints["copy2_var"]);
        Assert.Equal(2, m.Ints["chestcount"]);
        Assert.Equal(3, m.Ints["recolor"]);
        Assert.Equal(1, m.Ints["recolor_model"]);
        Assert.Equal(1, m.Ints["badmodify"]); // the original copies a sword with an armour part too

        // Acquire events are queued until the creating script finishes.
        Assert.False(m.Ints.ContainsKey("acq"));
        f.Tick();
        var sword = f.Obj<Item>("sword");
        var arrows = f.Obj<Item>("arrows");
        Assert.Equal(8, m.Ints["acq"]); // arrows twice, sword, bow, bag, potion, the two modified copies
        Assert.Equal(7, m.Ints["acq_sword_marked"]);
        Assert.Equal(15, m.Ints["acq_arrows"]);
        Assert.Equal(f.Pc.Id, m.Objects["acq_by"]);

        // The queued equip ran; the bad slot failed; the arrows went to the chest.
        Assert.Same(sword, f.Pc.Equipment[EquipSlot.RightHand]);
        Assert.DoesNotContain(sword, f.Pc.Inventory);
        Assert.Equal(1, m.Ints["equ"]);
        Assert.Equal(sword.Id, m.Objects["equ_item"]);
        Assert.Equal(f.Pc.Id, m.Objects["equ_by"]);
        Assert.Equal(4, m.Ints["equ_slot"]);
        Assert.Equal(1, m.Ints["equ_inslot"]);
        Assert.Contains(f.Log, l => l.Contains("ActionEquipItem failed"));
        Assert.Contains(f.Output.Notifies, n => n.Kind == "creature-equipment" && n.Subject == f.Pc);
        Assert.Same(f.Chest, f.Inv.Owner(arrows));

        f.Tick();
        Assert.Equal(3, m.Ints["lost"]); // the two modified copies it destroyed (carried items), then the arrows
        Assert.Equal(arrows.Id, m.Objects["lost_item"]);
        Assert.Equal(f.Pc.Id, m.Objects["lost_by"]);
        Assert.Equal(1, m.Ints["dist"]);
        Assert.Equal(f.Pc.Id, m.Objects["dist_by"]);
        Assert.Equal(arrows.Id, m.Objects["dist_item"]);
        Assert.Equal(InventorySystem.DisturbAdded, m.Ints["dist_type"]);

        // Taking it back: acquired from the chest, chest disturbed (removed).
        f.Pc.Actions.Add(new TransferItemAction { ItemId = arrows.Id, Other = f.Chest.Id, Take = true });
        f.Tick(2); // close enough to the chest already
        Assert.Same(f.Pc, f.Inv.Owner(arrows));
        Assert.Equal(f.Chest.Id, m.Objects["acq_from"]);
        Assert.Equal(99, m.Ints["acq_stack"]);
        Assert.Equal(InventorySystem.DisturbRemoved, m.Ints["dist_type"]);

        // Unequip fires its event and returns the item to the inventory.
        Assert.True(f.Inv.Unequip(f.Pc, sword));
        Assert.Equal(sword.Id, m.Objects["unequ_item"]);
        Assert.Equal(4, m.Ints["unequ_slot"]);
        Assert.Contains(sword, f.Pc.Inventory);
    }

    [DataFact]
    public void EquipRulesAndGround()
    {
        var f = Make();
        var inv = f.Inv;
        var shield = inv.CreateOn("nw_ashsw001", f.Pc, 1, "")!;
        var bow = inv.CreateOn("nw_wbwln001", f.Pc, 1, "")!;
        var sword = inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        Assert.True(inv.Equip(f.Pc, sword, 4));
        Assert.True(inv.Equip(f.Pc, shield, 5));
        Assert.False(inv.Equip(f.Pc, shield, 0)); // shields don't go on heads
        // A bow needs both hands: the sword and the shield come off.
        Assert.True(inv.Equip(f.Pc, bow, 4));
        Assert.Same(bow, f.Pc.Equipment[EquipSlot.RightHand]);
        Assert.False(f.Pc.Equipment.ContainsKey(EquipSlot.LeftHand));
        Assert.Contains(sword, f.Pc.Inventory);
        Assert.Contains(shield, f.Pc.Inventory);
        f.Tick();
        Assert.Equal(3, f.M.Ints["equ"]);
        Assert.Equal(2, f.M.Ints["unequ"]); // the sword replaced by the bow, the shield displaced by it
        // Dropping and picking back up.
        Assert.True(inv.Drop(sword, f.Pc.Location, f.Pc));
        Assert.Same(f.Area, sword.Area);
        Assert.Null(inv.Owner(sword));
        f.Pc.Actions.Add(new PickUpItemAction { ItemId = sword.Id });
        f.Tick(2);
        Assert.Same(f.Area, sword.Area); // picking up takes 1.3 s
        f.Tick(13);
        Assert.Null(sword.Area);
        Assert.Same(f.Pc, inv.Owner(sword));
        Assert.DoesNotContain(sword, f.Area.Objects);
        // Gold items become gold.
        var gold = inv.CreateOn("nw_it_gold001", f.Chest, 250, "")!;
        Assert.Null(inv.MoveTo(gold, f.Pc, f.Pc));
        Assert.True(gold.IsDestroyed);
        Assert.Equal(250, f.Pc.Gold);
        // Destroying a carried item takes it out of the inventory.
        f.World.Destroy(shield);
        Assert.DoesNotContain(shield, f.Pc.Inventory);
    }

    [DataFact]
    public void StoreAcceptsNewStockButAbsorbsMatchingInfiniteWares()
    {
        var f = Make();
        var store = new Store(); f.World.AddToArea(store, f.Area, f.Pc.Position, 0);
        var armor = f.Inv.CreateOn("nw_aarcl001", store, 1, "")!;
        Assert.Same(store, f.Inv.Owner(armor)); armor.Infinite = true;
        Assert.True(f.Inv.CreateOn("nw_aarcl001", store, 1, "")!.IsDestroyed);
        Assert.False(f.Inv.CreateOn("nw_aarcl001", store, 1, "different_tag")!.IsDestroyed);
        var first = f.Inv.CreateOn("nw_it_mpotion001", store, 1, "")!;
        var second = f.Inv.CreateOn("nw_it_mpotion001", store, 1, "")!;
        Assert.NotSame(first, second); Assert.False(first.IsDestroyed); Assert.False(second.IsDestroyed);
        Assert.Equal(1, first.StackSize); Assert.Equal(1, second.StackSize);
        Assert.Equal(4, store.Inventory.Count);
    }

    [DataFact]
    public void ItemValueIsWorkedOutLazilyWithTheChargesOfTheFirstRead()
    {
        // items_value_charges: charges set before anything reads the value count; set after a read they don't.
        var f = Make();
        var unread = f.Inv.CreateOn("nw_wmgwn006", f.Pc, 1, "")!;
        f.Inv.SetCharges(unread, 3);
        int fresh = f.Inv.Rules.GoldValue(unread);
        var read = f.Inv.CreateOn("nw_wmgwn006", f.Pc, 1, "")!;
        int full = f.Inv.Rules.GoldValue(read);
        f.Inv.SetCharges(read, 3);
        Assert.Equal(full, f.Inv.Rules.GoldValue(read));
        Assert.True(fresh < full);
        read.Reprice(); // adding a property or identifying marks it stale
        Assert.Equal(fresh, f.Inv.Rules.GoldValue(read));
    }

    [DataFact]
    public void ReopeningTheSameStoreKeepsItsBonusMarkUpAndMarkDown()
    {
        // storeprices: OpenStore(s, pc, 20, -10) then OpenStore(s, pc) still bought at 170% and sold at 40%.
        var f = Make(); var store = new Store { MarkUp = 150, MarkDown = 50 };
        f.World.AddToArea(store, f.Area, f.Pc.Position, 0);
        var sword = f.Inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        f.Inv.OpenStore(store, f.Pc, 20, -10);
        f.Inv.OpenStore(store, f.Pc, 0, 0);
        int value = f.Inv.Rules.GoldValue(sword);
        Assert.Equal(value * 40 / 100, f.Inv.BuyPrice(store, sword, f.Pc));
        Assert.Equal(value * 170 / 100, f.Inv.SellPrice(store, sword, f.Pc));
        var other = new Store { MarkUp = 150, MarkDown = 50 };
        f.World.AddToArea(other, f.Area, f.Pc.Position, 0);
        f.Inv.OpenStore(other, f.Pc, 0, 0);
        f.Inv.OpenStore(store, f.Pc, 0, 0); // a fresh opening after another store
        Assert.Equal(value * 50 / 100, f.Inv.BuyPrice(store, sword, f.Pc));
    }

    [DataFact]
    public void ScriptChangesToAnOpenStoreOrTheBackpackRefreshTheStoreWindow()
    {
        var f = Make(); var store = new Store();
        f.World.AddToArea(store, f.Area, f.Pc.Position, 0);
        f.Inv.OpenStore(store, f.Pc, 0, 0);
        f.Tick();
        f.Output.Notifies.Clear();
        f.Inv.CreateOn("nw_aarcl001", store, 1, "");
        f.Tick();
        Assert.Contains(f.Output.Notifies, n => n.Kind == "store-open");
        f.Output.Notifies.Clear();
        f.Inv.CreateOn("nw_wswls001", f.Pc, 1, "");
        f.Tick();
        Assert.Contains(f.Output.Notifies, n => n.Kind == "store-open"); // the new item's offer
    }

    [DataFact]
    public void StoreClosesAtTwoMetresFromItsOpeningPositionAndOnlyOnce()
    {
        var f = Make(); var store = new Store();
        f.World.AddToArea(store, f.Area, new Vector3(30, 10, 0), 0);
        f.Inv.OpenStore(store, f.Pc, 0, 0);
        f.Pc.Position += Vector3.UnitX;
        f.Tick(); Assert.NotNull(f.Inv.SessionOf(f.Pc));
        f.Inv.OpenStore(store, f.Pc, 0, 0); // refreshing does not move the boundary
        f.Pc.Position += Vector3.UnitX * .99f;
        f.Tick(); Assert.NotNull(f.Inv.SessionOf(f.Pc));
        f.Pc.Position += Vector3.UnitX * .02f;
        f.Tick(); Assert.Null(f.Inv.SessionOf(f.Pc));
        f.Tick(10); Assert.Single(f.Output.Notifies, n => n.Kind == "store-close");
        f.Inv.OpenStore(store, f.Pc, 0, 0);
        f.Pc.Position += Vector3.UnitX;
        f.Tick(); Assert.NotNull(f.Inv.SessionOf(f.Pc));
        f.World.Destroy(store); f.Tick(); Assert.Null(f.Inv.SessionOf(f.Pc));
    }

    [DataFact]
    public void StoreRefreshClosesAViewerOutOfRangeInsteadOfReopeningIt()
    {
        var f = Make(); var store = new Store();
        f.World.AddToArea(store, f.Area, f.Pc.Position, 0);
        f.Inv.OpenStore(store, f.Pc, 0, 0);
        var unknown = f.Inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        unknown.Identified = false; f.Pc.Gold = 1000;
        f.Pc.Position += Vector3.UnitX * 3; // out of range, before the store tick notices
        f.Output.Notifies.Clear();
        Assert.True(f.Inv.Identify(f.Pc, store, unknown)); // refreshes every viewer's window
        Assert.Null(f.Inv.SessionOf(f.Pc));
        Assert.Single(f.Output.Notifies, n => n.Kind == "store-close");
        Assert.DoesNotContain(f.Output.Notifies, n => n.Kind == "store-open");
    }

    [DataFact]
    public void OpeningAnotherStoreClosesTheFirstAndMeasuresFromThere()
    {
        var f = Make(); var a = new Store(); var b = new Store();
        f.World.AddToArea(a, f.Area, new Vector3(30, 10, 0), 0);
        f.World.AddToArea(b, f.Area, new Vector3(30, 12, 0), 0);
        f.Inv.OpenStore(a, f.Pc, 0, 0);
        f.Pc.Position += Vector3.UnitX * 1.5f;
        f.Output.Notifies.Clear();
        f.Inv.OpenStore(b, f.Pc, 0, 0);
        Assert.Contains(f.Output.Notifies, n => n.Kind == "store-close" && n.Subject == a);
        Assert.Same(b, f.Inv.SessionOf(f.Pc)!.Store);
        f.Pc.Position += Vector3.UnitX * 1.9f; // 3.4 m from A's opening point, 1.9 m from B's
        f.Tick(); Assert.Same(b, f.Inv.SessionOf(f.Pc)?.Store);
        f.Pc.Position += Vector3.UnitX * .2f;
        f.Tick(); Assert.Null(f.Inv.SessionOf(f.Pc));
    }

    [DataFact]
    public void HelmetShopIconIncludesItsPaletteAndStockCoordinates()
    {
        var f = Make();
        var store = new Store { Template = f.World.Resources.ReadGff("nw_storgenral001", ResType.Utm)! };
        f.World.AddToArea(store, f.Area, f.Pc.Position, 0); f.Inv.Load(store);
        var helmet = store.Inventory.First(i => i.ResRef == "nw_arhe004");
        Assert.StartsWith("iconplt:ihelm_005:", Assert.Single(f.Inv.Icon(helmet, f.Pc)));
        Assert.Equal(10, f.Inv.Shape(store).PageHeight);
        Assert.Equal((4, 2), (helmet.InvX, helmet.InvY));
        Assert.Equal(2, store.CategoryPages[0]);
        foreach (var item in store.Inventory.ToArray()) f.World.Destroy(item);
        Assert.Equal(2, store.CategoryPages[0]);
    }

    [DataFact]
    public void StorePurchaseAcknowledgesItsItemSoundOnlyAfterSuccessfulTransfer()
    {
        var f = Make(); var store = new Store { MarkUp = 100 };
        f.World.AddToArea(store, f.Area, f.Pc.Position, 0);
        var armor = f.Inv.CreateOn("nw_aarcl001", store, 1, "")!;
        var potion = f.Inv.CreateOn("nw_it_mpotion001", store, 1, "")!;
        potion.Infinite = true;
        var existing = f.Inv.CreateOn("nw_it_mpotion001", f.Pc, 1, "")!;
        f.Output.Notifies.Clear();
        f.Pc.Gold = 0;
        Assert.False(f.Inv.Buy(f.Pc, store, armor));
        Assert.DoesNotContain(f.Output.Notifies, n => n.Kind == "item-placed");
        f.Pc.Gold = 10000;
        Assert.True(f.Inv.Buy(f.Pc, store, armor));
        Assert.False(f.Inv.Buy(f.Pc, store, armor)); // stale second request cannot replay it
        Assert.True(f.Inv.Buy(f.Pc, store, potion)); // infinite copy merges into the carried stack
        var placed = f.Output.Notifies.Where(n => n.Kind == "item-placed").ToArray();
        Assert.Equal(2, placed.Length);
        Assert.All(placed, n => { Assert.Same(f.Pc, n.Subject); Assert.Same(f.Pc, n.Player); });
        var a = System.Text.Json.JsonSerializer.SerializeToElement(placed[0].Data);
        var b = System.Text.Json.JsonSerializer.SerializeToElement(placed[1].Data);
        Assert.Equal(armor.Id, a.GetProperty("item").GetUInt32());
        Assert.Equal(existing.Id, b.GetProperty("item").GetUInt32());
        Assert.Equal(f.Inv.Rules.InventorySound(armor), a.GetProperty("sound").GetString());
        Assert.Equal(f.Inv.Rules.InventorySound(potion), b.GetProperty("sound").GetString());
        Assert.NotEqual(a.GetProperty("sound").GetString(), b.GetProperty("sound").GetString());
        Assert.DoesNotContain(f.Output.Notifies, n => n.Kind == "item-acquired");
    }

    [DataFact]
    public void StorePricingBuySellIdentify()
    {
        var f = Make();
        var utm = f.World.Resources.ReadGff("nw_storeweap001", ResType.Utm)!;
        var store = new Store { Tag = "test_store", Template = utm };
        f.World.AddToArea(store, f.Area, new Vector3(12, 10, 0), 0);
        int stock = utm.GetList("StoreList").Sum(cat => cat.GetList("ItemList").Count);

        f.Run("items_store");
        var m = f.M;
        Assert.Equal(-1, m.Ints["sgold"]);
        Assert.Equal(-1, m.Ints["smaxbuy"]);
        Assert.Equal(100, m.Ints["sident"]);
        Assert.Equal(-2, m.Ints["snotstore"]);
        Assert.Equal(1525, m.Ints["sset"]);
        Assert.Equal(stock, m.Ints["sitems"]);
        Assert.Equal(150, store.MarkUp);
        Assert.Equal(35, store.MarkDown);
        var open = Assert.Single(f.Output.Notifies, n => n.Kind == "store-open");
        Assert.Same(f.Pc, open.Player);
        Assert.Equal(160, (int)open.Data.GetType().GetProperty("markUp")!.GetValue(open.Data)!);

        var inv = f.Inv;
        var forSale = store.Inventory.First(i => i.BaseItem == 1 && i.Properties.Count == 0); // plain longsword
        Assert.True(forSale.Infinite);
        Assert.Equal(48, inv.SellPrice(store, forSale, f.Pc)); // 30 gp x (150 + 10)%
        Assert.False(inv.Buy(f.Pc, store, forSale)); // no money
        inv.AddGold(f.Pc, 100);
        Assert.True(inv.Buy(f.Pc, store, forSale));
        Assert.Equal(52, f.Pc.Gold);
        Assert.Equal(1048, store.Gold);
        Assert.Same(store, inv.Owner(forSale)); // infinite stock stays
        var bought = f.Pc.Inventory.Single(i => i.BaseItem == 1);
        Assert.False(bought.Infinite);
        f.Tick();
        Assert.Equal(store.Id, m.Objects["acq_from"]);
        Assert.Equal(bought.Id, m.Objects["acq_item"]);

        Assert.Equal(9, inv.BuyPrice(store, bought, f.Pc)); // 30 gp x (35 - 5)%
        bought.Stolen = true;
        Assert.Equal(-1, inv.BuyPrice(store, bought, f.Pc));
        bought.Stolen = false;
        store.WillNotBuy.Add(1);
        Assert.Equal(-1, inv.BuyPrice(store, bought, f.Pc));
        store.WillNotBuy.Clear();
        Assert.True(inv.Sell(f.Pc, store, bought));
        Assert.Equal(61, f.Pc.Gold);
        Assert.Equal(1039, store.Gold);
        Assert.True(bought.IsDestroyed); // the matching infinite ware absorbs the sale
        Assert.Same(store, inv.Owner(forSale));

        var unknown = inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        unknown.Identified = false;
        Assert.True(inv.Identify(f.Pc, store, unknown));
        Assert.True(unknown.Identified);
        Assert.Equal(36, f.Pc.Gold);
        store.MaxBuyPrice = 5;
        Assert.Equal(5, inv.BuyPrice(store, unknown, f.Pc));
    }

    [DataFact]
    public void ActivatingItemsSpendsChargesAndUses()
    {
        var f = Make();
        var inv = f.Inv;
        var wand = inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        // The fixture is a longsword with authored powers; wearable powers require equipping.
        Assert.True(inv.Equip(f.Pc, wand, 4));
        // Cast spell: Activate Item (Self), 5 charges per use.
        var unique = new ItemProperty { Type = 15, SubType = 335, CostTable = 3, CostTableValue = 2, DurationType = 2 };
        var daily = new ItemProperty { Type = 15, SubType = 329, CostTable = 3, CostTableValue = 9, DurationType = 2 }; // 2 uses/day
        wand.Properties.Add(unique);
        wand.Properties.Add(daily);
        wand.Charges = 7;
        f.Pc.Actions.Add(new UseItemAction { ItemId = wand.Id, Property = unique, Target = f.Chest.Id });
        f.Tick(15); // activation goes through spell casting: one second of cast time
        Assert.Equal(1, f.M.Ints["act"]);
        Assert.Equal(wand.Id, f.M.Objects["act_item"]);
        Assert.Equal(f.Pc.Id, f.M.Objects["act_by"]);
        Assert.Equal(f.Chest.Id, f.M.Objects["act_target"]);
        Assert.Equal(2, wand.Charges);
        Assert.False(inv.UseItemProperty(f.Pc, wand, unique, f.Chest.Id, Location.Invalid, true)); // not enough charges
        Assert.True(inv.UseItemProperty(f.Pc, wand, daily, f.Chest.Id, Location.Invalid, true));
        Assert.True(inv.UseItemProperty(f.Pc, wand, daily, f.Chest.Id, Location.Invalid, true));
        Assert.False(inv.UseItemProperty(f.Pc, wand, daily, f.Chest.Id, Location.Invalid, true));
        Assert.Equal(3, f.M.Ints["act"]);
        // SetItemCharges(0) keeps the charges and destroys the item once the script is over.
        inv.SetCharges(wand, 0);
        Assert.Equal(2, wand.Charges);
        Assert.False(wand.IsDestroyed);
        f.Tick();
        Assert.True(wand.IsDestroyed);
        var sword = inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        inv.SetCharges(sword, 1000);
        Assert.Equal(250, sword.Charges);
    }

    [DataFact]
    public void LoadsModuleStoreAndPlaceableInventories()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var inv = world.Inventory;
        inv.EnsureLoaded();
        var store = world.ByTag("row_alchemy").OfType<Store>().First();
        Assert.Equal(85, store.MarkDown);
        Assert.Equal(125, store.IdentifyPrice);
        Assert.Equal(1, store.MaxBuyPrice);
        Assert.Contains(62, store.WillOnlyBuy);
        Assert.True(store.Inventory.Count > 0);
        Assert.All(store.Inventory, i => Assert.Same(store, inv.Owner(i)));
        Assert.Contains(store.Inventory, i => i.Infinite && i.StorePanel == 2);

        int expected = 0, loaded = 0;
        foreach (var p in world.AllObjects.OfType<Placeable>().ToList())
        {
            expected += p.Template.GetList("ItemList").Count;
            loaded += p.Inventory.Count;
        }
        _out.WriteLine($"placeable items: {loaded} of {expected}");
        Assert.True(expected == 0 || loaded >= expected * 9 / 10);
        // Creature inventories point back at their creatures.
        var carrier = world.AllObjects.OfType<Creature>().FirstOrDefault(c => c.Inventory.Count > 0 || c.Equipment.Count > 0);
        if (carrier != null) Assert.All(inv.AllItems(carrier), i => Assert.Same(carrier, inv.Owner(i)));
    }

    /// <summary>Values measured on the original server (parity suites items_blueprints*, items_value).</summary>
    [DataFact]
    public void GoldValueMatchesOriginalServer()
    {
        var f = Make();
        var inv = f.Inv;
        Item Make1(string resref) => inv.CreateOn(resref, f.Chest, 1, "")!;
        ItemProperty Spell(int spell, int uses) => new() { Type = 15, SubType = spell, CostTable = 3, CostTableValue = uses, DurationType = 2 };

        // +1 battleaxe: (10 + int(1000 * 0.9^2)) * 2 in single precision.
        var axe = Make1("nw_waxbt001");
        axe.Properties.Add(new ItemProperty { Type = 6, CostTable = 2, CostTableValue = 1, DurationType = 2 });
        Assert.Equal(1638, inv.Rules.GoldValue(axe));
        axe.Identified = false;
        Assert.Equal(20, inv.Rules.GoldValue(axe));
        axe.Plot = true;
        Assert.Equal(0, inv.Rules.GoldValue(axe));

        // Arrows are worth 1 only as a full stack.
        var arrows = Make1("nw_wamar001");
        arrows.StackSize = 99;
        Assert.Equal(1, inv.Rules.GoldValue(arrows));
        arrows.StackSize = 98;
        Assert.Equal(0, inv.Rules.GoldValue(arrows));

        // Cast spells: dearest in full, the dearest that never led at 75%, the rest at 50%; charge uses scale with charges / 50.
        var sword = Make1("nw_wswls001");
        sword.Charges = 50;
        foreach (var spell in new[] { 180, 241, 2, 33 }) sword.Properties.Add(Spell(spell, 6));
        sword.Reprice();
        Assert.Equal(198904, inv.Rules.GoldValue(sword));
        var other = Make1("nw_wswls001");
        other.Charges = 50;
        foreach (var spell in new[] { 33, 2, 180, 241, 1 }) other.Properties.Add(Spell(spell, 6));
        other.Reprice();
        Assert.Equal(200030, inv.Rules.GoldValue(other));
        var charged = Make1("nw_wswls001");
        charged.Charges = 53;
        charged.Properties.Add(Spell(180, 6));
        charged.Reprice();
        Assert.Equal(190828, inv.Rules.GoldValue(charged));
        // SetItemCharges doesn't reprice; adding a property does.
        inv.SetCharges(charged, 1);
        Assert.Equal(190828, inv.Rules.GoldValue(charged));
        charged.Reprice();
        Assert.Equal(3630, inv.Rules.GoldValue(charged));
    }

    /// <summary>Equip restrictions measured on the original server (items_equip).</summary>
    [DataFact]
    public void EquipRestrictionsLikeTheOriginal()
    {
        var f = Make();
        var inv = f.Inv;
        var spear = inv.CreateOn("nw_wplss001", f.Pc, 1, "")!;
        Assert.True(inv.Equip(f.Pc, spear, 5)); // a two-hander meant for the off hand goes to the main hand
        Assert.Same(spear, f.Pc.Equipment[EquipSlot.RightHand]);
        var dagger = inv.CreateOn("nw_wswdg001", f.Pc, 1, "")!;
        Assert.True(inv.Equip(f.Pc, dagger, 4));
        var sword = inv.CreateOn("nw_wswls001", f.Pc, 1, "")!;
        Assert.True(inv.Equip(f.Pc, sword, 5));
        Assert.False(inv.Equip(f.Pc, dagger, 5)); // equipped items only move into empty slots
        var gnome = new Creature { Tag = "gnome", Size = 2 };
        gnome.Feats.Add(46);
        f.World.AddToArea(gnome, f.Area, new Vector3(9, 9, 0), 0);
        var staff = inv.CreateOn("nw_wdbqs001", gnome, 1, "")!;
        Assert.False(inv.Equip(gnome, staff, 4));  // too big for a small creature
        var longsword = inv.CreateOn("nw_wswls001", gnome, 1, "")!;
        Assert.False(inv.Equip(gnome, longsword, 4)); // no martial proficiency
        var heavy = inv.CreateOn("nw_aarcl007", gnome, 1, "")!;
        Assert.False(inv.Equip(gnome, heavy, 1));
    }

    /// <summary>CreateItemOnObject and inventory iteration as measured on the original server (items_inventory).</summary>
    [DataFact]
    public void CreateItemOnObjectLikeTheOriginal()
    {
        var f = Make();
        var inv = f.Inv;
        Assert.Null(inv.CreateOn("nw_wamar001", f.Chest, 0, ""));
        var big = inv.CreateOn("nw_wamar001", f.Chest, 250, "")!;
        Assert.Equal(99, big.StackSize);
        var sword = inv.CreateOn("nw_wswls001", f.Chest, 1, "")!;
        Assert.Same(sword, inv.Items(f.Chest)[0]); // newest first
        // A full backpack: the item is possessed but not listed.
        for (int i = 0; i < 30; i++) inv.CreateOn("nw_ashto001", f.Pc, 1, "");
        var extra = inv.CreateOn("nw_ashto001", f.Pc, 1, "")!;
        Assert.Same(f.Pc, inv.Owner(extra));
        Assert.Null(extra.Area);
        Assert.DoesNotContain(extra, inv.Items(f.Pc));
        Assert.Equal(30, inv.Items(f.Pc).Count);
        // A bag in a bag exists but is nowhere.
        var bag = inv.CreateOn("nw_it_contain001", f.Pc, 1, "")!;
        var inner = inv.CreateOn("nw_it_contain001", bag, 1, "")!;
        Assert.Null(inv.Holder(inner));
        Assert.Null(inner.Area);
        // Placeables fill 7 x 5 pages; GetBaseItemFitsInInventory only looks at the last one.
        var chest = new Placeable { Tag = "fits", HasInventory = true };
        f.World.AddToArea(chest, f.Area, new Vector3(12, 12, 0), 0);
        for (int i = 0; i < 3; i++) inv.CreateOn("nw_ashto001", chest, 1, "");
        Assert.False(inv.BaseItemFits(chest, 57));
        inv.CreateOn("nw_ashto001", chest, 1, "");
        Assert.True(inv.BaseItemFits(chest, 57));
    }

    /// <summary>ItemPropertyCustom validation and itemprops.2da restrictions, as measured on the original server (items_props, items_value*).</summary>
    [DataFact]
    public void ItemPropertiesValidateLikeTheOriginal()
    {
        var f = Make();
        var w = f.World;
        string Key(ItemProperty ip) => $"{ip.Type}.{ip.SubType}.{ip.CostTable}.{ip.CostTableValue}.{ip.Param1}.{ip.Param1Value}.{ip.DurationType}";
        Assert.Equal("35.-1.0.-1.-1.-1.2", Key(ItemPropertyRules.Make(w, 35)));             // haste
        Assert.Equal("35.5.0.-1.-1.-1.2", Key(ItemPropertyRules.Make(w, 35, 5)));           // no subtype table: kept
        Assert.False(ItemPropertyRules.Make(w, 35, -1, 0).Valid);                           // no cost table: cost must be -1
        Assert.Equal("17.1.4.4.-1.14.2", Key(ItemPropertyRules.Make(w, 17, 1, 4, 14)));
        Assert.False(ItemPropertyRules.Make(w, 17, 1, 4, 15).Valid);
        Assert.False(ItemPropertyRules.Make(w, 17, 1, 4).Valid);                            // param required
        Assert.True(ItemPropertyRules.Make(w, 48, 2, 3).Valid);                             // ...unless it comes from the subtype
        Assert.False(ItemPropertyRules.Make(w, 0, 1, 0).Valid);                             // cost row without a Name
        Assert.False(ItemPropertyRules.Make(w, 4, 28, 1).Valid);                            // INVALID_RACE
        Assert.Equal("83.3.-1.-1.-1.-1.2", Key(ItemPropertyRules.Make(w, 83, 3)));          // no cost table at all
        Assert.True(ItemPropertyRules.Make(w, 999).Valid);
        var rules = f.Inv.Rules;
        Assert.False(rules.AllowsProperty(1, 45));  // mighty on a longsword
        Assert.True(rules.AllowsProperty(8, 45));   // ...on a longbow
        Assert.False(rules.AllowsProperty(1, 999));
    }

    [DataFact]
    public void TorchVisualCarriesItsLight()
    {
        // The standard torch's Light property (yellow, bright) travels with its equipped look; a sword has none.
        var world = new World(TestData.Module);
        var torch = world.Items.FromBlueprint("nw_it_torch001")!;
        var v = ItemFactory.Visual(0x20, torch);
        Assert.Equal((byte)1, v.LightColor);
        Assert.Equal((byte)4, v.LightLevel);
        var w = new MessageWriter(MessageType.Appearance);
        new Appearance { Equipment = { v } }.Write(w);
        var r = new MessageReader(w.ToArray());
        var back = Appearance.Read(r).Equipment.Single();
        Assert.Equal((v.LightColor, v.LightLevel, v.BaseItem), (back.LightColor, back.LightLevel, back.BaseItem));
        Assert.Equal(ItemVisual.NoLight, ItemFactory.Visual(0x10, world.Items.FromBlueprint("nw_wswls001")!).LightColor);
    }

    [DataFact]
    public void GoldValueMatchesToolsetCosts()
    {
        var world = new World(TestData.Module);
        var rules = world.Inventory.Rules;
        int n = 0, ok = 0;
        foreach (var key in world.Resources.All(ResType.Uti).ToList())
        {
            var item = world.Items.FromBlueprint(key.ResRef);
            // The game prices creature items (no BaseCost) at 0; the toolset doesn't.
            if (item == null || world.Resources.TwoDA("baseitems").IsEmpty(item.BaseItem, "BaseCost") && item.BaseItem != ItemRules.BaseItemArmor) continue;
            n++;
            // The toolset caches the value (including AddCost) in Cost, as if identified and not plot.
            item.Identified = true;
            item.Plot = false;
            if (Math.Abs(rules.GoldValue(item) - item.Cost) <= Math.Max(1, item.Cost / 50)) ok++;
        }
        _out.WriteLine($"{ok} of {n} blueprint values match");
        Assert.True(n == 0 || ok >= n * 85 / 100);
    }
}
