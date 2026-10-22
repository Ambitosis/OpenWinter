using System.Numerics;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Moves a creature along the path the server sent. The original protocol sends a path once (start, waypoints,
/// walk or run) and the final position on arrival; the client moves the creature in between at its rate.
/// </summary>
public sealed class NwnMover(Vector3 start, IReadOnlyList<Vector2> waypoints, float speed)
{
    int _leg;
    Vector3 _at = start;

    public Vector3 Position => _at;
    public bool Done => _leg >= waypoints.Count || speed <= 0;

    /// <summary>The facing along the current leg, radians from +X; null when there is no leg left.</summary>
    public float? Facing
    {
        get
        {
            if (Done) return null;
            var d = waypoints[_leg] - new Vector2(_at.X, _at.Y);
            return d.LengthSquared() < 1e-8f ? null : MathF.Atan2(d.Y, d.X);
        }
    }

    /// <summary>Advances by <paramref name="seconds"/>; heights stay those of the start (the client snaps to the walkmesh).</summary>
    public void Advance(float seconds)
    {
        var budget = speed * seconds;
        while (budget > 0 && !Done)
        {
            var target = waypoints[_leg];
            var here = new Vector2(_at.X, _at.Y);
            var left = Vector2.Distance(here, target);
            if (left <= budget)
            {
                _at = new Vector3(target.X, target.Y, _at.Z);
                budget -= left;
                _leg++;
                continue;
            }
            var step = here + (target - here) / left * budget;
            _at = new Vector3(step.X, step.Y, _at.Z);
            budget = 0;
        }
    }
}
