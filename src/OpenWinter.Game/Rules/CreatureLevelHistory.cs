using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>The level history (LvlStatList): what each character level gave.</summary>
public static partial class CreatureRules
{
    /// <summary>The ability an entry without an increase reports (NWNX_CREATURE_ABILITY_NONE).</summary>
    public const int NoAbilityIncrease = 6;

    /// <summary>
    /// The creature's level history. A creature made from a blueprint has none stored, and the original gives it a
    /// single entry for its first level: its first class, no hit points, feats, skill ranks or ability increase
    /// (nwserver nwnx_creature_levels: a goblin and a bandit read one entry, and levels added later follow it).
    /// A player's character file carries its own.
    /// </summary>
    public static List<GffStruct> LevelHistory(World w, Creature c)
    {
        var t = c.Template;
        var history = t.GetList("LvlStatList");
        if (c.IsPC) return history;
        if (!t.Has("LvlStatList")) t.SetList("LvlStatList", history);
        if (history.Count == 0 && c.Classes.Count > 0 && c.Classes[0].Level > 0)
        {
            var e = new GffStruct();
            e.SetByte("LvlStatHitDie", 0);
            e.SetByte("LvlStatClass", c.Classes[0].Class);
            e.SetByte("EpicLevel", 0);
            e.SetWord("SkillPoints", 0);
            int rows = Math.Max(c.Skills.Length, w.Resources.TwoDA("skills").RowCount);
            e.SetList("SkillList", Enumerable.Range(0, rows).Select(_ => new GffStruct().SetByte("Rank", 0)).ToList());
            e.SetList("FeatList", new List<GffStruct>());
            history.Add(e);
        }
        return history;
    }

    /// <summary>The history entry of a character level (1-based), or null when there is none.</summary>
    public static GffStruct? LevelEntry(World w, Creature c, int level)
    {
        var history = LevelHistory(w, c);
        return level >= 1 && level <= history.Count ? history[level - 1] : null;
    }

    /// <summary>The feats an entry lists.</summary>
    public static List<GffStruct> LevelFeats(GffStruct entry)
    {
        var feats = entry.GetList("FeatList");
        if (!entry.Has("FeatList")) entry.SetList("FeatList", feats);
        return feats;
    }

    /// <summary>The skill ranks an entry gave, one struct per skills.2da row, grown to reach <paramref name="skill"/>.</summary>
    public static List<GffStruct> LevelSkills(GffStruct entry, int skill = -1)
    {
        var skills = entry.GetList("SkillList");
        if (!entry.Has("SkillList")) entry.SetList("SkillList", skills);
        while (skills.Count <= skill) skills.Add(new GffStruct().SetByte("Rank", 0));
        return skills;
    }

    /// <summary>The character level whose history entry granted a feat, or 0.</summary>
    public static int FeatGrantLevel(World w, Creature c, int feat)
    {
        var history = LevelHistory(w, c);
        for (int i = 0; i < history.Count; i++)
            if (LevelFeats(history[i]).Any(f => f.GetInt("Feat") == feat)) return i + 1;
        return 0;
    }
}
