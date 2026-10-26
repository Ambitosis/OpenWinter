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

    /// <summary>A few baseitems rows: longsword (1), armour (16), dagger (22), potion (49), ring (52), large box (66).</summary>
    sealed class Data : INwnBridgeData
    {
        static readonly Dictionary<int, Dictionary<string, string>> BaseItems = new()
        {
            [1] = new() { ["ItemClass"] = "WSwLs", ["ModelType"] = "2", ["InvSlotWidth"] = "1", ["InvSlotHeight"] = "4", ["EquipableSlots"] = "0x1c030", ["Category"] = "1", ["WeaponType"] = "3", ["TenthLBS"] = "40", ["Name"] = "1", ["StorePanel"] = "1" },
            [16] = new() { ["ItemClass"] = "", ["ModelType"] = "3", ["DefaultIcon"] = "iit_armor", ["EquipableSlots"] = "0x00002", ["StorePanel"] = "0" },
            [22] = new() { ["ItemClass"] = "WSwDg", ["ModelType"] = "2", ["InvSlotWidth"] = "1", ["InvSlotHeight"] = "2", ["EquipableSlots"] = "0x1c030", ["Category"] = "1", ["WeaponType"] = "1", ["NumDice"] = "1", ["DieToRoll"] = "4", ["TenthLBS"] = "10", ["Name"] = "22", ["StorePanel"] = "1" },
            [49] = new() { ["ItemClass"] = "it_potion", ["ModelType"] = "0", ["Category"] = "9", ["TenthLBS"] = "1", ["Stacking"] = "10", ["Name"] = "49", ["StorePanel"] = "2" },
            [52] = new() { ["ItemClass"] = "it_ring", ["ModelType"] = "0", ["EquipableSlots"] = "0x180", ["Name"] = "52", ["StorePanel"] = "3" },
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

        public Host(uint me = Pc) => Inventory = new NwnInventory(new Data(), new NwnInventoryHost(Sent.Add,
            b => Notices.Add(NotifyMessage.Read(new MessageReader(b))), () => me, () => new Vector3(20, 21, 0), id => id == 104 ? "Remains" : "Chest"));

        /// <summary>Lets the bridge's waits run out, as frames pass.</summary>
        public void Pass(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.1) Inventory.Tick(Now += 0.1);
        }

        /// <summary>The player opens the inventory; the server answers with the equipment and the page shown, then
        /// the bridge looks through the other pages and the bags.</summary>
        public void Open(params (char Element, char Op, INwnStruct Value)[] answer)
        {
            Inventory.FromClient("inventory", "{}");
            Inventory.ObjectList(Gui(answer));
            Pass(3);
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

    static List<int> BackpackPagesShown(Host h) => h.Sent.OfType<GuiInventoryMessages.GuiInventorySelectPanelC2S>().Select(m => (int)m.Panel).ToList();

    [Fact]
    public void OpeningTheInventoryLooksAtEveryBackpackPageThenIntoBags()
    {
        var h = new Host();
        Assert.True(h.Inventory.FromClient("inventory", "{}"));
        // The inventory is opened anew (the equipment and the shown page follow).
        Assert.Equal([(false, Pc), (true, Pc)], h.Sent.Cast<GuiInventoryMessages.GuiInventoryStatusC2S>().Select(m => (m.Active, m.Inventory)));
        h.Inventory.ObjectList(Gui(('I', 'A', new GuiInventorySlotAdd { Slot = 0x10, ItemId = 10, Item = Item(1, 1) }),
            ('R', 'A', new GuiRepositoryItemAdd { X = 3, Y = 0, ItemId = 12, Item = Item(66, 66) })));
        h.Pass(0.4);
        Assert.Equal(1, ((GuiInventoryMessages.GuiInventorySelectPanelC2S)h.Sent[^1]).Panel);
        // Nothing shows before the whole look is done.
        Assert.DoesNotContain(h.Notices, n => n.Kind == "inventory-open");
        h.Inventory.ObjectList(Gui(('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 7, ItemId = 11, Item = Item(22, 22) })));
        h.Pass(0.2);
        // Page 1 answered: page 2 is shown; the empty pages wait a moment each, and the last shown is the first.
        Assert.Equal(2, ((GuiInventoryMessages.GuiInventorySelectPanelC2S)h.Sent[^1]).Panel);
        h.Pass(1.8);
        Assert.Equal(0, ((GuiInventoryMessages.GuiInventorySelectPanelC2S)h.Sent[^1]).Panel);
        h.Pass(0.4);
        // Then the bag is opened and its contents read; it stays open on the server.
        Assert.Equal(12u, Assert.IsType<GuiContainerMessages.GuiContainerStatus>(h.Sent[^1]).Container);
        h.Inventory.ObjectList(Gui(('C', 'A', new GuiContainerItemAdd { X = 0, Y = 0, ItemId = 13, Item = Item(49, 49, stack: 3) })));
        h.Pass(0.6);
        Assert.IsType<GuiContainerMessages.GuiContainerStatus>(h.Sent[^1]);
        Assert.Single(h.Notices, n => n.Kind == "inventory-open");

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
    public void ConfirmedChangesAreGatheredIntoOneLookAndWhatItMissesIsGone()
    {
        var h = new Host();
        h.Open(('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 0, ItemId = 20, Item = Item(22, 22) }),
            ('R', 'A', new GuiRepositoryItemAdd { X = 1, Y = 0, ItemId = 21, Item = Item(49, 49) }));
        h.Sent.Clear();
        // The server's order: the acknowledgement and its feedback, then the object list a moment later.
        h.Inventory.ToClient(Frame<InventoryMessages.InventoryRepositoryMove>("500c0a11670000000200"));
        h.Inventory.Feedback(new ClientSideMessageFeedbackMessages.ClientSideMessageFeedback
        {
            Payload = new NwnTagged(0, new ClientSideMessageFeedbackMessages.FeedbackItemReceived()),
        });
        h.Pass(0.1);
        h.Inventory.ObjectList(Gui(('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 0, ItemId = 20, Item = Item(22, 22) })));
        h.Pass(3);
        // One look: pages 1 to 5, then the first; the potion it didn't see again is gone.
        Assert.Equal([1, 2, 3, 4, 5, 0], BackpackPagesShown(h));
        Assert.DoesNotContain(h.Last("inventory-open").GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetUInt32() == 21);
    }

    [Fact]
    public void AnInventoryTheServerClosesStaysClosedUntilThePlayerOpensIt()
    {
        var h = new Host();
        h.Open();
        h.Sent.Clear();
        h.Inventory.ToClient(new GuiInventoryMessages.GuiInventoryStatus { Active = false, Inventory = Pc });
        h.Inventory.ToClient(Frame<InventoryMessages.InventoryRepositoryMove>("500c0a11670000000200"));
        h.Pass(3);
        Assert.Equal([], h.Sent.Select(m => m.GetType().Name));
        h.Inventory.FromClient("inventory", "{}");
        Assert.Contains(h.Sent, m => m is GuiInventoryMessages.GuiInventoryStatusC2S { Active: true });
    }

    [Fact]
    public void AnotherAreaClosesWhatWasOpenAndLooksAgainOnArrival()
    {
        var h = new Host();
        h.Open();
        h.Inventory.ToClient(Frame<GuiContainerMessages.GuiContainerStatusS2C>("50190111680000000201"));
        h.Sent.Clear();
        h.Inventory.AreaChanged();
        Assert.Equal(104u, h.Notices.Last(n => n.Kind == "container-close").Subject);
        h.Pass(1);
        Assert.Equal([], h.Sent.Select(m => m.GetType().Name));
        h.Pass(3);
        Assert.Equal([(false, Pc), (true, Pc)], h.Sent.OfType<GuiInventoryMessages.GuiInventoryStatusC2S>().Select(m => (m.Active, m.Inventory)));
    }

    [Fact]
    public void PossessionStartsAfreshButTheMomentWithoutACreatureDoesNot()
    {
        uint me = Pc;
        var sent = new List<NwnMessage>();
        var notices = new List<NotifyMessage>();
        var inventory = new NwnInventory(new Data(), new NwnInventoryHost(sent.Add, b => notices.Add(NotifyMessage.Read(new MessageReader(b))),
            () => me, () => Vector3.Zero, _ => ""));
        double now = 0;
        void Pass() { for (int i = 0; i < 30; i++) inventory.Tick(now += 0.1); }
        inventory.FromClient("inventory", "{}");
        inventory.ObjectList(Gui(('R', 'A', new GuiRepositoryItemAdd { ItemId = 20, Item = Item(22, 22) })));
        Pass();
        sent.Clear();
        // As recorded at an area change: control passes to no object, then back.
        me = InvalidObjectId;
        Pass();
        me = Pc;
        Pass();
        Assert.Empty(sent);
        // A familiar is possessed: the player's items are not its items, and its inventory is looked at.
        me = 0x7FFFFFF0;
        Pass();
        Assert.Contains(sent, m => m is GuiInventoryMessages.GuiInventoryStatusC2S { Active: true, Inventory: 0x7FFFFFF0 });
        Assert.Empty(JsonDocument.Parse(notices.Last(n => n.Kind == "inventory-open").Json).RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public void AContainerOpenedDuringABagReadKeepsTheBagAndBagsAreReadAgainAfter()
    {
        var h = new Host();
        h.Open(('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 0, ItemId = 12, Item = Item(66, 66) }));
        Assert.Equal(12u, ((GuiContainerMessages.GuiContainerStatus)h.Sent[^1]).Container);
        h.Inventory.ObjectList(Gui(('C', 'A', new GuiContainerItemAdd { ItemId = 13, Item = Item(49, 49) })));
        h.Pass(0.5);
        // A container closes, so the bags are read again; the player opens a body bag during that: nothing closes it.
        h.Inventory.ToClient(Frame<GuiContainerMessages.GuiContainerStatusS2C>("50190111030000000201"));
        h.Sent.Clear();
        h.Inventory.ToClient(new GuiContainerMessages.GuiContainerStatusS2C { Container = InvalidObjectId });
        Assert.Equal(12u, ((GuiContainerMessages.GuiContainerStatus)h.Sent[^1]).Container);
        h.Inventory.ToClient(Frame<GuiContainerMessages.GuiContainerStatusS2C>("50190111680000000201"));
        h.Inventory.ObjectList(Gui(('C', 'A', new GuiContainerItemAdd { ItemId = 0x67, Item = Item(22, 191) })));
        h.Pass(1);
        Assert.DoesNotContain(h.Sent, m => m is GuiContainerMessages.GuiContainerStatus { Container: InvalidObjectId });
        var items = h.Last("inventory-open").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetUInt32()).ToList();
        Assert.Contains(13u, items);
        Assert.DoesNotContain(0x67u, items);
        Assert.Single(h.Last("container-open").GetProperty("items").EnumerateArray());
    }

    [Fact]
    public void TakingFromABodyBagMovesTheItemIntoTheBackpack()
    {
        var h = new Host();
        h.Open();
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
        // The look the move starts doesn't close the inventory, which would close the body bag.
        h.Pass(3);
        Assert.DoesNotContain(h.Sent, m => m is GuiInventoryMessages.GuiInventoryStatusC2S { Active: false });
    }

    [Fact]
    public void RequestsBecomeTheMessagesNwmainSends()
    {
        var h = new Host();
        h.Open(('R', 'A', new GuiRepositoryItemAdd { X = 9, Y = 1, ItemId = 0x7c, Item = Item(1, 1) }));
        h.Sent.Clear();
        // Equipping into the first slot the base item allows (the right hand), as recorded.
        h.Inventory.FromClient("equip", """{"item":124}""");
        Assert.Equal("700c01117c00000011ffffff7f0710000000", Bytes(h.Sent[^1]));
        h.Inventory.FromClient("equip", """{"item":124,"slot":40}""");
        Assert.Equal(0x10u, ((InventoryMessages.InventoryEquipC2S)h.Sent[^1]).EquipSlot);
        h.Inventory.FromClient("store-buy", """{"item":11}""");
        Assert.Equal("700701110b00000003ff03ff11ffffff7f", Bytes(h.Sent[^1]));
        h.Inventory.FromClient("drop", """{"item":124}""");
        Assert.Equal((0x7cu, (20f, 21f, 0f)), (((InventoryMessages.InventoryDropC2S)h.Sent[^1]).Item, ((InventoryMessages.InventoryDropC2S)h.Sent[^1]).Position));
        h.Inventory.FromClient("item-split", """{"item":124,"count":70000}""");
        Assert.Equal(ushort.MaxValue, ((InputMessages.InputSplitItem)h.Sent[^1]).Count);
        Assert.False(h.Inventory.FromClient("area-ready", "not json"));
        // The server's confirmation puts the sword in hand.
        h.Inventory.ToClient(Frame<InventoryMessages.InventoryEquip>("500c01117c00000002000710000000"));
        Assert.Equal(4, JsonDocument.Parse(h.Notices.Last(n => n.Kind == "creature-equipment").Json).RootElement.GetProperty("slot").GetInt32());
        // The look that follows opens the inventory anew; the server lists the equipment again.
        h.Pass(0.2);
        h.Inventory.ObjectList(Gui(('I', 'A', new GuiInventorySlotAdd { Slot = 0x10, ItemId = 0x7c, Item = Item(1, 1) })));
        h.Pass(3);
        Assert.Equal(0x7cu, Assert.Single(h.Last("inventory-open").GetProperty("equipped").EnumerateArray()).GetProperty("id").GetUInt32());
    }

    [Fact]
    public void ASecondRingGoesOnTheOtherHandAndADisplacedRingComesBack()
    {
        var h = new Host();
        h.Open(('I', 'A', new GuiInventorySlotAdd { Slot = 0x80, ItemId = 30, Item = Item(52, 52) }),
            ('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 0, ItemId = 31, Item = Item(52, 52) }),
            ('R', 'A', new GuiRepositoryItemAdd { X = 1, Y = 0, ItemId = 32, Item = Item(52, 52) }));
        h.Inventory.FromClient("equip", """{"item":31}""");
        Assert.Equal(0x100u, ((InventoryMessages.InventoryEquipC2S)h.Sent[^1]).EquipSlot);
        // Equipped over the first ring: that ring is back in the backpack at once.
        h.Inventory.ToClient(new InventoryMessages.InventoryEquip { Item = 32, EquipSlot = 0x80 });
        var view = h.Last("inventory-open");
        Assert.Equal(32u, Assert.Single(view.GetProperty("equipped").EnumerateArray()).GetProperty("id").GetUInt32());
        Assert.Contains(view.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetUInt32() == 30);
    }

    [Fact]
    public void AnItemIsIdentifiedOnlyWhenTheStoreConfirms()
    {
        var h = new Host();
        h.Open(('R', 'A', new GuiRepositoryItemAdd { X = 0, Y = 0, ItemId = 40, Item = Item(52, 52, cost: 1, identified: false) }));
        h.Inventory.ToClient(Frame<StoreMessages.StoreOpenPanelS2C>("500704030006960003230319086400000008ffffffff0888130000020002000800000000"));
        bool Identified() => h.Last("inventory-open").GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetUInt32() == 40).GetProperty("identified").GetBoolean();
        // Refused (not enough gold): only gold and weight change.
        h.Inventory.FromClient("store-identify", """{"item":40}""");
        h.Inventory.ObjectList(new GameObjUpdateObjList
        {
            Entries = { new GameObjUpdatePlayerinfoMessages.GameObjUpdateObjListPlayerInfo { ObjectId = Pc, AcGoldWeightLevelup = new() { Gold = 50 } } },
        });
        Assert.False(Identified());
        // As recorded when it succeeds: the item's new value.
        h.Inventory.FromClient("store-identify", """{"item":40}""");
        h.Inventory.ObjectList(Gui(('R', 'U', new GuiRepositoryItemUpdate { ItemId = 40, StackSize = 1, Cost = 1000 })));
        Assert.True(Identified());
    }

    /// <summary>Answers the bridge's store views as the server does: the category's page count, the items shown before
    /// taken off, the new view's items added (y counting down the pages, ten rows each).</summary>
    sealed class StoreServer(Host h, Dictionary<int, List<(uint Id, int Page)>> stock)
    {
        int _panel, _page, _answered;
        static uint Base(int panel) => panel switch { 0 => 16u, 2 => 49u, 3 => 52u, _ => 22u };

        public void Open()
        {
            h.Inventory.ToClient(Frame<StoreMessages.StoreOpenPanelS2C>("500704030006960003230319086400000008ffffffff0888130000020002000800000000"));
            Show(0, 0, pages: true);
        }

        public void Run(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.1)
            {
                h.Pass(0.1);
                var asked = h.Sent.Skip(_answered).ToList();
                _answered = h.Sent.Count;
                foreach (var m in asked)
                {
                    if (m is StoreMessages.StoreOpenPanelC2S panel) Show(panel.Panel, 0, pages: true);
                    else if (m is StoreMessages.StoreOpenPage page) Show(_panel, page.Page, pages: false);
                }
            }
        }

        void Show(int panel, int page, bool pages)
        {
            var entries = new List<(char, char, INwnStruct)>();
            if (pages) entries.Add(('M', 'P', new GuiStorePages { NumPanels = (byte)stock[panel].Select(i => i.Page + 1).DefaultIfEmpty(1).Max(), CurrentPage = (byte)panel }));
            foreach (var old in stock[_panel].Where(i => i.Page == _page)) entries.Add(('M', 'D', new GuiStoreItemDelete { ItemId = old.Id }));
            (_panel, _page) = (panel, page);
            foreach (var item in stock[panel].Where(i => i.Page == page))
                entries.Add(('M', 'A', new GuiStoreItemAdd { X = 0, Y = (byte)(page * 10), ItemId = item.Id, Item = Item(Base(panel), 49, cost: 30) }));
            h.Inventory.ObjectList(Gui([.. entries]));
        }

        public void Bought(uint id, int panel) => stock[panel].RemoveAll(i => i.Id == id);
    }

    [Fact]
    public void AStoreListsEveryCategoryAndDropsWhatIsBought()
    {
        var h = new Host();
        var server = new StoreServer(h, new Dictionary<int, List<(uint, int)>>
        {
            [0] = [(21, 0), (26, 1)], [1] = [(22, 0)], [2] = [(23, 0)], [3] = [(24, 0)], [4] = [(25, 0)],
        });
        server.Open();
        server.Run(8);
        Assert.Equal([1, 2, 3, 4, 0], h.Sent.OfType<StoreMessages.StoreOpenPanelC2S>().Select(m => (int)m.Panel));
        Assert.Equal(1, Assert.Single(h.Sent.OfType<StoreMessages.StoreOpenPage>()).Page);

        var store = h.Last("store-open");
        Assert.Equal((150, 35, 100, 5000), (store.GetProperty("markUp").GetInt32(), store.GetProperty("markDown").GetInt32(),
            store.GetProperty("identifyCost").GetInt32(), store.GetProperty("gold").GetInt32()));
        var items = store.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetUInt32());
        Assert.Equal(new uint[] { 21, 22, 23, 24, 25, 26 }, items.Keys.Order());
        Assert.Equal((0, 45, 10), (items[26].GetProperty("panel").GetInt32(), items[26].GetProperty("price").GetInt32(), items[26].GetProperty("y").GetInt32()));
        Assert.Equal(2, items[23].GetProperty("panel").GetInt32());
        // NWN's category 1 is weapons (the client's 4), its 4 miscellaneous (the client's 1).
        Assert.Equal((4, 1), (items[22].GetProperty("panel").GetInt32(), items[25].GetProperty("panel").GetInt32()));

        // A purchase in the server's order: the confirmation first, then the object list taking it off the panel.
        h.Inventory.ToClient(Frame<StoreMessages.StoreConfirmTransaction>("500703110b0000000897130000"));
        server.Bought(21, 0);
        h.Inventory.ObjectList(Gui(('M', 'D', new GuiStoreItemDelete { ItemId = 21 })));
        server.Run(8);
        store = h.Last("store-open");
        Assert.Equal(5015, store.GetProperty("gold").GetInt32());
        Assert.Equal(new uint[] { 22, 23, 24, 25, 26 }, store.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetUInt32()).Order());
    }

    [Fact]
    public void AMalformedStoreViewDoesNotBreakTheLook()
    {
        var h = new Host();
        h.Inventory.ToClient(new StoreMessages.StoreOpenPanelS2C { Panel = 200, SellRate = 100, BuyRate = 50 });
        h.Inventory.ObjectList(Gui(('M', 'P', new GuiStorePages { NumPanels = 255, CurrentPage = 99 })));
        h.Pass(40);
        Assert.InRange(h.Sent.OfType<StoreMessages.StoreOpenPage>().Count(), 1, 64);
        Assert.Equal(5, h.Sent.OfType<StoreMessages.StoreOpenPanelC2S>().Count());
    }

    [Fact]
    public void BarterShowsBothOffersAndEndsWhenBothAccept()
    {
        // Played as the player who asked (0x7FFFFFFE) with the other player (0x7FFFFFFF), as recorded.
        var h = new Host(me: 0x7FFFFFFE);
        JsonElement State() => h.Last("barter-state");

        h.Inventory.FromClient("barter-request", """{"target":2147483647}""");
        Assert.Equal("70230111ffffff7f110000007f", Bytes(h.Sent[^1]));
        h.Inventory.ToClient(Frame<BarterMessages.BarterAcptStartBarter>("50230211feffff7f11ffffff7f"));
        Assert.Equal(0x7FFFFFFFu, State().GetProperty("theirs").GetProperty("id").GetUInt32());
        // Fifty gold into the offer, as nwmain puts it there; it comes back as a gold pile on the player's side.
        h.Inventory.FromClient("barter-gold", """{"gold":50}""");
        Assert.Equal("70080111feffff7f0832000000020103ff03ff", Bytes(h.Sent[^1]));
        h.Inventory.ObjectList(Gui(('B', 'A', new GuiRepositoryItemAdd { ItemId = 160, Item = Item(76, 535, stack: 50) }),
            ('A', 'A', new GuiRepositoryItemAdd { ItemId = 121, Item = Item(49, 49, stack: 9) })));
        var state = State();
        Assert.Equal((50, 0), (state.GetProperty("mine").GetProperty("gold").GetInt32(), state.GetProperty("mine").GetProperty("items").GetArrayLength()));
        Assert.Equal(121u, Assert.Single(state.GetProperty("theirs").GetProperty("items").EnumerateArray()).GetProperty("id").GetUInt32());
        // Lowering it: the pile comes back out and the new amount goes in; the same amount sends nothing.
        h.Sent.Clear();
        h.Inventory.FromClient("barter-gold", """{"gold":50}""");
        Assert.Equal([], h.Sent.Select(m => m.GetType().Name));
        h.Inventory.FromClient("barter-gold", """{"gold":20}""");
        Assert.Equal(160u, Assert.IsType<BarterMessages.BarterRqstRemoveItem>(h.Sent[0]).Item);
        Assert.Equal(20, Assert.IsType<GoldMessages.GoldTransferGold>(h.Sent[1]).Amount);
        // Locking and accepting name the side by whether it asked.
        h.Inventory.ToClient(Frame<BarterMessages.BarterAcptLockList>("50230802010201"));
        h.Inventory.ToClient(Frame<BarterMessages.BarterAcptLockList>("50230802000201"));
        h.Inventory.ToClient(Frame<BarterMessages.BarterAcptAcceptTrade>("50230a02000201"));
        state = State();
        Assert.Equal((true, false), (state.GetProperty("mine").GetProperty("offered").GetBoolean(), state.GetProperty("mine").GetProperty("accepted").GetBoolean()));
        Assert.Equal((true, true), (state.GetProperty("theirs").GetProperty("offered").GetBoolean(), state.GetProperty("theirs").GetProperty("accepted").GetBoolean()));
        h.Inventory.ToClient(Frame<BarterMessages.BarterAcptCloseBarter>("5023040201"));
        Assert.False(h.Last("barter-close").GetProperty("cancelled").GetBoolean());
    }
}
