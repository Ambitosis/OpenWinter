using OpenWinter.Game.Inventory;
using OpenWinter.Client.Gameplay.Logic;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI;

public partial class RadialMenu
{
    ItemEntry? _inventoryItem;
    OpenWinter.Client.Views.AreaView? _inventoryArea;
    Func<bool>? _inventoryValid;
    InventoryPanel? _inventoryData;
    Action<ItemEntry>? _inventoryDrop;
    Action<ItemEntry>? _inventorySplit;
    Action<ItemEntry>? _inventoryOpenBag;
    Action<ItemEntry>? _inventorySell;
    Func<ItemEntry, bool>? _inventoryCanSell;
    ItemEntry? _foreignItem;
    Func<bool>? _foreignValid;

    /// <summary>An item ring is open: one of the player's items, or an item in a container or store.</summary>
    bool ItemContext => _inventoryItem != null || _foreignItem != null;

    /// <summary>
    /// Owned inventory items have no world view. Keep their authoritative snapshot and lifetime separately.
    /// <paramref name="sell"/> is set while a store is open: the ring then has Sell at E (the original's, measured), and
    /// <paramref name="canSell"/> greys it in the modern skin when the store won't take the item (the original never does).
    /// </summary>
    public void OpenInventory(ItemEntry item, Vector2 at, InventoryPanel data, Func<bool> visible,
        Action<ItemEntry> drop, Action<ItemEntry> split, Action<ItemEntry>? openBag = null,
        Action<ItemEntry>? sell = null, Func<ItemEntry, bool>? canSell = null)
    {
        Close();
        _target = null;
        _ground = null;
        _tileQuery++;
        _tileActions = [];
        _inventoryItem = item;
        _inventoryArea = Session.Area;
        _inventoryData = data;
        _inventoryValid = visible;
        _inventoryDrop = drop;
        _inventorySplit = split;
        _inventoryOpenBag = openBag;
        _inventorySell = sell;
        _inventoryCanSell = canSell;
        BeginPointerSelection(at);
        data.Changed += InventoryChanged;
        _controlledAtOpen = Session.ControlledId;
        _stack.Clear();
        _stack.Add(new Ring { Title = item.Name, Build = InventoryRoot });
        var view = GetViewportRect().Size;
        // Measured native item rings clamp their center 154 px from the top/left edge.
        float margin = Skin == null ? Outer + 34 : 154;
        _centre = new Vector2(Math.Clamp(at.X, margin, Math.Max(margin, view.X - margin)),
            Math.Clamp(at.Y, margin, Math.Max(margin, view.Y - margin)));
        _pointer = at;
        RecenterPointer();
        _hover = -1;
        _dragging = false;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = true;
        GetParent().MoveChild(this, -1);
        _book.Refresh(item.Id);
        if (Session.Content != null && _book.Spells.Count == 0) Session.Request("spellbook");
        Rebuild();
        Animate(0.86f);
    }

    void ClearInventoryContext()
    {
        if (_inventoryData != null) _inventoryData.Changed -= InventoryChanged;
        _inventoryData = null;
        _inventoryItem = null;
        _inventoryArea = null;
        _inventoryValid = null;
        _inventoryDrop = null;
        _inventorySplit = null;
        _inventoryOpenBag = null;
        _inventorySell = null;
        _inventoryCanSell = null;
        _foreignItem = null;
        _foreignValid = null;
    }

    /// <summary>
    /// A ring for an item the player doesn't own yet (in an open container or a store), with the caller's entries.
    /// The original has no drop-down lists, so these right-clicks open a ring too. It closes when
    /// <paramref name="valid"/> turns false (the window closed).
    /// </summary>
    public void OpenItemMenu(ItemEntry item, Vector2 at, Func<bool> valid, Func<List<RadialEntry>> build)
    {
        Close();
        _target = null;
        _ground = null;
        _custom = true;
        _tileQuery++;
        _tileActions = [];
        _foreignItem = item;
        _foreignValid = valid;
        BeginPointerSelection(at);
        _controlledAtOpen = Session.ControlledId;
        _stack.Clear();
        _stack.Add(new Ring { Title = item.Name, Build = build });
        var view = GetViewportRect().Size;
        float margin = Skin == null ? Outer + 34 : 154;
        _centre = new Vector2(Math.Clamp(at.X, margin, Math.Max(margin, view.X - margin)),
            Math.Clamp(at.Y, margin, Math.Max(margin, view.Y - margin)));
        _pointer = at;
        RecenterPointer();
        Preview = [];
        _hover = -1;
        _dragging = false;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = true;
        GetParent().MoveChild(this, -1);
        Rebuild();
        Animate(0.86f);
    }

    void InventoryChanged()
    {
        if (_inventoryItem == null || _inventoryData == null) return;
        var latest = _inventoryData.Carried.Concat(_inventoryData.Equipped.Values)
            .Concat(_inventoryData.BagContents.Values.SelectMany(items => items))
            .FirstOrDefault(item => item.Id == _inventoryItem.Id);
        if (latest == null || latest.Slot != _inventoryItem.Slot) { Close(); return; }
        _inventoryItem = latest;
        Rebuild();
    }

    List<RadialEntry> InventoryRoot()
    {
        if (_inventoryItem is not { } item) return [];
        var drop = _inventoryDrop;
        var split = _inventorySplit;
        var sell = _inventorySell;
        var openBag = _inventoryOpenBag;
        var ability = _book.Item(item.Id) is { Powers.Length: > 0 } a ? a : null;
        var layout = ItemRingLayout.Owned(new ItemRingFacts(item.IsBag && openBag != null, item.Slot >= 0,
            item.Info?.Slots > 0, Fact("equipOffhand"), item.Stack, ability != null, sell != null));
        var ring = FixedRing();
        for (int slot = 0; slot < 8; slot++)
        {
            RadialEntry? entry = layout[slot] switch
            {
                ItemRingCommand.Examine => new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(item.Id) },
                ItemRingCommand.CastSpell => Category("Cast Spell", "magic", () => Spells(null, false)),
                // The original's plot items keep an ordinary Drop (it does nothing); the modern skin greys it.
                ItemRingCommand.Drop => Icon(new RadialEntry
                {
                    Label = "Drop", Glyph = "drop",
                    Disabled = Skin == null && item.Info?.Plot == true, Act = () => drop?.Invoke(item)
                }, "ir_drop"),
                ItemRingCommand.Open => Icon(new RadialEntry { Label = "Open", Glyph = "bag", Act = () => openBag?.Invoke(item) }, "ir_open"),
                // The original shows Sell alike on stolen, plot, worn and too dear items; the modern skin greys those.
                ItemRingCommand.Sell => Icon(new RadialEntry
                {
                    Label = "Sell", Glyph = "bag",
                    Disabled = Skin == null && _inventoryCanSell?.Invoke(item) == false, Act = () => sell?.Invoke(item)
                }, "ir_sell"),
                ItemRingCommand.Equip => Icon(new RadialEntry
                {
                    Label = "Equip", Glyph = "weapon",
                    Act = () => Session.Request("equip", new { item = item.Id })
                }, "ir_equip_r"),
                ItemRingCommand.Unequip => Icon(new RadialEntry
                {
                    Label = "Unequip", Glyph = "bag",
                    Act = () => Session.Request("unequip", new { item = item.Id })
                }, "ir_unequip"),
                ItemRingCommand.EquipOffhand => Icon(new RadialEntry
                {
                    Label = "Equip Offhand", Glyph = "offhand",
                    Act = () => Session.Request("equip", new { item = item.Id, offhand = true })
                }, "ir_equip_l"),
                ItemRingCommand.Split => Icon(new RadialEntry { Label = "Split", Glyph = "bag", Act = () => split?.Invoke(item) }, "ir_split"),
                _ => null,
            };
            if (entry != null) ring[slot] = entry;
        }
        if (ability != null)
        {
            RadialEntry Power(ItemPower power)
            {
                uint id = item.Id;
                string icon = power.Spell >= 0 ? Session.Content?.Resources.TwoDA("spells").Get(power.Spell, "IconResRef") ?? "" : power.Icon;
                if (icon is "****" or "") icon = power.Icon;
                return new RadialEntry
                {
                    Label = "Use : " + power.Name, Glyph = "potion", Disabled = !ability.Usable || !power.Ready,
                    Look = new SlotLook(power.Name, icon.Length > 0 ? [icon] : item.Icon,
                        "potion", power.Uses, false, ability.Usable && power.Ready, "Item power", ""),
                    Slot = new BarSlot { Kind = "item", Item = id, Prop = power.Index, Name = item.Name },
                    Act = () => _user.Aim(ItemRules.IsPotion(item.BaseItem) ? AbilityUser.TargetSelf : power.TargetType, null, power.Name, (target, point) =>
                    {
                        if (point is { } p) Session.Request("use-item", new { item = id, prop = power.Index, pos = new[] { p.X, p.Y, p.Z } });
                        else Session.Request("use-item", new { item = id, prop = power.Index, target = target ?? Session.ControlledId });
                    }),
                };
            }
            ring[3] = ability.Powers.Length == 1 ? Power(ability.Powers[0])
                : Category("Use", "use", () => ability.Powers.Select(Power).ToList());
        }
        return ring;
    }
}
