using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>
/// ActionEquipMostDamagingMelee / ActionEquipMostDamagingRanged, as measured on nwserver (parity suite
/// equip_mostdamaging):
/// <list type="bullet">
/// <item>Melee candidates are the weapons loose in the inventory (not in bags), identified or not, that the creature can
/// wield, taken in GetFirstItemInInventory order. Each scores the most one non-critical hit can deal: every die at its
/// maximum (the weapon's and its damage bonus properties'), Strength, enhancement, and the physical damage bonuses
/// (bludgeoning, piercing, slashing; a versus-race one only against that race). Fire and other non-physical bonuses
/// count only against a valid creature that isn't plot, cut by its immunity and resistance to that type (equip_round3:
/// a 2d6 fire longsword beat a greataxe against a non-plot human, not against a plot one, a chest, no target, or a human
/// immune to fire). Against a valid oVersus the target's damage immunity, resistance and damage reduction count: a
/// greataxe lost to a warhammer against a target immune to slashing and to a +1 longsword against 10/+1.</item>
/// <item>The baseline is the weapon in hand (unarmed when it is empty or ranged). A candidate replaces the best so far
/// when it scores higher, or the same for a non-monk, so the later of two equal weapons is taken, even over the one in
/// hand. A monk keeps the earlier.</item>
/// <item>Nothing better than the baseline: nothing happens (no second search without a target: a fighter with only a
/// greataxe stayed unarmed against a target immune to slashing). No melee candidate at all: the ranged choice.</item>
/// <item>The off hand takes weapons no larger than the creature, against the main hand's weapon as baseline; with the main
/// hand empty the choice goes there.</item>
/// <item>Ranged: launchers and thrown weapons by attack bonus, a higher one strictly winning (ties keep the earlier, the
/// one in hand first); then the first matching ammunition in inventory order (damage bonuses on ammunition didn't
/// change the choice). No ammunition, or no ranged weapon: the melee choice.</item>
/// </list>
/// </summary>
public static class MostDamagingWeapon
{
    sealed class MaxDice : IDice
    {
        public static readonly MaxDice Instance = new();
        public int Roll(int sides) => Math.Max(1, sides);
    }

    /// <summary>The equips to queue for a melee choice (empty when nothing changes).</summary>
    public static List<(Item Item, int Slot)> Melee(World world, Creature c, GameObject? versus, bool offHand, bool fallback = true)
    {
        var inv = world.Inventory;
        var rules = inv.Rules;
        var combat = world.System<CombatSystem>().Rules as CreatureCombatRules ?? new CreatureCombatRules(world);
        bool monk = CreatureRules.ClassLevel(c, NwRules.ClassMonk) > 0;
        var main = c.Equipment.GetValueOrDefault(EquipSlot.RightHand);
        if (main != null && rules.IsRangedWeapon(main.BaseItem)) main = null;
        var candidates = Loose(world, c).Where(i => rules.IsWeapon(i.BaseItem) && !rules.IsRangedWeapon(i.BaseItem) && inv.CanWield(c, i)
            && (!offHand || (!rules.IsTwoHanded(i.BaseItem, c.Size) && rules.WeaponSize(i.BaseItem) <= c.Size))).ToList();
        if (candidates.Count == 0)
            return fallback && !monk ? Ranged(world, c, versus, fallback: false) : new();
        Item? best = null;
        int bestScore = MeleeScore(world, combat, c, main, versus, offHand: false);
        foreach (var item in candidates)
        {
            int score = MeleeScore(world, combat, c, item, versus, offHand);
            if (score > bestScore || (score == bestScore && !monk)) { best = item; bestScore = score; }
        }
        if (best == null) return new();
        int slot = offHand && c.Equipment.ContainsKey(EquipSlot.RightHand) ? 5 : 4;
        if (c.Equipment.GetValueOrDefault(EquipSlot.Bit(slot)) == best) return new();
        return new() { (best, slot) };
    }

    /// <summary>The equips to queue for a ranged choice: the launcher (or thrown weapon) and its ammunition.</summary>
    public static List<(Item Item, int Slot)> Ranged(World world, Creature c, GameObject? versus, bool fallback = true)
    {
        var inv = world.Inventory;
        var rules = inv.Rules;
        var combat = world.System<CombatSystem>().Rules as CreatureCombatRules ?? new CreatureCombatRules(world);
        var current = c.Equipment.GetValueOrDefault(EquipSlot.RightHand);
        Item? best = current != null && rules.IsRangedWeapon(current.BaseItem) ? current : null;
        int bestScore = best != null ? RangedScore(world, combat, c, best, versus) : int.MinValue;
        foreach (var item in Loose(world, c))
        {
            if (!rules.IsRangedWeapon(item.BaseItem) || !inv.CanWield(c, item)) continue;
            int score = RangedScore(world, combat, c, item, versus);
            if (score > bestScore) { best = item; bestScore = score; }
        }
        if (best == null) return fallback ? Melee(world, c, versus, offHand: false, fallback: false) : new();
        var result = new List<(Item, int)>();
        var stats = combat.Stats(best);
        if (!stats.Thrown)
        {
            int ammoSlot = AmmoSlot(rules, stats.AmmoBaseItem);
            var equipped = ammoSlot >= 0 ? c.Equipment.GetValueOrDefault(EquipSlot.Bit(ammoSlot)) : null;
            var ammo = equipped is { } e && e.BaseItem == stats.AmmoBaseItem ? e
                : Loose(world, c).FirstOrDefault(i => i.BaseItem == stats.AmmoBaseItem);
            if (ammo == null) return fallback ? Melee(world, c, versus, offHand: false, fallback: false) : new();
            if (ammo != equipped) result.Add((ammo, ammoSlot));
        }
        if (best != current) result.Insert(0, (best, 4));
        return result;
    }

    /// <summary>Items loose in the creature's inventory (not equipped, not in bags), in GetFirstItemInInventory order.</summary>
    static IEnumerable<Item> Loose(World world, Creature c) =>
        world.Inventory.Items(c).Where(i => !i.IsDestroyed && c.Inventory.Contains(i));

    static int AmmoSlot(ItemRules rules, int ammoBaseItem)
    {
        int mask = ammoBaseItem >= 0 ? rules.EquipableSlots(ammoBaseItem) : 0;
        for (int slot = 11; slot <= 13; slot++)
            if ((mask & EquipSlot.Bit(slot)) != 0) return slot;
        return -1;
    }

    static AttackContext Context(World world, Creature c, Item? weapon, GameObject? versus, bool offHand, bool ranged) => new()
    {
        World = world, Attacker = c, Target = versus ?? new Waypoint(), Dice = MaxDice.Instance, Weapon = weapon,
        OffHand = offHand, Ranged = ranged,
    };

    /// <summary>The most one non-critical melee hit with <paramref name="weapon"/> (null: unarmed) does to <paramref name="versus"/>.</summary>
    public static int MeleeScore(World world, CreatureCombatRules combat, Creature c, Item? weapon, GameObject? versus, bool offHand)
    {
        var a = Context(world, c, weapon, versus, offHand, ranged: false);
        var s = combat.Stats(weapon);
        int baseType = versus != null ? combat.PhysicalType(a, s) : PhysicalBits(s.Type);
        int damage = combat.WeaponDice(a, s) + combat.StrengthDamage(a, s) + Math.Max(0, combat.Enhancement(a));
        foreach (var (bits, amount) in combat.ItemDamageBonuses(combat.WeaponItem(a), a, baseType))
            if ((bits & CombatK.PhysicalDamage) != 0) damage += amount;
        damage = Math.Max(0, damage);
        if (versus == null) return damage;
        damage = Mitigated(world, versus, c, baseType, damage, combat.DamagePower(a, s));
        if (versus is Creature { Plot: false })
            foreach (var (bits, amount) in combat.ItemDamageBonuses(combat.WeaponItem(a), a, baseType))
                if ((bits & CombatK.PhysicalDamage) == 0 && amount > 0) damage += Mitigated(world, versus, c, bits, amount, power: -1);
        return damage;
    }

    static int RangedScore(World world, CreatureCombatRules combat, Creature c, Item weapon, GameObject? versus)
    {
        var s = combat.Stats(weapon);
        var a = Context(world, c, weapon, versus, offHand: false, ranged: true);
        a.Thrown = s.Thrown;
        return combat.AttackBonus(a);
    }

    static int PhysicalBits(int weaponType) => weaponType switch
    {
        1 => CombatK.DamagePiercing, 3 => CombatK.DamageSlashing, 4 => CombatK.DamageSlashing, 5 => CombatK.DamageBludgeoning,
        _ => CombatK.DamageBludgeoning,
    };

    /// <summary>What is left of damage after the target's immunity, resistance and (physical damage, <paramref name="power"/> 0 or more)
    /// damage reduction (nothing used up).</summary>
    static int Mitigated(World world, GameObject target, Creature attacker, int type, int amount, int power)
    {
        var k = EffectConstants.Of(world);
        int pct = ItemPropertyRules.ItemDamageImmunity(world, target, type);
        foreach (var e in EffectQueries.Active(target, k.DamageImmunityIncrease, attacker)) if ((e.Ints[0] & type) != 0) pct += e.Ints[1];
        foreach (var e in EffectQueries.Active(target, k.DamageImmunityDecrease, attacker)) if ((e.Ints[0] & type) != 0) pct -= e.Ints[1];
        pct = Math.Clamp(pct, -100, 100);
        amount -= pct > 0 ? Math.Min(amount, Math.Max(1, amount * pct / 100)) : amount * pct / 100;
        int resist = ItemPropertyRules.ItemDamageResistance(world, target, type);
        foreach (var e in EffectQueries.Active(target, k.DamageResistance, attacker))
            if ((e.Ints[0] & type) != 0 && e.Ints[3] == 0) resist = Math.Max(resist, e.Ints[1]);
        amount -= Math.Min(amount, resist);
        if (power < 0) return Math.Max(0, amount); // non-physical damage: no damage reduction
        int soak = ItemPropertyRules.ItemDamageReduction(world, target, power);
        foreach (var e in EffectQueries.Active(target, k.DamageReduction, attacker))
            if (power < e.Ints[1] && e.Ints[3] == 0) soak = Math.Max(soak, e.Ints[0]);
        return Math.Max(0, amount - soak);
    }
}
