using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>One reason a character failed Enforce Legal Characters or Item Level Restrictions, as NWNX_ELC reports it.</summary>
public sealed class ElcFailure
{
    public int Type, SubType, StrRef;
    public int Level = -1, Skill = -1, Feat = -1, Spell = -1;
    public uint Item = GameObject.InvalidId;
    public string Reason = "";
    public bool Skipped;
}

/// <summary>
/// NWNX_ELC: character validation (Enforce Legal Characters and Item Level Restrictions) with a script that hears each
/// failure and may let it pass, an optional custom check, and the NWNX_ON_ELC_VALIDATE_CHARACTER_BEFORE/AFTER events.
/// Options come from the plugin's environment variables (NWNX_ELC_ELC_SCRIPT, NWNX_ELC_CUSTOM_ELC_CHECK,
/// NWNX_ELC_ENFORCE_DEFAULT_EVENT_SCRIPTS, NWNX_ELC_ENFORCE_EMPTY_DIALOG_RESREF,
/// NWNX_ELC_ENFORCE_CASTER_PRIMARY_STAT_IS_11). Without a script it behaves like the plain check.
/// </summary>
public static class ElcPlugin
{
    const string P = "ELC";
    const string FailureKey = "nwnx:elc:failure";

    public const int TypeNone = 0, TypeCharacter = 1, TypeItem = 2, TypeSkill = 3, TypeFeat = 4, TypeSpell = 5, TypeCustom = 6;
    public const int SubNone = 0, SubServerLevel = 1, SubLevelHack = 2, SubColoredName = 3, SubUnidentifiedItem = 4, SubMinEquipLevel = 5,
        SubNonPc = 6, SubDm = 7, SubNonPlayerRace = 8, SubNonPlayerClass = 9, SubClassLevel = 10, SubPrestige = 11, SubClassAlignment = 12,
        SubStartingAbility = 13, SubPointBuy = 14, SubCasterPrimaryStat = 15, SubTooManyHitPoints = 17, SubUnusableSkill = 18,
        SubNotEnoughSkillPoints = 19, SubClassSkillRanks = 20, SubCrossClassSkillRanks = 21, SubInvalidFeat = 23, SubFeatSpellLevel = 24,
        SubFeatBab = 25, SubFeatAbility = 26, SubFeatSkill = 27, SubFeatFeat = 28, SubTooManyFeats = 29, SubInvalidSpell = 36,
        SubSpellList = 47, SubSkillList = 48, SubFeatList = 49, SubNumMulticlass = 52;

    /// <summary>The message a player gets when validation fails without a more specific one (dialog.tlk "Invalid character - player login refused.").</summary>
    public const int DefaultStrRef = 66168;

    /// <summary>The dialog.tlk message that names a failure subtype, else <see cref="DefaultStrRef"/>.</summary>
    public static int StrRefFor(int subType) => subType switch
    {
        SubServerLevel => 57924,
        SubMinEquipLevel => 68521,
        SubNonPlayerRace => 66166,
        SubNonPlayerClass => 66167,
        SubTooManyHitPoints => 3109,
        SubUnusableSkill => 63815,
        SubNotEnoughSkillPoints => 66155,
        SubClassSkillRanks or SubCrossClassSkillRanks or SubSkillList => 66165,
        SubInvalidFeat or SubFeatList => 76383,
        SubFeatSpellLevel => 66176,
        SubFeatAbility => 66175,
        SubFeatSkill => 66183,
        SubFeatFeat or SubFeatBab => 66182,
        SubTooManyFeats => 66222,
        SubInvalidSpell or SubSpellList => 66499,
        _ => DefaultStrRef,
    };

    public sealed class Options
    {
        public string Script = Env("NWNX_ELC_ELC_SCRIPT");
        public bool CustomCheck = Flag("NWNX_ELC_CUSTOM_ELC_CHECK");
        public bool EnforceDefaultEventScripts = Flag("NWNX_ELC_ENFORCE_DEFAULT_EVENT_SCRIPTS");
        public bool EnforceEmptyDialog = Flag("NWNX_ELC_ENFORCE_EMPTY_DIALOG_RESREF");
        public bool EnforceCasterPrimaryStat = Flag("NWNX_ELC_ENFORCE_CASTER_PRIMARY_STAT_IS_11");

        static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";
        static bool Flag(string name) => Env(name).Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on";
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, Options> _state = new();
    public static Options OptionsOf(World world) => _state.GetValue(world.System<NwnxRegistry>(), _ => new Options());

    [NwnxFunc(P, "SetELCScript")] static void SetScript(NwnxCall n) => OptionsOf(n.World).Script = n.PopString();
    [NwnxFunc(P, "EnableCustomELCCheck")] static void EnableCustom(NwnxCall n) => OptionsOf(n.World).CustomCheck = n.PopInt() != 0;

    static readonly string[] CreatureScriptFields =
    {
        "ScriptHeartbeat", "ScriptOnNotice", "ScriptSpellAt", "ScriptAttacked", "ScriptDamaged", "ScriptDisturbed", "ScriptEndRound",
        "ScriptDialogue", "ScriptSpawn", "ScriptRested", "ScriptDeath", "ScriptUserDefine", "ScriptOnBlocked",
    };

    static ElcFailure? Current(NwnxCall n) => n.Host?.CurrentEvent.Values.GetValueOrDefault(FailureKey) as ElcFailure;

    [NwnxFunc(P, "SkipValidationFailure")] static void Skip(NwnxCall n) { if (Current(n) is { } f) f.Skipped = true; }
    [NwnxFunc(P, "GetValidationFailureType")] static void FailureType(NwnxCall n) => n.Push(Current(n)?.Type ?? TypeNone);
    [NwnxFunc(P, "GetValidationFailureSubType")] static void FailureSubType(NwnxCall n) => n.Push(Current(n)?.SubType ?? SubNone);
    [NwnxFunc(P, "GetValidationFailureMessageStrRef")] static void FailureStrRef(NwnxCall n) => n.Push(Current(n)?.StrRef ?? 0);
    [NwnxFunc(P, "SetValidationFailureMessageStrRef")]
    static void SetFailureStrRef(NwnxCall n)
    {
        int strref = n.PopInt();
        if (strref > 0 && Current(n) is { } f) f.StrRef = strref;
    }
    [NwnxFunc(P, "GetValidationFailureItem")]
    static void FailureItem(NwnxCall n) => n.PushObject(Current(n) is { Type: TypeItem } f ? f.Item : GameObject.InvalidId);
    [NwnxFunc(P, "GetValidationFailureLevel")] static void FailureLevel(NwnxCall n) => n.Push(Current(n)?.Level ?? -1);
    [NwnxFunc(P, "GetValidationFailureSkillID")] static void FailureSkill(NwnxCall n) => n.Push(Current(n) is { Type: TypeSkill } f ? f.Skill : -1);
    [NwnxFunc(P, "GetValidationFailureFeatID")] static void FailureFeat(NwnxCall n) => n.Push(Current(n) is { Type: TypeFeat } f ? f.Feat : -1);
    [NwnxFunc(P, "GetValidationFailureSpellID")] static void FailureSpell(NwnxCall n) => n.Push(Current(n) is { Type: TypeSpell } f ? f.Spell : -1);

    /// <summary>A rule error from the legal character check, as an NWNX_ELC failure type and subtype.</summary>
    public static ElcFailure FromRuleError(RuleError e)
    {
        string m = e.Message;
        bool Has(string text) => m.Contains(text, StringComparison.OrdinalIgnoreCase);
        var (type, sub) = e.Code switch
        {
            "race" => (TypeCharacter, SubNonPlayerRace),
            "class" when Has("not available to players") => (TypeCharacter, SubNonPlayerClass),
            "class" when Has("maximum level") => (TypeCharacter, SubServerLevel),
            "class" when Has("prestige") => (TypeCharacter, SubPrestige),
            "class" when Has("at most") => (TypeCharacter, SubNumMulticlass),
            "class" when Has("maximum of") => (TypeCharacter, SubClassLevel),
            "class" or "history" => (TypeCharacter, SubLevelHack),
            "alignment" => (TypeCharacter, SubClassAlignment),
            "prereq" => (TypeCharacter, SubPrestige),
            "abilities" or "ability" => (TypeCharacter, Has("point") ? SubPointBuy : SubStartingAbility),
            "hitpoints" => (TypeCharacter, SubTooManyHitPoints),
            "skills" when Has("level history") => (TypeSkill, SubSkillList),
            "skills" when Has("point") => (TypeSkill, SubNotEnoughSkillPoints),
            "skills" when Has("cross-class") => (TypeSkill, SubCrossClassSkillRanks),
            "skills" when Has("rank") => (TypeSkill, SubClassSkillRanks),
            "skills" => (TypeSkill, SubUnusableSkill),
            "feats" when Has("levelling") => (TypeFeat, SubFeatList),
            "feats" when Has("too many") || Has("more feats") => (TypeFeat, SubTooManyFeats),
            "feats" => (TypeFeat, SubInvalidFeat),
            "feat-prereq" when Has("base attack") => (TypeFeat, SubFeatBab),
            "feat-prereq" when Has("ranks in") || Has("access to") => (TypeFeat, SubFeatSkill),
            "feat-prereq" when Has("cast level") => (TypeFeat, SubFeatSpellLevel),
            "feat-prereq" when Has("STR ") || Has("DEX ") || Has("CON ") || Has("INT ") || Has("WIS ") || Has("CHA ") => (TypeFeat, SubFeatAbility),
            "feat-prereq" => (TypeFeat, SubFeatFeat),
            "spells" when Has("levelling") => (TypeSpell, SubSpellList),
            "spells" => (TypeSpell, SubInvalidSpell),
            _ => (TypeCharacter, SubNone),
        };
        return new ElcFailure { Type = type, SubType = sub, StrRef = StrRefFor(sub), Level = e.Level, Feat = e.Feat, Skill = e.Skill, Spell = e.Spell, Reason = m };
    }

    /// <summary>Character checks NWNX_ELC adds to the rule check: a coloured name, the caster primary stat option.</summary>
    static IEnumerable<ElcFailure> CharacterChecks(World world, Creature pc, Options o)
    {
        string name = pc.Name.Resolve(world.Resources.Talk);
        if (name.Contains("<c", StringComparison.Ordinal) || name.Contains("</c>", StringComparison.Ordinal))
            yield return new ElcFailure { Type = TypeCharacter, SubType = SubColoredName, StrRef = DefaultStrRef, Reason = "coloured name" };
        if (o.EnforceCasterPrimaryStat && pc.Classes.Count > 0)
        {
            var classes = world.Resources.TwoDA("classes");
            int first = pc.Classes[0].Class;
            int ability = NwRules.AbilityFromLabel(classes.Get(first, "PrimaryAbil"));
            if (classes.GetInt(first, "SpellCaster") != 0 && ability >= 0 && pc.Abilities[ability] < 11)
                yield return new ElcFailure { Type = TypeCharacter, SubType = SubCasterPrimaryStat, StrRef = DefaultStrRef, Level = 1, Reason = "spellcaster primary ability below 11" };
        }
    }

    /// <summary>Item Level Restrictions on the equipped items: unidentified ones, and ones above the character's level.</summary>
    static IEnumerable<ElcFailure> ItemChecks(World world, Creature pc)
    {
        if (!world.ItemLevelRestrictions) yield break;
        foreach (var item in pc.Equipment.Values.Distinct())
        {
            if (!item.Identified)
                yield return new ElcFailure { Type = TypeItem, SubType = SubUnidentifiedItem, StrRef = DefaultStrRef, Item = item.Id, Reason = "unidentified equipped item" };
            else if (!world.CanEquipByLevel(pc, item))
                yield return new ElcFailure { Type = TypeItem, SubType = SubMinEquipLevel, StrRef = StrRefFor(SubMinEquipLevel), Item = item.Id, Reason = "equipped item above the character's level" };
        }
    }

    /// <summary>
    /// Validates a character entering the server: the rule check's errors, NWNX_ELC's own checks and Item Level
    /// Restrictions, each told to the ELC script (OBJECT_SELF the character) which may skip it, then the custom check
    /// when enabled. Returns the failure that stands, or null when the character passes; a passing character then gets
    /// the enforced event scripts and dialog.
    /// </summary>
    public static ElcFailure? Validate(World world, Creature pc, IEnumerable<RuleError> errors)
    {
        var o = OptionsOf(world);
        if (EventsPlugin.Signal(world, "NWNX_ON_ELC_VALIDATE_CHARACTER_BEFORE", pc).Skipped) return null;
        var failures = errors.Select(FromRuleError).Concat(CharacterChecks(world, pc, o)).Concat(ItemChecks(world, pc)).ToList();
        if (o.CustomCheck && o.Script.Length > 0)
            failures.Add(new ElcFailure { Type = TypeCustom, SubType = SubNone, StrRef = DefaultStrRef, Reason = "custom check" });
        foreach (var f in failures)
        {
            if (o.Script.Length > 0 && world.Scripts != null)
                world.Scripts.Run(o.Script, pc.Id, new EventContext().Set(FailureKey, f));
            if (!f.Skipped) return f;
        }
        if (o.EnforceDefaultEventScripts)
            foreach (var key in CreatureScriptFields) pc.Scripts[key] = "default";
        if (o.EnforceEmptyDialog) pc.Conversation = "";
        EventsPlugin.Signal(world, "NWNX_ON_ELC_VALIDATE_CHARACTER_AFTER", pc);
        return null;
    }
}
