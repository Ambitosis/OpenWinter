using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>
/// Prestige classes that advance spellcasting (classes.2da ArcSpellLvlMod / DivSpellLvlMod) raise their base
/// class's caster level, spells per day and, for a spontaneous caster, its spells known.
/// </summary>
public class PrestigeCastingTests
{
    readonly ITestOutputHelper _out;
    public PrestigeCastingTests(ITestOutputHelper output) { _out = output; }

    const int Sorcerer = 9, Wizard = 10, PaleMaster = 34;

    static int KnownGain(World w, int cls, int from, int to) =>
        Enumerable.Range(0, 10).Sum(l => ProgressionRules.KnownCount(w, cls, to, l) - ProgressionRules.KnownCount(w, cls, from, l));

    /// <summary>A neutral human of <paramref name="cls"/> levelled to <paramref name="level"/>.</summary>
    static (World, Creature) Caster(int cls, int level)
    {
        var w = new World(TestData.Module);
        var result = CharacterFactory.Build(w, ProgressionTests.Spec(w, 6, cls));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        var c = w.Creatures.FromTemplate(Gff.Read(Gff.Write(result.Bic!, "BIC ")));
        w.Register(c);
        c.LawfulChaotic = c.GoodEvil = 50;
        c.Experience = CreatureRules.XPForLevel(w, 20);
        for (int i = 1; i < level; i++) Assert.Equal(i + 1, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, c, cls, true, 255));
        return (w, c);
    }

    static LevelPlan TakePaleMaster(World w, Creature c)
    {
        var ch = PackageBuilder.Complete(w, c, new LevelChoice { Class = PaleMaster }, player: false);
        var plan = ProgressionRules.Plan(w, c, ch, player: false);
        Assert.True(plan.Ok, string.Join("; ", plan.Errors));
        LevelApplier.Apply(w, c, ch, plan, hitDie: 6);
        return plan;
    }

    [DataFact]
    public void APaleMasterLevelTeachesItsSorcererNewSpellsKnown()
    {
        var (w, c) = Caster(Sorcerer, 6);
        var sorc = c.Classes.Single(x => x.Class == Sorcerer);
        int knownBefore = sorc.KnownSpells.Sum(l => l.Count);
        int slots3 = CreatureRules.SpellSlots(w, c, Sorcerer, 3);
        Assert.Equal(6, CreatureRules.CasterLevel(w, c, Sorcerer));

        // Pale Master 1: caster level 7, so the sorcerer learns cls_spkn_sorc row 7's new spells.
        var plan = TakePaleMaster(w, c);
        Assert.Equal(7, CreatureRules.CasterLevel(w, c, Sorcerer));
        int gain = KnownGain(w, Sorcerer, 6, 7);
        Assert.True(gain > 0);
        var pick = Assert.Single(plan.Spells);
        Assert.Equal(Sorcerer, pick.Class);
        Assert.Equal(gain, pick.PerLevel.Values.Sum());
        Assert.Equal(knownBefore + gain, sorc.KnownSpells.Sum(l => l.Count));
        Assert.True(CreatureRules.SpellSlots(w, c, Sorcerer, 3) > slots3);
        var entry = c.Template.GetList("LvlStatList")[^1];
        Assert.Equal(PaleMaster, (int)entry.GetInt("LvlStatClass"));
        Assert.Equal(gain, Enumerable.Range(0, 10).Sum(l => entry.GetList($"KnownList{l}").Count));

        // Pale Master 2 adds no caster level (ArcSpellLvlMod 2), so nothing new to learn.
        plan = TakePaleMaster(w, c);
        Assert.Equal(7, CreatureRules.CasterLevel(w, c, Sorcerer));
        Assert.Empty(plan.Spells);

        // A sorcerer level after them counts known spells from the caster level (8), not the class level (7).
        var ch = PackageBuilder.Complete(w, c, new LevelChoice { Class = Sorcerer }, player: false);
        plan = ProgressionRules.Plan(w, c, ch, player: false);
        Assert.True(plan.Ok, string.Join("; ", plan.Errors));
        Assert.Equal(KnownGain(w, Sorcerer, 7, 8), plan.Spells.Single().PerLevel.Values.Sum());

        // Undoing the Pale Master levels takes the spells back off the sorcerer.
        Assert.True(CreatureRules.RemoveLastLevel(w, c));
        Assert.True(CreatureRules.RemoveLastLevel(w, c));
        Assert.Equal(knownBefore, sorc.KnownSpells.Sum(l => l.Count));
        Assert.Equal(6, CreatureRules.CasterLevel(w, c, Sorcerer));
    }

    [DataFact]
    public void TheLegalCharacterCheckAcceptsSpellsAPrestigeLevelTaught()
    {
        var (w, c) = Caster(Sorcerer, 6);
        TakePaleMaster(w, c);
        var bic = Gff.Read(Gff.Write(ObjectSerializer.CreatureGff(w, c, 0xFFFFFFFF, asCharacter: true), "BIC "));
        var errors = LegalCharacterCheck.Check(w, bic).Where(e => e.Code is "spells" or "history").ToList();
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    [DataFact]
    public void AWizardsPaleMasterLevelOpensHigherSpellLevelsForItsNextWizardLevel()
    {
        var (w, c) = Caster(Wizard, 9);
        TakePaleMaster(w, c);
        Assert.Equal(10, CreatureRules.CasterLevel(w, c, Wizard));
        var ch = new LevelChoice { Class = Wizard };
        var plan = ProgressionRules.Plan(w, c, ch, player: false);
        // Wizard 10 with a Pale Master level casts as an 11th-level wizard: the book can take 6th-level spells.
        Assert.Equal(5, ProgressionRules.MaxSpellLevel(w, Wizard, 10));
        Assert.Equal(6, plan.Spells.Single().MaxLevel);
    }
}
