using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class VisualEffectModelTests
{
    [DataFact]
    public void MediumOnlyPolymorphAlsoSelectsModelForTinyCreature()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var visuals = resources.TwoDA("visualeffects");
        // Native summonidentity applies this exact table row to a medium PC and a tiny bat.
        Assert.Equal("", visuals.Get(85, "Imp_Root_S_Node"));
        Assert.Equal("vim_magpoly", VisualEffectModel.Root(visuals, 85, "Imp", "S"));
    }

    [Fact]
    public void AuthoredSizeAndCessationOverridesWinWithoutInventingMissingModels()
    {
        var table = TwoDA.Parse(Encoding.ASCII.GetBytes("""
            2DA V2.0

            Imp_Root_S_Node Imp_Root_M_Node Imp_Root_L_Node Ces_Root_S_Node Ces_Root_M_Node
            0 small medium large **** end_medium
            1 **** **** **** **** ****
            """));
        Assert.Equal("small", VisualEffectModel.Root(table, 0, "Imp", "S"));
        Assert.Equal("large", VisualEffectModel.Root(table, 0, "Imp", "L"));
        Assert.Equal("end_medium", VisualEffectModel.Root(table, 0, "Ces", "S"));
        Assert.Equal("", VisualEffectModel.Root(table, 1, "Imp", "S"));
    }
}
