using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>
/// Character creation and level-up rules, read entirely from the module's 2DAs (haks first):
/// classes.2da (PlayerClass, prerequisites, alignment restrictions, tables), racialtypes.2da
/// (point buy, extra feats and skill points), cls_feat_* / cls_bfeat_* / cls_skill_* /
/// cls_spkn_* / cls_spgn_* / cls_pres_* / cls_stat_*, feat.2da prerequisites, skills.2da,
/// spells.2da, domains.2da, spellschools.2da and the CHARGEN_* rows of ruleset.2da.
/// Everything here is pure: it reads a creature and returns answers without changing it.
/// </summary>
public static partial class ProgressionRules
{
    public const int MaxClasses = 3;
    static readonly string[] AbilityColumns = { "Str", "Dex", "Con", "Int", "Wis", "Cha" };
    static readonly string[] FeatAbilityColumns = { "MINSTR", "MINDEX", "MINCON", "MININT", "MINWIS", "MINCHA" };

    static TwoDA Classes(World w) => w.Resources.TwoDA("classes");
    static TwoDA Races(World w) => w.Resources.TwoDA("racialtypes");
    static TwoDA FeatTable(World w) => w.Resources.TwoDA("feat");
    static TwoDA SkillTable(World w) => w.Resources.TwoDA("skills");

    static TwoDA Sub(World w, string name) => name.Length == 0 ? TwoDA.Empty : w.Resources.TwoDA(name.ToLowerInvariant());

    // ------------------------------------------------------------------ classes

    public static bool ClassExists(World w, int cls) => cls >= 0 && cls < Classes(w).RowCount && Classes(w).Get(cls, "Label").Length > 0 && Classes(w).Get(cls, "Name").Length > 0;
    public static bool IsPlayerClass(World w, int cls) => ClassExists(w, cls) && Classes(w).GetInt(cls, "PlayerClass") == 1;
    /// <summary>A class with a prerequisite table is a prestige class: never available at first level.</summary>
    public static bool IsPrestige(World w, int cls) => Classes(w).Get(cls, "PreReqTable").Length > 0;
    public static int HitDie(World w, int cls) => Math.Max(1, Classes(w).GetInt(cls, "HitDie", 8));

    /// <summary>
    /// classes.2da AlignRestrict (0x01 neutral, 0x02 lawful, 0x04 chaotic, 0x08 good, 0x10 evil),
    /// AlignRstrctType (0x1 law/chaos axis, 0x2 good/evil axis) and InvertRestrict (the flags list
    /// the allowed alignments instead of the forbidden ones).
    /// </summary>
    public static bool AlignmentAllowed(World w, int cls, int lawChaos, int goodEvil)
    {
        var t = Classes(w);
        int restrict = t.GetInt(cls, "AlignRestrict"), type = t.GetInt(cls, "AlignRstrctType");
        if (restrict == 0 || type == 0) return true;
        bool invert = t.GetInt(cls, "InvertRestrict") != 0;
        int lc = CreatureRules.LawChaos(lawChaos), ge = CreatureRules.GoodEvil(goodEvil);
        int lcFlag = lc == NwRules.AlignLawful ? 0x02 : lc == NwRules.AlignChaotic ? 0x04 : 0x01;
        int geFlag = ge == NwRules.AlignGood ? 0x08 : ge == NwRules.AlignEvil ? 0x10 : 0x01;
        bool match = ((type & 1) != 0 && (restrict & lcFlag) != 0) || ((type & 2) != 0 && (restrict & geFlag) != 0);
        return invert ? match : !match;
    }

    /// <summary>
    /// Why a creature can't take its next level in a class (empty when it can). Players may only take
    /// classes.2da PlayerClass rows; first-level characters only base classes.
    /// </summary>
    public static List<RuleError> ClassErrors(World w, Creature c, int cls, bool player, bool checkVariables = true)
    {
        var errors = new List<RuleError>();
        if (!ClassExists(w, cls)) { errors.Add(new("class", "That class does not exist.")); return errors; }
        string name = ClassName(w, cls);
        if (player && !IsPlayerClass(w, cls)) errors.Add(new("class", $"{name} is not available to players."));
        int total = c.TotalLevel, current = CreatureRules.ClassLevel(c, cls);
        if (total >= CreatureRules.MaxLevel) errors.Add(new("class", "The maximum level has been reached."));
        if (total == 0 && IsPrestige(w, cls)) errors.Add(new("class", $"{name} is a prestige class and cannot be taken at first level."));
        if (current == 0 && c.Classes.Count >= MaxClasses) errors.Add(new("class", $"A character can have at most {MaxClasses} classes."));
        int max = Classes(w).GetInt(cls, "MaxLevel");
        if (max > 0 && current >= max) errors.Add(new("class", $"{name} has a maximum of {max} levels."));
        if (!AlignmentAllowed(w, cls, c.LawfulChaotic, c.GoodEvil)) errors.Add(new("alignment", $"Your alignment does not allow {name} levels."));
        if (total > 0 || !IsPrestige(w, cls))
            foreach (var e in PrerequisiteErrors(w, c, cls, checkVariables)) errors.Add(e);
        return errors;
    }

    /// <summary>
    /// cls_pres_* prerequisites: FEAT, FEATOR (any one), SKILL (ranks), RACE (any one), BAB, VAR
    /// (local int on the character equals the value; modules set e.g. X1_AllowShadow to bar a class),
    /// ARCSPELL / SPELL (highest castable arcane / any spell level), CLASSOR (any one), CLASSNOT, and
    /// EE's SAVE (ReqParam1 1 Fortitude, 2 Reflex, 3 Will; ReqParam2 the least class save plus feat save
    /// bonuses, without ability modifiers or effects; other save types are met). Measured on nwserver
    /// (parity suite prog_prereq); ALIGN rows block nothing there (ReqParam1 1, ReqParam2 0 under any
    /// alignment), so they are met here too.
    /// </summary>
    public static List<RuleError> PrerequisiteErrors(World w, Creature c, int cls, bool checkVariables = true)
    {
        var errors = new List<RuleError>();
        var t = Sub(w, Classes(w).Get(cls, "PreReqTable"));
        if (t.RowCount == 0) return errors;
        var groups = new Dictionary<string, (bool Met, List<string> Names)>();
        for (int r = 0; r < t.RowCount; r++)
        {
            string type = t.Get(r, "ReqType").ToUpperInvariant();
            string p1 = t.Get(r, "ReqParam1"), p2 = t.Get(r, "ReqParam2");
            int i1 = t.GetInt(r, "ReqParam1", -1), i2 = t.GetInt(r, "ReqParam2", 0);
            switch (type)
            {
                case "FEAT":
                    if (!c.Feats.Contains(i1)) errors.Add(new("prereq", $"Requires the feat {FeatName(w, i1)}."));
                    break;
                case "SKILL":
                    if (CreatureRules.SkillRankBase(c, i1) < i2) errors.Add(new("prereq", $"Requires {i2} ranks in {SkillName(w, i1)}."));
                    break;
                case "BAB":
                    if (CreatureRules.BaseAttackBonus(w, c) < i1) errors.Add(new("prereq", $"Requires a base attack bonus of +{i1}."));
                    break;
                case "VAR":
                    if (checkVariables && c.Locals.Ints.GetValueOrDefault(p1) != i2) errors.Add(new("prereq", $"This class is not allowed here ({p1})."));
                    break;
                case "ARCSPELL":
                    if (HighestSpellLevel(w, c, arcaneOnly: true) < i1) errors.Add(new("prereq", $"Requires the ability to cast level {i1} arcane spells."));
                    break;
                case "SPELL":
                    if (HighestSpellLevel(w, c, arcaneOnly: false) < i1) errors.Add(new("prereq", $"Requires the ability to cast level {i1} spells."));
                    break;
                case "CLASSNOT":
                    if (CreatureRules.ClassLevel(c, i1) > 0) errors.Add(new("prereq", $"Cannot be taken by a {ClassName(w, i1)}."));
                    break;
                case "FEATOR":
                case "RACE":
                case "CLASSOR":
                {
                    bool met = type switch
                    {
                        "FEATOR" => c.Feats.Contains(i1),
                        "RACE" => c.Race == i1,
                        _ => CreatureRules.ClassLevel(c, i1) > 0,
                    };
                    string what = type switch { "FEATOR" => FeatName(w, i1), "RACE" => RaceName(w, i1), _ => ClassName(w, i1) };
                    var g = groups.TryGetValue(type, out var found) ? found : (Met: false, Names: new List<string>());
                    g.Names.Add(what);
                    groups[type] = (g.Met || met, g.Names);
                    break;
                }
                case "SAVE":
                    if (i1 is >= 1 and <= 3 && CreatureRules.PrerequisiteSave(w, c, i1) < i2)
                        errors.Add(new("prereq", $"Requires a base {SaveName(i1)} save of {i2}."));
                    break;
                // ALIGN: met (prog_prereq); any other type: ignored.
            }
        }
        foreach (var (type, g) in groups)
            if (!g.Met)
                errors.Add(new("prereq", type switch
                {
                    "FEATOR" => $"Requires one of the feats: {string.Join(", ", g.Names.Take(6))}{(g.Names.Count > 6 ? ", ..." : "")}.",
                    "RACE" => $"Requires the race: {string.Join(" or ", g.Names)}.",
                    _ => $"Requires a level in: {string.Join(" or ", g.Names)}.",
                }));
        return errors;
    }

    static string SaveName(int save) => save switch { 1 => "Fortitude", 2 => "Reflex", _ => "Will" };

    /// <summary>The highest spell level the creature can cast (has a slot for), or -1.</summary>
    public static int HighestSpellLevel(World w, Creature c, bool arcaneOnly)
    {
        int best = -1;
        foreach (var cc in c.Classes)
        {
            if (!CreatureRules.IsSpellCaster(w, cc.Class)) continue;
            if (arcaneOnly && Classes(w).GetInt(cc.Class, "Arcane") == 0) continue;
            for (int lvl = 9; lvl > best; lvl--)
                if (CreatureRules.SpellSlots(w, c, cc.Class, lvl) > 0 || (lvl == 0 && CasterTableValue(w, c, cc.Class, 0) >= 0)) { best = lvl; break; }
        }
        return best;
    }

    static int CasterTableValue(World w, Creature c, int cls, int level)
    {
        var t = CreatureRules.ClassTable(w, cls, "SpellGainTable");
        int cl = Math.Min(CreatureRules.CasterLevel(w, c, cls), t.RowCount);
        if (cl <= 0) return -1;
        var raw = t.Get(cl - 1, $"SpellLevel{level}");
        return raw.Length == 0 ? -1 : t.GetInt(cl - 1, $"SpellLevel{level}");
    }

    // ------------------------------------------------------------------ abilities

    public static int PointBuyBudget(World w, int race) => Races(w).GetInt(race, "AbilitiesPointBuyNumber", 30);
    public static int AbilityMin(World w) => Ruleset.Int(w, "CHARGEN_BASE_ABILITY_MIN", 8);
    public static int AbilityMax(World w) => Ruleset.Int(w, "CHARGEN_BASE_ABILITY_MAX", 18);

    /// <summary>Point-buy cost of one base score: 1 per point up to CHARGEN_ABILITY_COST_INCREMENT2, then 2, then 3 (ruleset.2da).</summary>
    public static int PointBuyCost(World w, int score)
    {
        int min = AbilityMin(w);
        int inc2 = Ruleset.Int(w, "CHARGEN_ABILITY_COST_INCREMENT2", 14);
        int inc3 = Ruleset.Int(w, "CHARGEN_ABILITY_COST_INCREMENT3", 16);
        int inc4 = Ruleset.Int(w, "CHARGEN_ABILITY_COST_INCREMENT4", 255);
        int cost = 0;
        for (int s = min; s < score; s++) cost += s < inc2 ? 1 : s < inc3 ? 2 : s < inc4 ? 3 : 4;
        return cost;
    }

    public static int PointBuyTotal(World w, int[] scores) => scores.Sum(s => PointBuyCost(w, s));

    public static int RacialAdjustment(World w, int race, int ability) => Races(w).GetInt(race, AbilityColumns[ability] + "Adjust");

    // ------------------------------------------------------------------ skills

    /// <summary>A class's skills table (cls_skill_*): skill → class skill.</summary>
    public static IReadOnlyDictionary<int, bool> ClassSkills(World w, int cls) =>
        SkillCache.GetValue(Sub(w, Classes(w).Get(cls, "SkillsTable")), t =>
        {
            var map = new Dictionary<int, bool>();
            for (int r = 0; r < t.RowCount; r++)
            {
                int s = t.GetInt(r, "SkillIndex", -1);
                if (s < 0) continue;
                map[s] = map.GetValueOrDefault(s) || t.GetInt(r, "ClassSkill") == 1;
            }
            return map;
        });
    static readonly ConditionalWeakTable<TwoDA, IReadOnlyDictionary<int, bool>> SkillCache = new();

    public static bool SkillExists(World w, int skill) => skill >= 0 && skill < SkillTable(w).RowCount && SkillTable(w).Get(skill, "Label").Length > 0 && SkillTable(w).Get(skill, "Name").Length > 0;

    /// <summary>Ranks can be bought in a skill when levelling a class: skills.2da AllClassesCanUse, or the class's skill table lists it.</summary>
    public static bool CanBuySkill(World w, int cls, int skill) =>
        SkillExists(w, skill) && (SkillTable(w).GetInt(skill, "AllClassesCanUse") == 1 || ClassSkills(w, cls).ContainsKey(skill));

    public static bool IsClassSkill(World w, int cls, int skill) => ClassSkills(w, cls).GetValueOrDefault(skill);

    /// <summary>A class skill for any of the creature's classes (sets the level + 3 rank cap instead of half).</summary>
    public static bool IsClassSkillForAny(World w, Creature c, int skill) => c.Classes.Any(cc => IsClassSkill(w, cc.Class, skill));

    public static int MaxRanks(World w, Creature after, int skill)
    {
        int cap = after.TotalLevel + Ruleset.Int(w, "CHARGEN_SKILL_MAX_LEVEL_1_BONUS", 3);
        return IsClassSkillForAny(w, after, skill) ? cap : cap / 2;
    }

    /// <summary>
    /// Skill points gained taking a level in a class: max(1, SkillPointBase + ability modifier) plus the
    /// race's ExtraSkillPointsPerLevel, times FirstLevelSkillPointsMultiplier at first level. The
    /// ability (SkillPointModifierAbility, normally INT) is the score after this level's increase.
    /// </summary>
    public static int SkillPointsGained(World w, Creature after, int cls)
    {
        var races = Races(w);
        int ability = NwRules.AbilityFromLabel(races.Get(after.Race, "SkillPointModifierAbility") is { Length: > 0 } a ? a : "INT");
        if (ability < 0) ability = NwRules.AbilityInt;
        int mod = CreatureRules.Modifier(CreatureRules.AbilityScore(w, after, ability, baseOnly: true));
        int pts = Math.Max(1, Classes(w).GetInt(cls, "SkillPointBase") + mod) + races.GetInt(after.Race, "ExtraSkillPointsPerLevel");
        if (after.TotalLevel == 1) pts *= races.GetInt(after.Race, "FirstLevelSkillPointsMultiplier", 4);
        return Math.Max(0, pts);
    }

    // ------------------------------------------------------------------ feats

    /// <summary>A class's feat table (cls_feat_*): feat → (bit per List value 0..3, GrantedOnLevel or -1).</summary>
    public static IReadOnlyDictionary<int, (int Lists, int Granted)> ClassFeats(World w, int cls) =>
        FeatCache.GetValue(Sub(w, Classes(w).Get(cls, "FeatsTable")), t =>
        {
            var map = new Dictionary<int, (int Lists, int Granted)>();
            for (int r = 0; r < t.RowCount; r++)
            {
                int f = t.GetInt(r, "FeatIndex", -1);
                if (f < 0 || t.Get(r, "List").Length == 0) continue;
                int list = t.GetInt(r, "List"), granted = t.GetInt(r, "GrantedOnLevel", -1);
                var old = map.GetValueOrDefault(f, (0, -1));
                map[f] = (old.Item1 | (1 << Math.Clamp(list, 0, 3)), old.Item2 > 0 ? old.Item2 : granted);
            }
            return map;
        });
    static readonly ConditionalWeakTable<TwoDA, IReadOnlyDictionary<int, (int, int)>> FeatCache = new();

    public static bool FeatExists(World w, int feat) =>
        feat >= 0 && feat < FeatTable(w).RowCount && FeatTable(w).Get(feat, "LABEL").Length > 0 && FeatTable(w).Get(feat, "FEAT").Length > 0;

    /// <summary>General feat slots for a new character level: 1 + ExtraFeatsAtFirstLevel at level 1, then NumberNormalFeatsEveryNthLevel every NormalFeatEveryNthLevel levels.</summary>
    public static int GeneralFeatSlots(World w, int race, int newCharacterLevel)
    {
        var r = Races(w);
        if (newCharacterLevel == 1) return 1 + r.GetInt(race, "ExtraFeatsAtFirstLevel");
        int every = r.GetInt(race, "NormalFeatEveryNthLevel", 3);
        return every > 0 && newCharacterLevel % every == 0 ? r.GetInt(race, "NumberNormalFeatsEveryNthLevel", 1) : 0;
    }

    /// <summary>Class bonus feats at a class level (cls_bfeat_* Bonus).</summary>
    public static int BonusFeatSlots(World w, int cls, int newClassLevel)
    {
        var t = CreatureRules.ClassTable(w, cls, "BonusFeatsTable");
        return newClassLevel >= 1 && newClassLevel <= t.RowCount ? Math.Max(0, t.GetInt(newClassLevel - 1, "Bonus")) : 0;
    }

    /// <summary>
    /// Feats granted automatically: class feats of List 3 at their GrantedOnLevel, racial feats at first level, domain
    /// feats. A GrantedOnLevel on another List is only the class level from which the feat can be picked (parity suite
    /// prog_feats: a wizard 3 who had no feat to pick doesn't have Brew Potion, List 1 level 3).
    /// </summary>
    public static List<int> GrantedFeats(World w, int race, int cls, int newClassLevel, bool firstCharacterLevel, int domain1 = -1, int domain2 = -1)
    {
        var list = new List<int>();
        foreach (var (f, (lists, granted)) in ClassFeats(w, cls))
            if (granted == newClassLevel && (lists & 0b1000) != 0 && FeatExists(w, f)) list.Add(f);
        if (firstCharacterLevel)
        {
            var rt = Sub(w, Races(w).Get(race, "FeatsTable"));
            for (int r = 0; r < rt.RowCount; r++)
                if (rt.GetInt(r, "FeatIndex", -1) is int f and >= 0 && FeatExists(w, f)) list.Add(f);
        }
        var domains = w.Resources.TwoDA("domains");
        foreach (var d in new[] { domain1, domain2 })
            if (d >= 0 && domains.GetInt(d, "GrantedFeat", -1) is int f and >= 0 && FeatExists(w, f)) list.Add(f);
        return list.Distinct().OrderBy(f => f).ToList();
    }

    /// <summary>
    /// A feat can be picked with a general feat slot: ALLCLASSESCANUSE, or List 0/1 in one of the creature's class feat
    /// tables once that class has reached the row's GrantedOnLevel (a first-level wizard isn't offered Brew Potion,
    /// List 1 level 3: menu capture c27; a wizard 4 takes no Craft Wand, level 5, with a general feat: prog_feats).
    /// </summary>
    public static bool IsGeneralFeat(World w, Creature after, int feat) =>
        FeatTable(w).GetInt(feat, "ALLCLASSESCANUSE") == 1
        || after.Classes.Any(cc => ClassFeats(w, cc.Class).GetValueOrDefault(feat, (Lists: 0, Granted: -1)) is var e && (e.Lists & 0b011) != 0 && (e.Granted <= 0 || cc.Level >= e.Granted));

    /// <summary>A feat can be picked with a class bonus feat slot: List 1/2 in the class's feat table, from its GrantedOnLevel.</summary>
    public static bool IsBonusFeat(World w, int cls, int feat, int classLevel = int.MaxValue) =>
        ClassFeats(w, cls).GetValueOrDefault(feat, (Lists: 0, Granted: -1)) is var e && (e.Lists & 0b110) != 0 && (e.Granted <= 0 || classLevel >= e.Granted);

    /// <summary>
    /// feat.2da prerequisites against the creature as it will be after this level: MINATTACKBONUS,
    /// MINSTR..MINCHA (base scores), MINSPELLLVL, PREREQFEAT1/2, OrReqFeat0-4, REQSKILL(2) with
    /// ReqSkillMinRanks(2), MinLevel/MinLevelClass, MaxLevel, MinFortSave, PreReqEpic.
    /// </summary>
    /// <param name="before">The creature before the level, when planning a later level: MINSPELLLVL is checked against
    /// it (a fighter's first wizard level can't take Combat Casting, "ability to cast 1st-level spells": prog_feats).</param>
    public static List<RuleError> FeatPrerequisiteErrors(World w, Creature after, int feat, ICollection<int> have, Creature? before = null)
    {
        var errors = new List<RuleError>();
        var t = FeatTable(w);
        string name = FeatName(w, feat);
        void Fail(string why) => errors.Add(new("feat-prereq", $"{name}: {why}"));
        if (t.Get(feat, "MINATTACKBONUS").Length > 0 && CreatureRules.BaseAttackBonus(w, after) < t.GetInt(feat, "MINATTACKBONUS"))
            Fail($"requires base attack bonus +{t.GetInt(feat, "MINATTACKBONUS")}.");
        for (int a = 0; a < 6; a++)
            if (t.Get(feat, FeatAbilityColumns[a]).Length > 0 && CreatureRules.AbilityScore(w, after, a, baseOnly: true) < t.GetInt(feat, FeatAbilityColumns[a]))
                Fail($"requires {AbilityColumns[a].ToUpperInvariant()} {t.GetInt(feat, FeatAbilityColumns[a])}.");
        if (t.Get(feat, "MINSPELLLVL").Length > 0 && HighestSpellLevel(w, before ?? after, arcaneOnly: false) < t.GetInt(feat, "MINSPELLLVL"))
            Fail($"requires the ability to cast level {t.GetInt(feat, "MINSPELLLVL")} spells.");
        foreach (var col in new[] { "PREREQFEAT1", "PREREQFEAT2" })
            if (t.GetInt(feat, col, -1) is int p and >= 0 && !have.Contains(p)) Fail($"requires {FeatName(w, p)}.");
        var or = Enumerable.Range(0, 5).Select(i => t.GetInt(feat, $"OrReqFeat{i}", -1)).Where(f => f >= 0).ToList();
        if (or.Count > 0 && !or.Any(have.Contains)) Fail($"requires one of {string.Join(", ", or.Select(f => FeatName(w, f)))}.");
        foreach (var (skillCol, rankCol) in new[] { ("REQSKILL", "ReqSkillMinRanks"), ("REQSKILL2", "ReqSkillMinRanks2") })
        {
            int skill = t.GetInt(feat, skillCol, -1);
            if (skill < 0) continue;
            if (t.Get(feat, rankCol).Length > 0)
            {
                if (CreatureRules.SkillRankBase(after, skill) < t.GetInt(feat, rankCol)) Fail($"requires {t.GetInt(feat, rankCol)} ranks in {SkillName(w, skill)}.");
            }
            else if (!after.Classes.Any(cc => CanBuySkill(w, cc.Class, skill))) Fail($"requires access to {SkillName(w, skill)}.");
        }
        if (t.Get(feat, "MinLevel").Length > 0)
        {
            int min = t.GetInt(feat, "MinLevel");
            int cls = t.GetInt(feat, "MinLevelClass", -1);
            if (cls >= 0 ? CreatureRules.ClassLevel(after, cls) < min : after.TotalLevel < min)
                Fail(cls >= 0 ? $"requires {ClassName(w, cls)} level {min}." : $"requires character level {min}.");
        }
        if (t.Get(feat, "MaxLevel").Length > 0 && after.TotalLevel > t.GetInt(feat, "MaxLevel")) Fail($"only available up to level {t.GetInt(feat, "MaxLevel")}.");
        if (t.Get(feat, "MinFortSave").Length > 0 && CreatureRules.BaseSave(w, after, 1) < t.GetInt(feat, "MinFortSave")) Fail($"requires a base Fortitude save of {t.GetInt(feat, "MinFortSave")}.");
        if (t.GetInt(feat, "PreReqEpic") == 1 && after.TotalLevel < 21) Fail("requires an epic level.");
        return errors;
    }

    // ------------------------------------------------------------------ spells

    /// <summary>Spells known count at a class level (cls_spkn_*), 0 if none.</summary>
    public static int KnownCount(World w, int cls, int classLevel, int spellLevel)
    {
        var t = CreatureRules.ClassTable(w, cls, "SpellKnownTable");
        if (classLevel <= 0 || t.RowCount == 0) return 0;
        return Math.Max(0, t.GetInt(Math.Min(classLevel, t.RowCount) - 1, $"SpellLevel{spellLevel}"));
    }

    public static bool HasKnownTable(World w, int cls) => Classes(w).Get(cls, "SpellKnownTable").Length > 0;

    /// <summary>A spellbook class (wizard): memorises spells and is limited to the spells it has learnt.</summary>
    public static bool HasSpellbook(World w, int cls) =>
        !HasKnownTable(w, cls) && CreatureRules.IsSpellCaster(w, cls) && Classes(w).GetInt(cls, "MemorizesSpells") == 1 && Classes(w).GetInt(cls, "SpellbookRestricted") == 1;

    /// <summary>The highest spell level a class's gain table gives at a class level (NumSpellLevels - 1, or the last non-empty column).</summary>
    public static int MaxSpellLevel(World w, int cls, int classLevel)
    {
        var t = CreatureRules.ClassTable(w, cls, "SpellGainTable");
        if (classLevel <= 0 || t.RowCount == 0) return -1;
        int row = Math.Min(classLevel, t.RowCount) - 1;
        if (t.Get(row, "NumSpellLevels").Length > 0) return t.GetInt(row, "NumSpellLevels") - 1;
        for (int l = 9; l >= 0; l--) if (t.Get(row, $"SpellLevel{l}").Length > 0) return l;
        return -1;
    }

    /// <summary>Spells of a level on a class's list (spells.2da class column), minus the opposition school of a specialist.</summary>
    public static IEnumerable<int> ClassSpells(World w, int cls, int level, int school = -1)
    {
        var col = CreatureRules.SpellColumn(w, cls);
        if (col.Length == 0) yield break;
        var spells = w.Resources.TwoDA("spells");
        int ci = spells.ColumnIndex(col);
        string banned = "";
        if (school > 0)
        {
            var schools = w.Resources.TwoDA("spellschools");
            int opp = schools.GetInt(school, "Opposition", -1);
            if (opp >= 0) banned = schools.Get(opp, "Letter");
        }
        for (int s = 0; s < spells.RowCount; s++)
        {
            var v = spells.Get(s, ci);
            if (v.Length == 0 || !int.TryParse(v, out int l) || l != level) continue;
            if (spells.Get(s, "Label").Length == 0) continue;
            if (banned.Length > 0 && string.Equals(spells.Get(s, "School"), banned, StringComparison.OrdinalIgnoreCase)) continue;
            yield return s;
        }
    }

    // ------------------------------------------------------------------ names

    static string Tlk(World w, string table, int row, string column) =>
        w.Resources.TwoDA(table).GetInt(row, column, -1) is var strref and >= 0 ? StripColors(w.Resources.TlkString(unchecked((uint)strref))) : "";

    static readonly System.Text.RegularExpressions.Regex ColorTokens = new(@"<c[\s\S]{3}>|</c>", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Removes NWN colour tokens (&lt;cRGB&gt;...&lt;/c&gt;) from a string.</summary>
    public static string StripColors(string s) => s.Contains("<c") || s.Contains("</c>") ? ColorTokens.Replace(s, "") : s;
    public static string ClassName(World w, int cls) => Tlk(w, "classes", cls, "Name") is { Length: > 0 } n ? n : Classes(w).Get(cls, "Label");
    public static string RaceName(World w, int race) => Tlk(w, "racialtypes", race, "Name") is { Length: > 0 } n ? n : Races(w).Get(race, "Label");
    public static string FeatName(World w, int feat) => Tlk(w, "feat", feat, "FEAT") is { Length: > 0 } n ? n : FeatTable(w).Get(feat, "LABEL");
    public static string SkillName(World w, int skill) => Tlk(w, "skills", skill, "Name") is { Length: > 0 } n ? n : SkillTable(w).Get(skill, "Label");
    public static string SpellName(World w, int spell) => Tlk(w, "spells", spell, "Name") is { Length: > 0 } n ? n : w.Resources.TwoDA("spells").Get(spell, "Label");
}
