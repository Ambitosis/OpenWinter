using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>Equipment commands share one presentation boundary, including compound hand changes.</summary>
public abstract class EquipmentAction : ItemAction
{
    public sealed override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature creature) return ActionStatus.Failed;
        var right = creature.Equipment.GetValueOrDefault(EquipSlot.RightHand);
        var left = creature.Equipment.GetValueOrDefault(EquipSlot.LeftHand);
        var result = Execute(world, creature);
        // Publish one draw for the completed hand change, not each internal remove/equip.
        // Commands without a net hand change stay silent; a partial change still needs its new look.
        bool rightChanged = right != creature.Equipment.GetValueOrDefault(EquipSlot.RightHand);
        bool leftChanged = left != creature.Equipment.GetValueOrDefault(EquipSlot.LeftHand);
        if (rightChanged || leftChanged)
            world.Output?.Notify("equipment-draw", creature, new { animation = rightChanged || UsesBothHands ? "drwright" : "drwleft" });
        return result;
    }

    protected abstract ActionStatus Execute(World world, Creature creature);
    protected virtual bool UsesBothHands => false;
}
