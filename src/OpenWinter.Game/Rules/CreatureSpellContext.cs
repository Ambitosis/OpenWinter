using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>Spell save DCs, damage bookkeeping and creature size.</summary>
public static partial class CreatureRules
{
    /// <summary>Spell school letter (spells.2da School) to Spell Focus, Greater and Epic Spell Focus feats.</summary>
    static readonly Dictionary<string, (int Focus, int Greater, int Epic)> SchoolFocus = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = (35, 393, 610),
        ["C"] = (166, 394, 611),
        ["D"] = (167, 395, 612),
        ["E"] = (168, 396, 613),
        ["V"] = (169, 397, 614),
        ["I"] = (170, 398, 615),
        ["N"] = (171, 399, 616),
        ["T"] = (172, 400, 617),
    };

    /// <summary>Spell Focus bonus to a spell's save DC for a caster (best of focus / greater / epic).</summary>
    public static int SpellFocusBonus(World w, Creature c, int spell)
    {
        var school = w.Resources.TwoDA("spells").Get(spell, "School");
        if (!SchoolFocus.TryGetValue(school, out var f)) return 0;
        if (HasFeat(w, c, f.Epic, ignoreUses: true)) return Ruleset.Int(w, "FEAT_EPIC_SPELL_FOCUS_BONUS", 6);
        if (HasFeat(w, c, f.Greater, ignoreUses: true)) return Ruleset.Int(w, "FEAT_GREATER_SPELL_FOCUS_BONUS", 4);
        if (HasFeat(w, c, f.Focus, ignoreUses: true)) return Ruleset.Int(w, "FEAT_SPELL_FOCUS_BONUS", 2);
        return 0;
    }

    /// <summary>
    /// GetSpellSaveDC: 10 + spell level (for the casting class, else innate) + casting ability modifier
    /// + spell focus, for the spell the object is casting (<see cref="GameObject.LastSpell"/>).
    /// Items and areas of effect carry an explicit DC.
    /// </summary>
    public static int SpellSaveDC(World w, GameObject caster)
    {
        var info = caster.LastSpell;
        if (info == null) return 10;
        if (info.SaveDC >= 0) return info.SaveDC;
        var spells = w.Resources.TwoDA("spells");
        int innate = spells.GetInt(info.SpellId, "Innate");
        if (caster is not Creature c) return 10 + innate;
        int level = info.DomainLevel > 0 ? info.DomainLevel
            : info.ClassType != NwRules.ClassInvalid ? SpellLevelForClass(w, info.ClassType, info.SpellId) : -1;
        if (level < 0) level = innate;
        int ability = info.ClassType != NwRules.ClassInvalid
            ? NwRules.AbilityFromLabel(w.Resources.TwoDA("classes").Get(info.ClassType, "SpellcastingAbil"))
            : NwRules.AbilityCha;
        int mod = ability >= 0 ? AbilityModifier(w, c, ability) : 0;
        return 10 + level + mod + SpellFocusBonus(w, c, info.SpellId)
            + Nwnx.Plugins.RulesModifiers.SpellSaveDCBonus(w, c, info.SpellId);
    }

    /// <summary>
    /// Records a hit for GetLastDamager / GetDamageDealtByType / GetTotalDamageDealt. Damage code
    /// (effects, combat) calls this before running the OnDamaged event.
    /// </summary>
    public static void RecordDamage(GameObject target, GameObject? damager, IReadOnlyDictionary<int, int> damageByType)
    {
        target.LastDamager = damager?.Id ?? GameObject.InvalidId;
        if (target is Creature hurt && damageByType.Values.Any(v => v > 0)) hurt.DetectAlertPending = true;
        target.LastDamageByType.Clear();
        foreach (var (type, amount) in damageByType) target.LastDamageByType[type] = amount;
    }

    /// <summary>GetCreatureSize: the creature's size, from its appearance when the template doesn't say.</summary>
    public static int CreatureSize(World w, Creature c)
    {
        EnsureInit(w, c);
        return c.Size;
    }

    /// <summary>Size category of an appearance (appearance.2da SIZECATEGORY).</summary>
    public static int AppearanceSize(World w, int appearance) =>
        w.Resources.TwoDA("appearance").GetInt(appearance, "SIZECATEGORY", 3);
}
