using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>
/// Who holds which item: creature inventories and equipment, placeable and store inventories,
/// containers and the ground. Every move goes through here so possession stays consistent and
/// the module's item events (OnAcquireItem, OnUnAcquireItem, OnPlayerEquipItem, ...) and
/// container OnDisturbed events fire.
///
/// <para><see cref="Item.Possessor"/> is the immediate holder (creature, placeable, store or
/// container item); <see cref="Owner"/> walks up to the top-level holder.</para>
///
/// <para>Inventories the area loader leaves as template data (placeable ItemList, store StoreList
/// and pricing, ground items' properties) are filled in lazily on first use, and in one pass on the
/// first world step.</para>
/// </summary>
public sealed partial class InventorySystem : IWorldSystem
{
    public const int DisturbAdded = 0, DisturbRemoved = 1, DisturbStolen = 2;

    readonly World _world;
    public ItemRules Rules { get; }
    bool _loaded, _rescannedThisStep;
    readonly HashSet<uint> _gridChecked = new(), _ammoChecked = new();

    public InventorySystem(World world)
    {
        _world = world;
        Rules = new ItemRules(world);
        world.ObjectAdded += OnAdded;
        world.ObjectRemoved += OnRemoved;
    }

    public int Order => -100;

    public void Step(float dt)
    {
        _rescannedThisStep = false;
        EnsureLoaded();
        CheckStoreSessions();
        FlushChanges();
    }

    // ------------------------------------------------------------------ loading

    /// <summary>Fills every inventory the loader left as template data. Runs once; cheap afterwards.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        foreach (var o in _world.AllObjects.ToList())
        {
            if (o is Item { Area: not null } ground && ground.Possessor == GameObject.InvalidId) RefreshGroundItem(ground);
            else Load(o);
        }
    }

    /// <summary>Makes sure one object's inventory is filled in and its items point back at it.</summary>
    public void Load(GameObject? o)
    {
        switch (o)
        {
            case Creature c:
                Normalize(c);
                if (_ammoChecked.Add(c.Id)) GiveDefaultAmmo(c);
                break;
            case Placeable p:
                if (!p.InventoryLoaded)
                {
                    p.InventoryLoaded = true;
                    foreach (var entry in p.Template.GetList("ItemList"))
                        if (_world.Items.FromListEntry(entry) is { } item) { _world.Register(item); p.Inventory.Add(item); }
                }
                Normalize(p);
                break;
            case Store s:
                if (!s.InventoryLoaded) LoadStore(s);
                Normalize(s);
                break;
            case Item i when i.Inventory.Count > 0:
                Normalize(i);
                break;
        }
    }

    void LoadStore(Store s)
    {
        s.InventoryLoaded = true;
        var t = s.Template;
        if (t.Has("MarkUp")) s.MarkUp = (int)t.GetInt("MarkUp");
        if (t.Has("MarkDown")) s.MarkDown = (int)t.GetInt("MarkDown");
        if (t.Has("StoreGold")) s.Gold = (int)t.GetInt("StoreGold");
        if (t.Has("MaxBuyPrice")) s.MaxBuyPrice = (int)t.GetInt("MaxBuyPrice");
        if (t.Has("IdentifyPrice")) s.IdentifyPrice = (int)t.GetInt("IdentifyPrice");
        s.BlackMarket = t.GetInt("BlackMarket") != 0;
        s.BlackMarketMarkDown = (int)t.GetInt("BM_MarkDown", 25);
        foreach (var b in t.GetList("WillNotBuy")) s.WillNotBuy.Add((int)b.GetInt("BaseItem"));
        foreach (var b in t.GetList("WillOnlyBuy")) s.WillOnlyBuy.Add((int)b.GetInt("BaseItem"));
        foreach (var category in t.GetList("StoreList"))
            foreach (var entry in category.GetList("ItemList"))
                if (_world.Items.FromListEntry(entry) is { } item)
                {
                    _world.Register(item);
                    item.Infinite = entry.GetInt("Infinite") != 0;
                    // The base item decides the category, whichever list the store file put it in.
                    item.StorePanel = Rules.StorePanel(item.BaseItem);
                    item.Identified = true; // a store's stock is identified
                    s.Inventory.Add(item);
                }
        var bi = _world.Resources.TwoDA("baseitems");
        var stock = s.Inventory.OrderBy(i => bi.GetInt(i.BaseItem, "StorePanelSort", 0))
            .ThenBy(i => Rules.GoldValue(i) / Math.Max(1, i.StackSize))
            .ThenByDescending(s.Inventory.IndexOf).ToArray();
        foreach (var item in stock) item.InvX = item.InvY = -1;
        foreach (var item in stock)
            if (FirstFit(s, item) is { } cell) (item.InvX, item.InvY) = cell;

    }

    /// <summary>Ground items from a GIT only get their common fields from the loader; read the rest of the UTI data.</summary>
    void RefreshGroundItem(Item item)
    {
        if (!item.Template.Has("BaseItem")) return;
        var fresh = _world.Items.FromTemplate(item.Template);
        if (item.Properties.Count == 0) item.Properties.AddRange(fresh.Properties);
        item.Identified = fresh.Identified;
        item.Stolen = fresh.Stolen;
        item.Cursed = fresh.Cursed;
        item.Charges = fresh.Charges;
        item.Cost = fresh.Cost;
        item.AddCost = fresh.AddCost;
        item.Droppable = fresh.Droppable;
        item.Pickpocketable = fresh.Pickpocketable;
        if (item.Description.Length == 0) item.Description = fresh.Description;
        if (item.Inventory.Count == 0) item.Inventory.AddRange(fresh.Inventory);
        Normalize(item);
    }

    /// <summary>Points every item held by <paramref name="holder"/> (recursively through containers) back at its holder.</summary>
    void Normalize(GameObject holder)
    {
        if (holder.Id == 0) return;
        // Inventory grid: check stored positions once per holder, then give new arrivals a cell.
        if (_gridChecked.Add(holder.Id)) ArrangeLoaded(holder);
        else if (InventoryList(holder) is { } items && items.Any(i => i.InvX < 0 && !i.IsDestroyed)) PlaceUnplaced(holder);
        if (holder is Store store) RecordStorePages(store);
        if (holder is Creature c)
            foreach (var item in c.Equipment.Values) { item.Possessor = c.Id; if (item.Inventory.Count > 0) Normalize(item); }
        if (InventoryList(holder) is { } list)
            foreach (var item in list) { item.Possessor = holder.Id; if (item.Inventory.Count > 0) Normalize(item); }
    }

    /// <summary>
    /// The original arms a spawned NPC whose launcher has no ammunition with a full, undroppable stack of
    /// basic ammunition (iprp_ammocost row 1: arrows, bolts or bullets).
    /// </summary>
    void GiveDefaultAmmo(Creature c)
    {
        if (c.IsPC || !c.Equipment.TryGetValue(EquipSlot.RightHand, out var launcher)) return;
        var (slot, column) = _world.Resources.TwoDA("baseitems").GetInt(launcher.BaseItem, "AmmunitionType", 0) switch
        {
            1 => (EquipSlot.Arrows, "Arrow"),
            2 => (EquipSlot.Bolts, "Bolt"),
            3 => (EquipSlot.Bullets, "Bullet"),
            _ => (0, ""),
        };
        if (slot == 0 || c.Equipment.ContainsKey(slot)) return;
        var ammo = _world.Items.FromBlueprint(_world.Resources.TwoDA("iprp_ammocost").Get(1, column).ToLowerInvariant());
        if (ammo == null) return;
        _world.Register(ammo);
        ammo.StackSize = Rules.MaxStack(ammo.BaseItem);
        ammo.Droppable = false;
        ammo.DefaultAmmo = true;
        ammo.Possessor = c.Id;
        c.Equipment[slot] = ammo;
    }

    void OnAdded(GameObject o)
    {
        if (o is Creature or Placeable or Store) Load(o);
    }

    void OnRemoved(GameObject o)
    {
        _cursors.Remove(o.Id);
        if (!o.IsDestroyed) return; // lifted off the ground, not destroyed
        if (o is Item item && FindHolder(item) is { } holder)
        {
            var owner = holder is Item bag ? Owner(bag) : holder;
            // A polymorph's own items, and the equipment it put away, go without an event.
            bool polymorph = holder is Creature { PolymorphForm: { } p } && (p.Form.Contains(item) || p.Stored.ContainsValue(item));
            // Preserve that protected cleanup path; its automatic voices are unmeasured.
            int loadBefore = !polymorph && holder is Creature ? VoiceLoadBefore(holder) : -1;
            DetachFrom(holder, item);
            VoiceLoadChanged(holder, loadBefore);
            // The original fires OnUnAcquireItem (with the item already invalid) when a carried item is destroyed,
            // but not for the items of a creature that is destroyed itself.
            if (owner is Creature { Destroying: false } lost && !polymorph)
            {
                // A used-up potion, scroll or wand says nothing; a carried item a script destroys says "Lost Item"
                // (visual scene castlines3).
                if (!item.UsedUp) ReportLost(lost, item);
                FireUnacquire(lost, item);
                foreach (var inner in item.Inventory.ToArray()) FireUnacquire(lost, inner);
            }
        }
        if (o is Placeable or Store or Item && InventoryList(o) is { Count: > 0 } list)
            foreach (var inner in list.ToList()) _world.Destroy(inner);
    }

    // ------------------------------------------------------------------ queries

    /// <summary>The list of items directly inside an object (not equipment), or null if it has no inventory.</summary>
    public static List<Item>? InventoryList(GameObject? o) => o switch
    {
        Creature c => c.Inventory,
        Placeable p => p.Inventory,
        Store s => s.Inventory,
        Item i => i.Inventory,
        _ => null,
    };

    /// <summary>GetHasInventory: creatures and stores always; placeables when flagged; items when they are containers.</summary>
    public bool HasInventory(GameObject? o) => o switch
    {
        Creature or Store => true,
        Placeable p => p.HasInventory,
        Item i => Rules.IsContainer(i.BaseItem),
        _ => false,
    };

    static bool DirectlyHolds(GameObject holder, Item item) =>
        (holder is Creature c && (c.Equipment.ContainsValue(item) || c.PolymorphForm?.Stored.ContainsValue(item) == true))
        || (InventoryList(holder)?.Contains(item) ?? false);

    /// <summary>The object an item is directly inside (a container item, or a creature, placeable or store), or null on the ground.</summary>
    public GameObject? Holder(Item item) => item.IsDestroyed ? null : FindHolder(item);

    GameObject? FindHolder(Item item)
    {
        var h = item.Possessor == GameObject.InvalidId ? null : _world.Get(item.Possessor);
        if (h != null && DirectlyHolds(h, item)) return h;
        if (item.Possessor == GameObject.InvalidId && item.Area != null) return null;
        // Stale or never set (items built before their holder had an id): rescan, at most once per step.
        if (_rescannedThisStep) return null;
        _rescannedThisStep = true;
        foreach (var o in _world.AllObjects.ToList())
            if (o is Creature or Placeable or Store) Load(o);
        h = _world.Get(item.Possessor);
        return h != null && DirectlyHolds(h, item) ? h : null;
    }

    /// <summary>The top-level holder (creature, placeable or store), looking through containers; null on the ground.</summary>
    public GameObject? Owner(Item item)
    {
        var h = Holder(item);
        for (int guard = 0; h is Item bag && guard < 8; guard++) h = Holder(bag);
        return h is Item ? null : h;
    }

    /// <summary>GetItemPossessor.</summary>
    public GameObject? Possessor(Item item, bool returnBags) => returnBags ? Holder(item) : Owner(item);

    /// <summary>
    /// Inventory items in iteration order (GetFirst/NextItemInInventory): newest first, as the original adds
    /// items to the front of the list, and each container followed by its contents. Items that found no room
    /// in a full backpack or bag are possessed but not listed until room opens up.
    /// </summary>
    public List<Item> Items(GameObject o)
    {
        EnsureLoaded();
        Load(o);
        var result = new List<Item>();
        if (o is Store store) return StoreOrder(store);
        if (InventoryList(o) is { } list)
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var item = list[i];
                if (item.IsDestroyed || Waiting(o, item)) continue;
                result.Add(item);
                if (o is not Item)
                    for (int j = item.Inventory.Count - 1; j >= 0; j--)
                        if (item.Inventory[j] is { IsDestroyed: false } inner && !Waiting(item, inner)) result.Add(inner);
            }
        return result;
    }

    /// <summary>Store categories in the order the original lists them (armour, weapons, potions, rings, miscellaneous).</summary>
    static readonly int[] StorePanelOrder = { 0, 4, 2, 3, 1 };

    /// <summary>
    /// A store's stock as the original iterates it: by category (the base item's, whatever list the store file
    /// put it in), then baseitems StorePanelSort (highest first), then the whole-gold value of one unit (highest
    /// first), otherwise in the order the store got them.
    /// </summary>
    List<Item> StoreOrder(Store s)
    {
        var bi = _world.Resources.TwoDA("baseitems");
        var stock = s.Inventory.Where(i => !i.IsDestroyed).ToList();
        return stock
            .OrderBy(i => Array.IndexOf(StorePanelOrder, Rules.StorePanel(i.BaseItem)))
            .ThenByDescending(i => bi.GetInt(i.BaseItem, "StorePanelSort", 0))
            .ThenByDescending(i => Rules.GoldValue(i) / Math.Max(1, i.StackSize))
            .ThenBy(stock.IndexOf)
            .ToList();
    }

    /// <summary>An item waiting for room in a full backpack or bag (it has no cell).</summary>
    bool Waiting(GameObject holder, Item item) => item.InvX < 0 && Shape(holder).Limited;

    /// <summary>Everything an object holds: equipment, inventory and container contents.</summary>
    public IEnumerable<Item> AllItems(GameObject o)
    {
        var items = Items(o);
        return o is Creature c ? c.Equipment.Values.Where(i => !i.IsDestroyed).Concat(items) : items;
    }

    /// <summary>GetItemPossessedBy: first item with the tag in inventory, containers or equipment.</summary>
    public Item? FindByTag(GameObject o, string tag) =>
        AllItems(o).FirstOrDefault(i => i.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

    /// <summary>The INVENTORY_SLOT_* an item is equipped in, or -1.</summary>
    public static int SlotOf(Creature c, Item item)
    {
        foreach (var kv in c.Equipment) if (kv.Value == item) return EquipSlot.Index(kv.Key);
        return -1;
    }

    // ------------------------------------------------------------------ inventory iteration cursors

    readonly Dictionary<uint, (List<Item> List, int Index)> _cursors = new();

    public Item? FirstItem(GameObject o)
    {
        var list = Items(o);
        _cursors[o.Id] = (list, 0);
        return list.Count > 0 ? list[0] : null;
    }

    public Item? NextItem(GameObject o)
    {
        if (!_cursors.TryGetValue(o.Id, out var cur)) return null;
        int i = cur.Index + 1;
        while (i < cur.List.Count && cur.List[i].IsDestroyed) i++;
        _cursors[o.Id] = (cur.List, i);
        return i < cur.List.Count ? cur.List[i] : null;
    }

    // ------------------------------------------------------------------ moving items

    /// <summary>Whether <paramref name="target"/> can take <paramref name="item"/> into its inventory.</summary>
    public bool CanHold(GameObject target, Item item)
    {
        if (target.IsDestroyed || target == item) return false;
        switch (target)
        {
            case Creature or Store:
                return true;
            case Placeable p:
                return p.HasInventory;
            case Item bag:
                // Containers only hold non-containers, and can't end up inside themselves.
                if (!Rules.IsContainer(bag.BaseItem) || Rules.IsContainer(item.BaseItem) || item.Inventory.Count > 0) return false;
                for (GameObject? h = bag; h is Item i; h = Holder(i)) if (i == item) return false;
                return true;
            default:
                return false;
        }
    }

    void DetachFrom(GameObject holder, Item item)
    {
        if (holder is Creature c)
        {
            foreach (var kv in c.Equipment.Where(kv => kv.Value == item).ToList()) c.Equipment.Remove(kv.Key);
            // Equipment put away for a polymorph can still be taken.
            if (c.PolymorphForm is { } poly)
                foreach (var kv in poly.Stored.Where(kv => kv.Value == item).ToList()) poly.Stored.Remove(kv.Key);
        }
        InventoryList(holder)?.Remove(item);
        Changed(holder);
    }

    /// <summary>
    /// Takes an item out of wherever it is (inventory, equipment slot, container or the ground).
    /// Unequipping fires OnPlayerUnEquipItem for players; nothing else fires.
    /// </summary>
    public void Detach(Item item)
    {
        if (Holder(item) is { } h)
        {
            if (h is Creature c && SlotOf(c, item) is var slot and >= 0)
            {
                c.Equipment.Remove(EquipSlot.Bit(slot));
                PropertyEffects(c, item, slot, applied: false);
                if (slot == 4) RemoveDefaultAmmo(c);
                EquipmentChanged(c, item, -1);
                FireUnequip(c, item, slot);
            }
            var leaving = LeavingBefore(h, item);
            DetachFrom(h, item);
            leaving?.After();
            if (h is Store) item.StorePanel = -1;
        }
        else if (item.Area != null) _world.LiftFromArea(item);
        item.Possessor = GameObject.InvalidId;
        item.InvX = item.InvY = -1;
    }

    /// <summary>
    /// Two items would merge into one stack. The original compares the blueprint, tag, identified, stolen
    /// and plot flags, charges and properties (a property removed in the running script still counts), but
    /// not names, local variables, or the cursed and droppable flags.
    /// </summary>
    public bool CanStack(Item a, Item b)
    {
        if (a == b || a.BaseItem != b.BaseItem || Rules.MaxStack(a.BaseItem) <= 1) return false;
        if (!a.ResRef.Equals(b.ResRef, StringComparison.OrdinalIgnoreCase) || !a.Tag.Equals(b.Tag, StringComparison.Ordinal)) return false;
        if (a.Identified != b.Identified || a.Stolen != b.Stolen || a.Plot != b.Plot || a.Charges != b.Charges || a.Infinite != b.Infinite) return false;
        return PropertyKeys(a).SequenceEqual(PropertyKeys(b));
    }

    List<string> PropertyKeys(Item item)
    {
        var props = item.Properties.AsEnumerable();
        if (item.RemovedProperties is { } removed && item.HeldAt == _world.Time) props = props.Concat(removed);
        return props.Select(p => $"{p.Type}.{p.SubType}.{p.CostTable}.{p.CostTableValue}.{p.Param1}.{p.Param1Value}.{p.DurationType}")
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Moves an item into <paramref name="target"/>'s inventory, merging it into matching stacks.
    /// Fires acquire/unacquire when the top-level owner changes and OnDisturbed on containers
    /// another <paramref name="actor"/> adds to or takes from. Returns the stack the item ended up in
    /// (the item itself, or the stack it merged into), or null if the target can't hold it.
    /// <paramref name="source"/> names where a new item came from (a store's infinite stock) for OnAcquireItem.
    /// </summary>
    public Item? MoveTo(Item item, GameObject target, GameObject? actor = null, bool merge = true, GameObject? source = null, (int X, int Y)? cell = null, bool force = false)
    {
        EnsureLoaded();
        Load(target);
        _refused = null;
        if (item.IsDestroyed || !CanHold(target, item)) return null;
        // A polymorph's own items never leave the creature (they are destroyed when it changes back).
        if (global::OpenWinter.Game.Rules.EffectSystem.IsFormItem(_world, item)) return null;
        // Grid room: what matching stacks can't take needs a free place (the given cell, else the first that fits).
        (int X, int Y)? place = null;
        bool goldToCreature = item.BaseItem == ItemRules.BaseItemGold && (target is Creature || target is Item b0 && Owner(b0) is Creature);
        if (!goldToCreature && (!merge || StackRoom(target, item) < item.StackSize))
        {
            if (cell is { } c0 && FitsAt(target, item, c0.X, c0.Y, ignore: item)) place = c0;
            else if (cell != null) return null;
            else place = FirstFit(target, item, ignore: item);
            // Forced in (CreateItemOnObject), an item that fits nowhere waits for room.
            if (place == null && Shape(target).Limited && !force)
            {
                InventoryFull(target);
                return null;
            }
        }
        var oldOwner = Owner(item);
        var oldHolder = Holder(item);
        var newOwner = target is Item bag ? Owner(bag) ?? target : target;
        // Mutations inside carried containers and their weight propagation have
        // not been measured for automatic voices. Whole-container transfers are included.
        bool directTransfer = oldHolder is not Item && target is not Item;
        int oldLoad = directTransfer ? VoiceLoadBefore(oldOwner) : -1;
        int newLoad = directTransfer && newOwner != oldOwner ? VoiceLoadBefore(newOwner) : -1;
        var arrival = Arriving(item, target, oldOwner, newOwner, source);
        if (arrival.Skipped) { _refused = item; arrival.After(false); return null; }
        Detach(item);
        int stack = item.StackSize;

        if (newOwner is Creature gc && item.BaseItem == ItemRules.BaseItemGold)
        {
            // Gold items turn into gold when a creature picks them up, with the coins' sound as any pickup.
            string sound = Rules.InventorySound(item);
            _world.Destroy(item);
            AddGold(gc, stack);
            if (gc.IsPC && oldOwner != newOwner) _world.Output?.Notify("item-acquired", gc, new { item = item.Id, sound }, gc);
            Disturb(oldOwner, actor, item, DisturbRemoved);
            arrival.After(true);
            return null;
        }

        var list = InventoryList(target)!;
        Item result = item;
        // Native stores retain separate finite wares, including matching potion stacks.
        // An identical infinite ware absorbs the incoming item instead.
        if (merge && target is Store && list.FirstOrDefault(other => other.Infinite
            && other.BaseItem == item.BaseItem && other.ResRef.Equals(item.ResRef, StringComparison.OrdinalIgnoreCase)
            && other.Tag == item.Tag && other.Identified == item.Identified && other.Stolen == item.Stolen
            && other.Plot == item.Plot && other.Charges == item.Charges
            && PropertyKeys(other).SequenceEqual(PropertyKeys(item))) is { } infinite)
        {
            item.StackSize = 0;
            result = infinite;
        }
        else if (merge && target is not Store)
        {
            int max = Rules.MaxStack(item.BaseItem);
            foreach (var other in list)
            {
                if (item.StackSize <= 0) break;
                if (other.StackSize >= max || !CanStack(other, item)) continue;
                int moved = Math.Min(max - other.StackSize, item.StackSize);
                other.StackSize += moved;
                item.StackSize -= moved;
                result = other;
            }
        }
        if (item.StackSize > 0)
        {
            if (target is Store) item.StorePanel = Rules.StorePanel(item.BaseItem);
            list.Add(item);
            item.Possessor = target.Id;
            (item.InvX, item.InvY) = place ?? FirstFit(target, item) ?? (-1, -1);
            result = item;
        }
        else
        {
            item.StackSize = stack; // keep the merged-away object's numbers meaningful for event handlers
            _world.Destroy(item);
        }

        if (target is Store store) RecordStorePages(store);
        Changed(target);

        // An item a player takes from someone or somewhere else becomes droppable and pickpocketable (nwmain
        // pcdropflag: a ground item reading 0 and 0 reads 1 and 1 once picked up; CreateItemOnObject on a player
        // gives 1 and 1). A creature that isn't a player keeps the item's flags (corpse_drops: pickup, give, take).
        if (oldOwner != newOwner && newOwner is Creature { IsPC: true })
        {
            result.Droppable = true;
            result.Pickpocketable = true;
        }

        if (oldOwner != newOwner) ReportLost(oldOwner, item);

        // Confirm actual acquisition, including a merge that destroys the incoming object.
        // Inventory rearrangement, splitting and initial character loading are not pickups.
        if (oldOwner != newOwner) ReportAcquired(newOwner, result, oldOwner is Store || source is Store ? "item-placed" : "item-acquired");

        // Like the original: acquire and unacquire when the item changes hands, and also when it goes into a bag
        // (even the owner's own: lost, then acquired from nowhere with a stack size of 0). A bag's contents come and
        // go with it, after it. The source is where the item was (a bag it was in, a chest, a creature).
        bool intoBag = target is Item;
        if (oldOwner != newOwner || intoBag && newOwner is Creature)
        {
            var inner = result == item && !intoBag ? item.Inventory.Where(i => !i.IsDestroyed).ToList() : new List<Item>();
            if (oldOwner is Creature lost)
            {
                FireUnacquire(lost, item);
                foreach (var i in inner) FireUnacquire(lost, i);
            }
            if (newOwner is Creature by)
            {
                var from = intoBag ? null : oldHolder ?? source;
                FireAcquire(by, result, from, intoBag ? 0 : stack);
                foreach (var i in inner) FireAcquire(by, i, from, i.StackSize);
            }
        }
        if (oldOwner != newOwner)
        {
            Disturb(oldOwner, actor, item, DisturbRemoved);
            Disturb(newOwner, actor, result, DisturbAdded);
        }
        VoiceLoadChanged(oldOwner, oldLoad);
        VoiceLoadChanged(newOwner, newLoad);
        arrival.After(true, item.IsDestroyed ? result : null);
        return result;
    }

    /// <summary>Puts an item on the ground at a location (ActionPutDownItem, CopyItem without a target).</summary>
    public bool Drop(Item item, Location loc, GameObject? actor = null)
    {
        EnsureLoaded();
        if (item.IsDestroyed || _world.Get(loc.Area) is not Area area) return false;
        if (global::OpenWinter.Game.Rules.EffectSystem.IsFormItem(_world, item)) return false;
        var oldOwner = Owner(item);
        int loadBefore = Holder(item) is Creature ? VoiceLoadBefore(oldOwner) : -1;
        Detach(item);
        var pos = loc.Position;
        if (area.Walkmesh.GroundHeight(pos.X, pos.Y, pos.Z, 2f) is { } z) pos.Z = z;
        _world.AddToArea(item, area, pos, loc.FacingDegrees * MathF.PI / 180f);
        ReportLost(oldOwner, item);
        if (oldOwner is Creature lost)
        {
            FireUnacquire(lost, item);
            foreach (var inner in item.Inventory) FireUnacquire(lost, inner);
        }
        Disturb(oldOwner, actor, item, DisturbRemoved);
        VoiceLoadChanged(oldOwner, loadBefore);
        return true;
    }

    void ReportAcquired(GameObject? newOwner, Item item, string soundEvent = "item-acquired")
    {
        if (newOwner is not Creature { IsPC: true } pc) return;
        foreach (var acquired in new[] { item }.Concat(item.Inventory))
            _world.Output?.SendMessage(pc, _world.Resources.TlkString(10468).Replace("<CUSTOM0>", FeedbackName(acquired)));
        _world.Output?.Notify(soundEvent, pc, new { item = item.Id, sound = Rules.InventorySound(item) }, pc);
    }

    void ReportLost(GameObject? oldOwner, Item item)
    {
        if (oldOwner is Creature { IsPC: true } pc)
            foreach (var lost in new[] { item }.Concat(item.Inventory))
                _world.Output?.SendMessage(pc, _world.Resources.TlkString(10469).Replace("<CUSTOM0>", FeedbackName(lost)));
    }

    /// <summary>
    /// The name "Acquired Item" / "Lost Item" give: an unidentified item's is its base item's ("Acquired Item: Magic
    /// Wand" for an unidentified wand of Lesser Summoning, visual scenes castlines3 and castlines4).
    /// </summary>
    string FeedbackName(Item item) =>
        !item.Identified && _world.Resources.TwoDA("baseitems").GetInt(item.BaseItem, "Name", -1) is >= 0 and var strref
            && _world.TlkStringOverridden(strref) is { Length: > 0 } baseName
            ? baseName : item.Name.Resolve(_world.Resources.Talk);

    /// <summary>Splits <paramref name="count"/> off a stack into a new, unplaced item.</summary>
    public Item? Split(Item item, int count)
    {
        if (count <= 0 || count >= item.StackSize) return null;
        var copy = Copy(item, copyVars: true);
        copy.StackSize = count;
        item.StackSize -= count;
        Changed(item);
        return copy;
    }

    /// <summary>
    /// CreateItemOnObject: a new item from a blueprint in an object's inventory, merging into matching stacks
    /// (what's left becomes a new item, which is returned). A stack size below 1 creates nothing. Items go in
    /// even when the backpack or bag is full (they wait for room); one the target can't hold at all (a bag
    /// in a bag) is created but left nowhere.
    /// </summary>
    public Item? CreateOn(string resref, GameObject target, int stackSize, string newTag)
    {
        EnsureLoaded();
        if (!HasInventory(target) || stackSize < 1) return null;
        var item = _world.Items.FromBlueprint(resref);
        if (item == null) return null;
        // CreateItemOnObject makes the item droppable, not pickpocketable (nwserver corpse_drops: on a creature and in
        // a chest; a player's own get both when they take it, below).
        item.Droppable = true;
        _world.Register(item);
        Normalize(item);
        if (newTag.Length > 0) _world.SetTag(item, newTag);
        item.StackSize = Math.Min(stackSize, Rules.MaxStack(item.BaseItem));
        if (!CanHold(target, item)) return item;
        var result = MoveTo(item, target, force: true);
        // An item whose acquisition a subscriber refused stays, held by nothing (nwnx_events_items).
        // An item whose acquisition a subscriber refused stays, held by nothing (nwnx_events_items).
        if (Refused(item)) return item;
        if (result == null && !item.IsDestroyed && item.Area == null && Holder(item) == null) _world.Destroy(item);
        return target is Store && item.IsDestroyed ? item : result;
    }

    // ------------------------------------------------------------------ equipment

    /// <summary>Equips an item the creature possesses into a slot (INVENTORY_SLOT_*). Fires OnPlayerEquipItem for players.</summary>
    public bool Equip(Creature c, Item item, int slot)
    {
        EnsureLoaded();
        if (slot < 0 || slot > 17 || item.IsDestroyed) return false;
        // Nothing can be equipped while polymorphed.
        if (c.PolymorphForm != null) return false;
        // A weapon that needs both hands goes to the main hand whichever hand it was meant for.
        if (slot == 5 && Rules.IsTwoHanded(item.BaseItem, c.Size)) slot = 4;
        int bit = EquipSlot.Bit(slot);
        if (Owner(item) != c) return false;
        if (c.Equipment.TryGetValue(bit, out var current) && current == item) return true;
        // Moving an equipped item to another slot only works into an empty one.
        if (current != null && SlotOf(c, item) >= 0) return false;
        // NWNX_ON_VALIDATE_ITEM_EQUIP: a skip with a result decides alone, "0" refusing and anything else allowing any slot.
        var validation = ValidateEquipBefore(c, item, slot);
        bool valid = validation is { Skipped: true } ? validation.ResultIsTrue
            : (Rules.EquipableSlots(item.BaseItem) & bit) != 0
              && _world.CanEquipByLevel(c, item) // server item level restriction
              && HasProficiency(c, item) && _world.System<Skills.SkillSystem>().CanUseItem(c, item, out _)
              && Rules.FitsWielder(item.BaseItem, c.Size); // too big or too small to wield
        validation?.Add("BEFORE_RESULT", valid).After();
        if (!valid) return false;

        // What comes off goes where the new item was (when it was in the backpack and fits there).
        (int X, int Y)? freed = Holder(item) == c && c.Inventory.Contains(item) && item.InvX >= 0 ? (item.InvX, item.InvY) : null;
        Detach(item); // out of its bag, inventory or other slot (unequipping fires its event)
        if (c.Equipment.TryGetValue(bit, out var occupant)) Unequip(c, occupant, freed, force: true);
        if (bit == EquipSlot.RightHand && Rules.IsTwoHanded(item.BaseItem, c.Size)
            && c.Equipment.TryGetValue(EquipSlot.LeftHand, out var left)) Unequip(c, left, freed, force: true);
        if (bit == EquipSlot.LeftHand && c.Equipment.TryGetValue(EquipSlot.RightHand, out var right)
            && Rules.IsTwoHanded(right.BaseItem, c.Size)) Unequip(c, right, freed, force: true);

        c.Equipment[bit] = item;
        item.Possessor = c.Id;
        EquipmentChanged(c, item, slot);
        PropertyEffects(c, item, slot, applied: true);
        // Only for players: the original fires nothing when other creatures equip.
        if (c.IsPC)
            _world.RunEvent(_world.Module, "Mod_OnPlrEqItm",
                new EventContext().Set("EquippedItem", item.Id).Set("EquippedBy", c.Id).Set("EquippedSlot", slot));
        return true;
    }

    /// <summary>Whether the creature could hold a weapon in its main hand (the checks <see cref="Equip"/> makes).</summary>
    public bool CanWield(Creature c, Item item) =>
        !item.IsDestroyed && c.PolymorphForm == null && (Rules.EquipableSlots(item.BaseItem) & EquipSlot.RightHand) != 0
        && _world.CanEquipByLevel(c, item) && HasProficiency(c, item) && _world.System<Skills.SkillSystem>().CanUseItem(c, item, out _)
        && Rules.FitsWielder(item.BaseItem, c.Size);

    int ArmorProficiency(Item item)
    {
        int ac = Rules.BaseAC(item);
        return ac >= 6 ? 2 : ac >= 4 ? 4 : ac >= 1 ? 3 : -1; // heavy / medium / light; clothing needs none
    }

    /// <summary>
    /// The proficiency the original requires to equip an item: one of the base item's ReqFeat0-4, or for
    /// armour the feat of its weight class (AC 1-3 light, 4-5 medium, 6+ heavy).
    /// </summary>
    public bool HasProficiency(Creature c, Item item)
    {
        if (item.BaseItem == ItemRules.BaseItemArmor)
        {
            int feat = ArmorProficiency(item);
            return feat < 0 || global::OpenWinter.Game.Rules.CreatureRules.HasFeat(_world, c, feat, ignoreUses: true);
        }
        var bi = _world.Resources.TwoDA("baseitems");
        bool any = false;
        for (int i = 0; i < 5; i++)
        {
            if (bi.ColumnIndex("ReqFeat" + i) < 0 || bi.IsEmpty(item.BaseItem, "ReqFeat" + i)) continue;
            any = true;
            if (global::OpenWinter.Game.Rules.CreatureRules.HasFeat(_world, c, bi.GetInt(item.BaseItem, "ReqFeat" + i), ignoreUses: true)) return true;
        }
        return !any;
    }

    /// <summary>
    /// Moves an equipped item back into the backpack, at <paramref name="cell"/> when given and free,
    /// else the first place it fits. Fails when the backpack is full, unless <paramref name="force"/>
    /// (making room for another item: it is kept unplaced until room opens up). Fires OnPlayerUnEquipItem for players.
    /// </summary>
    public bool Unequip(Creature c, Item item, (int X, int Y)? cell = null, bool force = false)
    {
        int slot = SlotOf(c, item);
        if (slot < 0 || c.PolymorphForm != null) return false; // nor unequipped
        (int X, int Y)? place = cell is { } at && FitsAt(c, item, at.X, at.Y) ? at : FirstFit(c, item);
        if (place == null && !force) { InventoryFull(c); return false; }
        c.Equipment.Remove(EquipSlot.Bit(slot));
        PropertyEffects(c, item, slot, applied: false);
        if (slot == 4) RemoveDefaultAmmo(c);
        c.Inventory.Add(item);
        item.Possessor = c.Id;
        (item.InvX, item.InvY) = place ?? (-1, -1);
        EquipmentChanged(c, item, -1);
        FireUnequip(c, item, slot);
        return true;
    }

    /// <summary>The ammunition a spawned NPC was given goes when its launcher leaves the main hand.</summary>
    void RemoveDefaultAmmo(Creature c)
    {
        foreach (var bit in new[] { EquipSlot.Arrows, EquipSlot.Bolts, EquipSlot.Bullets })
            if (c.Equipment.TryGetValue(bit, out var ammo) && ammo.DefaultAmmo)
            {
                c.Equipment.Remove(bit);
                _world.Destroy(ammo);
            }
    }

    void FireUnequip(Creature c, Item item, int slot)
    {
        if (!c.IsPC) return; // only for players, like OnPlayerEquipItem
        _world.RunEvent(_world.Module, "Mod_OnPlrUnEqItm",
            new EventContext().Set("UnequippedItem", item.Id).Set("UnequippedBy", c.Id).Set("UnequippedSlot", slot));
    }

    /// <summary>Tells clients a creature's visible equipment changed (slot -1: removed).</summary>
    public void EquipmentChanged(Creature c, Item item, int slot)
    {
        Changed(c);
        _world.MarkChanged(c);
        if (_world.Output == null || c.Area == null) return;
        object? visual = null;
        if (slot >= 0 && !item.HiddenWhenEquipped)
        {
            var v = ItemFactory.Visual(EquipSlot.Bit(slot), item);
            visual = new { slot = v.Slot, baseItem = v.BaseItem, modelParts = v.ModelParts, colors = v.Colors, armorParts = v.ArmorParts };
        }
        _world.Output.Notify("creature-equipment", c, new { item = item.Id, slot, visual });
    }

    // ------------------------------------------------------------------ gold

    public void AddGold(Creature c, int amount)
    {
        if (amount == 0) return;
        c.Gold = (int)Math.Clamp((long)c.Gold + amount, 0, int.MaxValue);
        Changed(c);
        if (c.IsPC) _world.Output?.Notify("gold-changed", c, new { gold = c.Gold, delta = amount }, c);
    }

    // ------------------------------------------------------------------ events

    void FireAcquire(Creature by, Item item, GameObject? from, int stack)
    {
        _world.System<Skills.SkillSystem>().OnAcquired(by, item); // Lore identifies what players pick up
        // Queued like the original: the script that created or moved the item finishes first.
        var ctx = new EventContext().Set("AcquiredItem", item.Id).Set("AcquiredBy", by.Id)
            .Set("AcquiredFrom", from?.Id ?? GameObject.InvalidId).Set("AcquiredStack", stack);
        _world.After(0, () => _world.RunEvent(_world.Module, "Mod_OnAcquirItem", ctx));
    }

    void FireUnacquire(Creature lostBy, Item item)
    {
        Requests.QuickbarRequests.LostPairItem(_world, lostBy, item);
        var ctx = new EventContext().Set("LostItem", item.Id).Set("LostBy", lostBy.Id);
        _world.After(0, () => _world.RunEvent(_world.Module, "Mod_OnUnAqreItem", ctx));
    }

    /// <summary>OnInvDisturbed (placeables) / OnDisturbed (creatures) when someone else changes their inventory.</summary>
    void Disturb(GameObject? container, GameObject? actor, Item item, int type)
    {
        if (container == null || actor == null || actor == container) return;
        string field = container switch { Placeable => "OnInvDisturbed", Creature => "ScriptDisturbed", _ => "" };
        if (field.Length == 0) return;
        var ctx = new EventContext().Set("Disturber", actor.Id).Set("DisturbItem", item.Id).Set("DisturbType", type);
        _world.After(0, () => _world.RunEvent(container, field, ctx));
    }

    /// <summary>The "inventory-open" payload: gold, equipped items and carried items (with their bag).</summary>
    public object InventoryView(Creature target)
    {
        var tlk = _world.Resources.Talk;
        // Carrying capacity (encumbrance.2da by strength, tenths of a pound).
        var enc = _world.Resources.TwoDA("encumbrance");
        int str = Math.Clamp(Rules.CarryStrength(target), 0, Math.Max(0, enc.RowCount - 1));
        return new
        {
            creature = target.Id,
            gold = target.Gold,
            weight = Rules.CarriedTenths(target),
            normalLoad = enc.GetInt(str, "Normal", 0),
            heavyLoad = enc.GetInt(str, "Heavy", 0),
            encumbrance = Rules.Encumbrance(target),
            grid = new { width = BackpackGrid.Width, pageHeight = BackpackGrid.PageHeight, pages = BackpackGrid.Pages },
            bagGrid = new { width = BagGrid.Width, pageHeight = BagGrid.PageHeight, pages = BagGrid.Pages },
            equipped = target.Equipment.Select(kv => new
            {
                slot = EquipSlot.Index(kv.Key), id = kv.Value.Id, name = kv.Value.Name.Resolve(tlk), baseItem = kv.Value.BaseItem,
                stack = kv.Value.StackSize, icon = Icon(kv.Value, target), identified = kv.Value.Identified, info = ItemInfo(kv.Value, target),
            }).ToArray(),
            items = Items(target).Select(i => new
            {
                id = i.Id, name = i.Name.Resolve(tlk), baseItem = i.BaseItem, stack = i.StackSize, container = Holder(i)?.Id ?? target.Id,
                icon = Icon(i, target), identified = i.Identified, x = i.InvX, y = i.InvY, info = ItemInfo(i, target),
            }).ToArray(),
        };
    }

    /// <summary>
    /// Inventory icon layers, bottom first: simple and layered items are one i&lt;class&gt;_&lt;model&gt;
    /// icon, composite weapons stack bottom/middle/top part icons, armour composes palette-tinted ipm_* parts.
    /// A spell scroll shows its spell's icon instead (<see cref="SpellScrollIcon"/>).
    /// </summary>
    public string[] Icon(Item item, Creature? viewer = null)
    {
        if (SpellScrollIcon(item) is { } spell) return [spell];
        var bi = _world.Resources.TwoDA("baseitems");
        string cls = bi.Get(item.BaseItem, "ItemClass").ToLowerInvariant();
        int type = int.TryParse(bi.Get(item.BaseItem, "ModelType"), out var t) ? t : 0;
        string fallback = bi.Get(item.BaseItem, "DefaultIcon").ToLowerInvariant();
        if (cls.Length == 0) return fallback.Length > 0 ? [fallback] : [];
        return type switch
        {
            // Composite parts are shape*10 + colour, the same number the part models use.
            2 => [$"i{cls}_b_{Part(0):000}", $"i{cls}_m_{Part(1):000}", $"i{cls}_t_{Part(2):000}"],
            3 => ArmorIcon(item, viewer ?? Owner(item) as Creature),
            1 => LayeredIcon(item, $"i{cls}_{GetAppearance(item, 0, 0):000}"),
            _ => [$"i{cls}_{GetAppearance(item, 0, 0):000}"],
        };
        int Part(int i) => Wide(item.Template, $"ModelPart{i + 1}");
    }

    /// <summary>
    /// The original draws a spell scroll (base item 75 only: not blank or crafted scrolls, nor a copy of row 75 in
    /// another row) as the iprp_spells.2da Icon of its first Cast Spell property in property order (the iss_* art,
    /// a spell glyph on parchment), whole and unscaled in place of its own icon, identified or not, wherever an item
    /// icon is drawn. Without a Cast Spell property, or when the row's Icon is empty or names no texture, the scroll
    /// keeps its own icon; other items with Cast Spell properties (potions, wands, rods, rings) keep theirs
    /// (visual scene <c>scrollicons</c>).
    /// </summary>
    string? SpellScrollIcon(Item item)
    {
        if (item.BaseItem != ItemRules.SpellScrollBaseItem) return null;
        var cast = item.Properties.FirstOrDefault(p => p.Valid && p.Type == ItemRules.PropCastSpell);
        if (cast == null) return null;
        string icon = _world.Resources.TwoDA("iprp_spells").Get(cast.SubType, "Icon").ToLowerInvariant();
        return icon.Length > 0 && HasIcon(icon) ? icon : null;
    }

    /// <summary>
    /// The original fires OnAcquireItem for every item a creature brings with it when it is made: from its blueprint
    /// (placed in an area, CreateObject), by CopyObject, or loaded from SQL, the campaign database or JSON. Equipped
    /// items come first in slot order, then the backpack in the order the items went in, each bag followed by its
    /// contents; there is no source and the stack size is 0. Queued, so the events run after the creating script and
    /// before the creature's OnSpawn; call it before the creature enters its area (its OnEnter comes after them).
    /// </summary>
    public void FireSpawnAcquires(Creature c)
    {
        _world.Register(c);
        foreach (var kv in c.Equipment.OrderBy(kv => kv.Key).ToList()) FireAcquire(c, kv.Value, null, 0);
        foreach (var item in c.Inventory.ToList())
        {
            FireAcquire(c, item, null, 0);
            foreach (var inner in item.Inventory) FireAcquire(c, inner, null, 0);
        }
    }

    /// <summary>
    /// OnAcquireItem for an item put straight into <paramref name="holder"/> (loaded from SQL, the campaign database or
    /// JSON) when a creature ends up with it; into a bag the stack size is 0, as with every move into a bag.
    /// </summary>
    public void Acquired(GameObject holder, Item item)
    {
        Changed(holder);
        var owner = holder is Item bag ? Owner(bag) : holder;
        ReportAcquired(owner, item);
        if (owner is Creature by)
        {
            FireAcquire(by, item, null, holder is Item ? 0 : item.StackSize);
            foreach (var inner in item.Inventory) FireAcquire(by, inner, null, inner.StackSize);
        }
    }

    /// <summary>The original fires OnAcquireItem for everything a player carries when they enter the module.</summary>
    public void FireLoginAcquires(Creature pc)
    {
        KeepPlayerItemFlags(pc);
        foreach (var item in AllItems(pc).ToList()) FireAcquire(pc, item, null, item.StackSize);
    }

    /// <summary>
    /// The original's character files and saves carry both flags on every item (a save's player items all read 1). A new
    /// character's blueprint references, and characters and save games OpenWinter wrote before it always wrote the flags
    /// (it left them out when 1), don't: a player's own items without them are droppable and pickpocketable, as anything
    /// a player takes becomes. Run at login and when a save game is loaded.
    /// </summary>
    public void KeepPlayerItemFlags(Creature pc)
    {
        EnsureLoaded();
        Load(pc);
        foreach (var item in AllItems(pc))
        {
            if (!item.Template.Has("Dropable")) item.Droppable = true;
            if (!item.Template.Has("Pickpocketable")) item.Pickpocketable = true;
        }
    }

    // ------------------------------------------------------------------ misc state kept per world

    /// <summary>SetUseableFlag on doors and creatures (placeables and items have their own fields).</summary>
    public readonly Dictionary<uint, bool> UseableOverrides = new();
}
