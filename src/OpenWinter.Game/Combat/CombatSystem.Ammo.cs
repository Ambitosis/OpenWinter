using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>Ammunition: each shot uses one arrow, bolt or bullet from the ammunition slot (thrown weapons use themselves).</summary>
public sealed partial class CombatSystem
{
    /// <summary>
    /// Uses one piece of ammunition for a ranged attack. Launchers with Unlimited Ammunition use none. When a
    /// stack runs out the next matching stack from the inventory is equipped. False when there is nothing to fire.
    /// </summary>
    bool ConsumeAmmo(AttackContext a)
    {
        if (Creature is not { } r || a.Weapon == null) return true;
        var c = a.Attacker;
        if (a.Thrown)
        {
            UseOne(c, a.Weapon, EquipSlot.Index(EquipSlot.RightHand));
            return true;
        }
        if (r.HasUnlimitedAmmo(a.Weapon)) return true;
        var ammo = a.Ammo ?? r.AmmoFor(c, a.Weapon);
        if (ammo == null) return false;
        a.Ammo = ammo;
        int slot = CreatureCombatRules.AmmoSlot(r.Stats(a.Weapon).AmmoType);
        UseOne(c, ammo, slot == 0 ? -1 : EquipSlot.Index(slot));
        return true;
    }

    /// <summary>Takes one from an equipped stack; the last one is destroyed and a matching stack from the inventory takes its place.</summary>
    void UseOne(Creature c, Item item, int slotIndex)
    {
        if (item.StackSize > 1)
        {
            item.StackSize--;
            _world.MarkChanged(item);
            _world.Inventory.Changed(item);
            return;
        }
        var inv = _world.Inventory;
        inv.Detach(item);
        _world.Destroy(item);
        if (slotIndex < 0) return;
        var next = c.Inventory.Where(i => !i.IsDestroyed && i.BaseItem == item.BaseItem)
            .OrderByDescending(i => i.ResRef.Equals(item.ResRef, StringComparison.OrdinalIgnoreCase) && i.Tag == item.Tag)
            .FirstOrDefault();
        if (next != null) inv.Equip(c, next, slotIndex);
    }

    /// <summary>Out of ammunition: the attack ends ("You have run out of ammunition").</summary>
    void OutOfAmmo(Creature c)
    {
        if (c.IsPC) _world.Output?.SendMessage(c, "You have run out of ammunition.");
        _world.Output?.Notify("combat-feedback", c, new { creature = c.Id, message = "out-of-ammo" });
        var s = State(c);
        s.Target = Objects.GameObject.InvalidId;
        s.NextAttack = s.Plan.Count;
    }
}
