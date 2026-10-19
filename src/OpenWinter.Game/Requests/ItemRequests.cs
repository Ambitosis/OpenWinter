using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Inventory window actions beyond equip/unequip/drop: using an item (potions, scrolls, wands,
/// healer's kits) and splitting a stack.
/// </summary>
static class ItemRequests
{
    /// <summary>
    /// Uses an item: its first usable property that has a charge or use left, on {target} or the
    /// player (the inventory's Use / double-click on a potion).
    /// </summary>
    [ClientRequest("item-use")]
    static void Use(Request r)
    {
        if (r.Player.IsDead || !r.Player.Commandable || r.Player.Area == null) return;
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item)) return;
        var inv = r.World.Inventory;
        var prop = item.Properties.FirstOrDefault(p => p.Useable && p.DurationType is 1 or 2
            && (p.Type == ItemRules.PropCastSpell ? inv.CanUseItemProperty(item, p) : p.Type == 80));
        if (prop == null) return;
        uint target = r.Id("target");
        if (ItemRules.IsPotion(item.BaseItem) || r.World.Get(target) == null) target = r.Player.Id;
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, new UseItemAction { ItemId = item.Id, Property = prop, Target = target });
    }

    /// <summary>Splits {count} off a stack into a new stack in the same bag: {item, count}.</summary>
    [ClientRequest("item-split")]
    static void Split(Request r)
    {
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item) || InventorySystem.SlotOf(r.Player, item) >= 0) return;
        int count = r.Int("count");
        var inv = r.World.Inventory;
        if (inv.Holder(item) is not { } holder || inv.Split(item, count) is not { } part) return;
        if (inv.MoveTo(part, holder, merge: false) == null)
        {
            // No room: put the split part back.
            item.StackSize += part.StackSize;
            r.World.Destroy(part);
        }
    }

    /// <summary>
    /// Moves an item to a cell of an inventory grid: {item, x, y, target}. The target is the player's
    /// backpack (default), one of their bags, or the container they have open; the item is theirs (from
    /// the backpack, a bag or an equipment slot) or comes from that open container. Dropped onto a
    /// matching stack it merges; onto an item of the same size in the same grid the two swap.
    /// </summary>
    [ClientRequest("item-move")]
    static void Move(Request r)
    {
        if (r.Obj<Item>("item") is not { } item) return;
        var inv = r.World.Inventory;
        var open = r.World.System<ContainerService>().OpenFor(r.Player);
        uint targetId = r.Id("target");
        GameObject target = targetId == 0 || targetId == GameObject.InvalidId ? r.Player : r.World.Get(targetId) ?? r.Player;
        bool ownTarget = target == r.Player || target is Item bag && inv.Owner(bag) == r.Player;
        if (!ownTarget && target != open) return;
        bool fromOpen = open != null && inv.Owner(item) == open;
        if (!r.Owns(item) && !fromOpen) return;
        int x = r.Int("x", -1), y = r.Int("y", -1);
        if (x < 0 || y < 0)
        {
            // No cell: wherever it fits.
            if (target == r.Player && InventorySystem.SlotOf(r.Player, item) >= 0)
                ActionQueues.EnqueuePlayerTask(r.World, r.Player, new UnequipItemAction { ItemId = item.Id });
            else if (inv.Holder(item) != target) inv.MoveTo(item, target, actor: r.Player);
        }
        else if (target == r.Player && InventorySystem.SlotOf(r.Player, item) >= 0)
        {
            // Out of an equipment slot straight into a cell.
            if (inv.FitsAt(r.Player, item, x, y))
                ActionQueues.EnqueuePlayerTask(r.World, r.Player, new UnequipItemAction { ItemId = item.Id, Cell = (x, y) });
        }
        else inv.MoveToCell(item, target, x, y, actor: r.Player);
    }

    /// <summary>Repacks the player's backpack (or one of their bags: {target}) server-side, largest items first.</summary>
    [ClientRequest("inventory-arrange")]
    static void Arrange(Request r)
    {
        var inv = r.World.Inventory;
        GameObject target = r.Obj<Item>("target") is { } bag && inv.Owner(bag) == r.Player ? bag : r.Player;
        inv.AutoArrange(target);
    }

    [ClientRequest("drop-gold")]
    static void DropGold(Request r)
    {
        int count = r.Int("count");
        if (count <= 0 || count > r.Player.Gold || !ActionQueues.Accepts(r.Player)) return;
        if (!ItemDropDestination.Read(r, out var destination)) return;
        var drop = new DropGoldAction { Count = count, Destination = destination };
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, drop);
        if (destination is { } spot && r.World.Output is GameServer server) server.TrackOrder(r.Player, drop, spot.Position);
    }

}
