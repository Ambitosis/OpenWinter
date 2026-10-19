using OpenWinter.Game;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Movement rules measured on the original server (parity/suites/movement_paths); speed factors: EffectParityTests.</summary>
public sealed class MovementTests
{
    [Fact]
    public void BrakingStopsExactlyOnTheGoalOneSecondAfterFullSpeed()
    {
        // Run speed 3.5: full speed until 1.75 m out, then a one-second stop.
        float max = 3.5f, dist = 14f, t = 0;
        const float dt = 1 / 30f;
        while (dist > 0 && t < 10)
        {
            dist -= Movement.BrakingStep(max, dist, dt);
            t += dt;
        }
        Assert.Equal(0f, dist);
        Assert.InRange(t, 4.49f, 4.54f); // 12.25 m at 3.5 m/s, then 1 s
        Assert.Equal(3.5f / 30, Movement.BrakingStep(max, 10f, dt), 4);
    }
}

/// <summary>How long ActionPlayAnimation holds the queue (parity/suites/animations).</summary>
public sealed class AnimationTimingTests
{
    [Theory]
    [InlineData(0, 1f, 0f, 2f)]        // looping, no duration: one play
    [InlineData(4, 1f, 0f, 7f)]        // worship
    [InlineData(0, 2f, 3f, 3f)]        // a duration wins, whatever the speed
    [InlineData(2, 1f, 5f, 2f)]        // except for listen
    [InlineData(107, 1f, 5f, 2f)]      // fire and forget ignores the duration
    [InlineData(105, 4f, 0f, 0.5f)]    // speed divides
    [InlineData(0, 0f, 0f, 2f)]        // speed 0 counts as 1
    [InlineData(25, 1f, 0f, 1f)]       // custom animations
    public void BlockingTime(int animation, float speed, float duration, float expected) =>
        Assert.Equal(expected, AnimationAction.BlockingTime(animation, speed, duration), 3);
}
