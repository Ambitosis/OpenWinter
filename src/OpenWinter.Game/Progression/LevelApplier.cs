using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>Server settings for character creation and levelling (set by the host from ServerOptions).</summary>
public sealed class ProgressionSystem : IWorldSystem
{
    public ProgressionSystem(World world) { }

    /// <summary>Every level gets the full hit die (otherwise levels above ruleset MIN_LEVEL_FOR_MAX_HP roll).</summary>
    public bool AlwaysMaxHitPoints;
    /// <summary>Uploaded local-vault characters are checked against the rules (ELC).</summary>
    public bool EnforceLegalCharacters;
    /// <summary>Players may create new characters.</summary>
    public bool AllowCharacterCreation = true;
    public Random Random = Random.Shared;

    public void Step(float dt) { }
}

/// <summary>Applies a validated level to a creature, recording it in the level history (LvlStatList) like the original.</summary>
public static class LevelApplier
{
    /// <summary>
    /// Hit points for a level: the full hit die up to ruleset MIN_LEVEL_FOR_MAX_HP (or always, by
    /// server setting); above it a roll from half the die to the die (observed in original
    /// level histories: a d6 class rolled 3-6 from level 4 on).
    /// </summary>
    public static int HitPointsForLevel(World w, int newCharacterLevel, int die, bool alwaysMax, Random rng)
    {
        if (alwaysMax || newCharacterLevel <= Ruleset.Int(w, "MIN_LEVEL_FOR_MAX_HP", 3)) return die;
        int low = Math.Max(1, die / 2);
        return low + rng.Next(die - low + 1);
    }

    /// <summary>
    /// Takes the level: class, ability, stat gains, skills, feats, spells, class options, hit points and
    /// the LvlStatList entry. The plan must come from <see cref="ProgressionRules.Plan"/> for the same
    /// creature and choice (call it right before). Returns the hit points gained.
    /// </summary>
    public static int Apply(World w, Creature c, LevelChoice ch, LevelPlan plan, int? hitDie = null)
    {
        CreatureRules.EnsureInit(w, c);
        var opts = w.System<ProgressionSystem>();
        int cls = ch.Class;
        var cc = c.Classes.FirstOrDefault(x => x.Class == cls);
        if (cc == null) c.Classes.Add(cc = new CreatureClass { Class = cls });
        cc.Level++;
        if (cc.Level == 1 && plan.PickDomains) { cc.Domain1 = ch.Domain1; cc.Domain2 = ch.Domain2; }
        if (cc.Level == 1 && plan.PickSchool) cc.School = ch.School;
        int level = c.TotalLevel;

        bool raised = plan.AbilityIncrease && ch.Ability is >= 0 and <= 5;
        if (raised) c.Abilities[ch.Ability]++;
        ProgressionRules.ApplyStatGain(w, c, cls, cc.Level);

        int rows = w.Resources.TwoDA("skills").RowCount;
        if (c.Skills.Length < rows) Array.Resize(ref c.Skills, rows);
        foreach (var (sk, ranks) in ch.Skills)
            if (sk >= 0 && sk < c.Skills.Length && ranks > 0) c.Skills[sk] += ranks;

        var gained = new List<int>();
        foreach (var f in plan.GrantedFeats.Concat(ch.Feats))
            if (!gained.Contains(f) && !c.Feats.Contains(f)) gained.Add(f);
        c.Feats.AddRange(gained);

        var learnt = new Dictionary<int, List<int>>();
        foreach (var pick in plan.Spells.Where(p => p.Class == cls))
        {
            foreach (var (lvl, list) in pick.Granted) Learn(lvl, list);
            foreach (var (lvl, list) in ch.Spells) Learn(lvl, list.Where(sp => pick.Choices.GetValueOrDefault(lvl)?.Contains(sp) == true));
        }
        void Learn(int lvl, IEnumerable<int> spells)
        {
            foreach (var sp in spells)
            {
                if (cc.KnownSpells[lvl].Contains(sp)) continue;
                cc.KnownSpells[lvl].Add(sp);
                (learnt.TryGetValue(lvl, out var l) ? l : learnt[lvl] = new List<int>()).Add(sp);
            }
        }

        var t = c.Template;
        // A later level that offers the associate again keeps it unless another is chosen.
        bool hadFamiliar = c.Feats.Contains(NwRules.FeatSummonFamiliar) && !gained.Contains(NwRules.FeatSummonFamiliar);
        bool hadCompanion = c.Feats.Contains(NwRules.FeatAnimalCompanion) && !gained.Contains(NwRules.FeatAnimalCompanion);
        if (plan.PickFamiliar)
        {
            c.FamiliarType = ch.Familiar >= 0 ? ch.Familiar : hadFamiliar ? c.FamiliarType : DefaultAssociate(w, c, ch, cls);
            if (ch.FamiliarName.Length > 0) c.FamiliarName = ch.FamiliarName;
            t.SetInt("FamiliarType", c.FamiliarType);
            t.SetString("FamiliarName", c.FamiliarName);
        }
        if (plan.PickCompanion)
        {
            c.CompanionType = ch.Companion >= 0 ? ch.Companion : hadCompanion ? c.CompanionType : DefaultAssociate(w, c, ch, cls);
            if (ch.CompanionName.Length > 0) c.CompanionName = ch.CompanionName;
            t.SetInt("CompanionType", c.CompanionType);
            t.SetString("CompanionName", c.CompanionName);
        }

        int hd = hitDie ?? HitPointsForLevel(w, level, ProgressionRules.HitDie(w, cls), opts.AlwaysMaxHitPoints, opts.Random);
        c.BaseHitPoints += hd;
        SetShort(t, "HitPoints", c.BaseHitPoints);
        SetWord(t, "SkillPoints", Math.Max(0, plan.SkillPointsLeft));

        // Level history, laid out as the original writes it.
        var history = t.GetList("LvlStatList");
        if (!t.Has("LvlStatList")) t.SetList("LvlStatList", history);
        if (history.Count == level - 1)
        {
            var e = new GffStruct();
            if (raised) e.SetByte("LvlStatAbility", ch.Ability);
            e.SetByte("LvlStatHitDie", hd);
            e.SetByte("LvlStatClass", cls);
            e.SetByte("EpicLevel", level > 20 ? 1 : 0);
            foreach (var (lvl, list) in learnt.OrderBy(k => k.Key))
                e.SetList($"KnownList{lvl}", list.Select(sp => new GffStruct().SetWord("Spell", sp)).ToList());
            e.SetWord("SkillPoints", Math.Max(0, plan.SkillPointsLeft));
            e.SetList("SkillList", Enumerable.Range(0, rows).Select(sk => new GffStruct().SetByte("Rank", Math.Max(0, ch.Skills.GetValueOrDefault(sk)))).ToList());
            e.SetList("FeatList", gained.Select(f => new GffStruct().SetWord("Feat", f)).ToList());
            history.Add(e);
        }

        CreatureRules.SyncHitPoints(w, c);
        w.MarkChanged(c);
        return hd;
    }

    /// <summary>The package's associate (packages.2da Associate), else the first row.</summary>
    static int DefaultAssociate(World w, Creature c, LevelChoice ch, int cls)
    {
        var packages = w.Resources.TwoDA("packages");
        int pkg = ch.Package >= 0 ? ch.Package : c.StartingPackage != 255 ? c.StartingPackage : -1;
        if (pkg >= 0 && packages.GetInt(pkg, "ClassID", -1) == cls && packages.GetInt(pkg, "Associate", -1) is int a and >= 0) return a;
        return 0;
    }

    static void SetShort(GffStruct s, string label, int v)
    {
        if (s.Field(label) is { } f && f.Type is GffType.Word or GffType.Int or GffType.DWord)
            f.Value = f.Type switch { GffType.Word => (object)(ushort)v, GffType.DWord => (uint)v, _ => v };
        else s.SetShort(label, v);
    }

    static void SetWord(GffStruct s, string label, int v)
    {
        if (s.Field(label) is { } f && f.Type is GffType.Short or GffType.Int or GffType.Byte)
            f.Value = f.Type switch { GffType.Short => (object)(short)v, GffType.Byte => (byte)Math.Min(255, v), _ => v };
        else s.SetWord(label, v);
    }
}
