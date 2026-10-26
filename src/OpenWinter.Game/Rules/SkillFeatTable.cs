using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>A feat that changes a skill's rank: a bonus, class levels, the key ability, the armour check penalty.</summary>
public sealed class SkillFeat
{
    public int Skill, Feat, Modifier;
    /// <summary>1 for a Skill Focus feat, 2 for an Epic Skill Focus one (their modifiers change together).</summary>
    public int FocusFeat;
    /// <summary>Bits by class (the rightmost character is class 0) whose levels count, "" for all.</summary>
    public string Classes = "";
    /// <summary>Points per counted class level (rounded down); 0 for none.</summary>
    public float ClassLevelMod;
    /// <summary>Area flags (1 interior, 2 underground, 4 natural) the area must have, and must not have.</summary>
    public int AreaFlagsRequired, AreaFlagsForbidden;
    /// <summary>1 by day, 2 by night (or in an area always at night), 0 always.</summary>
    public int DayOrNight;
    public bool BypassArmorCheckPenalty;
    /// <summary>Abilities (1 Str, 2 Dex, 4 Con, 8 Int, 16 Wis, 32 Cha) and how they combine (64 min, 128 max, 256 average, 512 sum).</summary>
    public int KeyAbilityMask;

    public SkillFeat Copy() => (SkillFeat)MemberwiseClone();
}

/// <summary>
/// The skill feats of the rules, as nwserver keeps them (parity suite nwnx_skillranks reads its table through
/// NWNX_SkillRanks): Skill Focus and Epic Skill Focus from feat.2da, and the racial, background and class feats with
/// their ruleset.2da bonuses. Modules change it at run time; area modifiers and the blindness penalty live here too.
/// </summary>
public sealed class SkillFeatTable
{
    static readonly ConditionalWeakTable<World, SkillFeatTable> Tables = new();
    public static SkillFeatTable Of(World w) => Tables.GetValue(w, world => new SkillFeatTable(world));

    readonly Dictionary<int, List<SkillFeat>> _bySkill = new();
    readonly Dictionary<(uint Area, int Skill), int> _areaModifiers = new();
    /// <summary>The penalty to Strength and Dexterity skills while blind (positive).</summary>
    public int BlindnessPenalty = 4;

    SkillFeatTable(World w)
    {
        int R(string label, int fallback) => Ruleset.Int(w, label, fallback);
        var feats = w.Resources.TwoDA("feat");
        var masters = w.Resources.TwoDA("masterfeats");
        int focus = masters.Find("LABEL", "SkillFocus"), epicFocus = masters.Find("LABEL", "EpicSkillFocus");
        for (int f = 0; f < feats.RowCount; f++)
        {
            if (!int.TryParse(feats.Get(f, "MASTERFEAT"), out int m) || m < 0 || (m != focus && m != epicFocus)) continue;
            int skill = feats.GetInt(f, "REQSKILL", -1);
            if (skill < 0) continue;
            bool epic = m == epicFocus;
            Add(new SkillFeat
            {
                Skill = skill, Feat = f, FocusFeat = epic ? 2 : 1,
                Modifier = epic ? R("EPIC_SKILL_FOCUS_SKILL_BONUS", 10) : R("SKILL_FOCUS_SKILL_BONUS", 3),
            });
        }
        void Plain(int feat, int skill, string rule, int fallback) => Add(new SkillFeat { Skill = skill, Feat = feat, Modifier = R(rule, fallback) });
        Plain(NwRules.FeatAlertness, Skill.Listen, "ALERTNESS_SKILL_BONUS", 2);
        Plain(NwRules.FeatAlertness, Skill.Spot, "ALERTNESS_SKILL_BONUS", 2);
        Plain(NwRules.FeatSkillAffinityConcentration, Skill.Concentration, "SKILL_AFFINITY_SKILL_BONUS", 2);
        Plain(NwRules.FeatSkillAffinityListen, Skill.Listen, "SKILL_AFFINITY_SKILL_BONUS", 2);
        Plain(NwRules.FeatSkillAffinityLore, Skill.Lore, "SKILL_AFFINITY_SKILL_BONUS", 2);
        Plain(NwRules.FeatSkillAffinityMoveSilently, Skill.MoveSilently, "SKILL_AFFINITY_SKILL_BONUS", 2);
        Plain(NwRules.FeatSkillAffinitySearch, Skill.Search, "SKILL_AFFINITY_SKILL_BONUS", 2);
        Plain(NwRules.FeatSkillAffinitySpot, Skill.Spot, "SKILL_AFFINITY_SKILL_BONUS", 2);
        Plain(NwRules.FeatPartialAffinityListen, Skill.Listen, "PARTIAL_SKILL_FOCUS_SKILL_BONUS", 1);
        Plain(NwRules.FeatPartialAffinitySearch, Skill.Search, "PARTIAL_SKILL_FOCUS_SKILL_BONUS", 1);
        Plain(NwRules.FeatPartialAffinitySpot, Skill.Spot, "PARTIAL_SKILL_FOCUS_SKILL_BONUS", 1);
        Plain(NwRules.FeatArtist, Skill.Perform, "ARTIST_PERFORM_BONUS", 2);
        Plain(NwRules.FeatArtist, Skill.Persuade, "ARTIST_PERSUADE_BONUS", 2);
        Plain(NwRules.FeatArtist, Skill.Spot, "ARTIST_SPOT_BONUS", 2);
        Plain(NwRules.FeatBlooded, Skill.Spot, "BLOODED_SPOT_BONUS", 2);
        Plain(NwRules.FeatCourtlyMagocracy, Skill.Lore, "COURTEOUS_MAGOCRACY_LORE_BONUS", 2);
        Plain(NwRules.FeatCourtlyMagocracy, Skill.Spellcraft, "COURTEOUS_MAGOCRACY_LORE_BONUS", 2);
        Plain(NwRules.FeatSilverPalm, Skill.Appraise, "SILVER_PALM_APPRAISE_BONUS", 2);
        Plain(NwRules.FeatSilverPalm, Skill.Persuade, "SILVER_PALM_PERSUADE_BONUS", 2);
        Plain(NwRules.FeatStealthy, Skill.Hide, "STEALTHY_HIDE_BONUS", 2);
        Plain(NwRules.FeatStealthy, Skill.MoveSilently, "STEALTHY_MOVE_SILENTLY_BONUS", 2);
        Plain(NwRules.FeatThug, Skill.Persuade, "THUG_PERSUADE_BONUS", 2);
        Plain(FeatImprovedParry, Skill.Parry, "IMPROVED_PARRY_MODIFIER", 4);
        foreach (int skill in new[] { Skill.Persuade, Skill.Taunt, Skill.Bluff, Skill.Intimidate })
            Plain(FeatEpicReputation, skill, "EPIC_REPUTATION_SKILL_BONUS", 4);
        // Stonecunning only underground; Trackless Step only outdoors in nature, not inside or underground.
        Add(new SkillFeat { Skill = Skill.Search, Feat = NwRules.FeatStonecunning, Modifier = R("STONECUNNING_SEARCH_SKILL_BONUS", 2), AreaFlagsRequired = AreaUnderground });
        foreach (int skill in new[] { Skill.Hide, Skill.MoveSilently })
            Add(new SkillFeat
            {
                Skill = skill, Feat = FeatTracklessStep, Modifier = R("TRACKLESS_STEP_SKILL_BONUS", 4),
                AreaFlagsRequired = AreaNatural, AreaFlagsForbidden = AreaInterior | AreaUnderground,
            });
        // Bardic Knowledge: a point of Lore per bard and harper scout level.
        Add(new SkillFeat { Skill = Skill.Lore, Feat = FeatBardicKnowledge, Classes = ClassBits(ClassBard, ClassHarper), ClassLevelMod = 1f });
    }

    public const int AreaInterior = 1, AreaUnderground = 2, AreaNatural = 4;
    const int FeatImprovedParry = 18, FeatEpicReputation = 586, FeatTracklessStep = 201, FeatBardicKnowledge = 197, ClassBard = 1, ClassHarper = 28;

    static class Skill
    {
        public const int Concentration = 1, Heal = 4, Hide = 5, Listen = 6, Lore = 7, MoveSilently = 8, Parry = 10, Perform = 11, Persuade = 12,
            Search = 14, Spellcraft = 16, Spot = 17, Taunt = 18, Appraise = 20, Bluff = 23, Intimidate = 24;
    }

    /// <summary>The class bit string for some classes: the rightmost character is class 0.</summary>
    public static string ClassBits(params int[] classes)
    {
        int len = classes.Max() + 1;
        var chars = Enumerable.Repeat('0', len).ToArray();
        foreach (int c in classes) chars[len - 1 - c] = '1';
        return new string(chars);
    }

    void Add(SkillFeat f)
    {
        if (!_bySkill.TryGetValue(f.Skill, out var list)) _bySkill[f.Skill] = list = new List<SkillFeat>();
        list.Add(f);
    }

    public IReadOnlyList<SkillFeat> ForSkill(int skill) => _bySkill.TryGetValue(skill, out var list) ? list : Array.Empty<SkillFeat>();

    public SkillFeat? Find(int skill, int feat) => ForSkill(skill).FirstOrDefault(f => f.Feat == feat);

    /// <summary>Replaces a skill feat's settings, adding it when <paramref name="create"/>; returns whether it is now in the table.</summary>
    public bool Set(SkillFeat f, bool create)
    {
        var list = ForSkill(f.Skill) as List<SkillFeat>;
        int at = list?.FindIndex(x => x.Feat == f.Feat) ?? -1;
        if (at >= 0) { list![at] = f; return true; }
        if (!create) return false;
        Add(f);
        return true;
    }

    /// <summary>Sets the modifier of every Skill Focus (or Epic Skill Focus) feat.</summary>
    public void SetFocusModifier(int modifier, bool epic)
    {
        foreach (var list in _bySkill.Values)
            foreach (var f in list)
                if (f.FocusFeat == (epic ? 2 : 1)) f.Modifier = modifier;
    }

    public int AreaModifier(uint area, int skill) => _areaModifiers.GetValueOrDefault((area, skill));
    public void SetAreaModifier(uint area, int skill, int modifier)
    {
        if (modifier == 0) _areaModifiers.Remove((area, skill));
        else _areaModifiers[(area, skill)] = modifier;
    }

    /// <summary>Whether a skill feat's area and time conditions hold for a creature where it stands.</summary>
    public static bool Applies(World w, Creature c, SkillFeat f)
    {
        if (f.AreaFlagsRequired != 0 || f.AreaFlagsForbidden != 0)
        {
            int flags = c.Area == null ? 0 : (c.Area.IsInterior ? AreaInterior : 0) | (c.Area.IsUnderground ? AreaUnderground : 0) | (c.Area.IsNatural ? AreaNatural : 0);
            if ((flags & f.AreaFlagsRequired) != f.AreaFlagsRequired || (flags & f.AreaFlagsForbidden) != 0) return false;
        }
        if (f.DayOrNight != 0)
        {
            // Night by the clock, or in an area set to always night.
            bool night = w.Clock.IsNight || (c.Area != null && c.Area.Are.GetInt("DayNightCycle") == 0 && c.Area.Are.GetInt("IsNight") != 0);
            if (f.DayOrNight == 1 && night || f.DayOrNight == 2 && !night) return false;
        }
        return true;
    }

    /// <summary>The class level part of a skill feat: the counted levels times its multiplier, rounded down.</summary>
    public static int ClassLevelBonus(Creature c, SkillFeat f)
    {
        if (f.ClassLevelMod == 0) return 0;
        int levels = 0;
        foreach (var cc in c.Classes)
        {
            bool counted = f.Classes.Length == 0 || (cc.Class < f.Classes.Length && f.Classes[f.Classes.Length - 1 - cc.Class] == '1');
            if (counted) levels += cc.Level;
        }
        return (int)Math.Floor(levels * f.ClassLevelMod);
    }
}
