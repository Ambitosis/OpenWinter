using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Effect lights as the original's (visual scene lightfalloff).</summary>
public class EffectLightTests
{
    [Fact]
    public void TheMultiplierStretchesTheLight()
    {
        // Radius 4 at multiplier 2 lit the floor exactly as radius 8 at 1.
        Assert.Equal(EffectLight.For(8, 1), EffectLight.For(4, 2));
        var (range, energy) = EffectLight.For(4, 1);
        Assert.Equal(8f, range);
        Assert.Equal(EffectLight.Strength * 16, energy, 5);
        // Twice the reach, four times the strength: the same light at twice the distance.
        Assert.Equal(4 * energy, EffectLight.For(8, 1).Energy, 5);
    }

    [Fact]
    public void AZeroMultiplierPutsTheLightOut()
    {
        var (range, energy) = EffectLight.For(4, 0);
        Assert.Equal(0f, energy);
        Assert.True(range > 0);
    }

    [Fact]
    public void KeysAreInterpolatedAndHeld()
    {
        float[] times = [0, 1, 2], values = [2, 8, 2];
        Assert.Equal(5f, EffectLight.Keyed(times, values, 0.5f, 0));
        Assert.Equal(2f, EffectLight.Keyed(times, values, -1, 0));
        Assert.Equal(2f, EffectLight.Keyed(times, values, 3, 0));
        Assert.Equal(7f, EffectLight.Keyed([], [], 1, 7));
    }
}
