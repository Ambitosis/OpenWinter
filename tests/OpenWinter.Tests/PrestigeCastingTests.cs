using OpenWinter.Game;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Prestige classes that advance spellcasting (classes.2da ArcSpellLvlMod / DivSpellLvlMod) raise the base class's
/// caster level and so its spells per day, but not its spells known: those follow the base class's own level
/// (parity suite prestige_casting).
/// </summary>
public class PrestigeCastingTests
{
    const int Sorcerer = 9, PaleMaster = 34;

    static int KnownGain(World w, int cls, int from, int to) =>
        Enumerable.Range(0, 10).Sum(l => ProgressionRules.KnownCount(w, cls, to, l) - ProgressionRules.KnownCount(w, cls, from, l));

    static (World, Creature) Sorcerer6()
    {
        var w = new World(TestData.Module);
        var result = CharacterFactory.Build(w, ProgressionTests.Spec(w, 6, Sorcerer));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        var c = w.Creatures.FromTemplate(Gff.Read(Gff.Write(result.Bic!, "BIC ")));
        w.Register(c);
        c.LawfulChaotic = c.GoodEvil = 50;
        c.Experience = CreatureRules.XPForLevel(w, 20);
        for (int i = 1; i < 6; i++) Assert.Equal(i + 1, OpenWinter.Game.Society.HenchmanLevels.LevelUp(w, c, Sorcerer, true, 255));
        return (w, c);
    }

    [DataFact]
    public void PaleMasterLevelsRaiseASorcerersSlotsButNotItsSpellsKnown()
    {
        var (w, c) = Sorcerer6();
        var sorc = c.Classes.Single(x => x.Class == Sorcerer);
        int known = sorc.KnownSpells.Sum(l => l.Count);
        int slots3 = CreatureRules.SpellSlots(w, c, Sorcerer, 3);
        for (int pm = 1; pm <= 3; pm++)
        {
            var ch = PackageBuilder.Complete(w, c, new LevelChoice { Class = PaleMaster }, player: false);
            var plan = ProgressionRules.Plan(w, c, ch, player: false);
            Assert.True(plan.Ok, string.Join("; ", plan.Errors));
            Assert.Empty(plan.Spells);
            LevelApplier.Apply(w, c, ch, plan, hitDie: 6);
        }
        Assert.Equal(8, CreatureRules.CasterLevel(w, c, Sorcerer)); // Pale Master 1 and 3 add a caster level each
        Assert.True(CreatureRules.SpellSlots(w, c, Sorcerer, 3) > slots3);
        Assert.Equal(known, sorc.KnownSpells.Sum(l => l.Count));

        // The next sorcerer level learns cls_spkn_sorc row 6 to 7, not the caster level's rows.
        var next = ProgressionRules.Plan(w, c, PackageBuilder.Complete(w, c, new LevelChoice { Class = Sorcerer }, player: false), player: false);
        Assert.True(next.Ok, string.Join("; ", next.Errors));
        Assert.Equal(KnownGain(w, Sorcerer, 6, 7), next.Spells.Single().PerLevel.Values.Sum());
    }
}
