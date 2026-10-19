using System.Numerics;
using OpenWinter.Content;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Measured zero-handle Bezier particles; gravity and nonzero handles remain separate.</summary>
public static class PointToPoint
{
    public static bool Supports(EmitterData e) => e.P2P && e.P2PBezier && e.Is("Fountain") &&
        e.P2PBezier2 == 0 && e.P2PBezier3 == 0 && !e.Inherit &&
        (e.Render.Equals("Normal", StringComparison.OrdinalIgnoreCase) ||
         e.Render.Equals("Linked", StringComparison.OrdinalIgnoreCase));

    // The existing GPU Bezier curve with coincident endpoint handles. Each
    // particle retains its birth position and traverses the path over its life.
    public static Vector3 Position(Vector3 birth, Vector3 target, float age, float life)
    {
        float t = Math.Clamp(age / Math.Max(life, 0.0001f), 0, 1);
        return Vector3.Lerp(birth, target, t * t * (3 - 2 * t));
    }
}
