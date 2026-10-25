using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Spells a class package picks when an NPC levels (LevelUpHenchman), as the original picks them (parity suite
/// package_spell_picks): only spells on the package's list, in its order, and a spellbook only from the levels the
/// class could cast before the level.
/// </summary>
public class PackageSpellPickTests
{
    static (World, Creature) Npc(int cls)
    {
        var w = new World(TestData.Module);
        var result = CharacterFactory.Build(w, ProgressionTests.Spec(w, 6, cls));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        var c = w.Creatures.FromTemplate(Gff.Read(Gff.Write(result.Bic!, "BIC ")));
        w.Register(c);
        c.Experience = CreatureRules.XPForLevel(w, 20);
        return (w, c);
    }

    [DataFact]
    public void ASorcererLearnsOnlyItsPackagesSpellsInListOrder()
    {
        var (w, c) = Npc(NwRules.ClassSorcerer);
        for (int i = 1; i < 10; i++) Assert.Equal(i + 1, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, c, NwRules.ClassSorcerer, true, NwRules.ClassSorcerer));
        var known = c.Classes.Single().KnownSpells;
        // The sorcerer package's list (packspwiz1) holds four cantrips, so it stays at four.
        Assert.Equal(new[] { 37, 100, 144, 151 }, known[0]);
        Assert.Equal(new[] { 102, 174, 107, 86, 165 }, known[1]);
        Assert.Equal(new[] { 120, 115, 175, 90 }, known[2]);
        Assert.Equal(new[] { 58, 21, 176 }, known[3]);
        Assert.Equal(new[] { 172, 47 }, known[4]);
        Assert.Equal(new[] { 99 }, known[5]);
    }

    [DataFact]
    public void AWizardLearnsFromTheLevelsItCouldCastBeforeTheLevel()
    {
        var (w, c) = Npc(NwRules.ClassWizard);
        var book = c.Classes.Single().KnownSpells;
        for (int level = 2; level <= 10; level++)
        {
            var before = Enumerable.Range(0, 10).Select(l => book[l].ToList()).ToList();
            Assert.Equal(level, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, c, NwRules.ClassWizard, true, NwRules.ClassWizard));
            var learnt = Enumerable.Range(0, 10).SelectMany(l => book[l].Except(before[l]).Select(_ => l)).ToList();
            Assert.Equal(2, learnt.Count);
            Assert.All(learnt, l => Assert.Equal(ProgressionRules.MaxSpellLevel(w, NwRules.ClassWizard, level - 1), l));
        }
    }

    [DataFact]
    public void APlayerSorcererWithAScriptGrantedSpellStillLearnsTheRowDifference()
    {
        var (w, c) = Npc(NwRules.ClassSorcerer);
        var sorc = c.Classes.Single();
        // A cantrip granted outside levelling (a script or server plugin).
        int extra = ProgressionRules.ClassSpells(w, NwRules.ClassSorcerer, 0).First(sp => !sorc.KnownSpells[0].Contains(sp));
        sorc.KnownSpells[0].Add(extra);
        int row = ProgressionRules.KnownCount(w, NwRules.ClassSorcerer, 2, 0) - ProgressionRules.KnownCount(w, NwRules.ClassSorcerer, 1, 0);
        Assert.True(row > 0);
        var player = ProgressionRules.Plan(w, c, new LevelChoice { Class = NwRules.ClassSorcerer }, player: true);
        Assert.Equal(row, player.Spells.Single().PerLevel.GetValueOrDefault(0));
        // The package path fills up to the table's total, which the extra cantrip already reaches.
        var npc = ProgressionRules.Plan(w, c, new LevelChoice { Class = NwRules.ClassSorcerer }, player: false);
        Assert.Equal(0, npc.Spells.SingleOrDefault()?.PerLevel.GetValueOrDefault(0) ?? 0);
    }
}
