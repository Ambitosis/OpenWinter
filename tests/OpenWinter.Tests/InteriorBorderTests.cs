using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using Xunit;

namespace OpenWinter.Tests;

public class InteriorBorderTests
{
    static readonly TwoDA Edges = TwoDA.Parse(Encoding.UTF8.GetBytes("""
2DA V2.0

  Corner1 Edge Corner2 Height Model
0 Wall **** **** 0 fallback
1 Wall **** Wall 0 cap
"""));

    static Ini Set(string corners = "Wall", int interior = 1) => Ini.Parse(Encoding.UTF8.GetBytes($"""
[GENERAL]
Interior={interior}
Border=Wall
Transition=3
[TILE0]
BottomRight={corners}
TopRight={corners}
TopLeft=Wall
BottomLeft=Wall
"""));

    [Fact]
    public void BorderSurroundsAreaButDoesNotCoverPlayableTiles()
    {
        var placements = InteriorBorder.Build(Set(), Edges, 2, 1, [new(0, 0, 0), new(0, 0, 2)]);
        Assert.Equal(12 * 11 - 2, placements.Count);
        Assert.All(placements, p =>
        {
            Assert.True(p.X < 0 || p.X >= 2 || p.Y < 0 || p.Y >= 1);
            Assert.Equal("cap", p.Model); // Exact pair, not the single-corner fallback.
            Assert.Equal(p.X <= 0 ? 0f : 6f, p.Height);
        });
    }

    [Fact]
    public void RotatedOpenEdgesAreNotFilledWithWallCaps()
    {
        // Local west is all Wall; after a quarter turn it faces south. The other corners are floor.
        var placements = InteriorBorder.Build(Set("Room"), Edges, 1, 1, [new(0, 1, 0)]);
        Assert.Contains(placements, p => p.X == 0 && p.Y == -1);
        Assert.DoesNotContain(placements, p => p.X == 0 && p.Y == 1);
        Assert.DoesNotContain(placements, p => p.X == -1 && p.Y == 0);
        Assert.Empty(InteriorBorder.Build(Set(interior: 0), Edges, 1, 1, [new(0, 0, 0)]));
        Assert.Empty(InteriorBorder.Build(Set(), TwoDA.Empty, 1, 1, [new(0, 0, 0)]));
    }

    [DataFact]
    public void PreludeBedroomBorderUsesTheAuthoredWallTopCap()
    {
        var rm = TestData.Module;
        var set = Ini.Parse(rm.Read("tic01", OpenWinter.Aurora.Resources.ResType.Set)!);
        var placements = InteriorBorder.Build(set, rm.TwoDA("tic01_edge"), 1, 1, [new(41, 3, 0)]);
        Assert.NotEmpty(placements);
        Assert.All(placements, p => Assert.Equal("tic01_o20_01", p.Model, ignoreCase: true));
        var cap = new ModelCache(rm).Get(placements[0].Model)!;
        var globals = cap.RestGlobals();
        var (part, index) = Assert.Single(cap.Parts.Select((p, i) => (p, i)), p => p.p.Mesh is { Render: true, IsWalkmesh: false });
        Assert.NotEmpty(part.Mesh!.Vertices);
        Assert.All(part.Mesh.Vertices, v => Assert.Equal(3f, System.Numerics.Vector3.Transform(v, globals[index]).Z, 3));
    }
}
