using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>Character creation and level-up against the module's own 2DAs (haks first).</summary>
public class ProgressionTests
{
    readonly ITestOutputHelper _out;
    public ProgressionTests(ITestOutputHelper output) { _out = output; }

    const string RealBic = "aaalfondodreanto.bic";

    static World NewWorld() => new(TestData.Module);

    /// <summary>A legal point buy (primary ability high, then CON, the rest spread), a portrait and an allowed alignment, completed from the class package.</summary>
    internal static NewCharacter Spec(World w, int race, int cls, int gender = 0, string name = "Testa")
    {
        var n = new NewCharacter { FirstName = name, LastName = "Builder", Race = race, Gender = gender, Level = new LevelChoice { Class = cls } };
        var classes = w.Resources.TwoDA("classes");
        int primary = NwRules.AbilityFromLabel(classes.Get(cls, "PrimaryAbil"));
        int budget = ProgressionRules.PointBuyBudget(w, race);
        var ab = Enumerable.Repeat(ProgressionRules.AbilityMin(w), 6).ToArray();
        void Raise(int a, int to) { while (ab[a] < to && ProgressionRules.PointBuyTotal(w, ab.Select((v, i) => i == a ? v + 1 : v).ToArray()) <= budget) ab[a]++; }
        Raise(primary, 16);
        Raise(NwRules.AbilityCon, 14);
        int spell = NwRules.AbilityFromLabel(classes.Get(cls, "SpellcastingAbil"));
        if (spell >= 0) Raise(spell, 14);
        for (int round = 0; round < 6; round++) for (int a = 0; a < 6; a++) Raise(a, 13 + round / 3);
        n.Abilities = ab;
        var portraits = w.Resources.TwoDA("portraits");
        for (int r = 0; r < portraits.RowCount && n.PortraitId < 0; r++)
            if (portraits.GetInt(r, "Race", -1) == race && portraits.GetInt(r, "Sex", -1) == gender && portraits.Get(r, "BaseResRef").Length > 0) n.PortraitId = r;
        if (n.PortraitId < 0) n.Portrait = "po_hu_m_01_";
        foreach (var (lc, ge) in new[] { (85, 85), (50, 50), (85, 50), (15, 50), (85, 15), (50, 85) })
            if (ProgressionRules.AlignmentAllowed(w, cls, lc, ge)) { n.LawChaos = lc; n.GoodEvil = ge; break; }
        n.Level = PackageBuilder.Complete(w, ChargenRequests.Blank(w, n), n.Level);
        return n;
    }

    static Creature Load(World w, GffStruct bic) => w.Creatures.FromTemplate(Gff.Read(Gff.Write(bic, "BIC ")));

    [DataFact]
    public void BuildsLegalFirstLevelCharactersThatLoadBackIdentically()
    {
        var w = NewWorld();
        var cases = new (int Race, int Class)[] { (6, 4), (1, 10), (0, 2), (3, 1), (5, 0), (2, 9), (4, 8), (6, 3), (6, 5), (6, 6), (6, 7) };
        foreach (var (race, cls) in cases)
        {
            var n = Spec(w, race, cls);
            var result = CharacterFactory.Build(w, n);
            _out.WriteLine($"{ProgressionRules.RaceName(w, race)} {ProgressionRules.ClassName(w, cls)}: abilities [{string.Join(",", n.Abilities)}] skills {n.Level.Skills.Count} feats [{string.Join(",", n.Level.Feats)}] spells {n.Level.Spells.Sum(s => s.Value.Count)} errors: {string.Join("; ", result.Errors)}");
            Assert.True(result.Ok, string.Join("; ", result.Errors));
            var plan = result.Plan;
            var c = Load(w, result.Bic!);
            CreatureRules.EnsureInit(w, c);

            Assert.Equal(1, c.TotalLevel);
            Assert.Equal(cls, c.Classes[0].Class);
            for (int a = 0; a < 6; a++)
            {
                Assert.Equal(n.Abilities[a], c.Abilities[a]); // stored without racial adjustments
                Assert.Equal(CreatureRules.AbilityScore(w, plan.After, a, baseOnly: true), CreatureRules.AbilityScore(w, c, a, baseOnly: true));
            }
            for (int sk = 0; sk < c.Skills.Length; sk++) Assert.Equal(n.Level.SkillRanks(sk), c.Skills[sk]);
            Assert.Equal(plan.GrantedFeats.Concat(n.Level.Feats).OrderBy(f => f), c.Feats.OrderBy(f => f));
            int hd = ProgressionRules.HitDie(w, cls);
            Assert.Equal(Math.Max(1, hd + CreatureRules.HitPointBonus(w, c)), CreatureRules.MaxHitPoints(w, c));
            Assert.Equal(CreatureRules.MaxHitPoints(w, c), c.CurrentHitPoints);
            Assert.Equal(hd, c.BaseHitPoints);
            Assert.Equal(CreatureRules.BaseAttackBonus(w, plan.After), CreatureRules.BaseAttackBonus(w, c));
            for (int save = 1; save <= 3; save++) Assert.Equal(CreatureRules.BaseSave(w, plan.After, save), CreatureRules.BaseSave(w, c, save));
            for (int lvl = 0; lvl < 10; lvl++) Assert.Equal(plan.After.Classes[0].KnownSpells[lvl], c.Classes[0].KnownSpells[lvl]);
            Assert.Equal(n.LawChaos, c.LawfulChaotic);
            Assert.Equal(n.GoodEvil, c.GoodEvil);
            Assert.StartsWith("po_", c.Portrait);
            Assert.NotEmpty(c.Inventory);
            var history = c.Template.GetList("LvlStatList");
            Assert.Single(history);
            Assert.Equal(cls, (int)history[0].GetInt("LvlStatClass"));
            Assert.Equal(hd, (int)history[0].GetInt("LvlStatHitDie"));
            // The rebuilt character is legal by the uploaded-character check too.
            Assert.Empty(LegalCharacterCheck.Check(w, Gff.Read(Gff.Write(result.Bic!, "BIC "))));
        }
    }

    /// <summary>The written BIC has the fields and layout of a character made by the original game.</summary>
    [DataFact]
    public void BicLayoutMatchesTheOriginal()
    {
        var path = Path.Combine(TestData.Config!.UserRoot, "localvault", RealBic);
        if (!File.Exists(path)) { _out.WriteLine("reference character missing"); return; }
        var real = Gff.Read(File.ReadAllBytes(path)); // read-only
        var w = NewWorld();
        var result = CharacterFactory.Build(w, Spec(w, 6, 4));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        var ours = Gff.Read(Gff.Write(result.Bic!, "BIC "));

        foreach (var f in real.Fields)
        {
            var mine = ours.Field(f.Label);
            Assert.True(mine != null, $"missing field {f.Label}");
            Assert.True(mine!.Type == f.Type, $"{f.Label}: {mine.Type} vs original {f.Type}");
        }
        var realEntry = real.GetList("LvlStatList")[0];
        var ourEntry = ours.GetList("LvlStatList")[0];
        Assert.Equal(realEntry.Fields.Select(f => (f.Label, f.Type)), ourEntry.Fields.Select(f => (f.Label, f.Type)));
        Assert.Equal(w.Resources.TwoDA("skills").RowCount, ourEntry.GetList("SkillList").Count);
        Assert.All(ourEntry.GetList("SkillList"), s => Assert.Equal(GffType.Byte, s.Field("Rank")!.Type));
        Assert.All(ourEntry.GetList("FeatList"), s => Assert.Equal(GffType.Word, s.Field("Feat")!.Type));
        Assert.Equal(GffType.Word, ours.GetList("FeatList")[0].Field("Feat")!.Type);
        Assert.Equal("nw_cloth001", ours.GetList("Equip_ItemList").Single().GetString("EquippedRes"));
        Assert.All(ours.GetList("ItemList"), i => Assert.NotEmpty(i.GetString("InventoryRes")));

        // A caster's level entry carries its new spells like the original's (KnownList<n> of Spell words).
        var wiz = CharacterFactory.Build(w, Spec(w, 1, 10));
        Assert.True(wiz.Ok, string.Join("; ", wiz.Errors));
        var wizEntry = wiz.Bic!.GetList("LvlStatList")[0];
        Assert.NotEmpty(wizEntry.GetList("KnownList0"));
        Assert.NotEmpty(wizEntry.GetList("KnownList1"));
        Assert.All(wizEntry.GetList("KnownList1"), s => Assert.Equal(GffType.Word, s.Field("Spell")!.Type));
        int cantrips = ProgressionRules.ClassSpells(w, 10, 0).Count();
        Assert.Equal(cantrips, wiz.Bic.GetList("ClassList")[0].GetList("KnownList0").Count);
        int intMod = CreatureRules.Modifier(Spec(w, 1, 10).Abilities[3] + ProgressionRules.RacialAdjustment(w, 1, 3));
        Assert.Equal(3 + intMod, wiz.Bic.GetList("ClassList")[0].GetList("KnownList1").Count);
    }

    [DataFact]
    public void IllegalBuildsAreRejectedWithReasons()
    {
        var w = NewWorld();
        List<RuleError> Errors(Action<NewCharacter> spoil, int race = 6, int cls = 4)
        {
            var n = Spec(w, race, cls);
            spoil(n);
            var errs = CharacterFactory.Validate(w, n).Errors;
            _out.WriteLine(string.Join(" | ", errs));
            return errs;
        }
        Assert.Contains(Errors(n => n.Abilities = new[] { 18, 18, 18, 18, 18, 18 }), e => e.Code == "abilities");
        Assert.Contains(Errors(n => n.Abilities[1] = 19), e => e.Code == "abilities");
        Assert.Contains(Errors(n => n.Abilities[1] = 6), e => e.Code == "abilities");
        Assert.Contains(Errors(n => n.Race = 8), e => e.Code == "race"); // Animal: not a player race
        Assert.Contains(Errors(n => n.FirstName = ""), e => e.Code == "name");
        Assert.Contains(Errors(n => n.Level.Skills[3] += 20), e => e.Code == "skills");
        Assert.Contains(Errors(n => n.Level.Feats.Clear()), e => e.Code == "feats-unspent");
        Assert.Contains(Errors(n => n.Level.Feats.Add(n.Level.Feats.Count > 0 ? n.Level.Feats[0] : 0)), e => e.Code == "feats");
        // A feat with an unmet prerequisite: Mobility without Dodge, or Cleave without Power Attack.
        var feats = w.Resources.TwoDA("feat");
        int needsPrereq = Enumerable.Range(0, feats.RowCount).First(f => feats.GetInt(f, "PREREQFEAT1", -1) >= 0 && feats.GetInt(f, "ALLCLASSESCANUSE") == 1
            || feats.GetInt(f, "PREREQFEAT1", -1) is 10 or 28);
        Assert.Contains(Errors(n => { n.Level.Feats.Clear(); n.Level.Feats.Add(needsPrereq); }), e => e.Code is "feat-prereq" or "feats");
        // Prestige classes can't be taken at first level.
        Assert.Contains(Errors(n => n.Level = new LevelChoice { Class = 27 }), e => e.Code == "class");
        // Clerics must pick two different domains.
        Assert.Contains(Errors(n => n.Level.Domain2 = n.Level.Domain1, race: 0, cls: 2), e => e.Code == "domains");
        // Spells: a sorcerer can't learn a spell off its list, nor too many.
        Assert.Contains(Errors(n => n.Level.Spells[1] = new List<int> { 0, 1, 2, 3, 4 }, race: 2, cls: 9), e => e.Code == "spells");
        // Point buy within budget and the class's primary ability minimum.
        Assert.Contains(Errors(n => { n.Abilities = new[] { 8, 8, 8, 8, 8, 8 }; }), e => e.Code == "abilities" && e.Message.Contains("primary"));
    }

    [DataFact]
    public void AlignmentRestrictionsFollowTheClassTable()
    {
        var w = NewWorld();
        var classes = w.Resources.TwoDA("classes");
        // Read whatever the module says and check the flags round-trip through the rule.
        for (int cls = 0; cls < classes.RowCount; cls++)
        {
            int restrict = classes.GetInt(cls, "AlignRestrict"), type = classes.GetInt(cls, "AlignRstrctType");
            if (restrict == 0 || type == 0) continue;
            int allowed = 0;
            foreach (var lc in new[] { 85, 50, 15 }) foreach (var ge in new[] { 85, 50, 15 }) if (ProgressionRules.AlignmentAllowed(w, cls, lc, ge)) allowed++;
            _out.WriteLine($"{classes.Get(cls, "Label")}: restrict 0x{restrict:x} type {type} invert {classes.GetInt(cls, "InvertRestrict")} -> {allowed}/9 alignments");
            Assert.InRange(allowed, 1, 8);
        }
        // Standard semantics: assassins (0x09 neutral|good on the good/evil axis) must be evil.
        Assert.True(ProgressionRules.AlignmentAllowed(w, 30, 50, 15) || classes.GetInt(30, "AlignRestrict") != 0x09);
        if (classes.GetInt(30, "AlignRestrict") == 0x09) Assert.False(ProgressionRules.AlignmentAllowed(w, 30, 50, 85));
    }

    sealed class Fixture
    {
        public required World World;
        public required Creature Pc;
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    static Fixture LevelFixture(int race = 6, int cls = 4)
    {
        var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
        rm.Add(new DirectorySource(ScriptsDir()));
        var w = new World(rm);
        w.EnableScripts();
        w.Register(w.Module);
        w.Module.Scripts["Mod_OnPlrLvlUp"] = "prog_lvlup";
        var result = CharacterFactory.Build(w, Spec(w, race, cls));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        var pc = Load(w, result.Bic!);
        w.Register(pc);
        return new Fixture { World = w, Pc = pc };
    }

    [DataFact]
    public void OversizedSkillRanksAreRefused()
    {
        var f = LevelFixture();
        var w = f.World;
        var c = f.Pc;
        c.Experience = CreatureRules.XPForLevel(w, 2);
        var ch = PackageBuilder.Complete(w, c, new LevelChoice { Class = 4 });
        // Two skills the character already has a rank in: adding int.MaxValue to each would wrap both the rank
        // limit and the points spent.
        var skills = ProgressionRules.Plan(w, c, ch).Skills.Take(2).Select(o => o.Skill).ToList();
        foreach (var sk in skills) { c.Skills[sk] = 1; ch.Skills[sk] = int.MaxValue; }
        Assert.Contains(LevelUpRequests.LevelUp(w, c, ch), e => e.Code == "skills");
        Assert.Equal(1, c.TotalLevel);
        Assert.All(skills, sk => Assert.Equal(1, c.Skills[sk]));
    }

    [DataFact]
    public void LevelUpAppliesAndFiresOnPlayerLevelUp()
    {
        var f = LevelFixture();
        var w = f.World;
        var c = f.Pc;
        int hpBefore = CreatureRules.MaxHitPoints(w, c);

        // Not enough experience yet.
        Assert.Contains(LevelUpRequests.LevelUp(w, c, new LevelChoice { Class = 4 }), e => e.Code == "xp");

        c.Experience = CreatureRules.XPForLevel(w, 4);
        // Level 2: package choices; an incomplete choice is refused with reasons.
        var partial = new LevelChoice { Class = 4, Skills = { [3] = 99 } };
        Assert.Contains(LevelUpRequests.LevelUp(w, c, partial), e => e.Code == "skills");
        Assert.Equal(1, c.TotalLevel);

        var ch = PackageBuilder.Complete(w, c, new LevelChoice { Class = 4 });
        var errors = LevelUpRequests.LevelUp(w, c, ch);
        Assert.Empty(errors);
        Assert.Equal(2, c.TotalLevel);
        Assert.Equal(1, w.Module.Locals.Ints.GetValueOrDefault("lvlup_count"));
        Assert.Equal(2, w.Module.Locals.Ints.GetValueOrDefault("lvlup_level"));
        Assert.Equal(c.Id, w.Module.Locals.Objects.GetValueOrDefault("lvlup_pc"));
        int hd = ProgressionRules.HitDie(w, 4);
        // Level 2 is below the module's MIN_LEVEL_FOR_MAX_HP: the full die.
        Assert.Equal(hpBefore + hd + CreatureRules.Modifier(CreatureRules.AbilityScore(w, c, 2)), CreatureRules.MaxHitPoints(w, c));
        Assert.Equal(2, c.Template.GetList("LvlStatList").Count);

        // Level 3, then level 4 needs an ability increase.
        Assert.Empty(LevelUpRequests.LevelUp(w, c, PackageBuilder.Complete(w, c, new LevelChoice { Class = 4 })));
        var noIncrease = PackageBuilder.Complete(w, c, new LevelChoice { Class = 4 });
        Assert.True(noIncrease.Ability >= 0);
        noIncrease.Ability = -1;
        Assert.Contains(LevelUpRequests.LevelUp(w, c, noIncrease), e => e.Code == "ability");
        int str = c.Abilities[0];
        Assert.Empty(LevelUpRequests.LevelUp(w, c, PackageBuilder.Complete(w, c, new LevelChoice { Class = 4, Ability = 0 })));
        Assert.Equal(str + 1, c.Abilities[0]);
        Assert.Equal(4, c.TotalLevel);
        Assert.Equal(3, w.Module.Locals.Ints["lvlup_count"]);

        // Saved and reloaded: the same character, still legal, and the history can be undone.
        var bic = Gff.Read(Gff.Write(ObjectSerializer.CreatureGff(w, c, 0xFFFFFFFF, asCharacter: true), "BIC "));
        var again = w.Creatures.FromTemplate(bic.DeepClone());
        Assert.Equal(4, again.TotalLevel);
        Assert.Equal(CreatureRules.MaxHitPoints(w, c), CreatureRules.MaxHitPoints(w, again));
        Assert.Equal(c.Feats.OrderBy(x => x), again.Feats.OrderBy(x => x));
        Assert.Equal(c.Skills.Take(w.Resources.TwoDA("skills").RowCount), again.Skills.Take(w.Resources.TwoDA("skills").RowCount));
        var elc = LegalCharacterCheck.Check(w, bic);
        Assert.True(elc.Count == 0, string.Join("; ", elc));
        Assert.True(CreatureRules.RemoveLastLevel(w, again));
        Assert.Equal(3, again.TotalLevel);
        Assert.Equal(str, again.Abilities[0]);
    }

    [DataFact]
    public void MulticlassAndSpellcasterLevelUps()
    {
        var f = LevelFixture(1, 10); // elf wizard
        var w = f.World;
        var c = f.Pc;
        c.Experience = CreatureRules.XPForLevel(w, 3);
        int known = c.Classes[0].KnownSpells.Sum(l => l.Count);
        Assert.Empty(LevelUpRequests.LevelUp(w, c, PackageBuilder.Complete(w, c, new LevelChoice { Class = 10 })));
        Assert.Equal(known + 2, c.Classes[0].KnownSpells.Sum(l => l.Count)); // a wizard learns two spells per level
        // Multiclass into a sorcerer: new known spells from the sorcerer table.
        var plan = ProgressionRules.Plan(w, c, PackageBuilder.Complete(w, c, new LevelChoice { Class = 9 }));
        Assert.True(plan.Ok, string.Join("; ", plan.Errors));
        Assert.True(plan.Spells.Single().PerLevel.Sum(k => k.Value) > 0);
        Assert.Empty(LevelUpRequests.LevelUp(w, c, PackageBuilder.Complete(w, c, new LevelChoice { Class = 9 })));
        Assert.Equal(2, c.Classes.Count);
        Assert.NotEmpty(c.Classes[1].KnownSpells[0]);
    }

    [DataFact]
    public void PrestigeClassPrerequisitesAreEnforced()
    {
        var w = NewWorld();
        const int shadowdancer = 27;
        var t = w.Resources.TwoDA(w.Resources.TwoDA("classes").Get(shadowdancer, "PreReqTable").ToLowerInvariant());
        Assert.True(t.RowCount > 0);
        var c = new Creature { Race = 6, RulesInitialised = true };
        c.Classes.Add(new CreatureClass { Class = 8, Level = 10 });
        c.Skills = new int[w.Resources.TwoDA("skills").RowCount];
        var before = ProgressionRules.PrerequisiteErrors(w, c, shadowdancer);
        _out.WriteLine(string.Join(" | ", before));
        Assert.NotEmpty(before);
        // Meet every requirement the table lists.
        for (int r = 0; r < t.RowCount; r++)
        {
            switch (t.Get(r, "ReqType").ToUpperInvariant())
            {
                case "SKILL": c.Skills[t.GetInt(r, "ReqParam1")] = t.GetInt(r, "ReqParam2"); break;
                case "FEAT": case "FEATOR": c.Feats.Add(t.GetInt(r, "ReqParam1")); break;
                case "VAR": c.Locals.Ints[t.Get(r, "ReqParam1")] = t.GetInt(r, "ReqParam2"); break;
            }
        }
        var after = ProgressionRules.PrerequisiteErrors(w, c, shadowdancer).Where(e => !e.Message.Contains("base attack")).ToList();
        Assert.Empty(after);
        // The module's variable bars the class.
        var varRow = Enumerable.Range(0, t.RowCount).FirstOrDefault(r => t.Get(r, "ReqType") == "VAR", -1);
        if (varRow >= 0)
        {
            c.Locals.Ints[t.Get(varRow, "ReqParam1")] = t.GetInt(varRow, "ReqParam2") + 1;
            Assert.Contains(ProgressionRules.PrerequisiteErrors(w, c, shadowdancer), e => e.Code == "prereq");
        }
        // Missing a skill rank fails again.
        c.Locals.Ints.Clear();
        int skillRow = Enumerable.Range(0, t.RowCount).First(r => t.Get(r, "ReqType") == "SKILL");
        c.Skills[t.GetInt(skillRow, "ReqParam1")]--;
        Assert.Contains(ProgressionRules.PrerequisiteErrors(w, c, shadowdancer), e => e.Message.Contains("ranks"));
        // Prestige classes are not offered at first level, and only PlayerClass rows go to players.
        var blank = new Creature { Race = 6, RulesInitialised = true };
        Assert.Contains(ProgressionRules.ClassErrors(w, blank, shadowdancer, player: false), e => e.Code == "class");
    }

    /// <summary>
    /// EE's SAVE rows in cls_pres_* tables (measured on nwserver, parity suite prog_prereq): ReqParam1 1/2/3 is
    /// Fortitude/Reflex/Will, compared with the class save plus feat bonuses (not abilities or effects); other save
    /// types and ALIGN rows are met.
    /// </summary>
    [DataFact]
    public void PrestigeSaveRequirementsCountClassSavesAndFeats()
    {
        // Its own resources: the shared test module must not see the authored table.
        var w = new World(ResMan.ForModule(TestData.Paths, TestData.Config!.Module));
        const int shadowdancer = 27;
        var source = new OpenWinter.Game.Persistence.MemorySource("prestige save rows");
        source.Set(ResKey.Of(w.Resources.TwoDA("classes").Get(shadowdancer, "PreReqTable").ToLowerInvariant(), ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(
            "2DA V2.0\n\nLABEL ReqType ReqParam1 ReqParam2\n0 Fort SAVE 1 8\n1 Ref SAVE 2 3\n2 Odd SAVE 0 99\n3 Align ALIGN 1 0\n"));
        w.Resources.AddFirst(source);
        var c = new Creature { Race = 6, RulesInitialised = true };
        c.Classes.Add(new CreatureClass { Class = 4, Level = 8 }); // fighter 8: Fort 6, Ref 2, Will 2
        c.Abilities[NwRules.AbilityCon] = 18;
        c.Skills = new int[w.Resources.TwoDA("skills").RowCount];
        Assert.Equal(6, CreatureRules.PrerequisiteSave(w, c, 1));
        var errors = ProgressionRules.PrerequisiteErrors(w, c, shadowdancer).Select(e => e.Message).ToList();
        _out.WriteLine(string.Join(" | ", errors));
        Assert.Equal(2, errors.Count); // Fortitude 6 < 8 (Constitution doesn't count), Reflex 2 < 3
        Assert.Contains(errors, e => e.Contains("Fortitude save of 8"));
        Assert.Contains(errors, e => e.Contains("Reflex save of 3"));
        c.Feats.Add(NwRules.FeatGreatFortitude);
        Assert.Equal(8, CreatureRules.PrerequisiteSave(w, c, 1));
        errors = ProgressionRules.PrerequisiteErrors(w, c, shadowdancer).Select(e => e.Message).ToList();
        Assert.Single(errors);
        Assert.Contains("Reflex", errors[0]);
    }

    [DataFact]
    public void TamperedCharactersFailTheLegalCheck()
    {
        var w = NewWorld();
        var result = CharacterFactory.Build(w, Spec(w, 6, 8));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        Assert.Empty(LegalCharacterCheck.Check(w, result.Bic!));
        var more = result.Bic!.DeepClone();
        more.GetList("SkillList")[5].SetByte("Rank", 30);
        Assert.Contains(LegalCharacterCheck.Check(w, more), e => e.Code == "skills");
        var feat = result.Bic!.DeepClone();
        feat.GetList("FeatList").Add(new GffStruct(1).SetWord("Feat", 40));
        Assert.Contains(LegalCharacterCheck.Check(w, feat), e => e.Code == "feats");
        var strong = result.Bic!.DeepClone();
        strong.SetByte("Str", 30);
        Assert.NotEmpty(LegalCharacterCheck.Check(w, strong));
    }

    [DataFact]
    public void LevelUpHenchmanUsesThePackage()
    {
        var w = NewWorld();
        var n = Spec(w, 6, 4);
        var result = CharacterFactory.Build(w, n);
        var npc = Load(w, result.Bic!);
        w.Register(npc);
        npc.Experience = CreatureRules.XPForLevel(w, 2);
        int feats = npc.Feats.Count, skills = npc.Skills.Sum();
        Assert.Equal(2, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, npc, 255, true, 255));
        Assert.Equal(2, npc.TotalLevel);
        Assert.True(npc.Skills.Sum() > skills || npc.Template.GetInt("SkillPoints") > 0);
        Assert.True(npc.Feats.Count >= feats);
        // Not enough experience for level 3.
        Assert.Equal(0, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, npc, 255, true, 255));
    }

    /// <summary>LevelUpHenchman works on player characters too: Hordes of the Underdark's "Automatic" level-up
    /// (bk_auto_level) levels a new PC to 15 with it, and the opening cutscene only starts at level 12 or more.</summary>
    [DataFact]
    public void LevelUpHenchmanLevelsPlayerCharacters()
    {
        var w = NewWorld();
        var result = CharacterFactory.Build(w, Spec(w, 6, 9)); // sorcerer
        var pc = Load(w, result.Bic!);
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        pc.Player = new PlayerSession { Connection = serverEnd };
        w.Register(pc);
        Assert.True(pc.IsPC);
        pc.Experience = 105000;
        for (int i = 1; i <= 14; i++) Assert.Equal(i + 1, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, pc, 255, true, 255));
        Assert.Equal(15, pc.TotalLevel);
        var sorc = pc.Classes.Single(k => k.Class == 9);
        Assert.Equal(15, sorc.Level);
        Assert.NotEmpty(sorc.KnownSpells[7]);
        // Experience still limits it.
        Assert.Equal(0, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, pc, 255, true, 255));
    }

    static List<(MessageType Type, MessageReader Reader)> Drain(OpenWinter.Net.IConnection client)
    {
        var list = new List<(MessageType, MessageReader)>();
        while (client.TryReceive(out var m)) { var r = new MessageReader(m); list.Add((r.Type, r)); }
        return list;
    }

    /// <summary>The selection screen offers "new character"; the server builds, saves (sandboxed local vault) and enters it.</summary>
    [DataFact]
    public void CreatingACharacterOverTheConnectionEntersTheGame()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", CharacterSelection = true, Vault = VaultMode.Local });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "ChargenTester", PlayerKey = "KEY" }.Write());
        server.Tick(1f / 30);
        var list = Drain(client).Where(m => m.Type == MessageType.CharacterList).Select(m => CharacterListMessage.Read(m.Reader)).Single();
        Assert.True(list.AllowNew);

        void Request(string kind, object data) => client.Send(new ClientRequestMessage { Kind = kind, Json = System.Text.Json.JsonSerializer.Serialize(data) }.Write());
        Request("chargen-data", new { });
        server.Tick(1f / 30);
        Assert.Contains(Drain(client), m => m.Type == MessageType.Notify && NotifyMessage.Read(m.Reader).Kind == "chargen-data");

        var n = Spec(world, 6, 4, name: "Conny");
        Request("chargen-validate", new { character = n.ToJson() });
        server.Tick(1f / 30);
        var plan = Drain(client).Where(m => m.Type == MessageType.Notify).Select(m => NotifyMessage.Read(m.Reader)).Single(m => m.Kind == "chargen-plan");
        Assert.Contains("\"ok\":true", plan.Json);

        Request("chargen-create", new { character = n.ToJson() });
        server.Tick(1f / 30);
        var result = Drain(client).Where(m => m.Type == MessageType.Notify).Select(m => NotifyMessage.Read(m.Reader)).Single(m => m.Kind == "chargen-result");
        _out.WriteLine(result.Json);
        Assert.Contains("\"ok\":true", result.Json);
        var pc = Assert.Single(world.AllObjects.OfType<Creature>(), c => c.IsPC);
        Assert.Equal("Conny Builder", pc.Name.Resolve(world.Resources.Talk));
        Assert.Equal(1, pc.TotalLevel);
        Assert.NotEmpty(pc.Inventory);
        // Saved to the sandboxed local vault, never the real one.
        var root = world.System<PersistenceSystem>().UserRoot;
        Assert.NotEqual(Path.GetFullPath(TestData.Config!.UserRoot), Path.GetFullPath(root));
        var file = Directory.GetFiles(Path.Combine(root, "localvault"), "connybuilder*.bic").OrderByDescending(File.GetLastWriteTimeUtc).First();
        Assert.Single(Gff.Read(File.ReadAllBytes(file)).GetList("LvlStatList"));

        // In game, the classic level-up window's requests: the data, the summary with the rolled hit points, the
        // level taken with that roll, and a familiar's record.
        pc.Experience = CreatureRules.XPForLevel(world, 2);
        Request("chargen-data", new { });
        Request("levelup-data", new { });
        server.Tick(1f / 30);
        var kinds = Drain(client).Where(m => m.Type == MessageType.Notify).Select(m => NotifyMessage.Read(m.Reader)).ToList();
        Assert.Contains(kinds, m => m.Kind == "chargen-data");
        var data = kinds.Single(m => m.Kind == "levelup-data");
        Assert.Contains("\"lastClass\":4", data.Json);
        var choice = PackageBuilder.Complete(world, pc, new LevelChoice { Class = 4 });
        Request("levelup-data", new { choice = choice.ToJson(), summary = true });
        server.Tick(1f / 30);
        var summary = Drain(client).Where(m => m.Type == MessageType.Notify).Select(m => NotifyMessage.Read(m.Reader)).Single(m => m.Kind == "levelup-summary");
        int shown = System.Text.Json.JsonDocument.Parse(summary.Json).RootElement.GetProperty("hitPoints").GetInt32();
        int hpBefore = CreatureRules.MaxHitPoints(world, pc);
        Request("levelup-apply", new { choice = choice.ToJson() });
        server.Tick(1f / 30);
        Assert.Equal(2, pc.TotalLevel);
        Assert.Equal(shown, CreatureRules.MaxHitPoints(world, pc) - hpBefore);
        Request("chargen-associate", new { table = "hen_familiar", id = 0, level = 1 });
        server.Tick(1f / 30);
        var familiar = Drain(client).Where(m => m.Type == MessageType.Notify).Select(m => NotifyMessage.Read(m.Reader)).Single(m => m.Kind == "chargen-associate");
        Assert.Contains("\"sheet\"", familiar.Json);
    }

    [DataFact]
    public void EnforceLegalCharactersRejectsTamperedUploads()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", CharacterSelection = true, Vault = VaultMode.Local, EnforceLegalCharacters = true });
        var result = CharacterFactory.Build(world, Spec(world, 6, 4));
        var tampered = result.Bic!.DeepClone();
        tampered.GetList("SkillList")[3].SetByte("Rank", 40);
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Cheater" }.Write());
        server.Tick(1f / 30);
        client.Send(new CharacterChoiceMessage { Bic = Gff.Write(tampered, "BIC ") }.Write());
        server.Tick(1f / 30);
        Assert.DoesNotContain(world.AllObjects.OfType<Creature>(), c => c.IsPC);
        client.Send(new CharacterChoiceMessage { Bic = Gff.Write(result.Bic!, "BIC ") }.Write());
        server.Tick(1f / 30);
        Assert.Single(world.AllObjects.OfType<Creature>(), c => c.IsPC);
    }

    [DataFact]
    public void ChargenDataIsResolvedThroughTheHaks()
    {
        var w = NewWorld();
        var json = System.Text.Json.JsonSerializer.Serialize(ChargenData.Static(w));
        _out.WriteLine($"chargen-data: {json.Length / 1024} KB");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("races").GetArrayLength() >= 7);
        Assert.Contains(root.GetProperty("classes").EnumerateArray(), c => c.GetProperty("player").GetBoolean());
        // The module's custom skill list (e.g. "Athletics" replacing Discipline) comes through.
        Assert.Contains(root.GetProperty("skills").EnumerateArray(), s => s.GetProperty("name").GetString() == ProgressionRules.SkillName(w, 3));
        var portraits = System.Text.Json.JsonSerializer.Serialize(ChargenData.Portraits(w, 6, 0));
        Assert.Contains("po_", portraits);
        Assert.True(root.GetProperty("pointBuy").GetProperty("cost").GetArrayLength() > 5);
    }

    [DataFact]
    public void ChargenDataGivesFamiliarAndCompanionAppearances()
    {
        var w = NewWorld();
        var json = System.Text.Json.JsonSerializer.Serialize(ChargenData.Static(w));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var appearance = w.Resources.TwoDA("appearance");
        foreach (var (list, table) in new[] { ("familiars", "hen_familiar"), ("companions", "hen_companion") })
        {
            var entries = doc.RootElement.GetProperty(list).EnumerateArray().ToList();
            Assert.NotEmpty(entries);
            int withLooks = 0;
            foreach (var e in entries)
            {
                var a = e.GetProperty("appearance");
                if (a.ValueKind == System.Text.Json.JsonValueKind.Null) continue; // the module lists a blueprint it doesn't ship
                withLooks++;
                int type = a.GetProperty("type").GetInt32();
                Assert.True(appearance.Get(type, "RACE").Length > 0, $"{list} {e.GetProperty("name")}: appearance {type} has no model");
                Assert.Equal(4, a.GetProperty("colors").GetArrayLength());
                Assert.Equal(CreatureFactory.BodyPartFields.Length, a.GetProperty("bodyParts").GetArrayLength());
            }
            Assert.True(withLooks > 0, $"no {list} with a blueprint");
            // The blueprint is the one summoning creates at level 1 (BASERESREF + "01").
            var baseRef = w.Resources.TwoDA(table).Get(0, "BASERESREF").ToLowerInvariant();
            if (w.Resources.ReadGff(baseRef + "01", ResType.Utc) is { } utc)
            {
                var first = entries.First(e => e.GetProperty("id").GetInt32() == 0).GetProperty("appearance");
                Assert.Equal(baseRef + "01", first.GetProperty("resref").GetString());
                Assert.Equal((int)utc.GetInt("Appearance_Type"), first.GetProperty("type").GetInt32());
            }
        }
        Assert.Null(ChargenData.AssociateAppearance(w, "no_such_blueprint"));
    }
}
