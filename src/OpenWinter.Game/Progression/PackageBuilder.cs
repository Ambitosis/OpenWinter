using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>
/// Fills in a level from a class package (packages.2da: Attribute, Domain1/2, School, Associate and
/// the SkillPref2DA / FeatPref2DA / SpellPref2DA preference lists): the "Recommended" choices of the
/// character wizard, and how LevelUpHenchman levels NPCs.
/// </summary>
public static class PackageBuilder
{
    /// <summary>The package a class uses by default (classes.2da Package), or -1.</summary>
    public static int DefaultPackage(World w, int cls) => w.Resources.TwoDA("classes").GetInt(cls, "Package", -1);

    /// <summary>
    /// Completes a level choice from a package. Parts the caller already chose (ability, skills, feats,
    /// spells, domains, school) are kept; empty parts are filled. The result is legal when any legal
    /// choice exists (check with <see cref="ProgressionRules.Plan"/>).
    /// </summary>
    public static LevelChoice Complete(World w, Creature c, LevelChoice partial, bool player = true, bool fillSkills = true, bool fillFeats = true, bool fillSpells = true)
    {
        var ch = Clone(partial);
        int cls = ch.Class;
        var packages = w.Resources.TwoDA("packages");
        int pkg = ch.Package >= 0 && packages.GetInt(ch.Package, "ClassID", -1) == cls ? ch.Package
            : c.StartingPackage != 255 && packages.GetInt(c.StartingPackage, "ClassID", -1) == cls ? c.StartingPackage
            : DefaultPackage(w, cls);
        ch.Package = pkg;

        var plan = ProgressionRules.Plan(w, c, ch, player, checkVariables: player);
        if (plan.AbilityIncrease && ch.Ability < 0)
        {
            int a = NwRules.AbilityFromLabel(packages.Get(pkg, "Attribute"));
            if (a < 0) a = NwRules.AbilityFromLabel(w.Resources.TwoDA("classes").Get(cls, "PrimaryAbil"));
            ch.Ability = a >= 0 ? a : NwRules.AbilityStr;
        }
        var domains = w.Resources.TwoDA("domains");
        if (plan.PickDomains && (ch.Domain1 < 0 || ch.Domain2 < 0 || ch.Domain1 == ch.Domain2))
        {
            var valid = Enumerable.Range(0, domains.RowCount).Where(d => domains.Get(d, "Label").Length > 0 && domains.Get(d, "Name").Length > 0).ToList();
            int d1 = packages.GetInt(pkg, "Domain1", -1), d2 = packages.GetInt(pkg, "Domain2", -1);
            if (!valid.Contains(d1)) d1 = valid.FirstOrDefault(-1);
            if (!valid.Contains(d2) || d2 == d1) d2 = valid.FirstOrDefault(d => d != d1, -1);
            ch.Domain1 = d1;
            ch.Domain2 = d2;
        }
        if (plan.PickSchool && ch.School < 0) ch.School = Math.Max(0, packages.GetInt(pkg, "School", 0));

        if (fillSkills && ch.Skills.Count == 0)
        {
            plan = ProgressionRules.Plan(w, c, ch, player, checkVariables: player);
            int points = plan.SkillPointsAvailable;
            var order = PrefList(w, packages.Get(pkg, "SkillPref2DA"), "SKILLINDEX").ToList();
            // Then the class skills, then anything else, so points aren't left over when the list runs out.
            order.AddRange(plan.Skills.Where(o => o.ClassSkill).Select(o => o.Skill));
            foreach (var sk in order.Distinct())
            {
                var opt = plan.Skills.FirstOrDefault(o => o.Skill == sk);
                if (opt == null || IsHiddenSkill(w, sk)) continue;
                int ranks = Math.Min(opt.MaxRanks - opt.Current, points / opt.Cost);
                if (ranks <= 0) continue;
                ch.Skills[sk] = ranks;
                points -= ranks * opt.Cost;
            }
        }

        if (fillFeats && ch.Feats.Count == 0)
        {
            var prefs = PrefList(w, packages.Get(pkg, "FeatPref2DA"), "FeatIndex").ToList();
            for (int guard = 0; guard < 16; guard++)
            {
                plan = ProgressionRules.Plan(w, c, ch, player, checkVariables: player);
                var open = plan.Errors.Where(e => e.Code == "feats-unspent").ToList();
                if (open.Count == 0) break;
                bool bonusOpen = open.Any(e => e.Message.Contains("bonus"));
                var choices = bonusOpen ? plan.BonusFeatChoices : plan.GeneralFeatChoices;
                int pick = prefs.FirstOrDefault(f => choices.Contains(f) && !ch.Feats.Contains(f), -1);
                if (pick < 0) pick = choices.FirstOrDefault(f => !ch.Feats.Contains(f), -1);
                if (pick < 0) break;
                ch.Feats.Add(pick);
            }
        }

        if (fillSpells && ch.Spells.Count == 0)
        {
            plan = ProgressionRules.Plan(w, c, ch, player, checkVariables: player);
            var prefs = PrefList(w, packages.Get(pkg, "SpellPref2DA"), "SpellIndex").ToList();
            // Only spells on the package's list, in its order (nwserver, parity suite package_spell_picks: a sorcerer
            // stays at the list's four cantrips). A spellbook takes them from the levels it could cast before this
            // level: a wizard learns 1st-level spells at wizard 3 and its first 5th-level ones at wizard 10.
            foreach (var pick in plan.Spells.Where(p => p.Class == cls))
            {
                foreach (var (lvl, n) in pick.PerLevel)
                {
                    var options = pick.Choices.GetValueOrDefault(lvl) ?? new List<int>();
                    var chosen = prefs.Where(options.Contains).Distinct().Take(n).ToList();
                    if (chosen.Count > 0) ch.Spells[lvl] = chosen;
                }
                if (pick.AnyLevel > 0)
                {
                    int maxLevel = plan.NewClassLevel > 1 ? ProgressionRules.MaxSpellLevel(w, cls, plan.NewClassLevel - 1) : pick.MaxLevel;
                    var all = pick.Choices.Where(k => k.Key <= maxLevel).SelectMany(k => k.Value.Select(sp => (Level: k.Key, Spell: sp))).ToList();
                    var chosen = prefs.SelectMany(sp => all.Where(x => x.Spell == sp)).Distinct().Take(pick.AnyLevel).ToList();
                    foreach (var (lvl, sp) in chosen)
                        (ch.Spells.TryGetValue(lvl, out var l) ? l : ch.Spells[lvl] = new List<int>()).Add(sp);
                }
            }
        }

        if (ch.Familiar < 0 && packages.GetInt(pkg, "Associate", -1) is int assoc and >= 0)
        {
            plan = ProgressionRules.Plan(w, c, ch, player, checkVariables: player);
            if (plan.PickFamiliar) ch.Familiar = assoc;
            if (plan.PickCompanion) ch.Companion = assoc;
        }
        return ch;
    }

    /// <summary>Placeholder rows modules leave in skills.2da ("{Not Used}" names) that the original's sheet hides.</summary>
    public static bool IsHiddenSkill(World w, int skill)
    {
        var name = ProgressionRules.SkillName(w, skill);
        return name.Length == 0 || name.StartsWith('{');
    }

    static IEnumerable<int> PrefList(World w, string table, string column)
    {
        if (table.Length == 0) yield break;
        var t = w.Resources.TwoDA(table.ToLowerInvariant());
        for (int r = 0; r < t.RowCount; r++)
            if (t.GetInt(r, column, -1) is int v and >= 0) yield return v;
    }

    static LevelChoice Clone(LevelChoice p) => new()
    {
        Class = p.Class, Ability = p.Ability, Skills = new(p.Skills), Feats = new(p.Feats),
        Spells = p.Spells.ToDictionary(k => k.Key, k => new List<int>(k.Value)),
        Domain1 = p.Domain1, Domain2 = p.Domain2, School = p.School, Familiar = p.Familiar, Companion = p.Companion,
        FamiliarName = p.FamiliarName, CompanionName = p.CompanionName, Package = p.Package,
    };

    /// <summary>
    /// LevelUpHenchman: one more level for an NPC, chosen from its package (nPackage, else its
    /// starting package, else the class default). Returns the new total level, or 0.
    /// </summary>
    public static int LevelUpNpc(World w, Creature c, int cls, int package, bool readyAllSpells)
    {
        CreatureRules.EnsureInit(w, c);
        if (cls == NwRules.ClassInvalid || cls < 0)
        {
            var main = c.Classes.OrderByDescending(k => k.Level).FirstOrDefault();
            if (main == null) return 0;
            cls = main.Class;
        }
        var ch = Complete(w, c, new LevelChoice { Class = cls, Package = package == 255 ? -1 : package }, player: false);
        // cls_pres_* VAR rows bar NPCs too (nwserver, parity suite prog_prereq: an unset variable that must be 1).
        var plan = ProgressionRules.Plan(w, c, ch, player: false);
        // Class errors (no such class, maximum level, alignment) stop the level; unfillable picks don't.
        if (plan.Errors.Any(e => e.Code is "class" or "alignment" or "prereq")) return 0;
        LevelApplier.Apply(w, c, ch, plan, ProgressionRules.HitDie(w, cls));
        if (readyAllSpells) CreatureRules.RestoreSpells(w, c);
        return c.TotalLevel;
    }
}
