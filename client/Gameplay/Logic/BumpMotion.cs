using System.Numerics;

namespace OpenWinter.Client.Gameplay.Logic;

/// <summary>A transient presentation of an already-authoritative bump, independent of frame rate.</summary>
public sealed class BumpMotion
{
    public bool Active { get; private set; }
    public Vector3 Origin { get; private set; }
    public Vector3 Destination { get; private set; }
    Vector3 _shownFrom;
    float _elapsed, _duration;

    public void Begin(Vector3 shown, Vector3 origin, Vector3 destination, float duration)
    {
        _shownFrom = shown;
        Origin = origin;
        Destination = destination;
        _duration = Math.Max(0.01f, duration);
        _elapsed = 0;
        Active = true;
    }

    public Vector3 Step(float delta)
    {
        _elapsed = Math.Min(_duration, _elapsed + Math.Max(0, delta));
        Active = _elapsed < _duration;
        return Vector3.Lerp(_shownFrom, Destination, _elapsed / _duration);
    }

    public void Cancel() => Active = false;

    /// <summary>Native cwalk directions are relative to the retained facing, not the push's world heading.</summary>
    public static string Clip(Vector3 displacement, float facing)
    {
        float forward = displacement.X * MathF.Cos(facing) + displacement.Y * MathF.Sin(facing);
        float left = -displacement.X * MathF.Sin(facing) + displacement.Y * MathF.Cos(facing);
        return MathF.Abs(forward) >= MathF.Abs(left) ? forward >= 0 ? "cwalkf" : "cwalkb"
            : left >= 0 ? "cwalkl" : "cwalkr";
    }
}
