using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class ParticleTrailTests
{
    [Fact]
    public void FramePartitionDoesNotChangeSpatialBirths()
    {
        static double[] Run(int frames)
        {
            double carry = 0;
            var positions = new List<double>();
            for (int i = 0; i < frames; i++)
            {
                var b = ParticleTrail.Advance(2.0 / frames, 5, ref carry);
                for (int n = 0; n < b.Count; n++) positions.Add((i + b.Fraction(n)) * 2 / frames);
            }
            return positions.ToArray();
        }
        foreach (int frames in new[] { 1, 10, 30, 120 })
        {
            var actual = Run(frames);
            Assert.Equal(10, actual.Length);
            for (int i = 0; i < actual.Length; i++) Assert.InRange(Math.Abs(actual[i] - (i + 1) * .2), 0, .000001);
        }
    }

    [Theory]
    [InlineData(.5, 2, .5)]
    [InlineData(1, 5, 0)]
    [InlineData(2, 10, 0)]
    public void DensityFollowsDistanceAboveAndBelowOneMetre(double distance, int count, double remainder)
    {
        double carry = 0;
        var births = ParticleTrail.Advance(distance, 5, ref carry);
        Assert.Equal(count, births.Count);
        Assert.Equal(remainder, carry, 6);
        Assert.Equal(0, ParticleTrail.Advance(0, 5, ref carry).Count);
    }

    [Fact]
    public void ExtremeDisplacementStaysBoundedAndKeepsLatestBirths()
    {
        double carry = 0;
        var births = ParticleTrail.Advance(10000, 10, ref carry);
        Assert.Equal(2000, births.Count);
        Assert.InRange(births.Fraction(0), .98f, 1f);
        Assert.Equal(1f, births.Fraction(1999));
    }
}
