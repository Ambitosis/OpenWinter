using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class SpellProjectileTimingTests
{
    [Fact]
    public void OrdinarySpellPathsFitMeasuredNativeImpactIntervals()
    {
        // Native projectile_far/short, subtracting each actor's no-projectile release.
        foreach (var (distance, path, measured) in new (float, string, float)[]
        {
            (8, "homing", .527f), (39, "homing", 1.504f),
            (8, "ballistic", 1f), (39, "ballistic", 3.004f),
            (8, "accelerating", .648f), (39, "accelerating", 1.996f),
            (0, "highballistic", 2.027f), (39, "highballistic", 2.027f),
            (.25f, "homing", .016f), (.75f, "homing", .332f),
            (1.5f, "ballistic", .465f), (.5f, "accelerating", .008f),
        })
            Assert.InRange(SpellTable.ProjectileFlightTime(distance, path), measured-.08f, measured+.08f);
    }

    [Fact]
    public void SpiralSpellPathFitsStockSeekerArrowImpactIntervals()
    {
        // Actual native ActionUseFeat(451), stationary equipped archers and stock
        // script events. Subtract the independently measured 0.5-second release.
        foreach (var (distance, arrival) in new (float, float)[]
        {
            (1, 3.518f), (1.5f, 3.469f), (2, 3.501f),
            (6, 3.815f), (10, 4.130f), (20, 4.832f),
        })
            Assert.InRange(SpellTable.ProjectileFlightTime(distance, "spiral"), arrival - .5f - .08f, arrival - .5f + .08f);
    }

    [Fact]
    public void SpiralLocationControlsResolveAtReleaseWhenLogarithmicFlightIsNotPositive()
    {
        // Original-game paired projectile/no-projectile controls avoid creature
        // collision separation. Values are normalized by each actor's release.
        foreach (var (distance, flight) in new (float, float)[]
        {
            (0, 0), (.25f, 0), (.5f, 0), (.75f, 3.160f), (1, 3.008f), (1.5f, 2.980f),
        })
            Assert.InRange(SpellTable.ProjectileFlightTime(distance, "spiral"), flight - .08f, flight + .08f);
    }

    [Fact]
    public void ScriptMirvTimingRemainsIndependentOfOrdinarySpellPaths()
    {
        var missile = new SpellProjectile("authored", "none", "burstup", "logarithmic");
        Assert.Equal(.1f, missile.FlightSeconds(.25f));
        Assert.Equal(.1f, missile.FlightSeconds(1f));
        Assert.InRange(missile.FlightSeconds(20), 1.82f, 1.83f);
        Assert.InRange(SpellTable.ProjectileFlightTime(20, "homing"), .91f, .92f);
        Assert.InRange(SpellTable.ProjectileFlightTime(20, "spiral"), 4.32f, 4.33f);
    }
}
