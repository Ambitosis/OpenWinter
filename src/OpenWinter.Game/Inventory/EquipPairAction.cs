using OpenWinter.Game.Objects;
using OpenWinter.Game.Skills;

namespace OpenWinter.Game.Inventory;

/// <summary>One quickbar command for the measured main/off-hand equipment toggle.</summary>
public sealed class EquipPairAction : EquipmentAction
{
    protected override bool UsesBothHands => true;
    public uint MainItem, SecondaryItem;

    protected override ActionStatus Execute(World world, Creature pc)
    {
        if (pc.PolymorphForm != null || MainItem == SecondaryItem
            || world.Get<Item>(MainItem) is not { IsDestroyed: false } main
            || world.Get<Item>(SecondaryItem) is not { IsDestroyed: false } secondary
            || world.Inventory.Owner(main) != pc || world.Inventory.Owner(secondary) != pc) return ActionStatus.Failed;
        var inventory = world.Inventory;
        bool right = pc.Equipment.GetValueOrDefault(EquipSlot.RightHand) == main;
        bool left = pc.Equipment.GetValueOrDefault(EquipSlot.LeftHand) == secondary;
        if (right && left)
        {
            bool a = inventory.Unequip(pc, secondary);
            bool b = inventory.Unequip(pc, main);
            return a && b ? ActionStatus.Done : ActionStatus.Failed;
        }
        bool Eligible(Item item, int bit) => (inventory.Rules.EquipableSlots(item.BaseItem) & bit) != 0
            && !inventory.Rules.IsTwoHanded(item.BaseItem, pc.Size)
            && world.CanEquipByLevel(pc, item) && inventory.HasProficiency(pc, item)
            && world.System<SkillSystem>().CanUseItem(pc, item, out _)
            && inventory.Rules.FitsWielder(item.BaseItem, pc.Size);
        if (!Eligible(main, EquipSlot.RightHand) || !Eligible(secondary, EquipSlot.LeftHand)) return ActionStatus.Failed;
        // The native pair command removes a different off-hand first, then replaces
        // main and off-hand. Matching partial members are re-equipped, not toggled off.
        if (pc.Equipment.GetValueOrDefault(EquipSlot.LeftHand) is { } oldLeft && oldLeft != secondary)
            inventory.Unequip(pc, oldLeft, force: true);
        if (right) inventory.Unequip(pc, main, force: true);
        bool equippedMain = inventory.Equip(pc, main, 4);
        if (left) inventory.Unequip(pc, secondary, force: true);
        bool equippedSecondary = inventory.Equip(pc, secondary, 5);
        return equippedMain && equippedSecondary ? ActionStatus.Done : ActionStatus.Failed;
    }
}
