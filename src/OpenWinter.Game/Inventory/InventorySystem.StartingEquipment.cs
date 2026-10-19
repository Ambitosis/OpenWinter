using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Inventory;

public sealed partial class InventorySystem
{
    /// <summary>
    /// UTC and area-instance equipment is validated before spawn, without equip actions/events.
    /// Saved characters and serialized object restoration retain their recorded equipment.
    /// </summary>
    internal void ValidateStartingEquipment(Creature c)
    {
        if (!c.Template.Has("CreatureSize")) c.Size = CreatureRules.AppearanceSize(_world, c.AppearanceType);
        var starting = c.Equipment.ToArray();
        c.Equipment.Clear();
        foreach (var (originalSlot, item) in starting)
        {
            int slot = originalSlot == EquipSlot.LeftHand && Rules.IsTwoHanded(item.BaseItem, c.Size)
                ? EquipSlot.RightHand : originalSlot;
            bool legal = (Rules.EquipableSlots(item.BaseItem) & slot) != 0 && HasProficiency(c, item)
                && Rules.FitsWielder(item.BaseItem, c.Size);
            if (!legal)
            {
                c.Inventory.Add(item);
                continue;
            }
            // Unlike an equip action, initialization preserves a two-handed weapon plus shield.
            // A malformed pair that redirects two weapons to one slot retains the displaced item
            // in the backpack; native's possessed-but-unlisted collision state is not modeled.
            if (c.Equipment.TryGetValue(slot, out var displaced)) c.Inventory.Add(displaced);
            c.Equipment[slot] = item;
        }
    }
}
