using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>Where the inventory reaches out to the rest of the bridge.</summary>
/// <param name="ToServer">Sends an NWN message to the server.</param>
/// <param name="Emit">Queues an OpenWinter message for the client.</param>
/// <param name="Controlled">The player's creature.</param>
/// <param name="Position">Where the player's creature stands.</param>
/// <param name="NameOf">A world object's name (containers are titled with it).</param>
public sealed record NwnInventoryHost(Action<NwnMessage> ToServer, Action<byte[]> Emit, Func<uint> Controlled,
    Func<Vector3> Position, Func<uint, string> NameOf);

/// <summary>
/// The player's items over NWN: the inventory (backpack, bags and equipment), placeable containers and body bags,
/// stores and gold, kept from the object list's GUI entries and the inventory and store messages, and given to the
/// client as the OpenWinter server describes them ("inventory-open", "container-open", "store-open", "gold-changed").
/// <para>
/// The server sends the panels an NWN client has open: the equipment and the shown page of the backpack while the
/// inventory is open, an open container's or bag's contents, and the shown category and page of a store. The client
/// lists everything at once, so once the player has opened the inventory the bridge keeps it open, looks at every
/// backpack page, bag and store category in turn, and looks again after each change the server confirms. What a look
/// doesn't see again is gone.
/// </para>
/// </summary>
public sealed partial class NwnInventory(INwnBridgeData data, NwnInventoryHost host)
{
    /// <summary>Backpack pages (ten by six cells each; y counts down the pages).</summary>
    public const int BackpackPages = 6;

    /// <summary>How long a look waits for the server's first answer and for the rest of it; how long changes the
    /// server confirms are gathered before one look; how long after an area change the inventory is looked at again.</summary>
    const double FirstAnswer = 1.0, Settle = 0.4, PageWait = 0.35, PageSettle = 0.12, Gather = 0.15, AfterArea = 3.0;

    const int SlotLeftHand = 5, Slots = 18, MaxStorePages = 64;

    /// <summary>The requests the inventory answers.</summary>
    static readonly HashSet<string> Kinds =
    [
        "inventory", "item-info", "equip", "unequip", "item-move", "container-take", "container-take-all", "container-put",
        "container-close", "pickup", "drop", "drop-gold", "item-split", "item-use", "use-item", "examine", "store-buy",
        "store-sell", "store-identify", "store-close", "barter-request", "barter-offer", "barter-withdraw", "barter-gold",
        "barter-lock", "barter-accept", "barter-cancel", "barter-answer", "inventory-arrange",
    ];

    sealed class Held(uint id, ItemAppearanceAndProps look)
    {
        public readonly uint Id = id;
        public ItemAppearanceAndProps Look = look;
        public int X = -1, Y = -1;
        /// <summary>The backpack (the player), a bag, a container or the store it is in.</summary>
        public uint Holder;
        /// <summary>A store's category (UTM StoreList id).</summary>
        public int Panel;
    }

    readonly Dictionary<uint, Held> _carried = [];
    readonly Dictionary<int, Held> _equipped = [];
    readonly Dictionary<uint, Held> _container = [];
    readonly Dictionary<uint, Held> _stock = [];
    int _gold = -1, _weight, _strength = 10;
    bool _female, _strengthShown;
    bool _inventoryChanged, _containerChanged, _storeChanged;
    double _now;
    /// <summary>The creature whose items these are; a new controlled creature starts afresh.</summary>
    uint _owner;

    // The inventory panel is open on the server (the player opened it), whose inventory the server shows, and the
    // look through its pages. Changes are gathered into one look; nothing is shown until a whole look is done.
    bool _active, _reopen, _wanted, _syncing, _ready;
    uint _shown = InvalidObjectId;
    double _syncAt, _syncQuiet;
    readonly Queue<int> _pages = new();
    readonly HashSet<uint> _seen = [];
    readonly HashSet<int> _seenSlots = [];
    bool _equipmentListed;

    // Container contents ('C' entries) belong to the open container, or to the bag the bridge looked into last (the
    // server keeps that bag open until a container opens). Bags are read one at a time while nothing else is open.
    uint _containerId = InvalidObjectId, _bagOpen = InvalidObjectId;
    readonly Queue<uint> _bags = new();
    readonly HashSet<uint> _bagSeen = [];
    bool _readingBag, _bagsWanted;
    double _bagQuiet;

    // The open store: its terms, the category and page shown, and the views still to look at.
    bool _storeOpen, _storeLooking, _storeAgain;
    StoreMessages.StoreOpenPanelS2C? _terms;
    int _storePanel, _storePage;
    readonly int[] _storePages = [1, 1, 1, 1, 1];
    readonly Queue<NwnMessage> _storeViews = new();
    readonly HashSet<uint> _storeSeen = [];
    double _storeQuiet;
    uint _pendingIdentify;

    uint Pc => host.Controlled();

    // -- client requests ----------------------------------------------------------------------------------------

    /// <summary>Turns a client request into the NWN messages nwmain sends for it; false when it isn't an inventory
    /// request.</summary>
    public bool FromClient(string kind, string json)
    {
        if (!Kinds.Contains(kind)) return false;
        Owner();
        JsonObject d;
        try { d = string.IsNullOrWhiteSpace(json) ? [] : JsonNode.Parse(json) as JsonObject ?? []; }
        catch (System.Text.Json.JsonException) { d = []; }
        uint Id(string key) => d[key] is JsonValue v && v.TryGetValue<long>(out var l) && l is >= 0 and <= uint.MaxValue ? (uint)l : 0;
        int Int(string key, int fallback = -1) => d[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
        byte Cell(string key) => Int(key) is >= 0 and < 255 and var c ? (byte)c : (byte)255;
        var send = host.ToServer;
        uint pc = Pc;
        switch (kind)
        {
            case "inventory":
                // The player opens the inventory: the server is asked for it again if it isn't open, or closed it.
                if (!_active || _reopen) Want(now: true);
                else _inventoryChanged = true;
                Flush();
                return true;
            case "item-info":
                ItemInfo(Id("item"));
                return true;
            case "equip":
            {
                uint item = Id("item");
                int slot = Int("slot");
                if (EquipSlot(item, slot, d["offhand"] is JsonValue o && o.TryGetValue<bool>(out var off) && off) is int to)
                    send(new InventoryMessages.InventoryEquipC2S { Item = item, Creature = pc, EquipSlot = 1u << to });
                return true;
            }
            case "unequip":
                send(new InventoryMessages.InventoryUnequipC2S { Item = Id("item"), TargetRepository = pc, X = Cell("x"), Y = Cell("y") });
                return true;
            case "item-move":
            {
                uint item = Id("item"), target = Id("target") is > 0 and not InvalidObjectId and var t ? t : pc;
                if (target == pc && _equipped.Values.Any(h => h.Id == item))
                    send(new InventoryMessages.InventoryUnequipC2S { Item = item, TargetRepository = pc, X = Cell("x"), Y = Cell("y") });
                else send(new InventoryMessages.InventoryRepositoryMoveC2S { Item = item, TargetRepository = target, X = Cell("x"), Y = Cell("y") });
                return true;
            }
            case "container-take":
                send(new InventoryMessages.InventoryRepositoryMoveC2S { Item = Id("item"), TargetRepository = pc, X = Cell("x"), Y = Cell("y") });
                return true;
            case "container-take-all":
                foreach (var h in _container.Values.ToList())
                    send(new InventoryMessages.InventoryRepositoryMoveC2S { Item = h.Id, TargetRepository = pc, X = 255, Y = 255 });
                return true;
            case "container-put":
                if (_containerId != InvalidObjectId)
                    send(new InventoryMessages.InventoryRepositoryMoveC2S { Item = Id("item"), TargetRepository = _containerId, X = Cell("x"), Y = Cell("y") });
                return true;
            case "container-close":
                if (_containerId != InvalidObjectId) send(new GuiContainerMessages.GuiContainerStatus { Container = InvalidObjectId });
                CloseContainer(notify: false);
                return true;
            case "pickup":
                send(new InventoryMessages.InventoryPickupC2S { Item = Id("item"), TargetRepository = InvalidObjectId, X = Cell("x"), Y = Cell("y") });
                return true;
            case "drop":
                send(new InventoryMessages.InventoryDropC2S { Item = Id("item"), Position = Spot(d) });
                return true;
            case "drop-gold":
                if (Int("count", 0) > 0)
                    send(new GoldMessages.GoldTransferGold { ObjectId = InvalidObjectId, Amount = Int("count", 0), Barter = false, Destination = Spot(d) });
                return true;
            case "item-split":
                send(new InputMessages.InputSplitItem { Item = Id("item"), Count = (ushort)Math.Clamp(Int("count", 1), 1, ushort.MaxValue) });
                return true;
            case "item-use" or "use-item":
            {
                uint item = Id("item");
                int prop = Int("prop");
                if (prop < 0 && Find(item) is { } h) prop = h.Look.Properties.FindIndex(p => p.Name is 15 or 55 or 70 or 80);
                if (prop is < 0 or > byte.MaxValue) return true;
                uint target = Id("target");
                (float, float, float)? pos = d["pos"] is JsonArray a && a.Count == 3 ? ((float)a[0]!, (float)a[1]!, (float)a[2]!) : null;
                send(new InputMessages.InputUseItem
                {
                    Item = item, ActivePropertyIndex = (byte)prop,
                    Target = pos == null ? (target is > 0 and not InvalidObjectId ? target : pc) : null, Position = pos,
                });
                return true;
            }
            case "examine":
            {
                uint id = Id("object");
                if (Find(id) == null && !_stock.ContainsKey(id) && !_container.ContainsKey(id)) return false;
                send(new InputMessages.InputExamine { Target = id });
                return true;
            }
            case "store-buy":
                send(new StoreMessages.StoreRequestBuy { Item = Id("item"), DesiredX = Cell("x"), DesiredY = Cell("y"), DesiredRepository = pc });
                return true;
            case "store-sell":
                send(new StoreMessages.StoreRequestSell { Item = Id("item") });
                return true;
            case "store-identify":
                _pendingIdentify = Id("item");
                send(new StoreMessages.StoreIdentify { Item = _pendingIdentify });
                return true;
            case "store-close":
                if (_storeOpen) send(new StoreMessages.StoreClose());
                CloseStore(notify: false);
                return true;
            case "barter-request" or "barter-offer" or "barter-withdraw" or "barter-gold" or "barter-lock" or "barter-accept"
                or "barter-cancel" or "barter-answer":
                BarterRequest(kind, Id, Int);
                return true;
            case "inventory-arrange":
                // nwmain has no such command; the backpack stays as it is.
                return true;
        }
        return false;
    }

    /// <summary>
    /// The slot an equip goes to: the one asked for, else the off hand when asked, else the first slot the base item
    /// allows that is empty (the second ring goes on the other hand), else the first it allows.
    /// </summary>
    int? EquipSlot(uint item, int asked, bool offhand)
    {
        if (asked is >= 0 and < Slots) return asked;
        int allowed = Find(item) is { } h ? NwnItems.EquipableSlots(data, (int)h.Look.BaseItemId) & ((1 << Slots) - 1) : 0;
        if (allowed == 0) return null;
        if (offhand && (allowed & (1 << SlotLeftHand)) != 0) return SlotLeftHand;
        for (int slot = 0; slot < Slots; slot++)
            if ((allowed & (1 << slot)) != 0 && !_equipped.ContainsKey(slot)) return slot;
        return BitOperations.TrailingZeroCount(allowed);
    }

    (float, float, float) Spot(JsonObject d)
    {
        if (d["position"] is JsonArray p && p.Count == 3) return ((float)p[0]!, (float)p[1]!, (float)p[2]!);
        var at = host.Position();
        return (at.X, at.Y, at.Z);
    }

    // -- server messages ----------------------------------------------------------------------------------------

    /// <summary>The object list's GUI entries (inventory, containers, stores), the player's gold and gender.</summary>
    public void ObjectList(GameObjUpdateObjList list)
    {
        Owner();
        bool storeView = false;
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case ObjListGuiEntry gui:
                    storeView |= gui.Element == 'M';
                    Gui(gui);
                    break;
                case GameObjUpdatePlayerinfoMessages.GameObjUpdateObjListPlayerInfo info when info.ObjectId == Pc:
                    // Carrying capacity goes by strength: the inventory's own figure once sent, else the base score.
                    if (info.AbilityScores?.Values is { } scores && !_strengthShown) _strength = scores.StrBase;
                    if (info.AcGoldWeightLevelup is not { } gold) break;
                    _weight = gold.Weight;
                    int now = (int)Math.Min(gold.Gold, int.MaxValue);
                    if (_gold != now)
                    {
                        int delta = _gold < 0 ? 0 : now - _gold;
                        _gold = now;
                        Notify("gold-changed", new JsonObject { ["gold"] = _gold, ["delta"] = delta }, Pc);
                        _storeChanged |= _storeOpen;
                    }
                    break;
                case GameObjUpdateObjListAppearanceCreature a when a.ObjectId == Pc && (a.Flags & UpdateCreatureAppearanceFlags.Gender) != 0:
                    _female = a.Gender == 1;
                    break;
            }
        }
        if (storeView && _storeLooking) NextStoreView();
        Flush();
    }

    void Gui(ObjListGuiEntry gui)
    {
        if (Barter(gui)) return;
        // Another creature's inventory (OpenInventory on a henchman) isn't the player's.
        bool mine = _shown == Pc;
        switch (gui.Element, gui.Value)
        {
            case ('I', GuiInventorySlotAdd add) when mine && add.Slot != 0:
            {
                int slot = BitOperations.TrailingZeroCount(add.Slot);
                _equipped[slot] = new Held(add.ItemId, add.Item) { Holder = Pc };
                _carried.Remove(add.ItemId);
                _seenSlots.Add(slot);
                Heard();
                _inventoryChanged = true;
                break;
            }
            case ('I', GuiInventorySlotDelete del) when mine && del.Slot != 0:
                _inventoryChanged |= _equipped.Remove(BitOperations.TrailingZeroCount(del.Slot));
                break;
            case ('I', GuiInventorySlotUpdate u) when mine:
                (_weight, _strength, _strengthShown) = (u.Weight, u.StrVal, true);
                _inventoryChanged = true;
                break;
            case ('R', GuiRepositoryItemAdd add) when mine:
                _carried[add.ItemId] = new Held(add.ItemId, add.Item) { X = add.X, Y = add.Y, Holder = Pc };
                _seen.Add(add.ItemId);
                Heard();
                _inventoryChanged = true;
                break;
            case ('R', GuiRepositoryItemDelete del) when mine:
                _inventoryChanged |= _carried.Remove(del.ItemId);
                break;
            case ('R', GuiRepositoryItemUpdate u) when mine && _carried.TryGetValue(u.ItemId, out var held):
                Restack(held, u.StackSize, u.Cost);
                // A store's identification is confirmed by the item's new value.
                if (u.ItemId == _pendingIdentify)
                {
                    held.Look.Identified = true;
                    _pendingIdentify = 0;
                }
                _inventoryChanged = true;
                break;
            case ('R', GuiRepositoryItemMove m) when mine && _carried.TryGetValue(m.ItemId, out var held):
                (held.X, held.Y) = (m.X, m.Y);
                _inventoryChanged = true;
                break;
            case ('C', GuiContainerItemAdd add):
                if (_containerId != InvalidObjectId)
                {
                    _container[add.ItemId] = new Held(add.ItemId, add.Item) { X = add.X, Y = add.Y, Holder = _containerId };
                    _containerChanged = true;
                }
                else if (_bagOpen != InvalidObjectId)
                {
                    _carried[add.ItemId] = new Held(add.ItemId, add.Item) { X = add.X, Y = add.Y, Holder = _bagOpen };
                    _bagSeen.Add(add.ItemId);
                    _bagQuiet = _now + PageSettle;
                    _inventoryChanged = true;
                }
                break;
            case ('C', GuiContainerItemDelete del):
                if (_containerId != InvalidObjectId) _containerChanged |= _container.Remove(del.ItemId);
                else if (_carried.TryGetValue(del.ItemId, out var inBag) && inBag.Holder == _bagOpen) _inventoryChanged |= _carried.Remove(del.ItemId);
                break;
            case ('C', GuiContainerItemUpdate u) when Contents(u.ItemId) is { } held:
                Restack(held, u.StackSize, u.Cost);
                Touched();
                break;
            case ('C', GuiContainerItemMove m) when Contents(m.ItemId) is { } held:
                (held.X, held.Y) = (m.X, m.Y);
                Touched();
                break;
            case ('M', GuiStorePages pages) when _storeOpen:
                // The category now shown (its "current page") and how many pages it has. NWN numbers the categories
                // as baseitems StorePanel does (weapons 1, miscellaneous 4); the store lists swap those two.
                _storePanel = StorePanel(pages.CurrentPage);
                _storePage = 0;
                _storePages[_storePanel] = Math.Clamp((int)pages.NumPanels, 1, MaxStorePages);
                break;
            case ('M', GuiStoreItemAdd add) when _storeOpen:
                _stock[add.ItemId] = new Held(add.ItemId, add.Item) { X = add.X, Y = add.Y, Panel = _storePanel };
                _storeSeen.Add(add.ItemId);
                _storeChanged = true;
                break;
            case ('M', GuiStoreItemDelete del):
                // Switching the shown category or page takes its items off the panel; they are still for sale. While
                // the bridge looks, what is gone shows as what the look doesn't see again.
                if (!_storeLooking) _storeChanged |= _stock.Remove(del.ItemId);
                break;
            case ('M', GuiStoreReposition m) when _stock.TryGetValue(m.ItemId, out var held):
                (held.X, held.Y) = (m.X, m.Y);
                _storeChanged = true;
                break;
        }
    }

    Held? Contents(uint id) => _containerId != InvalidObjectId ? _container.GetValueOrDefault(id)
        : _carried.GetValueOrDefault(id) is { } h && h.Holder == _bagOpen ? h : null;

    void Touched()
    {
        if (_containerId != InvalidObjectId) _containerChanged = true;
        else _inventoryChanged = true;
    }

    static int StorePanel(int nwnPanel) => Math.Clamp(nwnPanel, 0, 4) switch { 1 => 4, 4 => 1, var p => p };

    static void Restack(Held held, uint stack, uint cost)
    {
        held.Look.StackSize = stack;
        held.Look.Cost = cost;
    }

    /// <summary>The inventory, container and store messages; false for anything else.</summary>
    public bool ToClient(NwnMessage message)
    {
        Owner();
        switch (message)
        {
            case GuiInventoryMessages.GuiInventoryStatus status:
                if (status.Active)
                {
                    // A script opened an inventory (OpenInventory): the player's own is looked through; another
                    // creature's isn't shown.
                    _shown = status.Inventory;
                    if (status.Inventory == Pc)
                    {
                        _active = true;
                        _reopen = false;
                        Want(now: true);
                    }
                }
                else if (status.Inventory == Pc || status.Inventory == _shown)
                {
                    // The server closed it: left closed until the player opens it again.
                    _shown = InvalidObjectId;
                    _reopen = true;
                    _wanted = false;
                }
                break;
            case GuiContainerMessages.GuiContainerStatusS2C status:
                if (status.Container == InvalidObjectId) CloseContainer(notify: true);
                else
                {
                    // A container opened by the player takes over from a bag being read; that bag keeps what it had.
                    if (_readingBag) _ready = true;
                    _readingBag = false;
                    _bags.Clear();
                    _bagOpen = InvalidObjectId;
                    _containerId = status.Container;
                    _container.Clear();
                    _containerChanged = true;
                }
                break;
            case InventoryMessages.InventoryEquip equip:
                if (Find(equip.Item) is { } item && equip.EquipSlot != 0)
                {
                    int slot = BitOperations.TrailingZeroCount(equip.EquipSlot);
                    _carried.Remove(item.Id);
                    // What was in the slot goes back to the backpack; the next look places it.
                    if (_equipped.TryGetValue(slot, out var displaced) && displaced.Id != item.Id)
                    {
                        (displaced.X, displaced.Y, displaced.Holder) = (-1, -1, Pc);
                        _carried[displaced.Id] = displaced;
                        Notify("creature-equipment", new JsonObject { ["item"] = displaced.Id, ["slot"] = -1, ["visual"] = null }, Pc);
                    }
                    foreach (var other in _equipped.Where(kv => kv.Value.Id == item.Id).Select(kv => kv.Key).ToList()) _equipped.Remove(other);
                    (item.X, item.Y) = (-1, -1);
                    _equipped[slot] = item;
                    Notify("creature-equipment", new JsonObject { ["item"] = item.Id, ["slot"] = slot, ["visual"] = null }, Pc);
                }
                Want();
                _inventoryChanged = true;
                break;
            case InventoryMessages.InventoryUnequip unequip:
                foreach (var (slot, h) in _equipped.Where(kv => kv.Value.Id == unequip.Item).ToList())
                {
                    _equipped.Remove(slot);
                    (h.X, h.Y, h.Holder) = (-1, -1, Pc);
                    _carried[h.Id] = h;
                    Notify("creature-equipment", new JsonObject { ["item"] = h.Id, ["slot"] = -1, ["visual"] = null }, Pc);
                }
                Want();
                _inventoryChanged = true;
                break;
            case InventoryMessages.InventoryDrop drop:
                Forget(drop.Item);
                break;
            case InventoryMessages.InventoryRepositoryMove or InventoryMessages.InventoryPickup:
                Want();
                break;
            case InventoryMessages.InventoryEquipCancel or InventoryMessages.InventoryUnequipCancel or InventoryMessages.InventoryDropCancel
                or InventoryMessages.InventoryPickupCancel or InventoryMessages.InventoryRepositoryMoveCancel:
                // Refused: the client puts back what it moved ahead of the server.
                _inventoryChanged = true;
                _containerChanged = _containerId != InvalidObjectId;
                break;
            case StoreMessages.StoreOpenPanelS2C terms:
                OpenStore(terms);
                break;
            case StoreMessages.StoreConfirmTransaction done:
                // The object list with the change comes after this: looked at by the looks it starts.
                if (_terms != null) _terms.StoreGold = done.StoreGold;
                _storeChanged |= _storeOpen;
                Want();
                LookThroughStore();
                break;
            case StoreMessages.StoreClose:
                CloseStore(notify: true);
                break;
            case GuiInfoPopupMessages.GuiInfoPopupExamineItemData examine when examine.Item != InvalidObjectId:
                Examined(examine);
                break;
            case ItemPropertyMessages.ItemPropertyUpdateProps props when Find(props.Item) is { } h:
                h.Look.Properties = props.Properties.Select(p => new ItemPropertyEntry { Name = p.Name, Subtype = p.Subtype, CostTableValue = p.CostTableValue, Param1 = p.Param1 }).ToList();
                h.Look.UsesLeft = new ItemUsesLeft { UseableProperties = props.UsesLeft.UseableProperties, Uses = [.. props.UsesLeft.Uses] };
                _inventoryChanged = true;
                break;
            case ItemPropertyMessages.ItemPropertyUpdateUses uses when Find(uses.Item) is { } h:
                h.Look.UsesLeft = new ItemUsesLeft { UseableProperties = uses.UsesLeft.UseableProperties, Uses = [.. uses.UsesLeft.Uses] };
                _inventoryChanged = true;
                break;
            case ItemPropertyMessages.ItemPropertyUpdateName name when Find(name.Item) is { } h:
                h.Look.Name = name.Name.Value switch
                {
                    ItemPropertyMessages.OriginalItemName o => new NwnTagged(1, new OriginalItemName { Name = o.Name }),
                    ItemPropertyMessages.DisplayItemName n => new NwnTagged(0, new DisplayItemName { Name = n.Name }),
                    _ => h.Look.Name,
                };
                _inventoryChanged = true;
                break;
            case BarterMessages.BarterAcptStartBarter or BarterMessages.BarterAcptLockList or BarterMessages.BarterAcptAcceptTrade
                or BarterMessages.BarterAcptCloseBarter:
                Barter(message);
                break;
            default:
                return false;
        }
        Flush();
        return true;
    }

    /// <summary>Items the server reports gained or lost: the backpack is looked at again, since the server only
    /// shows the page on display.</summary>
    public void Feedback(ClientSideMessageFeedbackMessages.ClientSideMessageFeedback feedback)
    {
        if (feedback.Payload?.Value is ClientSideMessageFeedbackMessages.FeedbackItemReceived or ClientSideMessageFeedbackMessages.FeedbackItemLost)
            Want();
    }

    /// <summary>
    /// A new area: the server has closed what was open (containers, stores, barter, the inventory panel), which the
    /// client is told; the inventory is opened and looked at again once the player has arrived.
    /// </summary>
    public void AreaChanged()
    {
        CloseContainer(notify: true);
        CloseStore(notify: true);
        EndBarter(cancelled: true);
        _bagOpen = InvalidObjectId;
        _readingBag = false;
        _bags.Clear();
        _syncing = false;
        _pages.Clear();
        _shown = InvalidObjectId;
        if (_active && !_reopen)
        {
            _wanted = true;
            _syncAt = _now + AfterArea;
        }
        _active = false;
    }

    /// <summary>Another controlled creature (possession): its items are not the last one's. During an area change
    /// the server names no creature for a moment, which changes nothing.</summary>
    void Owner()
    {
        uint pc = Pc;
        if (pc == _owner || pc is 0 or InvalidObjectId) return;
        bool wasActive = _owner != 0 && _active && !_reopen;
        if (_owner != 0) AreaChanged();
        _owner = pc;
        _carried.Clear();
        _equipped.Clear();
        _gold = -1;
        _strengthShown = _ready = false;
        _inventoryChanged = true;
        if (wasActive)
        {
            _wanted = true;
            _syncAt = _now;
        }
    }

    /// <summary>Finishes looks through the backpack, bags and store once the server has answered; called every frame.</summary>
    public void Tick(double now)
    {
        _now = now;
        if (Pc is 0 or InvalidObjectId) return;
        Owner();
        if (_wanted && !_syncing && now >= _syncAt) StartSync();
        if (_syncing && now > _syncQuiet) NextPage();
        if (_readingBag && now > _bagQuiet) NextBag();
        if (_storeLooking && now > _storeQuiet) NextStoreView();
        Flush();
    }

    // -- looking through the backpack ---------------------------------------------------------------------------

    /// <summary>Asks for a look through the backpack. Changes the server confirms together are gathered into one look;
    /// one asked for during a look runs after it. Nothing is looked at while the inventory isn't open.</summary>
    void Want(bool now = false)
    {
        if (now)
        {
            _active = true;
            _reopen = false;
        }
        if (!_active || _reopen) return;
        if (!_wanted) _syncAt = _now + (now ? 0 : Gather);
        _wanted = true;
        if (now && !_syncing) StartSync();
    }

    /// <summary>
    /// Shows the server every backpack page in turn, ending on the first. The inventory is opened anew (which also
    /// sends the equipment) unless a container is open or a barter runs: closing the inventory closes those. The server
    /// answers a page on its next update and only for the page shown then, so each waits for its answer, or a moment
    /// when the page is empty.
    /// </summary>
    void StartSync()
    {
        _wanted = false;
        _syncing = _active = true;
        _seen.Clear();
        _seenSlots.Clear();
        uint pc = Pc;
        _equipmentListed = _containerId == InvalidObjectId && _mine == null;
        if (_equipmentListed) host.ToServer(new GuiInventoryMessages.GuiInventoryStatusC2S { Active = false, Inventory = pc });
        if (_equipmentListed || _shown != pc) host.ToServer(new GuiInventoryMessages.GuiInventoryStatusC2S { Active = true, Inventory = pc });
        _shown = pc;
        _pages.Clear();
        for (int page = 1; page <= BackpackPages; page++) _pages.Enqueue(page % BackpackPages);
        // The first answer (the equipment and the page shown) comes with the next update.
        _syncQuiet = _now + PageWait;
    }

    void NextPage()
    {
        if (_pages.Count == 0)
        {
            FinishSync();
            return;
        }
        host.ToServer(new GuiInventoryMessages.GuiInventorySelectPanelC2S { Panel = (byte)_pages.Dequeue(), PlayerPanel = true });
        _syncQuiet = _now + PageWait;
    }

    void Heard()
    {
        if (_syncing) _syncQuiet = _now + PageSettle;
    }

    void FinishSync()
    {
        _syncing = false;
        // What the pages no longer show is gone; bag contents are read next.
        foreach (var gone in _carried.Values.Where(h => h.Holder == Pc && !_seen.Contains(h.Id)).Select(h => h.Id).ToList()) _carried.Remove(gone);
        if (_equipmentListed)
            foreach (var slot in _equipped.Keys.Where(s => !_seenSlots.Contains(s)).ToList()) _equipped.Remove(slot);
        foreach (var lost in _carried.Values.Where(h => h.Holder != Pc && !_carried.ContainsKey(h.Holder)).Select(h => h.Id).ToList()) _carried.Remove(lost);
        _inventoryChanged = true;
        _bagsWanted = true;
        ReadBags();
        if (!_readingBag) _ready = true;
    }

    /// <summary>Bags show their contents only when opened, and opening one closes an open container (and a store's
    /// or barter's view of it): they are read while nothing else is open, again after a container closes.</summary>
    void ReadBags()
    {
        if (!_bagsWanted || _readingBag || _syncing || _containerId != InvalidObjectId || _storeOpen || _mine != null) return;
        _bagsWanted = false;
        _bags.Clear();
        foreach (var bag in _carried.Values.Where(h => h.Holder == Pc && NwnItems.Category(data, (int)h.Look.BaseItemId) == "container"))
            _bags.Enqueue(bag.Id);
        if (_bags.Count == 0) return;
        _readingBag = true;
        NextBag(first: true);
    }

    void NextBag(bool first = false)
    {
        // The bag just read keeps only what it showed.
        if (!first && _bagOpen != InvalidObjectId)
            foreach (var gone in _carried.Values.Where(h => h.Holder == _bagOpen && !_bagSeen.Contains(h.Id)).Select(h => h.Id).ToList())
                _carried.Remove(gone);
        _inventoryChanged = true;
        if (_bags.Count == 0)
        {
            // The last bag stays open on the server (so its changes keep coming) until a container opens.
            _readingBag = false;
            _ready = true;
            return;
        }
        _bagOpen = _bags.Dequeue();
        _bagSeen.Clear();
        _bagQuiet = _now + PageWait;
        host.ToServer(new GuiContainerMessages.GuiContainerStatus { Container = _bagOpen });
    }

    void CloseContainer(bool notify)
    {
        if (_containerId == InvalidObjectId) return;
        if (notify) Notify("container-close", new JsonObject { ["container"] = _containerId }, _containerId);
        _containerId = InvalidObjectId;
        _container.Clear();
        _bagsWanted = true;
    }

    Held? Find(uint id) => _carried.GetValueOrDefault(id) ?? _equipped.Values.FirstOrDefault(h => h.Id == id);

    void Forget(uint id)
    {
        _inventoryChanged |= _carried.Remove(id);
        foreach (var slot in _equipped.Where(kv => kv.Value.Id == id).Select(kv => kv.Key).ToList())
        {
            _equipped.Remove(slot);
            _inventoryChanged = true;
        }
    }

    // -- stores -------------------------------------------------------------------------------------------------

    void OpenStore(StoreMessages.StoreOpenPanelS2C terms)
    {
        _terms = terms;
        _storeOpen = true;
        _stock.Clear();
        _storeLooking = false;
        _storePanel = StorePanel(terms.Panel);
        _storePage = 0;
        Array.Fill(_storePages, 1);
        LookThroughStore();
    }

    void CloseStore(bool notify)
    {
        if (_storeOpen && notify) Notify("store-close", new JsonObject { ["store"] = InvalidObjectId }, InvalidObjectId);
        _storeOpen = _storeLooking = _storeAgain = false;
        _storeViews.Clear();
        _stock.Clear();
        _pendingIdentify = 0;
        _bagsWanted = true;
    }

    /// <summary>Shows the store's other categories and pages one after another, ending on the first. One asked for
    /// during a look runs after it.</summary>
    void LookThroughStore()
    {
        if (!_storeOpen) return;
        if (_storeLooking)
        {
            _storeAgain = true;
            return;
        }
        _storeViews.Clear();
        _storeSeen.Clear();
        for (int panel = 1; panel <= 5; panel++)
            _storeViews.Enqueue(new StoreMessages.StoreOpenPanelC2S { Panel = (byte)(panel % 5) });
        _storeLooking = true;
        _storeQuiet = _now + Settle;
    }

    void NextStoreView()
    {
        // The pages after the first of the category just shown come before the next category.
        if (_storePage + 1 < _storePages[_storePanel] && _storeViews.Count > 0)
        {
            _storePage++;
            host.ToServer(new StoreMessages.StoreOpenPage { Page = (byte)Math.Min(_storePage, byte.MaxValue) });
            _storeQuiet = _now + Settle;
            return;
        }
        if (_storeViews.Count > 0)
        {
            host.ToServer(_storeViews.Dequeue());
            _storeQuiet = _now + FirstAnswer;
            return;
        }
        // Done: what the look didn't see is no longer for sale.
        foreach (var gone in _stock.Keys.Where(id => !_storeSeen.Contains(id)).ToList()) _stock.Remove(gone);
        _storeLooking = false;
        _storeChanged = true;
        if (_storeAgain)
        {
            _storeAgain = false;
            LookThroughStore();
        }
    }

    // -- output -------------------------------------------------------------------------------------------------

    void Flush()
    {
        // Between areas the server names no creature for a moment: nothing is shown or read then.
        if (Pc is 0 or InvalidObjectId) return;
        // The inventory shows once a whole look is done; after that every change shows at once.
        if (_inventoryChanged && _ready)
        {
            Notify("inventory-open", InventoryView(), Pc);
            _inventoryChanged = false;
        }
        if (_containerChanged && _containerId != InvalidObjectId) Notify("container-open", ContainerView(), _containerId);
        _containerChanged = false;
        if (_storeChanged && _storeOpen && !_storeLooking)
        {
            Notify("store-open", StoreView(), InvalidObjectId);
            _storeChanged = false;
        }
        ReadBags();
    }

    JsonObject Entry(Held h)
    {
        var e = NwnItems.Entry(data, h.Id, h.Look, _female);
        if (h.X >= 0) (e["x"], e["y"]) = (h.X, h.Y);
        return e;
    }

    /// <summary>The "inventory-open" payload: gold, weight and load, equipped and carried items.</summary>
    JsonObject InventoryView()
    {
        var equipped = new JsonArray();
        foreach (var (slot, h) in _equipped.OrderBy(kv => kv.Key))
        {
            var e = Entry(h);
            e["slot"] = slot;
            equipped.Add(e);
        }
        var items = new JsonArray();
        foreach (var h in _carried.Values.OrderBy(h => h.Holder == Pc ? 0 : 1).ThenBy(h => h.Y).ThenBy(h => h.X))
        {
            var e = Entry(h);
            e["container"] = h.Holder;
            items.Add(e);
        }
        return new JsonObject
        {
            ["creature"] = Pc,
            ["gold"] = Math.Max(0, _gold),
            ["weight"] = _weight,
            ["normalLoad"] = Load("Normal"),
            ["heavyLoad"] = Load("Heavy"),
            ["encumbrance"] = _weight > Load("Heavy") ? 2 : _weight > Load("Normal") ? 1 : 0,
            ["grid"] = Grid(10, 6, BackpackPages),
            ["bagGrid"] = Grid(7, 5, 1),
            ["equipped"] = equipped,
            ["items"] = items,
        };
    }

    JsonObject ContainerView()
    {
        var items = new JsonArray();
        foreach (var h in _container.Values.OrderBy(h => h.Y).ThenBy(h => h.X)) items.Add(Entry(h));
        return new JsonObject { ["container"] = _containerId, ["name"] = host.NameOf(_containerId), ["grid"] = Grid(7, 5, 0), ["items"] = items };
    }

    /// <summary>The "store-open" payload: stock with the player's prices, the store's terms and what it would pay for
    /// each of the player's items.</summary>
    JsonObject StoreView()
    {
        var t = _terms!;
        var items = new JsonArray();
        foreach (var h in _stock.Values.OrderBy(h => h.Panel).ThenBy(h => h.Y).ThenBy(h => h.X))
        {
            var e = Entry(h);
            e["panel"] = h.Panel;
            e["price"] = SellPrice(h.Look, t.SellRate);
            items.Add(e);
        }
        var offers = new JsonArray();
        foreach (var h in _carried.Values)
            offers.Add(new JsonObject { ["id"] = h.Id, ["price"] = BuyPrice(h, t) });
        var categoryPages = new JsonArray();
        foreach (var pages in _storePages) categoryPages.Add(pages);
        return new JsonObject
        {
            ["store"] = InvalidObjectId,
            ["name"] = "",
            ["markUp"] = (int)t.SellRate,
            ["markDown"] = (int)t.BuyRate,
            ["gold"] = t.StoreGold,
            ["playerGold"] = Math.Max(0, _gold),
            ["grid"] = Grid(10, 10, 0),
            ["categoryPages"] = categoryPages,
            ["maxBuyPrice"] = t.MaxBuy,
            ["identifyCost"] = t.IdCost,
            ["blackMarket"] = t.BlackMarket,
            ["offers"] = offers,
            ["items"] = items,
        };
    }

    /// <summary>What the player pays: the item's value times the store's mark up (at least 1 for anything of value).</summary>
    static int SellPrice(ItemAppearanceAndProps item, int markUp)
    {
        long price = (long)item.Cost * Math.Max(0, markUp) / 100;
        return (int)Math.Clamp(price, item.Cost > 0 ? 1 : 0, int.MaxValue);
    }

    /// <summary>What the store pays, or -1 when it won't buy: base items it won't (or only will) buy, stolen goods outside
    /// a black market, bags with something in them; capped by its most.</summary>
    int BuyPrice(Held h, StoreMessages.StoreOpenPanelS2C t)
    {
        int b = (int)Math.Min(h.Look.BaseItemId, int.MaxValue);
        if (t.WillNotBuy ? t.BuyList.Contains(b) : t.BuyList.Count > 0 && !t.BuyList.Contains(b)) return -1;
        if (h.Look.Stolen && !t.BlackMarket) return -1;
        if (_carried.Values.Any(inside => inside.Holder == h.Id)) return -1;
        long price = (long)h.Look.Cost * (h.Look.Stolen ? t.BlackMarketRate : t.BuyRate) / 100;
        if (t.MaxBuy >= 0) price = Math.Min(price, t.MaxBuy);
        return (int)Math.Clamp(price, 0, int.MaxValue);
    }

    void ItemInfo(uint id)
    {
        if (Find(id) is not { } h) return;
        int slot = _equipped.Where(kv => kv.Value.Id == id).Select(kv => kv.Key).DefaultIfEmpty(-1).First();
        int slots = NwnItems.EquipableSlots(data, (int)h.Look.BaseItemId);
        var icon = new JsonArray();
        foreach (var layer in NwnItems.Icon(data, h.Look, _female)) icon.Add(layer);
        Notify("item-info", new JsonObject
        {
            ["item"] = id, ["name"] = NwnItems.Name(data, h.Look), ["icon"] = icon, ["stack"] = NwnItems.Stack(h.Look),
            ["equippable"] = slots != 0, ["equipped"] = slot >= 0, ["slot"] = slot, ["equipSlots"] = slots,
        }, Pc);
    }

    /// <summary>The examine window for an item, from the server's description and the item as last seen.</summary>
    void Examined(GuiInfoPopupMessages.GuiInfoPopupExamineItemData x)
    {
        var look = (Find(x.Item) ?? _stock.GetValueOrDefault(x.Item) ?? _container.GetValueOrDefault(x.Item))?.Look;
        if (look == null) return;
        bool identified = x.Identified ?? look.Identified;
        look.Identified = identified;
        string description = x.Description?.Value switch
        {
            GuiInfoPopupMessages.LocDescription l => l.Text.IsStrRef ? data.Text(l.Text.StrRef, l.Text.Gender) : l.Text.Text,
            GuiInfoPopupMessages.StringDescription s => s.Text,
            _ => "",
        };
        var properties = new JsonArray();
        if (!identified) properties.Add("Unidentified");
        else
            foreach (var p in x.Properties ?? [])
                if (NwnItems.PropertyText(data, new ItemPropertyEntry { Name = p.Name, Subtype = p.Subtype, CostTableValue = p.CostTableValue, Param1 = p.Param1 }) is { Length: > 0 } text)
                    properties.Add(text);
        var icon = new JsonArray();
        foreach (var layer in NwnItems.Icon(data, look, _female)) icon.Add(layer);
        var info = NwnItems.Info(data, look);
        if (x.NumCharges is { } charges && charges > 0) info["charges"] = charges;
        if (x.Weight is { } weight) info["weight"] = weight;
        Notify("examine", new JsonObject
        {
            ["object"] = x.Item, ["kind"] = "item", ["name"] = NwnItems.Name(data, look), ["description"] = description,
            ["portrait"] = "", ["icon"] = icon, ["properties"] = properties, ["subtitle"] = "", ["info"] = info, ["identified"] = identified,
        }, x.Item);
    }

    int Load(string column)
    {
        int row = Math.Clamp(_strength, 0, Math.Max(0, data.TwoDARowCount("encumbrance") - 1));
        return int.TryParse(data.TwoDA("encumbrance", row, column), out var v) ? v : 0;
    }

    static JsonObject Grid(int width, int pageHeight, int pages) => new() { ["width"] = width, ["pageHeight"] = pageHeight, ["pages"] = pages };

    void Notify(string kind, JsonObject json, uint subject) =>
        host.Emit(new NotifyMessage { Kind = kind, Subject = subject, Json = json.ToJsonString() }.Write());
}
