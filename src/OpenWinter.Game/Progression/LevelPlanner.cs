using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>A skill as offered for one level: whether ranks can be bought, their cost and the rank cap after the level.</summary>
public sealed record SkillOption(int Skill, int Cost, int MaxRanks, int Current, bool ClassSkill);

/// <summary>New spells a casting class learns this level.</summary>
public sealed class SpellPick
{
    public int Class;
    /// <summary>Spells to pick per spell level (spontaneous casters, a wizard's first level).</summary>
    public readonly Dictionary<int, int> PerLevel = new();
    /// <summary>Spells to pick from any level up to <see cref="MaxLevel"/> (a wizard's later levels).</summary>
    public int AnyLevel;
    public int MaxLevel = -1;
    /// <summary>Learnable spells per level (not yet known, on the class list, not of an opposed school).</summary>
    public readonly Dictionary<int, List<int>> Choices = new();
    /// <summary>Spells learnt automatically (a wizard's cantrips).</summary>
    public readonly Dictionary<int, List<int>> Granted = new();
}

/// <summary>
/// The options and the outcome of one level for a creature: what can be chosen (skills, feats,
/// spells, domains, school, associate) and, for a given choice, the broken rules and the state after.
/// </summary>
public sealed class LevelPlan
{
    public int Class = -1, NewCharacterLevel, NewClassLevel;
    public int HitDie;
    public bool AbilityIncrease;
    public int SkillPointsCarried, SkillPointsGained, SkillPointsSpent;
    public int SkillPointsAvailable => SkillPointsCarried + SkillPointsGained;
    public int SkillPointsLeft => SkillPointsAvailable - SkillPointsSpent;
    public readonly List<SkillOption> Skills = new();
    public int GeneralFeatSlots, BonusFeatSlots;
    public readonly List<int> GrantedFeats = new();
    public readonly List<int> GeneralFeatChoices = new();
    public readonly List<int> BonusFeatChoices = new();
    public readonly List<SpellPick> Spells = new();
    public bool PickDomains, PickSchool, PickFamiliar, PickCompanion;
    public readonly List<RuleError> Errors = new();
    /// <summary>A copy of the creature after the level (not part of the world).</summary>
    public Creature After = new();
    public bool Ok => Errors.Count == 0;
}

public static partial class ProgressionRules
{
    /// <summary>
    /// Works out a level for a creature: options, and every rule the choice breaks. A partial choice
    /// (only the class, or class and ability) gives the options for the later steps; validation of
    /// the unfilled parts reports them as missing.
    /// </summary>
    /// <param name="player">Player rules (PlayerClass only; prerequisite variables checked).</param>
    /// <param name="carriedSkillPoints">Unspent skill points from earlier levels (-1: read the creature's SkillPoints).</param>
    public static LevelPlan Plan(World w, Creature c, LevelChoice ch, bool player = true, int carriedSkillPoints = -1, bool checkVariables = true)
    {
        CreatureRules.EnsureInit(w, c);
        var plan = new LevelPlan { Class = ch.Class };
        if (!ClassExists(w, ch.Class)) { plan.Errors.Add(new("class", "Choose a class.")); return plan; }
        plan.Errors.AddRange(ClassErrors(w, c, ch.Class, player, checkVariables));
        int cls = ch.Class;
        var s = Snapshot(c);
        plan.After = s;
        var cc = s.Classes.FirstOrDefault(x => x.Class == cls);
        if (cc == null) s.Classes.Add(cc = new CreatureClass { Class = cls });
        cc.Level++;
        plan.NewClassLevel = cc.Level;
        plan.NewCharacterLevel = s.TotalLevel;
        plan.HitDie = HitDie(w, cls);
        bool firstClassLevel = cc.Level == 1;

        // Ability increase every fourth character level.
        plan.AbilityIncrease = s.TotalLevel % 4 == 0;
        if (plan.AbilityIncrease)
        {
            if (ch.Ability is >= 0 and <= 5) s.Abilities[ch.Ability]++;
            else plan.Errors.Add(new("ability", "Choose an ability score to increase."));
        }
        else if (ch.Ability != -1) plan.Errors.Add(new("ability", "No ability increase at this level."));
        ApplyStatGain(w, s, cls, cc.Level);

        // First-level class options.
        var domains = w.Resources.TwoDA("domains");
        if (firstClassLevel && Classes(w).GetInt(cls, "PickDomains") == 1)
        {
            plan.PickDomains = true;
            bool Valid(int d) => d >= 0 && d < domains.RowCount && domains.Get(d, "Label").Length > 0 && domains.Get(d, "Name").Length > 0;
            if (!Valid(ch.Domain1) || !Valid(ch.Domain2)) plan.Errors.Add(new("domains", "Choose two domains."));
            else if (ch.Domain1 == ch.Domain2) plan.Errors.Add(new("domains", "Choose two different domains."));
            cc.Domain1 = ch.Domain1;
            cc.Domain2 = ch.Domain2;
        }
        if (firstClassLevel && Classes(w).GetInt(cls, "PickSchool") == 1)
        {
            plan.PickSchool = true;
            var schools = w.Resources.TwoDA("spellschools");
            if (ch.School < 0 || ch.School >= schools.RowCount || schools.Get(ch.School, "Label").Length == 0) plan.Errors.Add(new("school", "Choose a school of magic."));
            else cc.School = ch.School;
        }

        PlanSkills(w, c, s, ch, plan, carriedSkillPoints);
        PlanFeats(w, c, s, ch, plan, cc);
        PlanSpells(w, s, ch, plan, cc);

        // Familiar and animal companion types come with the feats that summon them, and can be chosen again at every
        // level of a class that grants the feat (the original's level-up offers a wizard 2 the familiar again: menu
        // capture; a cleric level of the same character doesn't).
        plan.PickFamiliar = AssociatePick(w, c, s, cls, cc.Level, NwRules.FeatSummonFamiliar);
        plan.PickCompanion = AssociatePick(w, c, s, cls, cc.Level, NwRules.FeatAnimalCompanion);
        if (plan.PickFamiliar && ch.Familiar >= 0 && (ch.Familiar >= w.Resources.TwoDA("hen_familiar").RowCount || w.Resources.TwoDA("hen_familiar").Get(ch.Familiar, "BASERESREF").Length == 0))
            plan.Errors.Add(new("familiar", "That familiar is not available."));
        if (plan.PickCompanion && ch.Companion >= 0 && (ch.Companion >= w.Resources.TwoDA("hen_companion").RowCount || w.Resources.TwoDA("hen_companion").Get(ch.Companion, "BASERESREF").Length == 0))
            plan.Errors.Add(new("companion", "That animal companion is not available."));
        return plan;
    }

    static bool AssociatePick(World w, Creature before, Creature after, int cls, int classLevel, int feat)
    {
        if (!after.Feats.Contains(feat)) return false;
        if (!before.Feats.Contains(feat)) return true;
        return ClassFeats(w, cls).TryGetValue(feat, out var f) && f.Granted > 0 && f.Granted <= classLevel;
    }

    /// <summary>Validates a whole level choice (<see cref="Plan"/> with every step filled in).</summary>
    public static List<RuleError> Validate(World w, Creature c, LevelChoice ch, bool player = true) => Plan(w, c, ch, player).Errors;

    /// <summary>cls_stat_* (classes.2da StatGainTable): ability and natural armour gains at a class level.</summary>
    internal static void ApplyStatGain(World w, Creature c, int cls, int classLevel)
    {
        var t = CreatureRules.ClassTable(w, cls, "StatGainTable");
        if (classLevel < 1 || classLevel > t.RowCount) return;
        for (int a = 0; a < 6; a++) c.Abilities[a] += t.GetInt(classLevel - 1, AbilityColumns[a]);
        c.NaturalAC += t.GetInt(classLevel - 1, "NaturalAC");
    }

    static void PlanSkills(World w, Creature before, Creature s, LevelChoice ch, LevelPlan plan, int carried)
    {
        int cls = ch.Class;
        int rows = SkillTable(w).RowCount;
        if (s.Skills.Length < rows) Array.Resize(ref s.Skills, rows);
        plan.SkillPointsCarried = carried >= 0 ? carried : before.TotalLevel == 0 ? 0 : (int)before.Template.GetInt("SkillPoints");
        plan.SkillPointsGained = SkillPointsGained(w, s, cls);
        for (int sk = 0; sk < rows; sk++)
        {
            if (!SkillExists(w, sk)) continue;
            bool canBuy = CanBuySkill(w, cls, sk);
            bool classSkill = IsClassSkill(w, cls, sk);
            if (canBuy) plan.Skills.Add(new SkillOption(sk, classSkill ? 1 : 2, MaxRanks(w, s, sk), s.Skills[sk], classSkill));
        }
        int spent = 0;
        foreach (var (sk, ranks) in ch.Skills.OrderBy(k => k.Key))
        {
            if (ranks == 0) continue;
            var opt = plan.Skills.FirstOrDefault(o => o.Skill == sk);
            if (ranks < 0) { plan.Errors.Add(new("skills", $"Skill ranks can't be removed ({SkillName(w, sk)}).")); continue; }
            if (opt == null) { plan.Errors.Add(new("skills", $"{SkillName(w, sk)} can't be learnt by a {ClassName(w, cls)}.")); continue; }
            if (opt.Current + ranks > opt.MaxRanks) plan.Errors.Add(new("skills", $"{SkillName(w, sk)}: at most {opt.MaxRanks} ranks at this level."));
            spent += ranks * opt.Cost;
            s.Skills[sk] += ranks;
        }
        plan.SkillPointsSpent = spent;
        if (spent > plan.SkillPointsAvailable) plan.Errors.Add(new("skills", $"{spent} skill points spent but only {plan.SkillPointsAvailable} available."));
    }

    static void PlanFeats(World w, Creature before, Creature s, LevelChoice ch, LevelPlan plan, CreatureClass cc)
    {
        int cls = ch.Class;
        bool firstLevel = s.TotalLevel == 1;
        foreach (var f in GrantedFeats(w, s.Race, cls, cc.Level, firstLevel, plan.PickDomains ? cc.Domain1 : -1, plan.PickDomains ? cc.Domain2 : -1))
            if (!s.Feats.Contains(f)) { plan.GrantedFeats.Add(f); s.Feats.Add(f); }
        plan.GeneralFeatSlots = GeneralFeatSlots(w, s.Race, s.TotalLevel);
        plan.BonusFeatSlots = BonusFeatSlots(w, cls, cc.Level);

        var feats = FeatTable(w);
        var have = new HashSet<int>(s.Feats);
        // Spell-level requirements look at the character before a later level (prog_feats: a fighter's first wizard
        // level takes no Combat Casting), at a new character as it will be (a first-level wizard can: menu capture c27).
        var spellsFrom = before.TotalLevel > 0 ? before : null;
        var chosen = new List<int>();
        foreach (var f in ch.Feats)
        {
            if (chosen.Contains(f)) { plan.Errors.Add(new("feats", $"{FeatName(w, f)} chosen twice.")); continue; }
            chosen.Add(f);
        }
        var withChosen = new HashSet<int>(have);
        withChosen.UnionWith(chosen);

        // Options (prerequisites may be met by feats picked in the same level).
        if (plan.GeneralFeatSlots + plan.BonusFeatSlots > 0)
            for (int f = 0; f < feats.RowCount; f++)
            {
                if (!FeatExists(w, f) || (have.Contains(f) && feats.GetInt(f, "GAINMULTIPLE") != 1)) continue;
                bool asGeneral = plan.GeneralFeatSlots > 0 && IsGeneralFeat(w, s, f);
                bool asBonus = plan.BonusFeatSlots > 0 && IsBonusFeat(w, cls, f, cc.Level);
                if (!asGeneral && !asBonus) continue;
                var others = new HashSet<int>(withChosen);
                others.Remove(f);
                if (FeatPrerequisiteErrors(w, s, f, others, spellsFrom).Count > 0) continue;
                if (asGeneral) plan.GeneralFeatChoices.Add(f);
                if (asBonus) plan.BonusFeatChoices.Add(f);
            }

        int onlyGeneral = 0, onlyBonus = 0, either = 0;
        foreach (var f in chosen)
        {
            if (!FeatExists(w, f)) { plan.Errors.Add(new("feats", $"Feat {f} does not exist.")); continue; }
            if (have.Contains(f) && feats.GetInt(f, "GAINMULTIPLE") != 1) { plan.Errors.Add(new("feats", $"You already have {FeatName(w, f)}.")); continue; }
            bool isGeneral = IsGeneralFeat(w, s, f), isBonus = IsBonusFeat(w, cls, f, cc.Level);
            if (!isGeneral && !isBonus) { plan.Errors.Add(new("feats", $"{FeatName(w, f)} can't be chosen by a {ClassName(w, cls)}.")); continue; }
            var others = new HashSet<int>(withChosen);
            others.Remove(f);
            plan.Errors.AddRange(FeatPrerequisiteErrors(w, s, f, others, spellsFrom));
            if (isGeneral && isBonus) either++;
            else if (isGeneral) onlyGeneral++;
            else onlyBonus++;
            s.Feats.Add(f);
        }
        int general = plan.GeneralFeatSlots, bonusSlots = plan.BonusFeatSlots;
        if (onlyGeneral > general) plan.Errors.Add(new("feats", $"Only {general} general feat(s) can be chosen at this level."));
        if (onlyBonus > bonusSlots) plan.Errors.Add(new("feats", $"Only {bonusSlots} {ClassName(w, cls)} bonus feat(s) can be chosen at this level."));
        if (onlyGeneral + onlyBonus + either > general + bonusSlots) plan.Errors.Add(new("feats", $"Too many feats: {general + bonusSlots} can be chosen at this level."));
        else
        {
            // Unused slots are only allowed when nothing is left to pick for them.
            int bonusUsed = onlyBonus + Math.Min(either, Math.Max(0, bonusSlots - onlyBonus));
            int generalUsed = onlyGeneral + (either - Math.Min(either, Math.Max(0, bonusSlots - onlyBonus)));
            bool generalLeft = plan.GeneralFeatChoices.Any(f => !withChosen.Contains(f));
            bool bonusLeft = plan.BonusFeatChoices.Any(f => !withChosen.Contains(f));
            if (generalUsed < general && generalLeft) plan.Errors.Add(new("feats-unspent", $"Choose {general - generalUsed} more general feat(s)."));
            if (bonusUsed < bonusSlots && bonusLeft) plan.Errors.Add(new("feats-unspent", $"Choose {bonusSlots - bonusUsed} more bonus feat(s)."));
        }
    }

    static void PlanSpells(World w, Creature s, LevelChoice ch, LevelPlan plan, CreatureClass cc)
    {
        int cls = ch.Class;
        if (!CreatureRules.IsSpellCaster(w, cls)) { RejectSpells(w, ch, plan); return; }
        var pick = new SpellPick { Class = cls };
        if (HasKnownTable(w, cls))
        {
            for (int lvl = 0; lvl < 10; lvl++)
            {
                int need = KnownCount(w, cls, cc.Level, lvl) - KnownCount(w, cls, cc.Level - 1, lvl);
                if (need <= 0) continue;
                var options = ClassSpells(w, cls, lvl).Where(sp => !cc.KnownSpells[lvl].Contains(sp)).ToList();
                pick.PerLevel[lvl] = Math.Min(need, options.Count);
                pick.Choices[lvl] = options;
            }
        }
        else if (HasSpellbook(w, cls))
        {
            int school = cc.School;
            if (cc.Level == 1)
            {
                var cantrips = ClassSpells(w, cls, 0, school).Where(sp => !cc.KnownSpells[0].Contains(sp)).ToList();
                if (cantrips.Count > 0) pick.Granted[0] = cantrips;
                int ability = NwRules.AbilityFromLabel(Classes(w).Get(cls, "SpellcastingAbil"));
                int mod = ability >= 0 ? CreatureRules.Modifier(CreatureRules.AbilityScore(w, s, ability, baseOnly: true)) : 0;
                var options = ClassSpells(w, cls, 1, school).Where(sp => !cc.KnownSpells[1].Contains(sp)).ToList();
                pick.PerLevel[1] = Math.Min(Math.Max(0, 3 + mod), options.Count);
                pick.Choices[1] = options;
            }
            else
            {
                pick.MaxLevel = MaxSpellLevel(w, cls, cc.Level);
                int available = 0;
                for (int lvl = 0; lvl <= pick.MaxLevel; lvl++)
                {
                    var options = ClassSpells(w, cls, lvl, school).Where(sp => !cc.KnownSpells[lvl].Contains(sp)).ToList();
                    if (options.Count == 0) continue;
                    pick.Choices[lvl] = options;
                    available += options.Count;
                }
                pick.AnyLevel = Math.Min(2, available);
            }
        }
        else { RejectSpells(w, ch, plan); return; }
        if (pick.PerLevel.Count == 0 && pick.AnyLevel == 0 && pick.Granted.Count == 0) { RejectSpells(w, ch, plan); return; }
        plan.Spells.Add(pick);

        int anyPicked = 0;
        var seen = new HashSet<int>();
        foreach (var (lvl, list) in ch.Spells)
        {
            var options = pick.Choices.GetValueOrDefault(lvl) ?? new List<int>();
            foreach (var sp in list)
            {
                if (!seen.Add(sp)) { plan.Errors.Add(new("spells", $"{SpellName(w, sp)} chosen twice.")); continue; }
                if (!options.Contains(sp)) plan.Errors.Add(new("spells", $"{SpellName(w, sp)} can't be learnt as a level {lvl} spell now."));
            }
            if (pick.AnyLevel > 0) anyPicked += list.Count;
            else if (list.Count > pick.PerLevel.GetValueOrDefault(lvl)) plan.Errors.Add(new("spells", $"Too many level {lvl} spells: {pick.PerLevel.GetValueOrDefault(lvl)} can be learnt."));
            cc.KnownSpells[lvl].AddRange(list.Where(sp => options.Contains(sp) && !cc.KnownSpells[lvl].Contains(sp)));
        }
        foreach (var (lvl, n) in pick.PerLevel)
        {
            int got = ch.Spells.GetValueOrDefault(lvl)?.Count ?? 0;
            if (got < n) plan.Errors.Add(new("spells", $"Choose {n - got} more level {lvl} spell(s)."));
        }
        if (pick.AnyLevel > 0 && anyPicked != pick.AnyLevel)
            plan.Errors.Add(new("spells", anyPicked < pick.AnyLevel ? $"Choose {pick.AnyLevel - anyPicked} more spell(s) for your spellbook." : $"Only {pick.AnyLevel} new spells can be learnt."));
        foreach (var (lvl, list) in pick.Granted) cc.KnownSpells[lvl].AddRange(list);
    }

    static void RejectSpells(World w, LevelChoice ch, LevelPlan plan)
    {
        if (ch.Spells.Values.Any(l => l.Count > 0)) plan.Errors.Add(new("spells", $"No new spells are learnt at this level."));
    }

    /// <summary>A detached copy of the rules state the level-up rules read and change.</summary>
    internal static Creature Snapshot(Creature c)
    {
        var s = new Creature
        {
            Race = c.Race, Subrace = c.Subrace, LawfulChaotic = c.LawfulChaotic, GoodEvil = c.GoodEvil,
            NaturalAC = c.NaturalAC, Experience = c.Experience, RulesInitialised = true,
            StartingPackage = c.StartingPackage, FamiliarType = c.FamiliarType, CompanionType = c.CompanionType,
        };
        Array.Copy(c.Abilities, s.Abilities, 6);
        foreach (var cc in c.Classes)
        {
            var copy = new CreatureClass { Class = cc.Class, Level = cc.Level, Domain1 = cc.Domain1, Domain2 = cc.Domain2, School = cc.School };
            for (int l = 0; l < 10; l++) copy.KnownSpells[l].AddRange(cc.KnownSpells[l]);
            s.Classes.Add(copy);
        }
        s.Feats.AddRange(c.Feats);
        s.Skills = (int[])c.Skills.Clone();
        foreach (var (k, v) in c.Locals.Ints) s.Locals.Ints[k] = v;
        return s;
    }
}
