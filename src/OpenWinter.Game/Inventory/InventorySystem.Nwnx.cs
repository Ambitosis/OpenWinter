using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>
/// NWNX_Events item events raised by the inventory (parity suite nwnx_events_items): a creature acquiring an item
/// (NWNX_ON_ITEM_ACQUIRE, RESULT 0 before and the outcome after; skipping refuses the item), placeables and bags
/// gaining or losing one (NWNX_ON_INVENTORY_ADD_ITEM / _REMOVE_ITEM; a creature's own inventory raises neither, and
/// neither can be skipped), item destruction (NWNX_ON_ITEM_DESTROY_OBJECT), equip validation (NWNX_ON_VALIDATE_ITEM_EQUIP: a skip with a result decides alone,
/// "0" refusing and anything else allowing any slot) and gold (NWNX_ON_INVENTORY_ADD_GOLD / _REMOVE_GOLD).
/// </summary>
public sealed partial class InventorySystem
{
    /// <summary>The events of one item arriving somewhere; <see cref="Skipped"/> means it must not arrive.</summary>
    readonly struct Arrival
    {
        public readonly NwnxEvent? Acquire, Add;
        public readonly bool Skipped;
        public Arrival(NwnxEvent? acquire, NwnxEvent? add, bool skipped) { Acquire = acquire; Add = add; Skipped = skipped; }

        /// <param name="into">The stack the item merged into, named instead when the item itself is gone.</param>
        public void After(bool ok, Item? into = null)
        {
            if (into != null && Acquire != null) Acquire.Add("ITEM", into);
            Acquire?.Add("RESULT", ok).After();
            Add?.After();
        }
    }

    Arrival Arriving(Item item, GameObject target, GameObject? oldOwner, GameObject? newOwner, GameObject? source)
    {
        NwnxEvent? acquire = null, add = null;
        if (target is Creature by && newOwner == by && oldOwner != by)
            acquire = NwnxEvent.Begin(_world, "NWNX_ON_ITEM_ACQUIRE", by)?.Add("ITEM", item).Add("GIVER", oldOwner ?? source).Add("RESULT", 0);
        else if (target is Placeable or Item)
            add = NwnxEvent.Begin(_world, "NWNX_ON_INVENTORY_ADD_ITEM", target)?.Add("ITEM", item);
        // Skipping the container's event doesn't stop the item going in.
        bool skipped = acquire?.Before() == true;
        add?.Before();
        return new Arrival(acquire, add, skipped);
    }

    /// <summary>The item whose acquisition a subscriber refused in the last MoveTo.</summary>
    Item? _refused;

    /// <summary>
    /// Whether the last <see cref="MoveTo"/> returned null because an NWNX_ON_ITEM_ACQUIRE subscriber refused the item
    /// (it then stays where it was, or held by nothing if it was new).
    /// </summary>
    public bool Refused(Item item) => _refused == item;

    /// <summary>
    /// An item destroyed by DestroyObject or used up: NWNX_ON_ITEM_DESTROY_OBJECT around it, OBJECT_SELF the item;
    /// a skip keeps it (nwnx_events_items).
    /// </summary>
    public void DestroyItem(Item item)
    {
        var ev = NwnxEvent.Begin(_world, "NWNX_ON_ITEM_DESTROY_OBJECT", item);
        if (ev?.Before() != true) _world.Destroy(item);
        ev?.After();
    }

    /// <summary>NWNX_ON_INVENTORY_REMOVE_ITEM's _BEFORE half as an item leaves a placeable or a bag (not skippable).</summary>
    NwnxEvent? LeavingBefore(GameObject holder, Item item)
    {
        var ev = holder is Placeable or Item ? NwnxEvent.Begin(_world, "NWNX_ON_INVENTORY_REMOVE_ITEM", holder)?.Add("ITEM", item) : null;
        ev?.Before();
        return ev;
    }

    /// <summary>NWNX_ON_VALIDATE_ITEM_EQUIP's _BEFORE half (Skipped and Result set), or null when nobody listens.</summary>
    NwnxEvent? ValidateEquipBefore(Creature c, Item item, int slot)
    {
        var ev = NwnxEvent.Begin(_world, "NWNX_ON_VALIDATE_ITEM_EQUIP", c)?.Add("ITEM_OBJECT_ID", item).Add("SLOT", slot);
        ev?.Before();
        return ev;
    }

    /// <summary>
    /// Gold changing hands as NWNX sees it: NWNX_ON_INVENTORY_ADD_GOLD / _REMOVE_GOLD around the change; a skip
    /// leaves the gold as it was. Returns false when skipped.
    /// </summary>
    public bool ChangeGold(Creature c, int amount)
    {
        if (amount == 0) return true;
        var ev = NwnxEvent.Begin(_world, amount > 0 ? "NWNX_ON_INVENTORY_ADD_GOLD" : "NWNX_ON_INVENTORY_REMOVE_GOLD", c)?.Add("GOLD", Math.Abs(amount));
        bool skipped = ev?.Before() == true;
        if (!skipped) AddGold(c, amount);
        ev?.After();
        return !skipped;
    }

    /// <summary>
    /// NWNX_ON_ITEMPROPERTY_EFFECT_APPLIED / _REMOVED for each property of an item put on or taken off, OBJECT_SELF
    /// the item, inside the equip or unequip (nwnx_events_items). The ID whitelist (on by default) picks property
    /// types. Skipping the application keeps that property's effects off the creature until it comes off.
    /// </summary>
    void PropertyEffects(Creature c, Item item, int slot, bool applied, bool loading = false)
    {
        string name = applied ? "NWNX_ON_ITEMPROPERTY_EFFECT_APPLIED" : "NWNX_ON_ITEMPROPERTY_EFFECT_REMOVED";
        if (!EventsListening(name)) { if (!applied) foreach (var p in item.Properties) ItemPropertyEffects.Restore(p); return; }
        foreach (var p in item.Properties.ToList())
        {
            if (!p.Valid) continue;
            var ev = NwnxEvent.Begin(_world, name, item, "NWNX_ON_ITEMPROPERTY_EFFECT", p.Type);
            if (ev == null) { if (!applied) ItemPropertyEffects.Restore(p); continue; }
            ev.Add("CREATURE", c).Add("LOADING_GAME", loading).Add("INVENTORY_SLOT", slot).Add("PROPERTY", p.Type)
                .Add("ID", (int)p.Id).Add("SUBTYPE", p.SubType & 0xffff).Add("TAG", p.Tag).Add("COST_TABLE", p.CostTable & 0xff)
                .Add("COST_TABLE_VALUE", p.CostTableValue & 0xffff).Add("PARAM1", p.Param1 & 0xff).Add("PARAM1_VALUE", p.Param1Value & 0xff);
            bool skipped = ev.Before();
            if (applied && skipped) ItemPropertyEffects.Suppress(p);
            else if (!applied) ItemPropertyEffects.Restore(p);
            ev.After();
        }
    }

    bool EventsListening(string name) => Nwnx.Plugins.EventsPlugin.Listening(_world, name);
}

/// <summary>
/// Equipped item properties whose effects an NWNX subscriber kept off the wearer, keyed by the property instance (each
/// world has its own, so worlds never share entries; the count only lets the common case skip the lookup).
/// </summary>
public static class ItemPropertyEffects
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemProperty, object> _suppressed = new();
    static int _count;

    public static void Suppress(ItemProperty p)
    {
        if (_suppressed.TryAdd(p, p)) Interlocked.Increment(ref _count);
    }

    public static void Restore(ItemProperty p)
    {
        if (_count > 0 && _suppressed.Remove(p)) Interlocked.Decrement(ref _count);
    }

    /// <summary>Whether a property's effects apply to whoever wears its item.</summary>
    public static bool Active(ItemProperty p) => _count == 0 || !_suppressed.TryGetValue(p, out _);
}
