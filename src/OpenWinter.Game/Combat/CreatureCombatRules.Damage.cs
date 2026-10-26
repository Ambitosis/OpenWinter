using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Combat;

/// <summary>Damage of a hit.</summary>
public sealed partial class CreatureCombatRules
{
    /// <summary>
    /// Damage of a hit by DAMAGE_TYPE_*. The weapon's dice, Strength, enhancement, specialisation, Power Attack,
    /// favoured enemy, the weapon's (or ammunition's, or gloves') damage properties and damage effects are rolled
    /// once per critical multiplier; sneak attack, massive criticals, overwhelming critical, Bane of Enemies and
    /// Dirty Fighting are not multiplied. Penalties come off last and a hit always deals at least 1.
    /// </summary>
    public DamageRoll Damage(AttackContext a, bool critical)
    {
        var c = a.Attacker;
        var s = Stats(a.Weapon);
        var d = new DamageRoll();
        int mult = critical ? Math.Max(1, Critical(a).Multiplier) : 1;
        int baseType = PhysicalType(a, s);
        var source = DamageSource(a);

        // A launcher's enhancement doesn't add damage (only damage power); melee and thrown weapons' does.
        int perRoll = StrengthDamage(a, s) + (a.Ranged && !a.Thrown ? 0 : Math.Max(0, Enhancement(a))) + SpecializationDamage(c, s)
            + ModeDamage(a) + FavoredEnemyDamage(a) + EnchantArrow(a) + SmiteDamage(a);
        if (a.Ranged && Has(c, CombatK.FeatPointBlankShot) && a.Distance <= RF("POINT_BLANK_SHOT_MAX_RANGE", 5f))
            perRoll += RI("POINT_BLANK_SHOT_DAMAGE_BONUS", 1);

        int bonusTotal = 0;
        for (int m = 0; m < mult; m++)
        {
            d.Add(baseType, DifficultyDice(a, s) + perRoll);
            foreach (var (type, amount) in ItemDamageBonuses(source, a, baseType)) d.Add(type, amount);
            bonusTotal += EffectDamage(a, d, baseType, _w.Limits.DamageBonus - bonusTotal);
        }

        // Not multiplied.
        if (critical)
        {
            foreach (var p in Props(WeaponItem(a)))
                if (p.Type == CombatK.IpMassiveCriticals) d.Add(baseType, RollDamageCost(a.Dice, p.CostTableValue));
            if (HasWeaponFeat(c, s, WeaponFeat.OverwhelmingCritical)) d.Add(baseType, a.Dice.Roll(Math.Max(1, mult - 1), 6));
        }
        if (Has(c, CombatK.FeatEpicBaneOfEnemies) && FavoredEnemy(c, a.Target))
            d.Add(baseType, a.Dice.Roll(RI("EPIC_BANE_OF_ENEMIES_DAMAGE_DICE", 2), RI("EPIC_BANE_OF_ENEMIES_DAMAGE_DIE", 6)));
        if (DirtyFightingActive(c)) d.Add(baseType, a.Dice.Roll(1, RI("DIRTY_FIGHTING_BONUS_DICE", 4)));
        SneakAttack(a, d, baseType);

        // Penalties.
        int penalty = 0;
        foreach (var p in Props(source)) if (p.Type == CombatK.IpDecreasedDamage) penalty += Math.Abs(Value(p));
        Reduce(d, baseType, penalty);
        foreach (var e in c.Effects)
            if (e.Type == CombatK.EffectDamageDecrease && EffectQueries.AppliesVersus(e, a.Target))
                Reduce(d, e.Ints[1] == CombatK.DamageBaseWeapon || e.Ints[1] == 0 ? baseType : e.Ints[1], Math.Max(0, e.Ints[0]));

        foreach (var k in d.ByType.Where(kv => kv.Value <= 0).Select(kv => kv.Key).ToList()) d.ByType.Remove(k);
        if (d.Total <= 0) { d.ByType.Clear(); d.ByType[baseType] = 1; }
        foreach (var k in d.Unresisted.Keys.ToList()) d.Unresisted[k] = Math.Min(d.Unresisted[k], d.ByType.GetValueOrDefault(k));
        d.Power = DamagePower(a, s);
        return d;
    }

    static void Reduce(DamageRoll d, int type, int amount)
    {
        if (amount <= 0) return;
        if (d.ByType.TryGetValue(type, out int have)) { int take = Math.Min(have, amount); d.ByType[type] = have - take; amount -= take; }
        // Whatever is left comes off the physical damage.
        foreach (var k in d.ByType.Keys.Where(k => (k & CombatK.PhysicalDamage) != 0).ToList())
        {
            if (amount <= 0) break;
            int take = Math.Min(d.ByType[k], amount);
            d.ByType[k] -= take;
            amount -= take;
        }
    }

    /// <summary>
    /// The physical type a hit deals. Weapons with two types (WeaponType 4 and 5) and Extra Melee/Ranged Damage
    /// Type properties use whichever the target resists least.
    /// </summary>
    public int PhysicalType(AttackContext a, WeaponStats s)
    {
        int bits = WeaponDamageBits(s);
        foreach (var p in Props(a.Weapon))
            if ((p.Type == CombatK.IpExtraMeleeDamageType && !a.Ranged) || (p.Type == CombatK.IpExtraRangedDamageType && a.Ranged))
                bits |= p.SubType switch { 0 => CombatK.DamageBludgeoning, 1 => CombatK.DamagePiercing, 2 => CombatK.DamageSlashing, _ => 0 };
        int best = 0, bestScore = int.MaxValue;
        foreach (int t in new[] { CombatK.DamageBludgeoning, CombatK.DamagePiercing, CombatK.DamageSlashing })
        {
            if ((bits & t) == 0) continue;
            int score = Resistance(a.Target, t, a.Attacker);
            if (score < bestScore) { best = t; bestScore = score; }
        }
        return best == 0 ? CombatK.DamageBludgeoning : best;
    }

    /// <summary>How much a target resists a damage type (immunity percent, then resistance), to pick between a weapon's types.</summary>
    int Resistance(GameObject target, int type, GameObject attacker)
    {
        var k = EffectConstants.Of(_w);
        int pct = ItemPropertyRules.ItemDamageImmunity(_w, target, type);
        foreach (var e in EffectQueries.Active(target, k.DamageImmunityIncrease, attacker)) if ((e.Ints[0] & type) != 0) pct += e.Ints[1];
        foreach (var e in EffectQueries.Active(target, k.DamageImmunityDecrease, attacker)) if ((e.Ints[0] & type) != 0) pct -= e.Ints[1];
        int resist = ItemPropertyRules.ItemDamageResistance(_w, target, type);
        foreach (var e in EffectQueries.Active(target, k.DamageResistance, attacker)) if ((e.Ints[0] & type) != 0) resist = Math.Max(resist, e.Ints[1]);
        return pct * 1000 + resist;
    }

    /// <summary>The item whose damage properties a hit uses: the ammunition for launchers, else the weapon (gloves when unarmed).</summary>
    public Item? DamageSource(AttackContext a) => a.Ranged && !a.Thrown ? a.Ammo : WeaponItem(a);

    /// <summary>
    /// The weapon's dice as the game difficulty has them (<see cref="Difficulty.ScaleWeaponDice"/>): rolled, then topped
    /// up when a player hits a creature that isn't one, or cut or raised when such a creature hits a player, by the
    /// diffsettings.2da percentages; nothing else of the hit changes (visual scene diffscale, docs/DIFFICULTY.md).
    /// </summary>
    int DifficultyDice(AttackContext a, WeaponStats s)
    {
        var (count, die) = WeaponDiceOf(a, s);
        int roll = a.Dice.Roll(count, die);
        return Difficulty.ScaleWeaponDice(_w, a.Attacker, a.Target, roll, count * die, a.Dice);
    }

    /// <summary>Base dice: the weapon's (baseitems NumDice/DieToRoll), Monster Damage on creature weapons, monk or plain unarmed damage.</summary>
    public int WeaponDice(AttackContext a, WeaponStats s)
    {
        var (count, die) = WeaponDiceOf(a, s);
        return a.Dice.Roll(count, die);
    }

    /// <summary>How many dice of what size a hit rolls for the weapon.</summary>
    public (int Count, int Die) WeaponDiceOf(AttackContext a, WeaponStats s)
    {
        var c = a.Attacker;
        if (s.Unarmed)
        {
            bool small = c.Size <= 2;
            int monk = CreatureRules.ClassLevel(c, NwRules.ClassMonk);
            if (monk > 0)
            {
                string[] tiers = { "ZERO", "ONE", "TWO", "THREE", "FOUR" };
                int tier = 0;
                if (monk >= RI("MONK_DAMAGE_TIER_FOUR_THRESHHOLD", 16)) tier = 4;
                else if (monk >= RI("MONK_DAMAGE_TIER_THREE_THRESHHOLD", 12)) tier = 3;
                else if (monk >= RI("MONK_DAMAGE_TIER_TWO_THRESHHOLD", 8)) tier = 2;
                else if (monk >= RI("MONK_DAMAGE_TIER_ONE_THRESHHOLD", 4)) tier = 1;
                int[] dieDefault = small ? new[] { 4, 6, 8, 10, 6 } : new[] { 6, 8, 10, 12, 20 };
                int[] diceDefault = small ? new[] { 1, 1, 1, 1, 2 } : new[] { 1, 1, 1, 1, 1 };
                int die = RI($"MONK_DAMAGE_TIER_{tiers[tier]}_{(small ? "S" : "")}DAMAGE_DIE", dieDefault[tier]);
                int dice = small ? RI($"MONK_DAMAGE_TIER_{tiers[tier]}_SDAMAGE_DICE", diceDefault[tier]) : RI("MONK_DAMAGE_TIER_DICE", 1);
                return (dice, die);
            }
            return (RI("UNARMED_DAMAGE_DICE", 1), small ? RI("UNARMED_SDAMAGE_DIE", 2) : RI("UNARMED_DAMAGE_DIE", 3));
        }
        if (s.Creature)
        {
            int best = 0;
            ItemProperty? pick = null;
            foreach (var p in Props(a.Weapon))
            {
                if (p.Type != CombatK.IpMonsterDamage) continue;
                var t = _w.Resources.TwoDA("iprp_monstcost");
                int avg = t.GetInt(p.CostTableValue, "NumDice") * (t.GetInt(p.CostTableValue, "Die") + 1);
                if (avg > best) { best = avg; pick = p; }
            }
            if (pick != null)
            {
                var t = _w.Resources.TwoDA("iprp_monstcost");
                return (t.GetInt(pick.CostTableValue, "NumDice"), t.GetInt(pick.CostTableValue, "Die"));
            }
        }
        return (s.NumDice, s.Die);
    }

    /// <summary>
    /// Strength to damage: full, 1.5x two-handed, half off-hand; thrown weapons full; launchers only up to their Mighty
    /// rating, though bows and slings take a Strength penalty in full (crossbows don't). A melee weapon given
    /// one-and-a-half Strength adds half the bonus again, two-handed too (parity suite nwnx_weapon).
    /// </summary>
    public int StrengthDamage(AttackContext a, WeaponStats s)
    {
        int str = BaseStrengthDamage(a, s);
        if (!a.Ranged && !a.OffHand && a.Weapon != null && OneHalfStrength(a.Weapon))
        {
            int mod = Mod(a.Attacker, NwRules.AbilityStr);
            if (mod > 0) str += mod / 2;
        }
        return str;
    }

    /// <summary>The item has the one-and-a-half Strength flag (kept with the object, or saved with the item).</summary>
    public bool OneHalfStrength(Item item) => Overrides.OneHalfStrength.Contains(item.Id) || item.Template.GetInt(OneHalfStrengthField) != 0;

    /// <summary>The item field that carries a saved one-and-a-half Strength flag.</summary>
    public const string OneHalfStrengthField = "NWNX_WEAPON_ONEHALFSTR";

    int BaseStrengthDamage(AttackContext a, WeaponStats s)
    {
        var c = a.Attacker;
        int str = Mod(c, NwRules.AbilityStr);
        if (a.Ranged)
        {
            if (a.Thrown) return str;
            if (str < 0) return s.IsCrossbow ? 0 : str;
            int mighty = 0;
            foreach (var p in Props(a.Weapon)) if (p.Type == CombatK.IpMighty) mighty = Math.Max(mighty, Value(p));
            return Math.Min(str, mighty);
        }
        if (str <= 0) return str;
        if (a.OffHand) return str / 2;
        if (!s.Unarmed && !s.Creature && !s.IsDouble && (s.Wield == CombatK.WieldTwoHanded || s.Size > c.Size)) return str * 3 / 2;
        return str;
    }

    int SpecializationDamage(Creature c, WeaponStats s)
    {
        int b = 0;
        if (HasWeaponFeat(c, s, WeaponFeat.Specialization)) b += RI("WEAPON_SPECIALIZATION_BONUS", 2);
        if (HasWeaponFeat(c, s, WeaponFeat.EpicSpecialization)) b += RI("EPIC_WEAPON_SPECIALIZATION_BONUS", 4);
        if (HasWeaponFeat(c, s, WeaponFeat.GreaterSpecialization)) b += Overrides.GreaterSpecializationDamageBonus;
        return b;
    }

    int ModeDamage(AttackContext a)
    {
        var c = a.Attacker;
        if (a.Ranged || !c.IsPC) return 0;
        if (InMode(c, CombatK.ModeImprovedPowerAttack) && Has(c, CombatK.FeatImprovedPowerAttack)) return RI("IMPROVED_POWER_ATTACK_DAMAGE_MODIFIER", 10);
        if (InMode(c, CombatK.ModePowerAttack) && Has(c, CombatK.FeatPowerAttack)) return RI("POWER_ATTACK_DAMAGE_MODIFIER", 5);
        return 0;
    }

    /// <summary>Favoured enemy: +1, and +1 more per five ranger levels.</summary>
    int FavoredEnemyDamage(AttackContext a)
    {
        if (!FavoredEnemy(a.Attacker, a.Target)) return 0;
        return 1 + CreatureRules.ClassLevel(a.Attacker, NwRules.ClassRanger) / 5;
    }

    /// <summary>Smite: paladin (evil) or blackguard (good) levels, multiplied by Epic Great Smiting.</summary>
    int SmiteDamage(AttackContext a)
    {
        if (!SmiteApplies(a)) return 0;
        var c = a.Attacker;
        int level = Math.Max(1, CreatureRules.ClassLevel(c, a.SpecialFeat == NwRules.FeatSmiteEvil ? NwRules.ClassPaladin : NwRules.ClassBlackguard));
        for (int f = CombatK.FeatEpicGreatSmiting10; f >= CombatK.FeatEpicGreatSmiting1; f--)
            if (Has(c, f)) return level * RI($"EPIC_GREAT_SMITING_{f - CombatK.FeatEpicGreatSmiting1 + 1}", f - CombatK.FeatEpicGreatSmiting1 + 2);
        return level;
    }

    /// <summary>Rolls an iprp_damagecost.2da row (DAMAGE_BONUS_*): dice, or a flat amount when NumDice is 0.</summary>
    public int RollDamageCost(IDice dice, int row)
    {
        var t = _w.Resources.TwoDA("iprp_damagecost");
        int n = t.GetInt(row, "NumDice", 0), die = t.GetInt(row, "Die", 0);
        return n <= 0 ? die : dice.Roll(n, die);
    }

    /// <summary>Damage bonus properties of an item that apply against the target, by type (the best of each type).</summary>
    public Dictionary<int, int> ItemDamageBonuses(Item? item, AttackContext a, int baseType)
    {
        var result = new Dictionary<int, int>();
        foreach (var p in Props(item))
        {
            int typeRow;
            switch (p.Type)
            {
                case CombatK.IpDamageBonus: typeRow = p.SubType; break;
                case CombatK.IpDamageVsAlignGroup or CombatK.IpDamageVsRace or CombatK.IpDamageVsAlign:
                    if (!ItemPropertyVersus.Applies(_w, p, a.Target)) continue;
                    typeRow = p.Param1Value;
                    break;
                default: continue;
            }
            int bits = ItemPropertyRules.DamageTypeBits(typeRow);
            if (bits == 0) continue;
            if ((bits & CombatK.PhysicalDamage) == CombatK.PhysicalDamage) bits = baseType; // "physical"
            int amount = RollDamageCost(a.Dice, p.CostTableValue);
            result[bits] = Math.Max(result.GetValueOrDefault(bits), amount);
        }
        return result;
    }

    /// <summary>
    /// EffectDamageIncrease (DAMAGE_BONUS_* amounts, by type, versus the target) up to <paramref name="limit"/>; returns
    /// what was added. As in the original, physical bonuses join the weapon's own damage, and other types get past
    /// immunity and resistance.
    /// </summary>
    int EffectDamage(AttackContext a, DamageRoll d, int baseType, int limit)
    {
        int added = 0;
        foreach (var e in a.Attacker.Effects)
        {
            if (e.Type != CombatK.EffectDamageIncrease || !EffectQueries.AppliesVersus(e, a.Target)) continue;
            if (added >= limit) break;
            int type = e.Ints[1];
            bool physical = type == CombatK.DamageBaseWeapon || type == 0 || (type & CombatK.PhysicalDamage) != 0;
            int amount = Math.Min(RollDamageCost(a.Dice, e.Ints[0]), limit - added);
            if (physical) d.Add(baseType, amount);
            else
            {
                d.Add(type, amount);
                d.Unresisted[type] = d.Unresisted.GetValueOrDefault(type) + amount;
            }
            added += amount;
        }
        return added;
    }

    /// <summary>Sneak attack dice the attacker has (the best feat of each chain), and death attack dice.</summary>
    public (int Sneak, int Death) SneakDice(Creature c)
    {
        // Feats owned outright: feat.2da chains them by SUCCESSOR, and Death Attack 20's successor is Sneak Attack 10,
        // so following successors would give every rogue with Sneak Attack 10 twenty death attack dice.
        var owned = c.Feats.Concat(CreatureRules.BonusFeats(_w, c)).ToHashSet();
        int sneak = 0;
        foreach (var chain in CombatK.SneakAttackChains)
            for (int i = chain.Length - 1; i >= 0; i--)
                if (owned.Contains(chain[i])) { sneak += i + 1; break; }
        int death = 0;
        for (int i = CombatK.DeathAttackChain.Length - 1; i >= 0; i--)
            if (owned.Contains(CombatK.DeathAttackChain[i])) { death = i + 1; break; }
        return (sneak, death);
    }

    /// <summary>
    /// Sneak attack against a flat-footed or flanked target, or one denied its Dexterity bonus (ranged only up to
    /// MAX_RANGED_SNEAK_ATTACK_DISTANCE); death attack dice only against flat-footed targets or ones denied Dexterity.
    /// </summary>
    void SneakAttack(AttackContext a, DamageRoll d, int baseType)
    {
        if (!SneakAttackApplies(a, out int sneak, out int death)) return;
        int amount = a.Dice.Roll(sneak + death, 6);
        d.Add(baseType, amount);
        d.Sneak = amount;
        d.DeathAttack = death > 0;
    }

    /// <summary>Whether this attack would be a sneak (or death) attack, and with how many dice of each.</summary>
    public bool SneakAttackApplies(AttackContext a, out int sneak, out int death) =>
        SneakSituation(a, out sneak, out death) && !IsSneakImmune(a);

    /// <summary>The attack would be a sneak attack but the target is immune to it.</summary>
    public bool SneakAttackBlocked(AttackContext a) => SneakSituation(a, out _, out _) && IsSneakImmune(a);

    bool SneakSituation(AttackContext a, out int sneak, out int death)
    {
        sneak = death = 0;
        if (!(a.TargetDeniedDex || a.FlatFooted || a.Flanked) || a.Target is not Creature) return false;
        if (a.Ranged && a.Distance > RF("MAX_RANGED_SNEAK_ATTACK_DISTANCE", 10f)) return false;
        (sneak, death) = SneakDice(a.Attacker);
        if (!(a.TargetDeniedDex || a.FlatFooted)) death = 0;
        return sneak + death > 0;
    }

    /// <summary>DAMAGE_POWER_* of a hit: the weapon's (or launcher's) enhancement, Ki Strike for monks, Enchant Arrow.</summary>
    public int DamagePower(AttackContext a, WeaponStats s)
    {
        int e = Math.Max(Enhancement(a), EnchantArrow(a));
        if (s.Unarmed)
        {
            var c = a.Attacker;
            int ki = Has(c, CombatK.FeatEpicKiStrike5) ? 5 : Has(c, CombatK.FeatEpicKiStrike4) ? 4 : Has(c, CombatK.FeatKiStrike3) ? 3
                : Has(c, CombatK.FeatKiStrike2) ? 2 : Has(c, CombatK.FeatKiStrike) ? 1 : 0;
            e = Math.Max(e, ki);
        }
        return e <= 0 ? 0 : e <= 5 ? e : e + 1;
    }
}
