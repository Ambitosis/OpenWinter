using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>
/// The visual transform channels (SetObjectVisualTransform) of one object on the client.
/// Each channel is evaluated with the server's own <see cref="VisualTransform"/> curve code,
/// so lerp types, bounce behaviour and repeats match GetObjectVisualTransform exactly;
/// only the clock differs (client seconds, started when the notify arrived).
/// Godot-free so it can be unit tested.
/// </summary>
public sealed class VisualTransformSet
{
    public const int Scale = 10, RotateX = 21, RotateY = 22, RotateZ = 23, TranslateX = 31, TranslateY = 32, TranslateZ = 33, AnimationSpeed = 40;
    public const int ScopeBase = 0;

    readonly Dictionary<(int Scope, int Transform), VisualTransform> _channels = new();

    public bool IsEmpty => _channels.Count == 0;
    public IEnumerable<int> Scopes => _channels.Keys.Select(k => k.Scope).Distinct();

    /// <summary>Starts a channel animating from <paramref name="from"/> to <paramref name="to"/> at client time <paramref name="now"/>.</summary>
    /// <param name="elapsed">Seconds the transform has already been running (state re-sent on area entry).</param>
    public void Set(int scope, int transform, float from, float to, int lerp, float duration, int behavior, int repeats, double now, double elapsed = 0) =>
        _channels[(scope, transform)] = new VisualTransform
        {
            From = from, To = to, LerpType = lerp, LerpDuration = duration, Behavior = behavior, Repeats = repeats,
            StartTime = now - Math.Max(0, elapsed), PauseWithGame = false,
        };

    /// <summary>ClearObjectVisualTransform: one scope, or every scope for -1.</summary>
    public void Clear(int scope)
    {
        if (scope == -1) _channels.Clear();
        else foreach (var k in _channels.Keys.Where(k => k.Scope == scope).ToList()) _channels.Remove(k);
    }

    public float Value(int scope, int transform, double now) =>
        _channels.TryGetValue((scope, transform), out var vt) ? vt.ValueAt(now) : VisualTransform.Default(transform);

    public VisualPose Pose(int scope, double now) => new(
        Value(scope, Scale, now), Value(scope, RotateX, now), Value(scope, RotateY, now), Value(scope, RotateZ, now),
        Value(scope, TranslateX, now), Value(scope, TranslateY, now), Value(scope, TranslateZ, now), Value(scope, AnimationSpeed, now));

    /// <summary>True while any channel can still change value (lerping, or repeating forever).</summary>
    public bool Animating(double now) => _channels.Values.Any(vt =>
        vt.LerpType != 0 && vt.LerpDuration > 0 && (vt.Repeats < 0 || now - vt.StartTime < vt.LerpDuration * (vt.Repeats + 1) + 0.05));
}

/// <summary>Visual transform values of one scope: scale factor, rotations in degrees, translation in metres, animation speed factor.</summary>
public readonly record struct VisualPose(float Scale, float RotateX, float RotateY, float RotateZ, float TranslateX, float TranslateY, float TranslateZ, float AnimationSpeed)
{
    public static readonly VisualPose Identity = new(1, 0, 0, 0, 0, 0, 0, 1);

    public bool IsIdentity => Scale == 1 && RotateX == 0 && RotateY == 0 && RotateZ == 0 && TranslateX == 0 && TranslateY == 0 && TranslateZ == 0;

    /// <summary>
    /// Object-space matrix (row vectors, System.Numerics convention): scale, then rotate X, Y, Z
    /// (degrees, about the object's origin at its feet), then translate.
    /// </summary>
    public System.Numerics.Matrix4x4 Matrix() =>
        System.Numerics.Matrix4x4.CreateScale(Scale)
        * System.Numerics.Matrix4x4.CreateRotationX(RotateX * MathF.PI / 180f)
        * System.Numerics.Matrix4x4.CreateRotationY(RotateY * MathF.PI / 180f)
        * System.Numerics.Matrix4x4.CreateRotationZ(RotateZ * MathF.PI / 180f)
        * System.Numerics.Matrix4x4.CreateTranslation(TranslateX, TranslateY, TranslateZ);
}
