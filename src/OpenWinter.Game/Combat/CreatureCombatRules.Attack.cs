using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

/// <summary>Attack bonus.</summary>
public sealed partial class CreatureCombatRules
{
    /// <summary>
    /// Attack bonus for one attack: BAB + ability (Str; Dex for ranged, Weapon Finesse and Zen Archery) + size (not for
    /// touch attacks) + weapon enhancement/attack bonus properties (incl. versus the target) + feats + combat mode + two-weapon
    /// fighting + capped effects + the situation (flanking, unseen, prone/helpless target, point blank) +
    /// special attack modifiers - the iterative penalty.
    /// </summary>
    public int AttackBonus(AttackContext a)
    {
        var c = a.Attacker;
        CreatureRules.EnsureInit(_w, c);
        var s = Stats(a.Weapon);
        int ab = CreatureRules.BaseAttackBonus(_w, c);
        ab += AbilityAttackModifier(a, s);
        if (!a.Touch)
        {
            ab += SizeAttackModifier(c.Size); // touch attacks don't get it in the original
            ab += WeaponAttackBonus(a);
            ab += FeatAttackBonus(a, s);
            ab += ModeAttackModifier(a, s);
            ab += TwoWeaponModifier(a);
            ab += SpecialAttackModifier(a);
        }
        ab += EffectAttackModifier(a);
        if (!a.Touch) ab += SituationAttackModifier(a); // native touch_states: no weapon-combat situation bonuses/penalties
        return ab - a.Penalty + a.Modifier;
    }

    /// <summary>Strength for melee, Dexterity for ranged (Wisdom with Zen Archery), Dexterity with Weapon Finesse on a light weapon.</summary>
    public int AbilityAttackModifier(AttackContext a, WeaponStats s)
    {
        var c = a.Attacker;
        if (a.Ranged)
        {
            int dex = Mod(c, NwRules.AbilityDex);
            return Has(c, CombatK.FeatZenArchery) ? Math.Max(dex, Mod(c, NwRules.AbilityWis)) : dex;
        }
        int str = Mod(c, NwRules.AbilityStr);
        if (Has(c, CombatK.FeatWeaponFinesse) && Finessable(c, s)) return Math.Max(str, Mod(c, NwRules.AbilityDex));
        return str;
    }

    /// <summary>
    /// Weapon Finesse works with unarmed strikes, creature weapons and weapons whose smallest finesse size (baseitems
    /// WeaponFinesseMinimumCreatureSize, or a module override) the wielder reaches. Tables without the column:
    /// weapons smaller than the wielder and the rapier. A base item a module made count as unarmed isn't finessable
    /// for that (parity suite nwnx_weapon: a club or a greatsword so marked uses Strength on nwserver).
    /// </summary>
    public bool Finessable(Creature c, WeaponStats s)
    {
        if (s.Unarmed || s.Creature) return true;
        int size = FinesseSize(s.BaseItem);
        if (size >= 0) return c.Size >= size;
        return !s.FinesseColumn && (s.Size < c.Size || (s.BaseItem == CombatK.BaseRapier && c.Size >= 3));
    }

    /// <summary>The smallest creature size that finesses a base item, -1 for none.</summary>
    public int FinesseSize(int baseItem) =>
        Overrides.FinesseSize.TryGetValue(baseItem, out int size) ? size : baseItem < 0 ? -1 : Stats(baseItem).FinesseSize;

    /// <summary>Size attack modifier (ruleset.2da CREATURE_SIZE_*_ATTACK_*, else creaturesize.2da ACATTACKMOD).</summary>
    public int SizeAttackModifier(int size) => size switch
    {
        1 => RI("CREATURE_SIZE_TINY_ATTACK_BONUS", 2),
        2 => RI("CREATURE_SIZE_SMALL_ATTACK_BONUS", 1),
        4 => RI("CREATURE_SIZE_LARGE_ATTACK_PENALTY", -1),
        5 => RI("CREATURE_SIZE_HUGE_ATTACK_PENALTY", -2),
        _ => _w.Resources.TwoDA("creaturesize").GetInt(size, "ACATTACKMOD"),
    };

    /// <summary>Best of the weapon's enhancement and attack bonus properties that apply against the target, less attack/enhancement penalties.</summary>
    public int WeaponAttackBonus(AttackContext a)
    {
        int best = 0, penalty = 0;
        foreach (var p in Props(WeaponItem(a)))
        {
            switch (p.Type)
            {
                case CombatK.IpEnhancement or CombatK.IpAttackBonus: best = Math.Max(best, Value(p)); break;
                case CombatK.IpEnhancementVsAlignGroup or CombatK.IpEnhancementVsRace or CombatK.IpEnhancementVsAlign
                    or CombatK.IpAttackVsAlignGroup or CombatK.IpAttackVsRace or CombatK.IpAttackVsAlign:
                    if (ItemPropertyVersus.Applies(_w, p, a.Target)) best = Math.Max(best, Value(p));
                    break;
                case CombatK.IpDecreasedEnhancement or CombatK.IpDecreasedAttack: penalty += Math.Abs(Value(p)); break;
            }
        }
        return Math.Max(best, EnchantArrow(a)) - penalty;
    }

    /// <summary>Weapon Focus, Epic Weapon Focus, weapon master focus, Epic Prowess, Good Aim, Point Blank Shot, racial training, Bane of Enemies, proficiency.</summary>
    public int FeatAttackBonus(AttackContext a, WeaponStats s)
    {
        var c = a.Attacker;
        int b = 0;
        if (HasWeaponFeat(c, s, WeaponFeat.Focus)) b += RI("WEAPON_FOCUS_BONUS", 1);
        if (HasWeaponFeat(c, s, WeaponFeat.EpicFocus)) b += RI("EPIC_WEAPON_FOCUS_BONUS", 2);
        if (HasWeaponFeat(c, s, WeaponFeat.GreaterFocus)) b += Overrides.GreaterFocusAttackBonus;
        if (WeaponOfChoice(c, s))
        {
            if (Has(c, CombatK.FeatSuperiorWeaponFocus)) b += 1;
            if (Has(c, CombatK.FeatEpicSuperiorWeaponFocus)) b += 1;
        }
        if (Has(c, CombatK.FeatEpicProwess)) b += RI("EPIC_PROWESS_ATTACK_BONUS", 1);
        if (s.Thrown && Has(c, CombatK.FeatGoodAim)) b += RI("GOOD_AIM_MODIFIER", 1);
        if (a.Ranged && Has(c, CombatK.FeatPointBlankShot) && a.Distance <= RF("POINT_BLANK_SHOT_MAX_RANGE", 5f))
            b += RI("POINT_BLANK_SHOT_ATTACK_BONUS", 1);
        if (a.Target is Creature t)
        {
            int race = t.Race;
            if ((race == CombatK.RaceOrc && Has(c, CombatK.FeatBattleTrainingOrcs))
                || (race == CombatK.RaceGoblinoid && Has(c, CombatK.FeatBattleTrainingGoblins))
                || (race == CombatK.RaceReptilian && Has(c, CombatK.FeatBattleTrainingReptilians)))
                b += RI("OFFENSIVE_TRAINING_MODIFIER", 1);
            if (Has(c, CombatK.FeatEpicBaneOfEnemies) && FavoredEnemy(c, t)) b += RI("EPIC_BANE_OF_ENEMIES_ATTACK_BONUS", 2);
        }
        if (!s.Unarmed && !s.Creature && s.ProficiencyFeats.Length > 0 && !s.ProficiencyFeats.Any(f => Has(c, f))) b -= 4;
        return b;
    }

    /// <summary>Power Attack (melee), Expertise, Flurry of Blows and Rapid Shot modifiers.</summary>
    public int ModeAttackModifier(AttackContext a, WeaponStats s)
    {
        var c = a.Attacker;
        int m = 0;
        // Power Attack does nothing for a creature that isn't a player (nwserver, parity suite combat_modes).
        if (!a.Ranged && c.IsPC)
        {
            if (InMode(c, CombatK.ModeImprovedPowerAttack) && Has(c, CombatK.FeatImprovedPowerAttack))
                m += RI("IMPROVED_POWER_ATTACK_TO_HIT_MODIFIER", -10);
            else if (InMode(c, CombatK.ModePowerAttack) && Has(c, CombatK.FeatPowerAttack))
                m += RI("POWER_ATTACK_TO_HIT_MODIFIER", -5);
        }
        if (InMode(c, CombatK.ModeImprovedExpertise) && Has(c, CombatK.FeatImprovedExpertise))
            m += RI("IMPROVED_EXPERTISE_TO_HIT_MODIFIER", -10);
        else if (InMode(c, CombatK.ModeExpertise) && Has(c, CombatK.FeatExpertise))
            m += RI("EXPERTISE_TO_HIT_MODIFIER", -5);
        var main = Weapons(c).Main;
        if (FlurryActive(c, main)) m += RI("FLURRY_OF_BLOWS_TO_HIT_MODIFIER", -2);
        if (a.Ranged && RapidShotActive(c, main)) m += RI("RAPID_SHOT_TO_HIT_MODIFIER", -2);
        return m;
    }

    /// <summary>Two-weapon fighting: both hands take the normal penalties, lessened by a light off-hand weapon, Two-Weapon Fighting and Ambidexterity.</summary>
    public int TwoWeaponModifier(AttackContext a)
    {
        if (a.Ranged || a.CreatureWeapon) return 0;
        var c = a.Attacker;
        var (main, off, _) = Weapons(c);
        if (off == null) return 0;
        bool light = Stats(off).IsDouble || Stats(off).Size < c.Size;
        int m = a.OffHand ? RI("OFFHAND_NORMAL_OFFHAND_ATTACK_PENALTY", -10) : RI("ONHAND_NORMAL_OFFHAND_ATTACK_PENALTY", -6);
        if (light) m += RI("LIGHT_OFFHAND_WEAPON_BONUS", 2);
        if (Has(c, CombatK.FeatTwoWeaponFighting)) m += RI("TWO_WEAPON_FIGHTING_BONUS", 2);
        if (a.OffHand && Has(c, CombatK.FeatAmbidexterity)) m += RI("AMBIDEXTERITY_BONUS", 4);
        return m;
    }

    /// <summary>
    /// EffectAttackIncrease/Decrease for this hand and target, each capped at the attack bonus limit as in the original,
    /// and negative levels.
    /// </summary>
    public int EffectAttackModifier(AttackContext a)
    {
        int inc = 0, dec = 0, levels = 0;
        int hand = a.OffHand ? CombatK.AttackBonusOffHand : CombatK.AttackBonusOnHand;
        // Touch attacks don't count bonuses versus the target's race or alignment (parity suite fx_versus).
        var versus = a.Touch ? null : a.Target;
        foreach (var e in a.Attacker.Effects)
        {
            switch (e.Type)
            {
                case CombatK.EffectAttackIncrease when (e.Ints[1] == CombatK.AttackBonusMisc || e.Ints[1] == hand) && EffectQueries.AppliesVersus(e, versus):
                    inc += e.Ints[0]; break;
                case CombatK.EffectAttackDecrease when (e.Ints[1] == CombatK.AttackBonusMisc || e.Ints[1] == hand) && EffectQueries.AppliesVersus(e, versus):
                    dec += e.Ints[0]; break;
                case CombatK.EffectNegativeLevel: levels += Math.Max(0, e.Ints[0]); break;
            }
        }
        // Slowed: -2 (measured on the original).
        if (CreatureRules.IsSlowed(a.Attacker)) dec += 2;
        int limit = _w.Limits.AttackBonus;
        return Math.Min(Math.Max(0, inc), limit) - Math.Min(Math.Max(0, dec), limit) - levels;
    }

    /// <summary>
    /// Unseen attacker, blinded attacker, flanking, prone and blind targets, moving targets for ranged attacks,
    /// Opportunist. As in the original, paralysed, stunned or sleeping targets give no bonus, and a blind target only
    /// gives the unseen attacker's +2 once.
    /// </summary>
    public int SituationAttackModifier(AttackContext a)
    {
        int m = 0;
        if (a.AttackerUnseen) m += RI("INVISIBILITY_ATTACK_BONUS", 2);
        if (HasEffect(a.Attacker, CombatK.EffectBlindness)) m += RI("NOSIGHT_TARGET_PENALTY", -4);
        if (a.Flanked) m += RI("FLANK_ATTACK_BONUS", 2);
        if (a.Target is Creature t)
        {
            if (HasEffect(t, CombatK.EffectKnockdown))
                m += a.Ranged ? RI("PRONE_RANGED_TARGET_BONUS", -4) : RI("PRONE_MELEE_TARGET_BONUS", 4);
            if (HasEffect(t, CombatK.EffectBlindness) && !a.AttackerUnseen) m += RI("BLIND_TARGET_BONUS", 2);
            if (a.Ranged && t.MoveDirection != System.Numerics.Vector3.Zero) m -= Math.Abs(RI("MOVING_TARGET_PENALTY", 2));
        }
        if (a.Kind == AttackKind.Opportunity && Has(a.Attacker, CombatK.FeatOpportunist)) m += RI("OPPORTUNIST_TO_HIT_MODIFIER", 4);
        return m;
    }

    /// <summary>The attack roll modifier of a special attack (knockdown, disarm, called shot, sap, stunning fist, smite).</summary>
    public int SpecialAttackModifier(AttackContext a)
    {
        var c = a.Attacker;
        switch (a.SpecialAttack)
        {
            case CombatK.SpecialKnockdown or CombatK.SpecialImprovedKnockdown: return RI("KNOCKDOWN_TO_HIT_MODIFIER", -4);
            case CombatK.SpecialDisarm or CombatK.SpecialImprovedDisarm:
            {
                int m = a.SpecialAttack == CombatK.SpecialImprovedDisarm ? RI("IMPROVED_DISARM_TO_HIT_MODIFIER", -4) : RI("DISARM_TO_HIT_MODIFIER", -6);
                if (a.Target is Creature t && Weapons(t).Main is { } theirs)
                    m += (Stats(a.Weapon).Size - Stats(theirs).Size) * RI("DISARM_WEAPON_SIZE_MODIFIER", 4);
                return m;
            }
            case CombatK.SpecialCalledShotArm or CombatK.SpecialCalledShotLeg: return RI("CALLED_SHOT_TO_HIT_MODIFIER", -4);
            case CombatK.SpecialSap: return RI("SAP_TO_HIT_MODIFIER", -4);
            case CombatK.SpecialStunningFist: return RI("STUNNING_FIST_TO_HIT_MODIFIER", -4);
        }
        if (SmiteApplies(a)) return Math.Max(0, Mod(c, NwRules.AbilityCha));
        return 0;
    }

    /// <summary>Smite Evil against an evil target, Smite Good against a good one.</summary>
    public bool SmiteApplies(AttackContext a) => a.Target is Creature t && (
        (a.SpecialFeat == NwRules.FeatSmiteEvil && CreatureRules.GoodEvil(t.GoodEvil) == NwRules.AlignEvil) ||
        (a.SpecialFeat == NwRules.FeatSmiteGood && CreatureRules.GoodEvil(t.GoodEvil) == NwRules.AlignGood));
}
