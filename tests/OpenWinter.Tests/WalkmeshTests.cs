using System.Numerics;
using OpenWinter.Game;
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
    [Fact]
    public void ALedgeExactlyAStepHighIsLeftTheSameWayWhateverTheNoiseInItsHeight()
    {
        var w = new Walkmesh();
        w.Add(new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), 1, true);
        // Heights interpolated across an authored 0.9 m platform come out a hair either side of 0.9.
        foreach (float top in new[] { MathF.BitDecrement(0.9f), 0.9f, MathF.BitIncrement(0.9f) })
        {
            Assert.Equal(0f, w.GroundHeight(8, 1, top, 0.9f));
            Assert.Equal(0f, w.GroundHeight(8, 1, -top, 0.9f));
        }
    }

    [Fact]
    public void AFootprintExactlyAStepAboveTheFloorBlocksEverywhereOrNowhere()
    {
        foreach (float z in new[] { MathF.BitDecrement(0.9f), 0.9f, MathF.BitIncrement(0.9f) })
        {
            var w = new Walkmesh();
            w.Add(new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), 1, true);
            w.Add(new(8, 1, z), new(10, 1, z), new(10, 3, z), 2, false, owner: 7);
            w.SetPlaceableOwner(7);
            Assert.Equal(0f, w.GroundHeight(8, 1, 0, 0.9f));
        }
    }

    [Fact]
    public void WalkingOffAStairTopAgreesWithTheTraceForEveryStepSize()
    {
        // A stair from a Chapter 1 interior: a 0.44 m tread, a 0.9 m landing, and the floor below the landing's edge.
        var area = new Area();
        var w = area.Walkmesh;
        // The landing's edge vertex as a transformed tile leaves it, a hair above 0.9.
        var edge = new Vector3(32.62f, 33.31f, MathF.BitIncrement(0.9f));
        w.Add(new(33.78f, 32.53f, 0), new(32.62f, 33.31f, 0), new(33.68f, 35.59f, 0), 9, true);
        w.Add(new(33.68f, 35.59f, 0), new(32.62f, 33.31f, 0), new(31.25f, 33.75f, 0), 9, true);
        w.Add(new(32.42f, 32.83f, 0.9f), new(33.78f, 32.53f, 0.9f), new(33.42f, 32.17f, 0.9f), 4, true);
        w.Add(new(33.78f, 32.53f, 0.9f), new(32.42f, 32.83f, 0.9f), edge, 4, true);
        w.Add(new(31.25f, 33.07f, 0.9f), edge, new(32.42f, 32.83f, 0.9f), 4, true);
        w.Add(new(32.49f, 30.00f, 0.44f), new(32.42f, 32.83f, 0.44f), new(33.42f, 32.17f, 0.44f), 11, true);
        var from = new Vector3(32.852688f, 31.876997f, 0.44f);
        var to = new Vector3(32.75f, 33.25f, 0);
        Assert.Equal(1f, w.Trace(from, to, 0.9f));
        foreach (float step in new[] { 0.04f, 0.117f, 0.23f })
        {
            var pos = from;
            while (Vector2.Distance(new(pos.X, pos.Y), new(to.X, to.Y)) > 1e-4f)
            {
                var d = new Vector2(to.X - pos.X, to.Y - pos.Y);
                var next = Movement.Step(area, pos, d.Length() > step ? Vector2.Normalize(d) * step : d);
                Assert.True(next != null, $"refused at {pos} (step {step})");
                pos = d.Length() > step ? next.Value : new Vector3(to.X, to.Y, next.Value.Z);
            }
            Assert.Equal(0f, pos.Z);
        }
    }
}
