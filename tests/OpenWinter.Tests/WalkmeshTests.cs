using System.Numerics;
using OpenWinter.Game.Areas;
using Xunit;

namespace OpenWinter.Tests;

public class WalkmeshTests
{
    /// <summary>Two walkable floors (a 10 x 10 square at height 0 and a raised 2 x 2 one at height 4) with a gap between.</summary>
    static Walkmesh Floors()
    {
        var w = new Walkmesh();
        w.Add(new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), 1, true);
        w.Add(new(0, 0, 0), new(10, 10, 0), new(0, 10, 0), 1, true);
        w.Add(new(14, 0, 4), new(16, 0, 4), new(16, 2, 4), 1, true);
        w.Add(new(14, 0, 4), new(16, 2, 4), new(14, 2, 4), 1, true);
        // A blocking footprint over part of the gap.
        w.Add(new(11, 0, 0), new(13, 0, 0), new(13, 2, 0), 2, false);
        return w;
    }

    [Fact]
    public void AWalkablePointStaysWhereItIs()
    {
        var p = Floors().NearestWalkable(new Vector3(5, 5, 0))!.Value;
        Assert.Equal(new Vector3(5, 5, 0), p);
    }

    [Fact]
    public void AnUnwalkablePointMovesToTheNearestGroundWithItsHeight()
    {
        var w = Floors();
        var p = w.NearestWalkable(new Vector3(12, 1, 0))!.Value;
        Assert.InRange(p.X, 9.9f, 10f);
        Assert.Equal(0, p.Z, 3);
        var q = w.NearestWalkable(new Vector3(13.5f, 1, 0))!.Value;
        Assert.InRange(q.X, 14f, 14.1f);
        Assert.Equal(4, q.Z, 3);
        Assert.Null(w.NearestWalkable(new Vector3(40, 40, 0), radius: 5));
    }
}
