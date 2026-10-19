using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>The ground radial's Equip commands first complete the ordinary pickup action.</summary>
public sealed class PickUpAndEquipAction : ItemAction
{
    public uint ItemId;
    public bool Offhand;
    /// <summary>An explicit paperdoll slot; -1 keeps radial selection rules.</summary>
    public int Slot = -1;
    PickUpItemAction? _pickup;
    bool _acquired;

    public override int ActionType => 1;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature pc) return ActionStatus.Failed;
        if (!_acquired)
        {
            _pickup ??= new PickUpItemAction { ItemId = ItemId };
            var status = _pickup.Step(world, owner, dt);
            if (status != ActionStatus.Done) return status;
            // Native OnAcquire observes possession before either hand changes. Let queued
            // acquisition scripts finish; they may destroy/move the item or cancel this action.
            _acquired = true;
            return ActionStatus.InProgress;
        }
        if (world.Get<Item>(_pickup!.ResultItemId) is not { } acquired || world.Inventory.Owner(acquired) != pc || acquired.IsDestroyed)
            return ActionStatus.Failed;
        var item = acquired;

        int mask = world.Inventory.Rules.EquipableSlots(item.BaseItem);
        int slot = Slot;
        if (slot < 0 && Offhand)
        {
            slot = 5;
            // Native's offhand command puts a one-handed weapon into an empty main hand.
            if ((mask & EquipSlot.RightHand) != 0 && !pc.Equipment.ContainsKey(EquipSlot.RightHand)) slot = 4;
        }
        else if (slot < 0)
            for (int i = 0; i < 18; i++)
                if ((mask & EquipSlot.Bit(i)) != 0) { slot = i; break; }
        return slot >= 0
            ? new EquipItemAction { ItemId = item.Id, Slot = slot }.Step(world, pc, dt)
            : ActionStatus.Failed;
    }

    public override void Cancel(World world, GameObject owner) => _pickup?.Cancel(world, owner);
}
