using System.Numerics;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Projectile models face +Y in Aurora's Z-up coordinates.</summary>
public static class ProjectileOrientation
{
    public static Quaternion Rotation(Vector3 direction)
    {
        if (direction.LengthSquared() <= 1e-6f) return Quaternion.Identity;
        var forward = Vector3.Normalize(direction);
        // Preserve the horizontal heading up to the pole; switching reference axes
        // near it produces a visible quarter-turn during otherwise smooth travel.
        var horizontal = new Vector3(forward.Y, -forward.X, 0);
        var right = horizontal.LengthSquared() > 0
            ? Vector3.Normalize(horizontal) : Vector3.UnitX;
        var up = Vector3.Cross(right, forward);
        return Quaternion.CreateFromRotationMatrix(new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            up.X, up.Y, up.Z, 0,
            0, 0, 0, 1));
    }
}
