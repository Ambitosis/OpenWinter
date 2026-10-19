using System.Numerics;
using OpenWinter.Client.Gameplay.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class BumpMotionTests
{
    [Theory]
    [InlineData(0, "cwalkr")]
    [InlineData(180, "cwalkl")]
    [InlineData(90, "cwalkb")]
    [InlineData(270, "cwalkf")]
    public void SouthwardBumpSelectsClipRelativeToRetainedFacing(float degrees, string clip) =>
        Assert.Equal(clip, BumpMotion.Clip(new Vector3(0, -1, 0), degrees * MathF.PI / 180));

    [Fact]
    public void MotionTakesOneStrideAtEitherFrameRateAndEndsExactlyAtDestination()
    {
        var fast = new BumpMotion();
        var slow = new BumpMotion();
        var destination = new Vector3(0, -1, 0);
        fast.Begin(Vector3.Zero, Vector3.Zero, destination, 0.5f);
        slow.Begin(Vector3.Zero, Vector3.Zero, destination, 0.5f);
        Vector3 shown = default;
        for (int i = 0; i < 15; i++) shown = fast.Step(1 / 60f);
        Assert.InRange(Vector3.Distance(shown, slow.Step(0.25f)), 0, 0.00001f);
        Assert.Equal(destination / 2, slow.Step(0));
        Assert.True(slow.Active);
        Assert.Equal(destination, slow.Step(1));
        Assert.False(slow.Active);
    }

    [Fact]
    public void ChainedPushStartsFromShownPositionWithoutSnappingToPreviousServerDestination()
    {
        var motion = new BumpMotion();
        motion.Begin(Vector3.Zero, Vector3.Zero, Vector3.UnitX, 0.5f);
        var shown = motion.Step(0.2f);
        motion.Begin(shown, Vector3.UnitX, Vector3.UnitX * 2, 0.5f);
        Assert.Equal(shown, motion.Step(0));
        Assert.Equal(Vector3.UnitX * 2, motion.Step(0.5f));
    }
}
