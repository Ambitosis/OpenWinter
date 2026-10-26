using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>Saving throw rolls, immunities and skill checks. Spells and traps reuse these.</summary>
public static partial class CreatureRules
{
    public const int SaveFailed = 0, SaveSucceeded = 1, SaveImmune = 2;

    /// <summary>The immunity that makes a save of this SAVING_THROW_TYPE_* automatic (IMMUNITY_TYPE_*), or 0.</summary>
    public static int ImmunityForSaveType(int saveType) => saveType switch
    {
        NwRules.SaveTypeMind => NwRules.ImmunityMind,
        NwRules.SaveTypePoison => NwRules.ImmunityPoison,
        NwRules.SaveTypeDisease => NwRules.ImmunityDisease,
        NwRules.SaveTypeFear => NwRules.ImmunityFear,
        NwRules.SaveTypeTrap => NwRules.ImmunityTrap,
        NwRules.SaveTypeParalysis => NwRules.ImmunityParalysis,
        // Death immunity doesn't make death saves automatic: spell scripts check it themselves.
        _ => NwRules.ImmunityNone,
    };

    /// <summary>iprp_immunity.2da row to the IMMUNITY_TYPE_* values it grants.</summary>
    static readonly int[][] ItemImmunities =
    {
        new[] { NwRules.ImmunitySneakAttack },
        new[] { NwRules.ImmunityNegativeLevel, NwRules.ImmunityAbilityDecrease },
        new[] { NwRules.ImmunityMind },
        new[] { NwRules.ImmunityPoison },
        new[] { NwRules.ImmunityDisease },
        new[] { NwRules.ImmunityFear },
        new[] { NwRules.ImmunityKnockdown },
        new[] { NwRules.ImmunityParalysis },
        new[] { NwRules.ImmunityCriticalHit },
        new[] { NwRules.ImmunityDeath },
    };

    /// <summary>Immunities from class and racial feats.</summary>
    static readonly (int Feat, int Immunity)[] FeatImmunities =
    {
        (NwRules.FeatImmunityToSleep, NwRules.ImmunitySleep),
        (NwRules.FeatPurityOfBody, NwRules.ImmunityDisease),
        (NwRules.FeatDivineHealth, NwRules.ImmunityDisease),
        (NwRules.FeatDiamondBody, NwRules.ImmunityPoison),
        (NwRules.FeatVenomImmunity, NwRules.ImmunityPoison),
        (NwRules.FeatAuraOfCourage, NwRules.ImmunityFear),
    };

    /// <summary>GetIsImmune: immunity effects, item properties (immunity, mind blank, freedom) and feats.</summary>
    public static bool IsImmune(World w, Creature c, int immunity, GameObject? versus = null)
    {
        bool immune = IsImmuneByRules(w, c, immunity, versus);
        if (c.Overrides == null && (versus as Creature)?.Overrides == null) return immune;
        return BypassImmunity(w, c, immunity, versus as Creature, immune);
    }

    /// <summary>
    /// A chance to bypass an immunity (NWNX_Creature_SetBypassEffectImmunity): the source's outgoing chance and the
    /// target's incoming one each roll; positive makes the target not immune, negative immune. Outcomes that disagree
    /// cancel out and the immunity stands as the rules have it.
    /// </summary>
    internal static bool BypassImmunity(World w, Creature c, int immunity, Creature? versus, bool immune)
    {
        static int? Chance(CreatureOverrides? o, int key, int all) =>
            o == null ? null : o.BypassImmunity.TryGetValue(key, out int v) || o.BypassImmunity.TryGetValue(all, out v) ? v : null;
        var dice = w.System<Combat.CombatSystem>().Dice;
        bool? Roll(int? chance) => chance is int ch && ch != 0 && dice.Roll(100) <= Math.Abs(ch) ? ch < 0 : null;
        var outgoing = Roll(Chance(versus?.Overrides, immunity, 255));
        var incoming = Roll(Chance(c.Overrides, -immunity, -255));
        if (outgoing is bool a && incoming is bool b) return a == b ? a : immune;
        return outgoing ?? incoming ?? immune;
    }

    internal static bool IsImmuneByRules(World w, Creature c, int immunity, GameObject? versus)
    {
        if (immunity <= NwRules.ImmunityNone) return false;
        // Immunities versus a race or alignment only count against a matching object.
        foreach (var e in Effects(c))
            if (e.Type == NwRules.EffectImmunity && e.Ints[0] == immunity && EffectQueries.AppliesVersus(e, versus)) return true;
        foreach (var (_, _, p) in EquippedProperties(c))
        {
            switch (p.Type)
            {
                case NwRules.IpImmunityMisc when p.SubType >= 0 && p.SubType < ItemImmunities.Length && ItemImmunities[p.SubType].Contains(immunity):
                case NwRules.IpMindBlank when immunity == NwRules.ImmunityMind:
                case NwRules.IpFreedomOfMovement when immunity is NwRules.ImmunityParalysis or NwRules.ImmunityEntangle or NwRules.ImmunitySlow or NwRules.ImmunityMovementSpeedDecrease:
                    return true;
            }
        }
        foreach (var (feat, imm) in FeatImmunities)
            if (imm == immunity && HasFeat(w, c, feat, ignoreUses: true)) return true;
        return false;
    }

    static readonly string[] SaveNames = { "", "Fortitude", "Reflex", "Will" };
    static readonly string[] SaveTypeNames =
    {
        "", "Mind Affecting", "Poison", "Disease", "Fear", "Sonic", "Acid", "Fire", "Electricity", "Positive", "Negative",
        "Death", "Cold", "Divine", "Traps", "Spells", "Good", "Evil", "Law", "Chaos", "Paralysis",
    };

    /// <summary>
    /// Rolls a saving throw (FortitudeSave/ReflexSave/WillSave): 2 if immune to the save type,
    /// else d20 + save vs DC with natural 1 failing and natural 20 succeeding. Players involved
    /// get the roll in their combat log.
    /// </summary>
    public static int RollSave(World w, GameObject target, int save, int dc, int saveType = NwRules.SaveTypeAll, GameObject? versus = null, Func<int, int>? random = null)
    {
        if (save < 1 || save > 3) return SaveFailed;
        string label = SaveNames[save] + " Save" + (saveType > 0 && saveType < SaveTypeNames.Length ? " vs. " + SaveTypeNames[saveType] : "");
        if (target is Creature c && ImmunityForSaveType(saveType) is int imm && imm != 0 && IsImmune(w, c, imm, versus))
        {
            Feedback(w, target, versus, $"{Name(w, target)} : {label} : *immune*");
            return SaveImmune;
        }
        dc = Math.Clamp(dc, 1, 255);
        int bonus = SavingThrow(w, target, save, saveType, versus);
        int roll = (random ?? (n => Random.Shared.Next(n)))(20) + 1;
        bool ok = roll == 20 || (roll != 1 && roll + bonus >= dc);
        Feedback(w, target, versus, $"{Name(w, target)} : {label} : *{(ok ? "success" : "failure")}* : ({roll} + {bonus} = {roll + bonus} vs. DC: {dc})");
        return ok ? SaveSucceeded : SaveFailed;
    }

    /// <summary>GetIsSkillSuccessful: d20 + skill rank &gt;= DC (always fails if the creature can't use the skill).</summary>
    public static bool SkillCheck(World w, Creature c, int skill, int dc, Func<int, int>? random = null)
    {
        int rank = SkillRank(w, c, skill);
        if (rank == -1 && !HasSkill(w, c, skill)) return false;
        int roll = (random ?? (n => Random.Shared.Next(n)))(20) + 1;
        bool ok = roll + rank >= dc;
        var skills = w.Resources.TwoDA("skills");
        string name = uint.TryParse(skills.Get(skill, "Name"), out var strref) ? w.Resources.TlkString(strref) : skills.Get(skill, "Label");
        if (name.Length == 0) name = skills.Get(skill, "Label");
        Feedback(w, c, null, $"{Name(w, c)} : {name} : *{(ok ? "success" : "failure")}* : ({roll} + {rank} = {roll + rank} vs. DC: {dc})");
        return ok;
    }

    static string Name(World w, GameObject o) => o.Name.Resolve(w.Resources.Talk);

    static void Feedback(World w, GameObject target, GameObject? versus, string text)
    {
        if (w.Output == null) return;
        if (target is Creature { IsPC: true } pc) w.Output.SendMessage(pc, text);
        if (versus is Creature { IsPC: true } other && other != target) w.Output.SendMessage(other, text);
    }
}
