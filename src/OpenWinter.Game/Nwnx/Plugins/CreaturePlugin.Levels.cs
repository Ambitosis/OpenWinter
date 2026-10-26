using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Creature's level functions over the level history (CreatureRules.LevelHistory). Measured on nwserver
/// (parity suite nwnx_creature_levels): a level below 1 is an error (nothing is returned), a level past the history
/// reads -1, and the history setters change only the history, not the creature's current values.
/// </summary>
static partial class CreaturePlugin
{
    /// <summary>The entry for <paramref name="level"/>; pushes -1 and returns null past the history, pushes nothing below level 1.</summary>
    static GffStruct? Entry(NwnxCall n, Creature? c, int level)
    {
        if (c == null) { n.Push(-1); return null; }
        if (level < 1) return null;
        var e = CreatureRules.LevelEntry(n.World, c, level);
        if (e == null) n.Push(-1);
        return e;
    }

    [NwnxFunc("Creature", "GetClassByLevel")]
    static void ClassByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (Entry(n, c, level) is { } e) n.Push((int)e.GetInt("LvlStatClass"));
    }

    [NwnxFunc("Creature", "GetFeatCountByLevel")]
    static void FeatCountByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (Entry(n, c, level) is { } e) n.Push(CreatureRules.LevelFeats(e).Count);
    }

    [NwnxFunc("Creature", "GetFeatByLevel")]
    static void FeatByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt(); int index = n.PopInt();
        if (Entry(n, c, level) is not { } e) return;
        var feats = CreatureRules.LevelFeats(e);
        n.Push(index >= 0 && index < feats.Count ? (int)feats[index].GetInt("Feat") : -1);
    }

    /// <summary>The character level whose history entry has the feat; 0 when none has it (a blueprint's own feats).</summary>
    [NwnxFunc("Creature", "GetFeatGrantLevel")]
    static void FeatGrantLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        n.Push(c != null && c.Feats.Contains(feat) ? CreatureRules.FeatGrantLevel(n.World, c, feat) : 0);
    }

    /// <summary>Gives the feat and lists it in the level's history entry.</summary>
    [NwnxFunc("Creature", "AddFeatByLevel")]
    static void AddFeatByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt(); int level = n.PopInt();
        if (c == null) return;
        if (!c.Feats.Contains(feat)) c.Feats.Add(feat);
        if (CreatureRules.LevelEntry(n.World, c, level) is { } e)
        {
            var feats = CreatureRules.LevelFeats(e);
            if (!feats.Any(f => f.GetInt("Feat") == feat)) feats.Add(new GffStruct().SetWord("Feat", feat));
        }
    }

    /// <summary>Takes the feat out of the level's history entry only; the creature keeps it.</summary>
    [NwnxFunc("Creature", "RemoveFeatByLevel")]
    static void RemoveFeatByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt(); int level = n.PopInt();
        if (c != null && CreatureRules.LevelEntry(n.World, c, level) is { } e)
            CreatureRules.LevelFeats(e).RemoveAll(f => f.GetInt("Feat") == feat);
    }

    [NwnxFunc("Creature", "GetSkillRankByLevel")]
    static void SkillRankByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int skill = n.PopInt(); int level = n.PopInt();
        if (Entry(n, c, level) is not { } e) return;
        var skills = CreatureRules.LevelSkills(e);
        n.Push(skill >= 0 && skill < skills.Count ? (sbyte)skills[skill].GetInt("Rank") : -1);
    }

    [NwnxFunc("Creature", "SetSkillRankByLevel")]
    static void SetSkillRankByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int skill = n.PopInt(); int rank = n.PopInt(); int level = n.PopInt();
        if (c == null || skill is < 0 or > 255 || CreatureRules.LevelEntry(n.World, c, level) is not { } e) return;
        CreatureRules.LevelSkills(e, skill)[skill].SetByte("Rank", (byte)(sbyte)Math.Clamp(rank, -128, 127));
    }

    [NwnxFunc("Creature", "GetMaxHitPointsByLevel")]
    static void GetHpByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (Entry(n, c, level) is { } e) n.Push((int)e.GetInt("LvlStatHitDie"));
    }

    /// <summary>The hit points a level rolled, in the history (the current maximum stays: nwserver nwnx_creature_levels).</summary>
    [NwnxFunc("Creature", "SetMaxHitPointsByLevel")]
    static void SetHpByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt(); int value = n.PopInt();
        if (c != null && CreatureRules.LevelEntry(n.World, c, level) is { } e) e.SetByte("LvlStatHitDie", Math.Clamp(value, 0, 255));
    }

    [NwnxFunc("Creature", "GetAbilityIncreaseByLevel")]
    static void AbilityIncreaseByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (Entry(n, c, level) is { } e) n.Push(e.Has("LvlStatAbility") ? (int)e.GetInt("LvlStatAbility") : CreatureRules.NoAbilityIncrease);
    }

    /// <summary>
    /// The ability a level raised. The score moves with it: the old ability loses the point, the new one gains it
    /// (nwserver nwnx_creature_levels).
    /// </summary>
    [NwnxFunc("Creature", "SetAbilityIncreaseByLevel")]
    static void SetAbilityIncreaseByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt(); int ability = n.PopInt();
        if (c == null || ability is < 0 or > 6 || CreatureRules.LevelEntry(n.World, c, level) is not { } e) return;
        int old = e.Has("LvlStatAbility") ? (int)e.GetInt("LvlStatAbility") : CreatureRules.NoAbilityIncrease;
        if (old == ability) return;
        if (old is >= 0 and < 6) c.Abilities[old] = Math.Max(3, c.Abilities[old] - 1);
        if (ability < 6) { c.Abilities[ability]++; e.SetByte("LvlStatAbility", ability); }
        else e.Remove("LvlStatAbility");
        SyncHitPoints(n, c);
    }

    [NwnxFunc("Creature", "GetSkillPointsRemaining")]
    static void SkillPointsRemaining(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? (int)c.Template.GetInt("SkillPoints") : 0);
    }

    [NwnxFunc("Creature", "SetSkillPointsRemaining")]
    static void SetSkillPointsRemaining(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int points = n.PopInt();
        if (c != null) c.Template.SetWord("SkillPoints", Math.Clamp(points, 0, ushort.MaxValue));
    }

    [NwnxFunc("Creature", "GetSkillPointsRemainingByLevel")]
    static void SkillPointsRemainingByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (Entry(n, c, level) is { } e) n.Push((int)e.GetInt("SkillPoints"));
    }

    [NwnxFunc("Creature", "SetSkillPointsRemainingByLevel")]
    static void SetSkillPointsRemainingByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt(); int points = n.PopInt();
        if (c != null && CreatureRules.LevelEntry(n.World, c, level) is { } e) e.SetWord("SkillPoints", Math.Clamp(points, 0, ushort.MaxValue));
    }

    /// <summary>
    /// Changes the class in a position; with bUpdateLevels the history's levels of the old class become the new one.
    /// Base attack and saves follow (nwserver: a sixth-level fighter made a wizard has BAB 3, saves 3/3/6).
    /// </summary>
    [NwnxFunc("Creature", "SetClassByPosition")]
    static void SetClassByPosition(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int position = n.PopInt(); int cls = n.PopInt(); bool update = n.PopInt() != 0;
        if (c == null || position < 0 || position >= c.Classes.Count || cls is < 0 or > 255) return;
        int old = c.Classes[position].Class;
        c.Classes[position].Class = cls;
        if (update)
            foreach (var e in CreatureRules.LevelHistory(n.World, c))
                if (e.GetInt("LvlStatClass") == old) e.SetByte("LvlStatClass", cls);
        SyncHitPoints(n, c);
    }

    /// <summary>
    /// Sets the level in a class position that is taken. The history stays as it is; the maximum hit points follow
    /// the new level's Constitution and feat bonuses, the current ones stay (nwserver: a bandit set to level 6 reads 12/22).
    /// </summary>
    [NwnxFunc("Creature", "SetLevelByPosition")]
    static void SetLevelByPosition(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int position = n.PopInt(); int level = n.PopInt();
        if (c == null || position < 0 || position >= c.Classes.Count || level < 1 || level > CreatureRules.MaxLevel) return;
        CreatureRules.EnsureInit(n.World, c);
        int current = c.CurrentHitPoints;
        c.Classes[position].Level = level;
        CreatureRules.SyncHitPoints(n.World, c);
        c.CurrentHitPoints = Math.Min(current, c.MaxHitPoints);
        Changed(n, c);
    }

    /// <summary>
    /// Adds levels of a class from its package, without the usual checks (experience, prerequisites, alignment);
    /// not for players. Each level takes the full hit die, spell slots it adds start used, and a feat whose base
    /// attack bonus requirement only the new level meets isn't picked (nwserver nwnx_creature_levels: a goblin's
    /// first fighter level took no bonus feat, its second Weapon Focus). A creature whose history doesn't cover
    /// every level (a blueprint above level 1) gains nothing.
    /// </summary>
    [NwnxFunc("Creature", "LevelUp")]
    static void LevelUp(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int count = n.PopInt(); int package = n.PopInt();
        if (c == null || c.IsPC) return;
        var w = n.World;
        CreatureRules.EnsureInit(w, c);
        var feats = w.Resources.TwoDA("feat");
        for (int i = 0; i < count && c.TotalLevel < CreatureRules.MaxLevel; i++)
        {
            if (CreatureRules.LevelHistory(w, c).Count != c.TotalLevel) break;
            if (c.Classes.All(k => k.Class != cls) && c.Classes.Count >= ProgressionRules.MaxClasses) break;
            var left = SpontaneousSlotsLeft(w, c);
            int bab = CreatureRules.BaseAttackBonus(w, c);
            var ch = PackageBuilder.Complete(w, c, new LevelChoice { Class = cls, Package = package == 255 ? -1 : package }, player: false);
            ch.Feats.RemoveAll(f => !feats.IsEmpty(f, "MINATTACKBONUS") && feats.GetInt(f, "MINATTACKBONUS") > bab);
            var plan = ProgressionRules.Plan(w, c, ch, player: false);
            LevelApplier.Apply(w, c, ch, plan, ProgressionRules.HitDie(w, cls));
            foreach (var (key, value) in left) c.SpellSlotsLeft[key] = value;
            for (int ci = 0; ci < c.Classes.Count; ci++)
                if (CreatureRules.IsSpellCaster(w, c.Classes[ci].Class) && !CreatureRules.MemorizesSpells(w, c.Classes[ci].Class))
                    for (int lvl = 0; lvl < 10; lvl++)
                        c.SpellSlotsLeft.TryAdd((ci, lvl), 0);
        }
        Changed(n, c);
    }

    /// <summary>The spontaneous casting uses each class position has left now, explicitly.</summary>
    static Dictionary<(int, int), int> SpontaneousSlotsLeft(World w, Creature c)
    {
        var left = new Dictionary<(int, int), int>();
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            if (!CreatureRules.IsSpellCaster(w, c.Classes[ci].Class) || CreatureRules.MemorizesSpells(w, c.Classes[ci].Class)) continue;
            for (int lvl = 0; lvl < 10; lvl++) left[(ci, lvl)] = CreatureRules.SlotsLeft(w, c, ci, lvl);
        }
        return left;
    }

    /// <summary>
    /// Takes the last levels away; not for players. With a history for every level it goes as losing experience does;
    /// without one the last class loses a level and the maximum hit points its Constitution bonus, the current ones
    /// staying (nwserver: a level 12 hill giant reads 11 levels and 102/98).
    /// </summary>
    [NwnxFunc("Creature", "LevelDown")]
    static void LevelDown(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int count = n.PopInt();
        if (c == null || c.IsPC) return;
        var w = n.World;
        CreatureRules.EnsureInit(w, c);
        bool lost = false;
        for (int i = 0; i < count && c.TotalLevel > 1; i++)
        {
            if (CreatureRules.LevelHistory(w, c).Count == c.TotalLevel)
            {
                // CreatureRules.LevelDown raises NWNX_ON_LEVEL_DOWN around it.
                if (!CreatureRules.LevelDown(w, c)) break;
                CreatureRules.SyncHitPoints(w, c);
            }
            else
            {
                using var levelDown = new Nwnx.NwnxEvent.AfterScope(CreatureRules.LevelDownBefore(w, c));
                int current = c.CurrentHitPoints;
                var last = c.Classes[^1];
                if (--last.Level <= 0) c.Classes.RemoveAt(c.Classes.Count - 1);
                CreatureRules.SyncHitPoints(w, c);
                c.CurrentHitPoints = current;
            }
            lost = true;
        }
        if (lost) Changed(n, c);
    }
}
