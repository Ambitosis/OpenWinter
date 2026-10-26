using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

/// <summary>Armour class against an attacker, concealment, critical threat and immunity.</summary>
public sealed partial class CreatureCombatRules
{
    /// <summary>DAMAGE_TYPE_* bits a weapon deals (baseitems WeaponType), for AC-versus-damage-type and damage.</summary>
    public static int WeaponDamageBits(WeaponStats s) => s.Type switch
    {
        1 => CombatK.DamagePiercing,
        2 => CombatK.DamageBludgeoning,
        3 => CombatK.DamageSlashing,
        4 => CombatK.DamagePiercing | CombatK.DamageSlashing,
        5 => CombatK.DamageBludgeoning | CombatK.DamagePiercing,
        _ => CombatK.DamageBludgeoning,
    };

    /// <summary>
    /// The target's AC against this attack: the creature rules' AC with versus-attacker effects and item
    /// properties, minus Dexterity and the dodge part (bonuses and penalties) when denied Dexterity, plus Dodge
    /// against the current opponent, Mobility against attacks of opportunity, Expertise and racial defensive
    /// training. Scripted touch attacks face 10 + size + deflection and any Dexterity penalty. Doors and placeables use
    /// their template AC.
    /// </summary>
    public int ArmorClass(AttackContext a)
    {
        switch (a.Target)
        {
            case Placeable p: return (int)p.Template.GetInt("AC", 5);
            case Door d: return (int)d.Template.GetInt("AC", 5);
            case not Creature: return 10;
        }
        var t = (Creature)a.Target;
        // Weapon combat retains Dexterity under paralysis/stun/sleep; scripted touch attacks use the
        // helpless score of 3 (native touch_states), while only entanglement does so for weapons.
        var parts = CreatureRules.ArmorClass(_w, t, a.Attacker, WeaponDamageBits(Stats(a.Weapon)), helplessDex: a.Touch || HasEffect(t, CombatK.EffectEntangle));
        if (a.Touch) return 10 + parts.Size + parts.Deflection + Math.Min(0, parts.Dex);
        int dodge = parts.Dodge;
        if (a.TargetDeniedDex)
        {
            if (parts.Dex > 0) parts.Dex = 0;
            dodge = 0;
        }
        else
        {
            var combat = _w.System<CombatSystem>();
            uint opponent = combat.TargetOf(t);
            if (Has(t, CombatK.FeatDodge) && (opponent == a.Attacker.Id || opponent == GameObject.InvalidId))
                dodge += RI("DODGE_AC_BONUS", 1);
            if (a.Kind == AttackKind.Opportunity && Has(t, CombatK.FeatMobility)) dodge += RI("MOBILITY_DODGE_BONUS", 4);
            dodge = Math.Min(dodge, RI("MAX_AC_DODGE_MOD", 20));
        }
        parts.Dodge = dodge;
        int ac = parts.Total;
        if (InMode(t, CombatK.ModeImprovedExpertise) && Has(t, CombatK.FeatImprovedExpertise)) ac += RI("IMPROVED_EXPERTISE_AC_BONUS", 10);
        else if (InMode(t, CombatK.ModeExpertise) && Has(t, CombatK.FeatExpertise)) ac += RI("EXPERTISE_AC_BONUS", 5);
        if (a.Attacker.Race == CombatK.RaceGiant && Has(t, CombatK.FeatBattleTrainingGiants)) ac += RI("DEFENSIVE_TRAINING_MODIFIER", 4);
        return ac;
    }

    /// <summary>
    /// Miss chance: the best of the target's concealment (EffectConcealment versus the attacker and this kind of
    /// attack, Epic Self-Concealment, being unseen) and the attacker's own miss chance (EffectMissChance, blindness).
    /// </summary>
    public int Concealment(AttackContext a)
    {
        int kind = a.Ranged ? CombatK.MissVsRanged : CombatK.MissVsMelee;
        int best = 0;
        foreach (var e in a.Target.Effects)
            if (e.Type == CombatK.EffectConcealment && (e.Ints[1] == CombatK.MissNormal || e.Ints[1] == kind) && EffectQueries.AppliesVersus(e, a.Attacker))
                best = Math.Max(best, e.Ints[0]);
        if (a.Target is Creature t)
            for (int f = CombatK.FeatEpicSelfConcealment50; f >= CombatK.FeatEpicSelfConcealment10; f--)
                if (Has(t, f)) { int n = (f - CombatK.FeatEpicSelfConcealment10 + 1) * 10; best = Math.Max(best, RI($"EPIC_SELF_CONCEALMENT_{n}", n)); break; }
        if (a.TargetUnseen)
            best = Math.Max(best, HasEffect(a.Attacker, CombatK.EffectBlindness) || HasEffect(a.Attacker, CombatK.EffectDarkness)
                ? RI("BLIND_MISS_CHANCE", 50) : RI("INVISIBILITY_CONCEALMENT_CHANCE", 50));
        foreach (var e in a.Attacker.Effects)
            if (e.Type == CombatK.EffectMissChance && (e.Ints[1] == CombatK.MissNormal || e.Ints[1] == kind) && EffectQueries.AppliesVersus(e, a.Target))
                best = Math.Max(best, e.Ints[0]);
        return Math.Clamp(best, 0, 100);
    }

    /// <summary>
    /// Threat range (baseitems CritThreat, plus as much again for Improved Critical and again for Keen: they stack in
    /// the original; +2 with Ki Critical on the weapon of choice) and multiplier (baseitems CritHitMult, +1 with
    /// Increased Multiplier on the weapon of choice).
    /// </summary>
    public (int Threat, int Multiplier) Critical(AttackContext a)
    {
        var s = Stats(a.Weapon);
        var c = a.Attacker;
        int range = s.CritThreat;
        bool keen = a.Weapon != null && a.Weapon.Properties.Any(p => p.Valid && p.Type == CombatK.IpKeen);
        range += (keen ? s.CritThreat : 0) + (Has(c, s.ImprovedCritical) ? s.CritThreat : 0);
        int mult = s.CritMult;
        if (WeaponOfChoice(c, s))
        {
            if (Has(c, CombatK.FeatKiCritical)) range += RI("KI_CRITICAL_BONUS", 2);
            if (Has(c, CombatK.FeatIncreaseMultiplier)) mult += 1;
        }
        int threat = Math.Clamp(21 - range, 2, 20);
        if (c.Overrides is { } o)
        {
            // Script adjustments (NWNX_Creature critical settings) for all attacks, this hand, this base item
            // (gloves for an unarmed strike) or both: modifiers add up, the most specific override wins.
            int hand = a.OffHand ? 2 : 1, item = a.Weapon?.BaseItem ?? CombatK.BaseGloves;
            threat += CritSum(o.CritRangeModifier, hand, item);
            if (CritPick(o.CritRangeOverride, hand, item) is int minRoll) threat = minRoll;
            mult += CritSum(o.CritMultiplierModifier, hand, item);
            if (CritPick(o.CritMultiplierOverride, hand, item) is int m) mult = m;
            threat = Math.Clamp(threat, 1, 21);
            mult = Math.Max(1, mult);
        }
        return (threat, mult);
    }

    static int CritSum(Dictionary<(int, int), int> d, int hand, int item) =>
        d.GetValueOrDefault((0, -1)) + d.GetValueOrDefault((hand, -1)) + d.GetValueOrDefault((0, item)) + d.GetValueOrDefault((hand, item));

    static int? CritPick(Dictionary<(int, int), int> d, int hand, int item) =>
        d.TryGetValue((hand, item), out int v) || d.TryGetValue((0, item), out v) || d.TryGetValue((hand, -1), out v) || d.TryGetValue((0, -1), out v) ? v : null;

    /// <summary>Undead, constructs and objects ignore critical hits, as does anything with the critical hit immunity.</summary>
    public bool IsCriticalImmune(AttackContext a) => a.Target is not Creature t
        || t.Race is CombatK.RaceUndead or CombatK.RaceConstruct
        || CreatureRules.IsImmune(_w, t, NwRules.ImmunityCriticalHit, a.Attacker);

    /// <summary>
    /// Undead, constructs and objects ignore sneak attacks, as does anything immune to sneak attacks or, as in the
    /// original, to critical hits, and a plot creature (stealthself: "Bandit : Immune to Sneak Attacks." on a plot dummy).
    /// </summary>
    public bool IsSneakImmune(AttackContext a) => IsCriticalImmune(a) || a.Target.Plot
        || CreatureRules.IsImmune(_w, (Creature)a.Target, NwRules.ImmunitySneakAttack, a.Attacker);
}
