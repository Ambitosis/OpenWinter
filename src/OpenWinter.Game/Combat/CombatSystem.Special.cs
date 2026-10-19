using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

/// <summary>
/// Feat attacks (ActionUseFeat): knockdown, disarm, called shot, sap, stunning fist, smite, quivering palm,
/// and the whirlwind attack (DoWhirlwindAttack). A feat attack replaces the user's next main-hand attack.
/// </summary>
public sealed partial class CombatSystem
{
    /// <summary>Feats ActionUseFeat turns into a special attack on the target.</summary>
    static readonly HashSet<int> AttackFeats = new()
    {
        CombatK.FeatKnockdown, CombatK.FeatImprovedKnockdown, CombatK.FeatDisarm, CombatK.FeatImprovedDisarm,
        CombatK.FeatCalledShot, CombatK.FeatSap, CombatK.FeatStunningFist, NwRules.FeatSmiteEvil, NwRules.FeatSmiteGood,
        CombatK.FeatQuiveringPalm,
    };

    public static bool IsAttackFeat(int feat) => AttackFeats.Contains(feat);

    /// <summary>
    /// ActionUseFeat for combat feats: starts attacking the target (or the current one) with the feat attack queued
    /// for the next swing. Returns false for feats combat doesn't handle.
    /// </summary>
    public static bool TryUseCombatFeat(World world, Creature c, int feat, int subFeat, uint target)
    {
        if (!IsAttackFeat(feat)) return false;
        var combat = world.System<CombatSystem>();
        var t = world.Get(target) ?? world.Get(combat.TargetOf(c));
        if (t == null || t == c || t.Area != c.Area) return true;
        combat.Attack(c, t);
        var s = combat.State(c);
        // Ordered in the middle of a round (some of its attacks made, some left), the feat is lost and the attack goes
        // on as an ordinary one (nwserver ai_feat_use: Knockdown ordered between rounds or to start a fight knocks
        // down; ordered after the round's first or second swing it never does).
        if (s.RoundStart >= 0 && s.NextAttack > 0 && s.NextAttack < s.Plan.Count) return true;
        s.PendingFeat = feat;
        s.PendingSubFeat = subFeat;
        return true;
    }

    /// <summary>Turns a queued feat into the attack's special attack type (melee-only attacks are dropped for ranged weapons).</summary>
    void ApplyPendingFeat(AttackContext a, int feat, int subFeat)
    {
        a.SpecialFeat = feat;
        int special = feat switch
        {
            CombatK.FeatKnockdown => CombatK.SpecialKnockdown,
            CombatK.FeatImprovedKnockdown => CombatK.SpecialImprovedKnockdown,
            CombatK.FeatDisarm => CombatK.SpecialDisarm,
            CombatK.FeatImprovedDisarm => CombatK.SpecialImprovedDisarm,
            CombatK.FeatCalledShot => subFeat == CombatK.SpecialCalledShotArm ? CombatK.SpecialCalledShotArm : CombatK.SpecialCalledShotLeg,
            CombatK.FeatSap => CombatK.SpecialSap,
            CombatK.FeatStunningFist => CombatK.SpecialStunningFist,
            _ => 0,
        };
        if (a.Ranged && special != 0) { a.SpecialFeat = -1; return; }
        if (feat == CombatK.FeatQuiveringPalm && !a.Unarmed) { a.SpecialFeat = -1; return; }
        a.SpecialAttack = special;
    }

    static string? SpecialName(AttackContext a) => a.SpecialAttack switch
    {
        CombatK.SpecialKnockdown => "Knockdown",
        CombatK.SpecialImprovedKnockdown => "Improved Knockdown",
        CombatK.SpecialDisarm => "Disarm",
        CombatK.SpecialImprovedDisarm => "Improved Disarm",
        CombatK.SpecialCalledShotLeg => "Called Shot: Leg",
        CombatK.SpecialCalledShotArm => "Called Shot: Arm",
        CombatK.SpecialSap => "Sap",
        CombatK.SpecialStunningFist => "Stunning Fist",
        _ => a.SpecialFeat switch
        {
            NwRules.FeatSmiteEvil => "Smite Evil",
            NwRules.FeatSmiteGood => "Smite Good",
            CombatK.FeatQuiveringPalm => "Quivering Palm",
            _ => null,
        },
    };

    void SpecialAttackMissed(AttackOutcome o) { /* the feat is spent; nothing else happens */ }

    /// <summary>What a special attack does on a hit: Discipline checks against the attack roll, Fortitude saves, effects.</summary>
    void SpecialAttackHit(AttackOutcome o)
    {
        var a = o.Context;
        if (a.Target is not Creature t) return;
        var att = a.Attacker;
        int attackTotal = o.Roll + o.AttackBonus;
        switch (a.SpecialAttack)
        {
            case CombatK.SpecialKnockdown or CombatK.SpecialImprovedKnockdown:
            {
                if (CreatureRules.IsImmune(_world, t, NwRules.ImmunityKnockdown, att)) { o.Feedback.Add("Immune to Knockdown"); break; }
                int maxSize = att.Size + (a.SpecialAttack == CombatK.SpecialImprovedKnockdown ? 2 : 1);
                if (t.Size > maxSize) { o.Feedback.Add("Target too large"); break; }
                int sizeBonus = (t.Size - att.Size) * RI("KNOCKDOWN_CREATURE_SIZE_MODIFIER", 4);
                if (SkillCheck(t, CombatK.SkillDiscipline, attackTotal - sizeBonus)) { o.Feedback.Add("Discipline: resisted"); break; }
                ApplyEffect(MakeEffect("KNOCKDOWN", att), t, RF("KNOCKDOWN_EFFECT_DURATION", 6f));
                o.Feedback.Add("Knocked Down");
                break;
            }
            case CombatK.SpecialDisarm or CombatK.SpecialImprovedDisarm:
            {
                var weapon = t.Equipment.GetValueOrDefault(EquipSlot.RightHand);
                if (weapon == null || !t.Disarmable || weapon.Plot) { o.Feedback.Add("Cannot be disarmed"); break; }
                if (SkillCheck(t, CombatK.SkillDiscipline, attackTotal)) { o.Feedback.Add("Discipline: resisted"); break; }
                Disarm(t, weapon);
                o.Feedback.Add("Disarmed");
                break;
            }
            case CombatK.SpecialCalledShotLeg:
                if (SkillCheck(t, CombatK.SkillDiscipline, attackTotal)) { o.Feedback.Add("Discipline: resisted"); break; }
                ApplyEffect(MakeEffect("ABILITY_DECREASE", att, NwRules.AbilityDex, RI("CALLED_SHOT_LEG_ABILITY_PENALTY", 2)), t, RF("CALLED_SHOT_EFFECT_DURATION", 24f));
                ApplyEffect(MakeEffect("MOVEMENT_SPEED_DECREASE", att, RI("CALLED_SHOT_LEG_MOVEMENT_PENALTY", 20)), t, RF("CALLED_SHOT_EFFECT_DURATION", 24f));
                o.Feedback.Add("Leg Wounded");
                break;
            case CombatK.SpecialCalledShotArm:
                if (SkillCheck(t, CombatK.SkillDiscipline, attackTotal)) { o.Feedback.Add("Discipline: resisted"); break; }
                ApplyEffect(MakeEffect("ATTACK_DECREASE", att, RI("CALLED_SHOT_ARM_ATTACK_PENALTY", 2), CombatK.AttackBonusMisc), t, RF("CALLED_SHOT_EFFECT_DURATION", 24f));
                o.Feedback.Add("Arm Wounded");
                break;
            case CombatK.SpecialSap:
                if (SkillCheck(t, CombatK.SkillDiscipline, attackTotal)) { o.Feedback.Add("Discipline: resisted"); break; }
                ApplyEffect(MakeEffect("DAZED", att), t, RF("SAP_EFFECT_DURATION", 12f));
                o.Feedback.Add("Dazed");
                break;
            case CombatK.SpecialStunningFist:
            {
                if (Rules.IsCriticalImmune(a)) { o.Feedback.Add("Immune to Stunning Fist"); break; }
                int dc = MonkDC(att, RI("STUNNING_FIST_BASE_SAVE_DC", 10));
                for (int f = CombatK.FeatEpicImprovedStunningFist10; f >= CombatK.FeatEpicImprovedStunningFist1; f--)
                    if (HasFeat(att, f)) { int n = f - CombatK.FeatEpicImprovedStunningFist1 + 1; dc += RI($"EPIC_IMPROVED_STUNNING_FIST_DC_BONUS_{n}", 2 * n); break; }
                if (SavingThrow(t, NwRules.SaveFort, dc, NwRules.SaveTypeAll, att) != 0) { o.Feedback.Add("Fortitude: resisted"); break; }
                ApplyEffect(MakeEffect("STUNNED", att), t, RF("STUNNING_FIST_EFFECT_DURATION", 6f));
                o.Feedback.Add("Stunned");
                break;
            }
        }
        if (a.SpecialFeat == CombatK.FeatQuiveringPalm)
        {
            int monk = CreatureRules.ClassLevel(att, NwRules.ClassMonk);
            if (Rules.IsCriticalImmune(a) || t.TotalLevel >= Math.Max(1, monk)) { o.Feedback.Add("Quivering Palm: no effect"); return; }
            if (SavingThrow(t, NwRules.SaveFort, MonkDC(att, RI("QUIVERING_PALM_BASE_DC", 10)), NwRules.SaveTypeAll, att) != 0) { o.Feedback.Add("Fortitude: resisted"); return; }
            ApplyEffect(MakeEffect("DEATH", att, 1, 1), t, 0);
            o.Killed = HitPoints.IsDead(t);
        }
    }

    /// <summary>Base DC + half the monk level (half the hit dice for others) + Wisdom.</summary>
    int MonkDC(Creature c, int baseDc)
    {
        int monk = CreatureRules.ClassLevel(c, NwRules.ClassMonk);
        return baseDc + (monk > 0 ? monk : c.TotalLevel) / 2 + CreatureRules.AbilityModifier(_world, c, NwRules.AbilityWis);
    }

    /// <summary>A disarmed weapon falls to the ground (undroppable or cursed ones go to the inventory).</summary>
    void Disarm(Creature t, Item weapon)
    {
        var inv = _world.Inventory;
        if (weapon.Droppable && !weapon.Cursed && t.Area != null) inv.Drop(weapon, t.Location, t);
        else inv.Unequip(t, weapon);
        _world.MarkChanged(t);
    }

    /// <summary>
    /// DoWhirlwindAttack: one attack with the main weapon against every enemy within WHIRLWIND_ATTACK_RANGE
    /// (IMPROVED_WHIRLWIND_ATTACK_RANGE). Uses up the rest of the round's attacks.
    /// </summary>
    public List<AttackOutcome> Whirlwind(Creature c, bool improved, bool feedback)
    {
        var results = new List<AttackOutcome>();
        if (c.Area == null || !CanAttack(c) || Creature is not { } r) return results;
        var main = r.Weapons(c).Main;
        if (main != null && r.Stats(main).Ranged) return results;
        float range = RF(improved ? "IMPROVED_WHIRLWIND_ATTACK_RANGE" : "WHIRLWIND_ATTACK_RANGE", improved ? 4f : 2f);
        var targets = c.Area.Objects.OfType<Creature>()
            .Where(o => o != c && !o.IsDead && !o.IsDestroyed && (_world.Factions.IsEnemyOf(c, o) || TargetOf(c) == o.Id)
                && Distance2(c, o) <= range + Radius(c) + Radius(o))
            .OrderBy(o => Distance2(c, o)).ToList();
        float swing = RoundSeconds / Math.Max(1, targets.Count);
        bool first = true;
        foreach (var t in targets)
        {
            var a = Rules.Context(c, t, main, false, Dice);
            a.Kind = AttackKind.Whirlwind;
            a.Animation = first ? "whirlwind" : "";
            a.ShowFeedback = feedback;
            first = false;
            results.Add(Resolve(a, swing));
        }
        var s = State(c);
        s.NextAttack = s.Plan.Count;
        return results;
    }
}
