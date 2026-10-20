using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class ProjectileNearBoundaryTests
{
    [Fact]
    public void CloseRangeDelaysMatchOriginalIncludingTheLongHalfMetreFlight()
    {
        // Native projectile_near_boundary: actual impact minus each actor's
        // independently measured no-projectile release. Twenty real impacts.
        string[] paths = ["homing", "ballistic", "highballistic", "accelerating", "spiral"];
        float[] distances = [.52f, .55f, .6f, .75f];
        float[,] observed = {
            { 6.793f, 1.340f, .668f, .344f },
            { 13.602f, 2.668f, 1.293f, .668f },
            { 2.008f, 2.023f, 2.012f, 2.027f },
            { 9.086f, 1.773f, .855f, .418f },
            { 16.125f, 5.164f, 3.824f, 3.184f }
        };
        for (int p = 0; p < paths.Length; p++)
            for (int d = 0; d < distances.Length; d++)
                Assert.InRange(SpellTable.ProjectileFlightTime(distances[d], paths[p]), observed[p, d] - .09f, observed[p, d] + .09f);
    }

    [Fact]
    public void PoleAndInvalidInputsGiveFiniteDelays()
    {
        foreach (string path in new[] { "homing", "ballistic", "accelerating", "spiral", "highballistic", "bounce", "linked", "default" })
        {
            float pole = MathF.Exp(-2f / 3f);
            float[] distances = [float.NaN, float.NegativeInfinity, float.PositiveInfinity,
                -1, 0, float.Epsilon, MathF.BitDecrement(pole), pole, MathF.BitIncrement(pole), .5135f, float.MaxValue];
            foreach (float distance in distances)
            {
                float seconds = SpellTable.ProjectileFlightTime(distance, path);
                Assert.True(float.IsFinite(seconds) && seconds >= 0, $"{path} at {distance}");
            }
        }
        // A speed of zero or less is a 1 ms flight, spiral included (projectile_spiral_short: no 2.5 s at 0.5 m).
        Assert.Equal(.001f, SpellTable.ProjectileFlightTime(.513f, "ballistic"));
        Assert.Equal(.001f, SpellTable.ProjectileFlightTime(.5f, "spiral"));
        Assert.Equal(.001f, SpellTable.ProjectileFlightTime(0, "bounce"));
        Assert.Equal(.001f, SpellTable.ProjectileFlightTime(0, "linked"));
        // No upper bound (projectile_paths: 81.15 s at 0.5145 m, ballistic).
        Assert.InRange(SpellTable.ProjectileFlightTime(.5145f, "ballistic"), 80.5f, 82f);
    }

    [Fact]
    public void FlightTimeFollowsTheLogarithmicSpeedAndPathFactors()
    {
        // Measured by projectile_paths (bounce, linked, an empty ProjType,
        // 0.5145 m), projectile_clock and projectile_far.
        (float d, float ballistic, float homing)[] table =
        [
            (.52f, 13.6f, 6.8f), (.6f, 1.28f, .64f), (.75f, .66f, .33f), (1, .50f, .25f), (2, .49f, .245f),
            (5, .73f, .366f), (10, 1.12f, .56f), (20, 1.82f, .91f), (30, 2.46f, 1.23f), (40, 3.06f, 1.53f),
        ];
        foreach (var (d, ballistic, homing) in table)
        {
            Assert.InRange(SpellTable.ProjectileFlightTime(d, "ballistic"), ballistic * .99f - .002f, ballistic * 1.01f + .002f);
            Assert.InRange(SpellTable.ProjectileFlightTime(d, "default"), ballistic * .99f - .002f, ballistic * 1.01f + .002f);
            Assert.InRange(SpellTable.ProjectileFlightTime(d, "homing"), homing * .99f - .002f, homing * 1.01f + .002f);
            Assert.InRange(SpellTable.ProjectileFlightTime(d, "accelerating"), ballistic / 1.5f * .99f - .002f, ballistic / 1.5f * 1.01f + .002f);
            Assert.InRange(SpellTable.ProjectileFlightTime(d, "bounce"), ballistic / .8f * .99f - .002f, ballistic / .8f * 1.01f + .002f);
            Assert.InRange(SpellTable.ProjectileFlightTime(d, "spiral"), ballistic + 2.5f - .02f, ballistic + 2.5f + .02f);
            Assert.Equal(2f, SpellTable.ProjectileFlightTime(d, "linked"));
            Assert.Equal(2f, SpellTable.ProjectileFlightTime(d, "highballistic"));
        }
        // Measured on nwserver (projectile_paths, release subtracted).
        Assert.InRange(SpellTable.ProjectileFlightTime(1, "bounce"), .664f - .09f, .664f + .09f);
        Assert.InRange(SpellTable.ProjectileFlightTime(5, "bounce"), .926f - .09f, .926f + .09f);
        Assert.InRange(SpellTable.ProjectileFlightTime(20, "bounce"), 2.293f - .09f, 2.293f + .09f);
        Assert.InRange(SpellTable.ProjectileFlightTime(20, "default"), 1.832f - .09f, 1.832f + .09f);
        Assert.Equal(2f, SpellTable.ProjectileFlightTime(0, "highballistic"));
        // Whole milliseconds.
        float t = SpellTable.ProjectileFlightTime(7.3f, "ballistic");
        Assert.Equal(MathF.Round(t * 1000f), t * 1000f, 2);
        Assert.Equal("default", SpellTable.ProjectilePath("", 0));
        Assert.Equal("default", SpellTable.ProjectilePath("****", 0));
        Assert.Equal("bounce", SpellTable.ProjectilePath("bounce", 0));
        Assert.Equal("accelerating", SpellTable.ProjectilePath("bounce", 4));
    }
}
