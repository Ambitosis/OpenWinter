using System.Numerics;
using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class ProjectileOrientationTests
{
    [Fact]
    public void ArrowNoseFollowsHorizontalDiagonalAndVerticalTravel()
    {
        // Installed wamar_001 has its arrowhead on +Y; aligning +X turns it sideways.
        foreach (var direction in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY,
                     -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ, new Vector3(3, 4, 5), new Vector3(-2, 3, -4) })
        {
            var rotation = ProjectileOrientation.Rotation(direction);
            var nose = Vector3.Transform(Vector3.UnitY, rotation);
            Assert.True(Vector3.Distance(nose, Vector3.Normalize(direction)) < 1e-5f);
            var right = Vector3.Transform(Vector3.UnitX, rotation);
            var up = Vector3.Transform(Vector3.UnitZ, rotation);
            Assert.InRange(Vector3.Dot(Vector3.Cross(right, nose), up), .99999f, 1.00001f);
            Assert.InRange(rotation.Length(), .99999f, 1.00001f);
        }
    }

    [Fact]
    public void ApproachingVerticalPreservesRollAtAFixedHeading()
    {
        Vector3? previousRight = null;
        foreach (float degrees in new[] { 3f, 2.6f, 2.5f, 1f, .01f })
        {
            float angle = degrees * MathF.PI / 180;
            var direction = new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
            var right = Vector3.Transform(Vector3.UnitX, ProjectileOrientation.Rotation(direction));
            if (previousRight is { } previous)
                Assert.True(Vector3.Distance(previous, right) < 1e-5f);
            previousRight = right;
        }
    }

    [Fact]
    public void StationaryProjectileHasFiniteIdentityOrientation() =>
        Assert.Equal(Quaternion.Identity, ProjectileOrientation.Rotation(Vector3.Zero));
}
