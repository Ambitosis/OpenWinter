using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Requests;

/// <summary>Authenticated ground-item equip commands; possession is acquired through the pickup action.</summary>
static class GroundItemRequests
{
    [ClientRequest("ground-item-carry")]
    static void Carry(Request request)
    {
        if (request.Obj<Item>("item") is not { Area: not null } item || item.IsDestroyed
            || item.Area != request.Player.Area || item.Possessor != GameObject.InvalidId || !item.UseableOnGround
            || !ActionQueues.Accepts(request.Player)) return;
        var inventory = request.World.Inventory;
        request.World.Output?.Notify("ground-item-carry", item, new
        {
            area = item.Area.Id, id = item.Id, name = item.Name.Resolve(request.World.Resources.Talk),
            baseItem = item.BaseItem, stack = item.StackSize, icon = inventory.Icon(item, request.Player),
            identified = item.Identified, info = inventory.ItemInfo(item, request.Player),
        }, request.Player);
    }

    [ClientRequest("equip-ground")]
    static void Equip(Request request)
    {
        if (request.Obj<Item>("item") is not { Area: not null } item || item.IsDestroyed
            || item.Area != request.Player.Area || item.Possessor != GameObject.InvalidId || !item.UseableOnGround
            || !ActionQueues.Accepts(request.Player)) return;
        bool offhand = request.Bool("offhand");
        int slot = request.Int("slot", -1);
        int mask = request.World.Inventory.Rules.EquipableSlots(item.BaseItem);
        if (mask == 0 || offhand && (mask & EquipSlot.LeftHand) == 0
            || slot < -1 || slot >= 18 || slot >= 0 && (mask & EquipSlot.Bit(slot)) == 0) return;
        ActionQueues.EnqueuePlayerTask(request.World, request.Player, new PickUpAndEquipAction { ItemId = item.Id, Offhand = offhand, Slot = slot });
    }
}
