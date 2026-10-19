using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class SpellPresentationTests
{
    [DataFact]
    public void EveryInstalledMirvHasAPlayableModelAndItsAuthoredPath()
    {
        var rm = ResMan.ForClient(TestData.Paths, Array.Empty<string>(), "");
        var visuals = rm.TwoDA("visualeffects");
        var programs = rm.TwoDA("progfx");
        int count = 0;
        for (int i = 0; i < visuals.RowCount; i++)
        {
            int row = visuals.GetInt(i, "ProgFX_Impact", -1);
            if (programs.GetInt(row, "Type", -1) != 10) continue;
            var spec = Assert.IsType<SpellProjectile>(SpellProjectile.FromVisual(visuals, programs, i));
            Assert.NotNull(rm.Read(spec.Model, ResType.Mdl));
            Assert.Contains(spec.Path, new[] { "homing", "burstup" });
            Assert.True(float.IsFinite(spec.FlightSeconds(0)));
            count++;
        }
        Assert.Equal(6, count);
        var missile = SpellProjectile.FromVisual(visuals, programs, 181)!;
        Assert.Equal("burstup", missile.Path);
        Assert.Equal("none", missile.Orientation);
        var acid = SpellProjectile.FromVisual(visuals, programs, 245)!;
        Assert.Equal("homing", acid.Path);
        Assert.Equal("path", acid.Orientation);
        Assert.Equal(0.4f, acid.FlightSeconds(10));
    }

    [Fact]
    public void CustomVisualRowsUseProgramParametersAndRejectNonProjectiles()
    {
        static TwoDA Table(string text) => TwoDA.Parse(System.Text.Encoding.UTF8.GetBytes(text));
        var visual = Table("2DA V2.0\n\nProgFX_Impact\n0 4\n1 5\n2 ****\n");
        var programs = Table("2DA V2.0\n\nType Param1 Param2 Param3 Param4 Param5\n4 10 custommissile 9 1 2 linear\n5 7 beam **** **** **** ****\n");
        var spec = SpellProjectile.FromVisual(visual, programs, 0)!;
        Assert.Equal("custommissile", spec.Model);
        Assert.Equal("target", spec.Orientation);
        Assert.Equal("ballistic", spec.Path);
        Assert.Equal(1f, spec.FlightSeconds(50));
        Assert.Null(SpellProjectile.FromVisual(visual, programs, 1));
        Assert.Null(SpellProjectile.FromVisual(visual, programs, 2));
    }
}
