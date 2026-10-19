using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class BeamWidthTests
{
    [Fact]
    public void ThinPositiveStrandRemainsNarrowerThanMeasuredLargerControls()
    {
        float thin = FxMath.BeamWidth(.01f, .01f);
        Assert.InRange(thin, .0219f, .0221f);
        Assert.True(thin < FxMath.BeamWidth(.04f, .04f) / 3);
        Assert.Equal(.088f, FxMath.BeamWidth(.04f, .04f), 5);
        Assert.Equal(.33f, FxMath.BeamWidth(.15f, .15f), 5);
        Assert.Equal(.6f, FxMath.BeamWidth(.30f, .30f));
    }

    [Fact]
    public void UnmeasuredFallbacksAndLargerEndpointSelectionArePreserved()
    {
        Assert.Equal(.06f, FxMath.BeamWidth(0, 0));
        Assert.Equal(.06f, FxMath.BeamWidth(-1, -2));
        Assert.Equal(.6f, FxMath.BeamWidth(1, 1));
        Assert.Equal(FxMath.BeamWidth(.04f, .04f), FxMath.BeamWidth(.01f, .04f));
        Assert.Equal(FxMath.BeamWidth(.04f, .04f), FxMath.BeamWidth(.04f, .01f));
    }
}
