using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Combat;

/// <summary>
/// On-hit item properties: OnHit effects (iprp_onhit.2da: DC from iprp_onhitcost, chance and duration from
/// iprp_onhitdur) and OnHitCastSpell on the weapon (or ammunition, or gloves) and on the target's armour.
/// </summary>
public sealed partial class CombatSystem
{
    void OnHitProperties(AttackOutcome o)
    {
        var a = o.Context;
        if (Creature is not { } r) return;
        var source = r.DamageSource(a);
        if (source != null)
            foreach (var p in source.Properties.ToList())
            {
                if (HitPoints.IsDead(a.Target)) break;
                if (p.Type == CombatK.IpOnHit) OnHitEffect(a, p);
                else if (p.Type == CombatK.IpOnHitCastSpell) CastOnHit(a.Attacker, a.Target, source, p);
            }
        // Armour (and shields) cast their on-hit spell at melee attackers.
        if (!a.Ranged && a.Target is Creature tc && !tc.IsDead)
            foreach (int slot in new[] { EquipSlot.Chest, EquipSlot.LeftHand })
                if (tc.Equipment.TryGetValue(slot, out var worn) && (slot == EquipSlot.Chest || !r.IsWeapon(worn)))
                    foreach (var p in worn.Properties.ToList())
                        if (p.Type == CombatK.IpOnHitCastSpell) CastOnHit(tc, a.Attacker, worn, p);
    }

    void CastOnHit(Creature caster, GameObject target, Item item, ItemProperty p)
    {
        int spell = _world.Resources.TwoDA("iprp_onhitspell").GetInt(p.SubType, "SpellIndex", -1);
        if (spell < 0) return;
        int casterLevel = Math.Max(1, CreatureRules.PropertyValue(_world, p));
        CombatHooks.CastOnHitSpell(_world, caster, target, item, spell, casterLevel);
    }

    /// <summary>iprp_onhit.2da rows and what they do: (effect, save, save type, permanent).</summary>
    static readonly Dictionary<int, (string Effect, int Save, int SaveType)> OnHitTable = new()
    {
        [0] = ("SLEEP", NwRules.SaveWill, NwRules.SaveTypeMind),
        [1] = ("STUNNED", NwRules.SaveWill, NwRules.SaveTypeMind),
        [2] = ("PARALYZE", NwRules.SaveWill, NwRules.SaveTypeMind),
        [3] = ("CONFUSED", NwRules.SaveWill, NwRules.SaveTypeMind),
        [5] = ("DAZED", NwRules.SaveWill, NwRules.SaveTypeMind),
        [6] = ("DOOM", NwRules.SaveWill, NwRules.SaveTypeMind),
        [7] = ("FRIGHTENED", NwRules.SaveWill, NwRules.SaveTypeFear),
        [9] = ("SLOW", NwRules.SaveWill, NwRules.SaveTypeAll),
        [14] = ("SILENCE", NwRules.SaveWill, NwRules.SaveTypeAll),
        [15] = ("DEAF", NwRules.SaveFort, NwRules.SaveTypeAll),
        [16] = ("BLINDNESS", NwRules.SaveFort, NwRules.SaveTypeAll),
        [17] = ("NEGATIVELEVEL", NwRules.SaveFort, NwRules.SaveTypeNegative),
        [18] = ("ABILITY_DECREASE", NwRules.SaveFort, NwRules.SaveTypeAll),
        [19] = ("POISON", -1, 0),
        [20] = ("DISEASE", -1, 0),
        [21] = ("DEATH", NwRules.SaveFort, NwRules.SaveTypeDeath),
        [22] = ("DEATH", NwRules.SaveFort, NwRules.SaveTypeDeath),
        [23] = ("DEATH", NwRules.SaveFort, NwRules.SaveTypeDeath),
        [24] = ("DEATH", NwRules.SaveFort, NwRules.SaveTypeDeath),
    };

    /// <summary>
    /// An OnHit property: roll its chance (iprp_onhitdur EffectChance), check slay conditions, then the target
    /// saves against the property's DC (iprp_onhitcost Value) or suffers the effect for DurationRounds rounds.
    /// TODO(combat): dispel rows (spells package), Knock, Wounding.
    /// </summary>
    void OnHitEffect(AttackContext a, ItemProperty p)
    {
        if (a.Target is not Creature t || !OnHitTable.TryGetValue(p.SubType, out var row)) return;
        var onhit = _world.Resources.TwoDA("iprp_onhit");
        bool durationParam = onhit.GetInt(p.SubType, "Param1ResRef", -1) == 1;
        int chance = 100, rounds = 1;
        if (durationParam)
        {
            var dur = _world.Resources.TwoDA("iprp_onhitdur");
            chance = dur.GetInt(p.Param1Value, "EffectChance", 100);
            rounds = Math.Max(1, dur.GetInt(p.Param1Value, "DurationRounds", 1));
        }
        if (chance < 100 && Dice.Roll(100) > chance) return;
        switch (p.SubType)
        {
            case 21 when t.Race != p.Param1Value: return;
            case 22 when !ItemPropertyVersus.AlignGroup(p.Param1Value, t): return;
            case 23 when !ItemPropertyVersus.SpecificAlignment(p.Param1Value, t): return;
        }
        int dc = _world.Resources.TwoDA("iprp_onhitcost").GetInt(p.CostTableValue, "Value", 14);
        if (row.Save > 0 && SavingThrow(t, row.Save, dc, row.SaveType, a.Attacker) != 0) return;
        var att = a.Attacker;
        float seconds = rounds * RoundSeconds;
        switch (row.Effect)
        {
            case "DOOM":
                ApplyEffect(MakeEffect("ATTACK_DECREASE", att, 2, CombatK.AttackBonusMisc), t, seconds);
                ApplyEffect(MakeEffect("SAVING_THROW_DECREASE", att, NwRules.SaveAll, 2, NwRules.SaveTypeAll), t, seconds);
                ApplyEffect(MakeEffect("SKILL_DECREASE", att, NwRules.SkillAll, 2), t, seconds);
                break;
            case "NEGATIVELEVEL": ApplyEffect(MakeEffect("NEGATIVELEVEL", att, 1), t, -1); break;
            case "ABILITY_DECREASE": ApplyEffect(MakeEffect("ABILITY_DECREASE", att, p.Param1Value, 1), t, -1); break;
            case "POISON": ApplyEffect(MakeEffect("POISON", att, p.Param1Value), t, -1); break;
            case "DISEASE": ApplyEffect(MakeEffect("DISEASE", att, p.Param1Value), t, -1); break;
            case "DEATH": ApplyEffect(MakeEffect("DEATH", att, 1, 1), t, 0); break;
            default: ApplyEffect(MakeEffect(row.Effect, att), t, seconds); break;
        }
    }
}
