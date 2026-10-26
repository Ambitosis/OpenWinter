using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Spells;

/// <summary>
/// Spell resistance, spell immunity and spell absorption (globes, mantles), and evasion-adjusted
/// reflex damage. ResistSpell checks immunity first (2), then unlimited absorption (2, globes),
/// then limited absorption (3, mantles, which lose the absorbed levels), then spell resistance (1).
/// </summary>
public static class SpellDefense
{
    public const int NotResisted = 0, ResistedBySR = 1, ResistedByImmunity = 2, ResistedByAbsorption = 3;

    // EFFECT_TYPE_*, ITEM_PROPERTY_*, FEAT_*
    const int EffectSpellLevelAbsorption = 68, EffectSpellImmunity = 73;
    const int IpImmunitySpecificSpell = 53, IpImmunitySpellSchool = 54, IpImmunitySpellsByLevel = 78;
    const int FeatEvasion = 206, FeatImprovedEvasion = 212, FeatSpellPenetration = 36, FeatGreaterSpellPenetration = 401,
        FeatEpicSpellPenetration = 618;

    /// <summary>
    /// The spell a resistance check is about: the area of effect's spell when the check runs in that AoE's
    /// script (or the caster is an AoE), else the caster's current or last spell.
    /// </summary>
    public static SpellCastInfo? InfoFor(GameObject? self, GameObject caster)
    {
        if (self is AreaOfEffect aoe && aoe.Creator == caster.Id) return aoe.LastSpell ?? FromAoe(aoe);
        if (caster is AreaOfEffect a) return a.LastSpell ?? FromAoe(a);
        return caster.LastSpell;
    }

    static SpellCastInfo FromAoe(AreaOfEffect aoe) => new() { SpellId = aoe.SpellId, CasterLevel = aoe.CasterLevel };

    /// <summary>The level a spell counts as for absorption and immunity by level.</summary>
    public static int LevelOf(World w, SpellCastInfo? info, int spell)
    {
        if (info != null && info.SpellId == spell && info.SpellLevel >= 0) return info.SpellLevel;
        if (info != null && info.SpellId == spell && info.ClassType != NwRules.ClassInvalid)
        {
            int l = CreatureRules.SpellLevelForClass(w, info.ClassType, spell);
            if (l >= 0) return l;
        }
        return spell >= 0 ? SpellTable.Innate(w, spell) : 0;
    }

    /// <summary>ResistSpell: 0 not resisted, 1 spell resistance, 2 immunity or globe, 3 mantle.</summary>
    public static int Resist(World w, GameObject caster, GameObject target, SpellCastInfo? info)
    {
        // Outside a spell (the caster never cast one) nothing resists, like the original.
        if (info == null) return NotResisted;
        int spell = info.SpellId;
        int school = spell >= 0 ? SpellTable.School(w, spell) : SpellTable.SchoolGeneral;
        int level = LevelOf(w, info, spell);
        if (spell >= 0 && Immune(w, target, caster, spell, school, level, feedback: true)) return ResistedByImmunity;
        if (spell >= 0 && Absorb(w, target, caster, spell, school, level, limited: false, removeLevels: false, feedback: true)) return ResistedByImmunity;
        if (spell >= 0 && Absorb(w, target, caster, spell, school, level, limited: true, removeLevels: true, feedback: true)) return ResistedByAbsorption;
        if (ResistanceRoll(w, target, caster, info.CasterLevel, -1, feedback: true)) return ResistedBySR;
        return NotResisted;
    }

    /// <summary>EffectSpellImmunity (this spell or all spells) and item immunity to the spell, its school, or its level.</summary>
    public static bool Immune(World w, GameObject target, GameObject? caster, int spell, int school, int level, bool feedback)
    {
        bool immune = target.Effects.Any(e => e.Type == EffectSpellImmunity && (e.Ints[0] == spell || e.Ints[0] == -1));
        if (!immune && target is Creature c)
        {
            foreach (var (_, _, p) in CreatureRules.EquippedProperties(c))
            {
                if (p.Type == IpImmunitySpecificSpell
                    && w.Resources.TwoDA("iprp_spellcost").GetInt(p.CostTableValue, "SpellIndex", -2) == spell) immune = true;
                else if (p.Type == IpImmunitySpellSchool
                    && SpellTable.SchoolOfLetter(w.Resources.TwoDA("iprp_spellshl").Get(p.SubType, "Letter")) == school && school != SpellTable.SchoolGeneral) immune = true;
                else if (p.Type == IpImmunitySpellsByLevel && p.CostTableValue > 0 && level <= p.CostTableValue) immune = true;
                if (immune) break;
            }
        }
        if (immune && feedback)
        {
            // Measured (visual scene conditionai): "Goblin : Immune to Magic Missile", then "Goblin attempts to resist
            // spell : magic immunity" (dialog.tlk 52971, 8342 with 8344).
            string spellName = w.Resources.TlkString((uint)SpellTable.Table(w).GetInt(spell, "Name", 0));
            Feedback(w, target, caster, Tlk(w, ImmuneTo, Name(w, target), spellName));
            Feedback(w, target, caster, Tlk(w, AttemptsToResist, Name(w, target), w.Resources.TlkString(MagicImmunity)));
        }
        return immune;
    }

    /// <summary>
    /// Spell level absorption: unlimited (globes, total 0) or limited (mantles). A limited effect loses the
    /// absorbed levels when <paramref name="removeLevels"/>, and goes when none remain.
    /// </summary>
    public static bool Absorb(World w, GameObject target, GameObject? caster, int spell, int school, int level, bool limited, bool removeLevels, bool feedback)
    {
        foreach (var e in target.Effects.Where(e => e.Type == EffectSpellLevelAbsorption).ToList())
        {
            int max = e.Ints[0] < 0 ? 9 : e.Ints[0];
            bool isLimited = e.Ints[1] > 0;
            if (isLimited != limited || level > max) continue;
            if (e.Ints[2] != SpellTable.SchoolGeneral && e.Ints[2] != school) continue;
            if (limited && removeLevels)
            {
                e.Ints[1] -= level;
                if (e.Ints[1] <= 0) w.System<EffectSystem>().Remove(target, e);
            }
            // A globe (unlimited) reads like immunity (measured: "Goblin attempts to resist spell : magic immunity");
            // a mantle's line, "spell absorbed" (8345), is not measured.
            if (feedback) Feedback(w, target, caster, Tlk(w, AttemptsToResist, Name(w, target), w.Resources.TlkString(limited ? SpellAbsorbed : MagicImmunity)));
            return true;
        }
        return false;
    }

    /// <summary>Spell penetration bonus to beat spell resistance (best of the three feats).</summary>
    public static int Penetration(World w, Creature c)
    {
        if (CreatureRules.HasFeat(w, c, FeatEpicSpellPenetration, ignoreUses: true)) return Ruleset.Int(w, "FEAT_EPIC_SPELL_PENETRATION_LEVEL_BONUS", 6);
        if (CreatureRules.HasFeat(w, c, FeatGreaterSpellPenetration, ignoreUses: true)) return Ruleset.Int(w, "FEAT_GREATER_SPELL_PENETRATION_LEVEL_BONUS", 4);
        if (CreatureRules.HasFeat(w, c, FeatSpellPenetration, ignoreUses: true)) return Ruleset.Int(w, "FEAT_SPELL_PENETRATION_LEVEL_BONUS", 2);
        return 0;
    }

    /// <summary>
    /// SpellResistanceCheck: d20 + caster level + penetration against the target's spell resistance.
    /// True when the target resists. No spell resistance means no roll.
    /// </summary>
    public static bool ResistanceRoll(World w, GameObject target, GameObject caster, int casterLevel, int spellResistance, bool feedback, Func<int, int>? random = null)
    {
        int sr = spellResistance >= 0 ? spellResistance : target is Creature tc ? CreatureRules.SpellResistance(w, tc) : 0;
        if (sr <= 0) return false;
        var by = caster is AreaOfEffect aoe ? w.Get(aoe.Creator) : caster;
        int bonus = casterLevel + (by is Creature cc ? Penetration(w, cc) : 0);
        int roll = (random ?? (n => Random.Shared.Next(n)))(20) + 1;
        bool resisted = roll + bonus < sr;
        // Measured: "Goblin attempts to resist spell : spell resisted" (8342 with 8343), no roll shown. dialog.tlk has
        // no line for spell resistance that fails, so none is printed (not measured).
        if (feedback && resisted)
            Feedback(w, target, by, Tlk(w, AttemptsToResist, Name(w, target), w.Resources.TlkString(SpellResisted)));
        return resisted;
    }

    /// <summary>
    /// GetReflexAdjustedDamage: a reflex save halves the damage; Evasion makes a successful save take none,
    /// Improved Evasion also halves it on a failed save. Immunity to the save type takes no damage.
    /// </summary>
    public static int ReflexAdjustedDamage(World w, int damage, GameObject target, int dc, int saveType, GameObject? versus)
    {
        int result = CreatureRules.RollSave(w, target, NwRules.SaveReflex, dc, saveType, versus);
        if (result == CreatureRules.SaveImmune) return 0;
        bool saved = result == CreatureRules.SaveSucceeded;
        if (target is Creature c)
        {
            if (CreatureRules.HasFeat(w, c, FeatImprovedEvasion, ignoreUses: true)) return saved ? 0 : damage / 2;
            if (CreatureRules.HasFeat(w, c, FeatEvasion, ignoreUses: true)) return saved ? 0 : damage;
        }
        return saved ? damage / 2 : damage;
    }

    static string Name(World w, GameObject o) => o.Name.Resolve(w.Resources.Talk);

    // dialog.tlk: "<CUSTOM0> attempts to resist spell : <CUSTOM1>", its endings, and "<CUSTOM0> : Immune to <CUSTOM1>".
    const uint AttemptsToResist = 8342, SpellResisted = 8343, MagicImmunity = 8344, SpellAbsorbed = 8345, ImmuneTo = 52971;

    static string Tlk(World w, uint strref, string custom0, string custom1) =>
        w.Resources.TlkString(strref).Replace("<CUSTOM0>", custom0).Replace("<CUSTOM1>", custom1);

    /// <summary>Combat-log line for the players involved.</summary>
    public static void Feedback(World w, GameObject target, GameObject? other, string text)
    {
        if (w.Output == null) return;
        if (target is Creature { IsPC: true } pc) w.Output.SendFeedback(pc, text, FeedbackKind.SpellResistance);
        if (other is Creature { IsPC: true } opc && opc != target) w.Output.SendFeedback(opc, text, FeedbackKind.SpellResistance);
    }
}
