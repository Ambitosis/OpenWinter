using Godot;
using N = System.Numerics;

namespace OpenWinter.Client.Rendering;

/// <summary>System.Numerics → Godot conversions. Content stays Z-up; a root node rotates it into Godot's Y-up.</summary>
public static class Convert
{
    public static Vector3 V(N.Vector3 v) => new(v.X, v.Y, v.Z);
    public static Vector2 V(N.Vector2 v) => new(v.X, v.Y);
    public static Quaternion Q(N.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    public static N.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>Row-vector Numerics matrix → Godot transform.</summary>
    public static Transform3D T(N.Matrix4x4 m) => new(
        new Basis(new Vector3(m.M11, m.M12, m.M13), new Vector3(m.M21, m.M22, m.M23), new Vector3(m.M31, m.M32, m.M33)),
        new Vector3(m.M41, m.M42, m.M43));

    public static Transform3D Local(N.Vector3 position, N.Quaternion rotation) => new(new Basis(Q(rotation)), V(position));

    /// <summary>Aurora colours are 0x00BBGGRR.</summary>
    public static Color Bgr(uint value) => Color.Color8((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));

    public static Color C(N.Vector3 v, float a = 1f) => new(v.X, v.Y, v.Z, a);
}
