using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Textures;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>The game's TrueType faces' advances and TXI spacing, which the classic interface lays party names out with.</summary>
public class TrueTypeMetricsTests
{
    static ResMan Base => ResMan.ForClient(TestData.Paths, Array.Empty<string>(), "");

    [DataFact]
    public void Reads_the_serif_faces_advances()
    {
        var m = TrueTypeMetrics.Read(Base.Read("fnt_default_hr", ResType.Ttf)!);
        Assert.NotNull(m);
        Assert.Equal(2048, m!.UnitsPerEm);
        Assert.Equal(1642, m.AdvanceUnits('H'));
        Assert.Equal(1280, m.AdvanceUnits('n'));
        Assert.Equal(1116, m.AdvanceUnits('S'));
        Assert.Equal(600, m.AdvanceUnits(' '));
    }

    [DataFact]
    public void Tracked_advances_give_the_originals_name_widths()
    {
        // partyframes (large font): the original starts "Hench A"'s letters at 0, 11, 18, 27, 34, 42, 46
        // and "Summon"'s at 0, 7, 15, 27, 39, 47: each advance plus spacingR x 128, rounded.
        var res = Base;
        var m = TrueTypeMetrics.Read(res.Read("fnt_default_hr", ResType.Ttf)!)!;
        float tracking = TextureDecoder.ParseTxi(res.Read("fnt_default_hr", ResType.Txi)!).FontSpacingR * 128;
        Assert.Equal(0.384f, tracking, 3);
        int[] Pens(string s)
        {
            var pens = new List<int> { 0 };
            foreach (char c in s[..^1]) pens.Add(pens[^1] + (int)MathF.Round(m.Advance(c, 13) + tracking, MidpointRounding.AwayFromZero));
            return pens.ToArray();
        }
        Assert.Equal(new[] { 0, 11, 18, 27, 34, 42, 46 }, Pens("Hench A"));
        Assert.Equal(new[] { 0, 7, 15, 27, 39, 47 }, Pens("Summon"));
    }
}
