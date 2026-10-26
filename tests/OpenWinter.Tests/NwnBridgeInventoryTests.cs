using System.Numerics;
using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateMessages;

namespace OpenWinter.Tests;

/// <summary>How the NWN bridge keeps the player's items: inventory, containers, stores and gold.</summary>
public class NwnBridgeInventoryTests
{
    const uint Pc = 0x7FFFFFFF;

    /// <summary>A few baseitems rows: longsword (1), armour (16), dagger (22), potion (49), large box (66).</summary>
    sealed class Data : INwnBridgeData
    {
        static readonly Dictionary<int, Dictionary<string, string>> BaseItems = new()
        {
            [1] = new() { ["ItemClass"] = "WSwLs", ["ModelType"] = "2", ["InvSlotWidth"] = "1", ["InvSlotHeight"] = "4", ["EquipableSlots"] = "0x1c030", ["Category"] = "1", ["WeaponType"] = "3", ["TenthLBS"] = "40", ["Name"] = "1", ["StorePanel"] = "1" },
            [16] = new() { ["ItemClass"] = "", ["ModelType"] = "3", ["DefaultIcon"] = "iit_armor", ["EquipableSlots"] = "0x00002", ["StorePanel"] = "0" },
            [22] = new() { ["ItemClass"] = "WSwDg", ["ModelType"] = "2", ["InvSlotWidth"] = "1", ["InvSlotHeight"] = "2", ["EquipableSlots"] = "0x1c030", ["Category"] = "1", ["WeaponType"] = "1", ["NumDice"] = "1", ["DieToRoll"] = "4", ["TenthLBS"] = "10", ["Name"] = "22", ["StorePanel"] = "1" },
            [49] = new() { ["ItemClass"] = "it_potion", ["ModelType"] = "0", ["Category"] = "9", ["TenthLBS"] = "1", ["Stacking"] = "10", ["Name"] = "49", ["StorePanel"] = "2" },
            [66] = new() { ["ItemClass"] = "it_bigbox", ["ModelType"] = "0", ["Container"] = "1", ["InvSlotWidth"] = "2", ["InvSlotHeight"] = "2", ["Name"] = "66", ["StorePanel"] = "4" },
        };

        public int TwoDARowCount(string table) => table == "baseitems" ? 100 : 0;
        public string TwoDA(string table, int row, string column) =>
            table == "baseitems" && BaseItems.TryGetValue(row, out var r) ? r.GetValueOrDefault(column, "") : "";
        public string Text(uint strRef, byte gender) => strRef switch { 1 => "Longsword", 22 => "Dagger", 49 => "Potion", 191 => "Dagger", _ => $"#{strRef}" };
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    sealed class Host
    {
        public readonly List<NwnMessage> Sent = [];
        public readonly List<NotifyMessage> Notices = [];
        public readonly NwnInventory Inventory;
        public double Now;

        public Host() => Inventory = new NwnInventory(new Data(), new NwnInventoryHost(Sent.Add,
            b => Notices.Add(NotifyMessage.Read(new MessageReader(b))), () => Pc, () => new Vector3(20, 21, 0), id => id == 104 ? "Remains" : "Chest"));

        /// <summary>Lets the bridge's waits run out, as frames pass.</summary>
        public void Pass(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.1) Inventory.Tick(Now += 0.1);
        }

        public JsonElement Last(string kind) => JsonDocument.Parse(Notices.Last(n => n.Kind == kind).Json).RootElement;
    }

    static NwnSessionContext Context() => new(t => t == "baseitems" ? 100 : 0, (t, row, col) => new Data().TwoDA(t, row, col));

    /// <summary>A recorded frame ("P", major, minor, fields) read as its message.</summary>
    static T Frame<T>(string hex) where T : NwnMessage, new()
    {
        var m = new T();
        m.Read(new NwnWsReader(Convert.FromHexString(hex).AsMemory(3), Context()));
        return m;
    }

    static string Bytes(NwnMessage m)
    {
        var w = new NwnWsWriter(Context());
        m.Write(w);
        return "70" + m.Major.ToString("x2") + m.Minor.ToString("x2") + Convert.ToHexString(w.ToArray()).ToLowerInvariant();
    }

    static ItemAppearanceAndProps Item(uint baseItem, uint strRef, uint stack = 1, uint cost = 10, bool identified = true) => new()
    {
        BaseItemId = baseItem,
        ModelData = baseItem switch
        {
            1 or 22 => new NwnTagged(2, new CompositeModel { Part1 = 11, Part2 = 21, Part3 = 31 }),
            _ => new NwnTagged(0, new SimpleModel { Part1 = 4 }),
        },
        Name = new NwnTagged(1, new OriginalItemName { Name = NwnLocString.FromStrRef(strRef, 0) }),
        Identified = identified, Cost = cost, StackSize = stack,
    };

    static GameObjUpdateObjList Gui(params (char Element, char Op, INwnStruct Value)[] entries)
    {
        var list = new GameObjUpdateObjList();
        foreach (var e in entries) list.Entries.Add(new ObjListGuiEntry(e.Element, e.Op, e.Value));
        return list;
    }

    [Fact]
    public void OpeningTheInventoryLooksAtEveryBackpackPageThenIntoBags()
    {
        var h = new Host();
        Assert.True(h.Inventory.FromClient("inventory", "{}"));
        // The inventory opens (equipment and the shown page follow), then page 1 is shown.
        var open = Assert.IsType<GuiInventoryMessages.GuiInventoryStatusC2S>(h.Sent[0]);
        Assert.Equal((true, Pc), (open.Active, open.Inventory));
        Assert.Equal(1, Assert.IsType<GuiInventoryMessages.GuiInventorySelectPanelC2S>(h.Sent[1]).Panel);
        h.Inventory.ObjectList(Gui(('I', 'A', new GuiInventorySlotAdd { Slot = 0x10, ItemId = 10, Item = Item(1, 1) }),
            ('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 7, ItemId = 11, Item = Item(22, 22) })));
        h.Pass(0.2);
        // Page 1 answered: page 2 is shown; the empty pages wait a moment each, and the last shown is the first.
        Assert.Equal(2, ((GuiInventoryMessages.GuiInventorySelectPanelC2S)h.Sent[^1]).Panel);
        h.Pass(1.8);
        Assert.Equal(0, ((GuiInventoryMessages.GuiInventorySelectPanelC2S)h.Sent[^1]).Panel);
        h.Inventory.ObjectList(Gui(('R', 'A', new GuiRepositoryItemAdd { X = 3, Y = 0, ItemId = 12, Item = Item(66, 66) })));
        h.Pass(0.3);
        // Then the bag is opened, its contents read, and it is closed again.
        Assert.Equal(12u, Assert.IsType<GuiContainerMessages.GuiContainerStatus>(h.Sent[^1]).Container);
        h.Inventory.ObjectList(Gui(('C', 'A', new GuiContainerItemAdd { X = 0, Y = 0, ItemId = 13, Item = Item(49, 49, stack: 3) })));
        h.Pass(0.6);
        Assert.Equal(InvalidObjectId, Assert.IsType<GuiContainerMessages.GuiContainerStatus>(h.Sent[^1]).Container);

        var view = h.Last("inventory-open");
        var equipped = Assert.Single(view.GetProperty("equipped").EnumerateArray());
        Assert.Equal((10u, 4, "Longsword"), (equipped.GetProperty("id").GetUInt32(), equipped.GetProperty("slot").GetInt32(), equipped.GetProperty("name").GetString()));
        Assert.Equal(["iwswls_b_011", "iwswls_m_021", "iwswls_t_031"], equipped.GetProperty("icon").EnumerateArray().Select(i => i.GetString()));
        var items = view.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetUInt32());
        Assert.Equal((3, 0, Pc), (items[12].GetProperty("x").GetInt32(), items[12].GetProperty("y").GetInt32(), items[12].GetProperty("container").GetUInt32()));
        Assert.Equal((0, 7), (items[11].GetProperty("x").GetInt32(), items[11].GetProperty("y").GetInt32()));
        Assert.Equal((12u, 3), (items[13].GetProperty("container").GetUInt32(), items[13].GetProperty("stack").GetInt32()));
        var info = items[11].GetProperty("info");
        Assert.Equal(("weapon", 1, 2, "Piercing"), (info.GetProperty("category").GetString(), info.GetProperty("width").GetInt32(),
            info.GetProperty("height").GetInt32(), info.GetProperty("damageType").GetString()));
        Assert.Equal("container", items[12].GetProperty("info").GetProperty("category").GetString());
    }

    [Fact]
    public void TakingFromABodyBagMovesTheItemIntoTheBackpack()
    {
        var h = new Host();
        h.Inventory.FromClient("inventory", "{}");
        h.Pass(3);
        // As recorded: the body bag opens with a dagger in it.
        h.Inventory.ToClient(Frame<GuiContainerMessages.GuiContainerStatusS2C>("50190111680000000201"));
        h.Inventory.ObjectList(Gui(('C', 'A', new GuiContainerItemAdd { X = 0, Y = 0, ItemId = 0x67, Item = Item(22, 191, cost: 4) })));
        var bag = h.Last("container-open");
        Assert.Equal((104u, "Remains"), (bag.GetProperty("container").GetUInt32(), bag.GetProperty("name").GetString()));
        Assert.Equal(0x67u, Assert.Single(bag.GetProperty("items").EnumerateArray()).GetProperty("id").GetUInt32());

        h.Sent.Clear();
        h.Inventory.FromClient("container-take", """{"item":103}""");
        // The move nwmain sends: the item, into the player's inventory, wherever it fits.
        Assert.Equal("700c09116700000011ffffff7f03ff03ff", Bytes(Assert.Single(h.Sent)));
        // As recorded: gold and weight, the dagger off the body bag and into the backpack at 2,5.
        h.Inventory.ToClient(Frame<InventoryMessages.InventoryRepositoryMove>("500c0a11670000000200"));
        h.Inventory.ObjectList(Frame<GameObjUpdateObjList>(
            "500501044911ffffff7f050100060d0007e616000008840c0000020004470443044411670000000447045204410302030511670000000716000000050b00051500050b0003000800000000"
            + "08000000000201120100bf000000020107040000000701000000020102000200020008000000000e000000000300030003ff03000300030003000300030003000300"));
        Assert.Empty(h.Last("container-open").GetProperty("items").EnumerateArray());
        var dagger = h.Last("inventory-open").GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetUInt32() == 0x67);
        Assert.Equal(("Dagger", 2, 5), (dagger.GetProperty("name").GetString(), dagger.GetProperty("x").GetInt32(), dagger.GetProperty("y").GetInt32()));
        Assert.Equal(5862, h.Last("gold-changed").GetProperty("gold").GetInt32());
    }

    [Fact]
    public void RequestsBecomeTheMessagesNwmainSends()
    {
        var h = new Host();
        h.Inventory.FromClient("inventory", "{}");
        h.Inventory.ObjectList(Gui(('R', 'A', new GuiRepositoryItemAdd { X = 9, Y = 1, ItemId = 0x7c, Item = Item(1, 1) })));
        h.Pass(3);
        h.Sent.Clear();
        // Equipping into the first slot the base item allows (the right hand), as recorded.
        h.Inventory.FromClient("equip", """{"item":124}""");
        Assert.Equal("700c01117c00000011ffffff7f0710000000", Bytes(h.Sent[^1]));
        h.Inventory.FromClient("store-buy", """{"item":11}""");
        Assert.Equal("700701110b00000003ff03ff11ffffff7f", Bytes(h.Sent[^1]));
        h.Inventory.FromClient("drop", """{"item":124}""");
        Assert.Equal((0x7cu, (20f, 21f, 0f)), (((InventoryMessages.InventoryDropC2S)h.Sent[^1]).Item, ((InventoryMessages.InventoryDropC2S)h.Sent[^1]).Position));
        h.Inventory.FromClient("item-split", """{"item":124,"count":3}""");
        Assert.Equal(3, ((InputMessages.InputSplitItem)h.Sent[^1]).Count);
        Assert.False(h.Inventory.FromClient("area-ready", "{}"));
        // The server's confirmation puts the sword in hand.
        h.Inventory.ToClient(Frame<InventoryMessages.InventoryEquip>("500c01117c00000002000710000000"));
        Assert.Equal(4, h.Notices.Last(n => n.Kind == "creature-equipment") is var n ? JsonDocument.Parse(n.Json).RootElement.GetProperty("slot").GetInt32() : -1);
        h.Pass(3);
        Assert.Equal(0x7cu, Assert.Single(h.Last("inventory-open").GetProperty("equipped").EnumerateArray()).GetProperty("id").GetUInt32());
    }

    [Fact]
    public void AStoreListsEveryCategoryWithThePlayersPrices()
    {
        var h = new Host();
        // As recorded: a general store's terms (sells at 150%, buys at 35%, identifies for 100).
        h.Inventory.ToClient(Frame<StoreMessages.StoreOpenPanelS2C>("500704030006960003230319086400000008ffffffff0888130000020002000800000000"));
        h.Inventory.ObjectList(Gui(('M', 'P', new GuiStorePages { NumPanels = 1, CurrentPage = 0 }),
            ('M', 'A', new GuiStoreItemAdd { X = 0, Y = 0, ItemId = 21, Item = Item(16, 5, cost: 100) })));
        // Each other category is shown in turn; switching takes the last one's items off the panel.
        var views = new List<int>();
        for (int panel = 1; panel <= 4; panel++)
        {
            h.Pass(0.5);
            views.Add(((StoreMessages.StoreOpenPanelC2S)h.Sent.Last(m => m is StoreMessages.StoreOpenPanelC2S)).Panel);
            h.Inventory.ObjectList(Gui(('M', 'P', new GuiStorePages { NumPanels = 1, CurrentPage = (byte)panel }),
                ('M', 'D', new GuiStoreItemDelete { ItemId = (uint)(20 + panel) }),
                ('M', 'A', new GuiStoreItemAdd { X = 0, Y = 0, ItemId = (uint)(21 + panel), Item = Item(panel == 2 ? 49u : 22u, 49, cost: 30) })));
        }
        Assert.Equal([1, 2, 3, 4], views);
        h.Pass(0.5);
        Assert.Equal(0, ((StoreMessages.StoreOpenPanelC2S)h.Sent[^1]).Panel);
        h.Inventory.ObjectList(Gui(('M', 'P', new GuiStorePages { NumPanels = 1, CurrentPage = 0 }),
            ('M', 'D', new GuiStoreItemDelete { ItemId = 25 }), ('M', 'A', new GuiStoreItemAdd { X = 0, Y = 0, ItemId = 21, Item = Item(16, 5, cost: 100) })));
        h.Pass(1);

        var store = h.Last("store-open");
        Assert.Equal((150, 35, 100, 5000), (store.GetProperty("markUp").GetInt32(), store.GetProperty("markDown").GetInt32(),
            store.GetProperty("identifyCost").GetInt32(), store.GetProperty("gold").GetInt32()));
        var stock = store.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetUInt32());
        Assert.Equal(new uint[] { 21, 22, 23, 24, 25 }, stock.Keys.Order());
        Assert.Equal((0, 150), (stock[21].GetProperty("panel").GetInt32(), stock[21].GetProperty("price").GetInt32()));
        Assert.Equal((2, 45), (stock[23].GetProperty("panel").GetInt32(), stock[23].GetProperty("price").GetInt32()));
        // NWN's category 1 is weapons (the client's 4), its 4 miscellaneous (the client's 1).
        Assert.Equal((4, 1), (stock[22].GetProperty("panel").GetInt32(), stock[25].GetProperty("panel").GetInt32()));
        // Bought from the panel shown: it comes off.
        h.Inventory.ObjectList(Gui(('M', 'D', new GuiStoreItemDelete { ItemId = 21 })));
        // As recorded: the sale's confirmation with the store's gold; the categories are looked through again.
        h.Inventory.ToClient(Frame<StoreMessages.StoreConfirmTransaction>("500703110b0000000897130000"));
        h.Pass(8);
        store = h.Last("store-open");
        Assert.Equal(5015, store.GetProperty("gold").GetInt32());
        Assert.DoesNotContain(store.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetUInt32() == 21);
    }

    [Fact]
    public void BarterShowsBothOffersAndEndsWhenBothAccept()
    {
        // Played as the player who asked (0x7FFFFFFE) with the other player (0x7FFFFFFF), as recorded.
        uint me = 0x7FFFFFFE;
        var sent = new List<NwnMessage>();
        var notices = new List<NotifyMessage>();
        var inventory = new NwnInventory(new Data(), new NwnInventoryHost(sent.Add, b => notices.Add(NotifyMessage.Read(new MessageReader(b))),
            () => me, () => Vector3.Zero, _ => "Aluvian"));
        JsonElement State() => JsonDocument.Parse(notices.Last(n => n.Kind == "barter-state").Json).RootElement;

        inventory.FromClient("barter-request", """{"target":2147483647}""");
        Assert.Equal("70230111ffffff7f110000007f", Bytes(sent[^1]));
        inventory.ToClient(Frame<BarterMessages.BarterAcptStartBarter>("50230211feffff7f11ffffff7f"));
        Assert.Equal(0x7FFFFFFFu, State().GetProperty("theirs").GetProperty("id").GetUInt32());
        // Fifty gold into the offer, as nwmain puts it there; it comes back as a gold pile on the player's side.
        inventory.FromClient("barter-gold", """{"gold":50}""");
        Assert.Equal("70080111feffff7f0832000000020103ff03ff", Bytes(sent[^1]));
        inventory.ObjectList(Gui(('B', 'A', new GuiRepositoryItemAdd { ItemId = 160, Item = Item(76, 535, stack: 50) }),
            ('A', 'A', new GuiRepositoryItemAdd { ItemId = 121, Item = Item(49, 49, stack: 9) })));
        var state = State();
        Assert.Equal((50, 0), (state.GetProperty("mine").GetProperty("gold").GetInt32(), state.GetProperty("mine").GetProperty("items").GetArrayLength()));
        Assert.Equal(121u, Assert.Single(state.GetProperty("theirs").GetProperty("items").EnumerateArray()).GetProperty("id").GetUInt32());
        // Locking and accepting name the side by whether it asked.
        inventory.ToClient(Frame<BarterMessages.BarterAcptLockList>("50230802010201"));
        inventory.ToClient(Frame<BarterMessages.BarterAcptLockList>("50230802000201"));
        inventory.ToClient(Frame<BarterMessages.BarterAcptAcceptTrade>("50230a02000201"));
        state = State();
        Assert.Equal((true, false), (state.GetProperty("mine").GetProperty("offered").GetBoolean(), state.GetProperty("mine").GetProperty("accepted").GetBoolean()));
        Assert.Equal((true, true), (state.GetProperty("theirs").GetProperty("offered").GetBoolean(), state.GetProperty("theirs").GetProperty("accepted").GetBoolean()));
        inventory.ToClient(Frame<BarterMessages.BarterAcptCloseBarter>("5023040201"));
        Assert.False(JsonDocument.Parse(notices.Last(n => n.Kind == "barter-close").Json).RootElement.GetProperty("cancelled").GetBoolean());
    }
}
