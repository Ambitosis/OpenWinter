using System.Numerics;
using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class AreaLightDirectionTests
{
    static float Elevation(Vector3 d) => MathF.Asin(Vector3.Normalize(d).Z) * 180f / MathF.PI;
    static float Bearing(Vector3 d) => (MathF.Atan2(d.X, d.Y) * 180f / MathF.PI + 360f) % 360f;

    [Fact]
    public void DefaultIsTheOriginalsNorthEasternLight()
    {
        // Scene sundir: about 49.3 degrees up, bearing 41.6 (north-east), a unit vector.
        var d = AreaLightDirection.Default;
        Assert.Equal(1f, d.Length(), 4);
        Assert.Equal(49.3f, Elevation(d), 1);
        Assert.Equal(41.6f, Bearing(d), 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SunAndMoonShareTheDefault(bool sun)
    {
        Assert.Equal(AreaLightDirection.Default, AreaLightDirection.Current(sun, null, null));
    }

    [Fact]
    public void AScriptSetDirectionReplacesOnlyItsOwnLight()
    {
        var sun = new Vector3(-1f, 0.2f, 0.35f);
        Assert.Equal(Vector3.Normalize(sun), AreaLightDirection.Current(true, sun, null));
        Assert.Equal(AreaLightDirection.Default, AreaLightDirection.Current(false, sun, null));
        var moon = new Vector3(0f, 0f, 2f);
        Assert.Equal(Vector3.UnitZ, AreaLightDirection.Current(false, sun, moon));
        Assert.Equal(AreaLightDirection.Default, AreaLightDirection.Current(true, Vector3.Zero, moon));
    }

    [Fact]
    public void RendererSpaceIsYUpWithNorthAlongMinusZ()
    {
        var north = AreaLightDirection.ToRenderer(Vector3.UnitY);
        Assert.Equal(new Vector3(0, 0, -1), north);
        Assert.Equal(Vector3.UnitY, AreaLightDirection.ToRenderer(Vector3.UnitZ));
        var d = AreaLightDirection.Default;
        Assert.Equal(d, AreaLightDirection.FromRenderer(AreaLightDirection.ToRenderer(d)));
    }
}
