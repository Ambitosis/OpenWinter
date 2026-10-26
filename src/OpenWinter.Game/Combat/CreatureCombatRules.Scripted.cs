using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Combat;

/// <summary>
/// Combat figures read without an attack under way (NWNX_Creature): a creature's own attack bonus, its armour class
/// against someone, its effect bonuses. Measured on nwserver with NWNX (parity suite nwnx_creature_attack).
/// </summary>
public sealed partial class CreatureCombatRules
{
    /// <summary>
    /// The creature's best attack bonus from its own stats. A melee bonus with a ranged weapon in hand, or a ranged
    /// one without, is 0 (touch attacks aside). Without the base attack bonus the effect bonuses go too; the
    /// two-weapon penalties stay. An off-hand reading takes the off-hand penalty whenever something is in the left
    /// hand (a shield counts as a light weapon), and versus bonuses never count.
    /// </summary>
    public int OwnAttackBonus(Creature c, bool melee, bool touch, bool offHand, bool includeBase)
    {
        CreatureRules.EnsureInit(_w, c);
        var (main, off, _) = Weapons(c);
        bool rangedInHand = main != null && Stats(main).Ranged;
        if (!touch && (melee ? rangedInHand && !offHand : !rangedInHand)) return 0;
        var weapon = offHand ? off : main;
        var s = Stats(weapon);
        var a = new AttackContext
        {
            World = _w, Attacker = c, Target = new Placeable(), Dice = new RandomDice(), Weapon = weapon, OffHand = offHand,
            Ranged = !melee, Thrown = s.Thrown, CreatureWeapon = s.Creature, Touch = touch,
        };
        int ab = includeBase ? CreatureRules.BaseAttackBonus(_w, c) + EffectAttackModifier(a) : 0;
        ab += AbilityAttackModifier(a, s);
        if (touch) return ab;
        ab += SizeAttackModifier(c.Size) + WeaponAttackBonus(a) + FeatAttackBonus(a, s);
        if (melee)
        {
            var left = c.Equipment.GetValueOrDefault(EquipSlot.LeftHand);
            if (offHand && off == null && left != null && !Stats(left).Ranged)
            {
                int m = RI("OFFHAND_NORMAL_OFFHAND_ATTACK_PENALTY", -10);
                if (!IsWeapon(left) || Stats(left).Size < c.Size) m += RI("LIGHT_OFFHAND_WEAPON_BONUS", 2);
                if (Has(c, CombatK.FeatTwoWeaponFighting)) m += RI("TWO_WEAPON_FIGHTING_BONUS", 2);
                if (Has(c, CombatK.FeatAmbidexterity)) m += RI("AMBIDEXTERITY_BONUS", 4);
                ab += m;
            }
            else ab += TwoWeaponModifier(a);
        }
        return ab;
    }

    /// <summary>
    /// Armour class against an attacker (NWNX_Creature_GetArmorClassVersus): flat-footed (no Dexterity bonus, no dodge
    /// bonuses) when the defender attacks nobody or there is no attacker, with bonuses against that attacker on top
    /// either way (nwserver: a bandit with +2 dodge and +3 dodge versus goblinoids reads 11 alone and 14 against a goblin).
    /// </summary>
    public int ArmorClassVersus(Creature t, Creature? versus, bool touch, bool flatFooted)
    {
        var plain = CreatureRules.ArmorClass(_w, t, null);
        int against = versus != null ? CreatureRules.ArmorClass(_w, t, versus).Total - plain.Total : 0;
        if (flatFooted || versus == null)
        {
            plain.Dex = Math.Min(0, plain.Dex);
            plain.Dodge = Math.Min(0, plain.Dodge);
        }
        if (touch) return 10 + plain.Size + plain.Deflection + plain.Dex + plain.Dodge + against;
        return plain.Total + against;
    }

    /// <summary>
    /// The total of a creature's effect bonuses of one kind (NWNX_Creature_GetTotalEffectBonus), within the game's
    /// bonus limits: attack (and touch attack) for a hand, physical or elemental damage (dice rolled, or their
    /// maximum), a saving throw (and save type), an ability or a skill. Bonuses versus the target count when one
    /// is given.
    /// </summary>
    public int TotalEffectBonus(Creature c, int bonusType, GameObject? target, bool elemental, bool forceMax, int save, int saveType,
        int skill, int ability, bool offHand)
    {
        var limits = _w.Limits;
        int inc = 0, dec = 0;
        switch (bonusType)
        {
            case 1 or 6: // attack, touch attack
            {
                int hand = offHand ? CombatK.AttackBonusOffHand : CombatK.AttackBonusOnHand;
                foreach (var e in c.Effects)
                {
                    if (e.Type is not (CombatK.EffectAttackIncrease or CombatK.EffectAttackDecrease)) continue;
                    if ((e.Ints[1] != CombatK.AttackBonusMisc && e.Ints[1] != hand) || !EffectQueries.AppliesVersus(e, target)) continue;
                    if (e.Type == CombatK.EffectAttackIncrease) inc += e.Ints[0]; else dec += e.Ints[0];
                }
                return Math.Min(Math.Max(0, inc), limits.AttackBonus) - Math.Min(Math.Max(0, dec), limits.AttackBonus);
            }
            case 2: // damage
            {
                var dice = new RandomDice();
                foreach (var e in c.Effects)
                {
                    if (e.Type is not (CombatK.EffectDamageIncrease or CombatK.EffectDamageDecrease) || !EffectQueries.AppliesVersus(e, target)) continue;
                    int type = e.Ints[1];
                    bool physical = type == CombatK.DamageBaseWeapon || type == 0 || (type & CombatK.PhysicalDamage) != 0;
                    if (physical == elemental) continue;
                    int amount = forceMax ? MaxDamageCost(e.Ints[0]) : RollDamageCost(dice, e.Ints[0]);
                    if (e.Type == CombatK.EffectDamageIncrease) inc += amount; else dec += amount;
                }
                return Math.Min(Math.Max(0, inc), limits.DamageBonus) - Math.Min(Math.Max(0, dec), limits.DamageBonus);
            }
            case 3: // saving throw
            {
                var (b, p) = CreatureRules.SaveAdjustments(_w, c, Math.Max(0, save), saveType < 0 ? NwRules.SaveTypeAll : saveType, target);
                return Math.Min(b, limits.SavingThrowBonus) - Math.Min(p, limits.SavingThrowBonus);
            }
            case 4: // ability
            {
                if ((uint)ability > 5) return 0;
                var (b, p) = CreatureRules.AbilityAdjustments(_w, c, ability);
                return Math.Min(b, limits.AbilityBonus) - Math.Min(p, limits.AbilityPenalty);
            }
            case 5: // skill
            {
                if (skill < 0) return 0;
                foreach (var e in CreatureRules.Effects(c))
                {
                    if (e.Type is not (NwRules.EffectSkillIncrease or NwRules.EffectSkillDecrease) || !EffectQueries.AppliesVersus(e, target)) continue;
                    if (e.Ints[0] != skill && e.Ints[0] != NwRules.SkillAll) continue;
                    if (e.Type == NwRules.EffectSkillIncrease) inc += e.Ints[1]; else dec += e.Ints[1];
                }
                foreach (var (_, _, p) in CreatureRules.EquippedProperties(c))
                {
                    if (p.SubType != skill && p.SubType != NwRules.SkillAll) continue;
                    if (p.Type == NwRules.IpSkillBonus) inc += Value(p);
                    else if (p.Type == NwRules.IpDecreasedSkill) dec += Math.Abs(Value(p));
                }
                return Math.Min(Math.Max(0, inc), limits.SkillBonus) - Math.Min(Math.Max(0, dec), limits.SkillBonus);
            }
        }
        return 0;
    }

    /// <summary>The most an iprp_damagecost row can give.</summary>
    int MaxDamageCost(int row)
    {
        var t = _w.Resources.TwoDA("iprp_damagecost");
        int n = t.GetInt(row, "NumDice", 0), die = t.GetInt(row, "Die", 0);
        return n <= 0 ? die : n * die;
    }
}
