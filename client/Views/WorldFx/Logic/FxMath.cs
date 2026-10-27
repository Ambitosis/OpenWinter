using System.Numerics;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Godot-free helpers for world presentation: colours, grass scattering, lightning paths.</summary>
public static class FxMath
{
    /// <summary>Script colours are 0xRRGGBB; area (ARE) colours are 0x00BBGGRR.</summary>
    public static uint RgbToBgr(uint rgb) => ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);
    public static uint BgrToRgb(uint bgr) => RgbToBgr(bgr);

    /// <summary>Linear blend of two 0x00BBGGRR colours per channel, <paramref name="t"/> in 0..1.</summary>
    public static uint LerpBgr(uint a, uint b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        uint Ch(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) + ((((b >> shift) & 0xFF) - (float)((a >> shift) & 0xFF)) * t)) & 0xFF;
        return Ch(0) | (Ch(8) << 8) | (Ch(16) << 16);
    }

    /// <summary>
    /// Scatters grass clumps over triangles (three vertices each, Z up): on average
    /// <paramref name="density"/> clumps per square metre, at deterministic positions for a seed so
    /// a tile's grass doesn't change when it is rebuilt.
    /// </summary>
    public static List<(Vector3 Position, float Yaw, float Scale)> ScatterGrass(IReadOnlyList<Vector3> triangles, float density, int seed)
    {
        var result = new List<(Vector3, float, float)>();
        if (density <= 0) return result;
        var rng = new Random(seed);
        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            Vector3 a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            var cross = Vector3.Cross(b - a, c - a);
            // Steep faces (cliffs, walls) get no grass.
            if (cross.LengthSquared() < 1e-8f || Math.Abs(cross.Z) / cross.Length() < 0.6f) continue;
            float area = cross.Length() * 0.5f;
            float expected = area * density;
            int count = (int)expected + (rng.NextDouble() < expected - (int)expected ? 1 : 0);
            for (int k = 0; k < count; k++)
            {
                float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                if (u + v > 1) { u = 1 - u; v = 1 - v; }
                var p = a + (b - a) * u + (c - a) * v;
                result.Add((p, (float)(rng.NextDouble() * Math.PI * 2), 0.75f + 0.5f * (float)rng.NextDouble()));
            }
        }
        return result;
    }

    /// <summary>
    /// A jagged lightning path from <paramref name="from"/> to <paramref name="to"/>: the ends are fixed,
    /// interior points are pushed sideways by up to <paramref name="jitter"/> metres, most in the middle.
    /// </summary>
    public static Vector3[] LightningPath(Vector3 from, Vector3 to, int segments, float jitter, Random rng)
    {
        segments = Math.Max(1, segments);
        var dir = to - from;
        float length = dir.Length();
        var axis = length > 1e-5f ? dir / length : Vector3.UnitZ;
        var side = Vector3.Cross(axis, Math.Abs(axis.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX);
        side = Vector3.Normalize(side);
        var up = Vector3.Cross(side, axis);
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            var p = from + dir * t;
            if (i > 0 && i < segments)
            {
                float taper = MathF.Sin(t * MathF.PI);
                p += side * ((float)(rng.NextDouble() * 2 - 1) * jitter * taper) + up * ((float)(rng.NextDouble() * 2 - 1) * jitter * taper);
            }
            points[i] = p;
        }
        return points;
    }

    /// <summary>
    /// Authored zero-radius beams are straight. Nonzero-radius beams and the legacy
    /// generated connector retain the existing approximation pending separate calibration.
    /// </summary>
    public static Vector3[] BeamPath(Vector3 from, Vector3 to, int segments, float lightningRadius,
        Random rng, bool legacyFallback = false)
    {
        float jitter = !legacyFallback && lightningRadius == 0 ? 0 : 0.08f + Vector3.Distance(from, to) * 0.025f;
        return LightningPath(from, to, segments, jitter, rng);
    }

    /// <summary>
    /// Preserve authored thin positive beam widths. Nonpositive sizes and the upper
    /// cap retain their previous fallback until separately measured.
    /// </summary>
    public static float BeamWidth(float sizeStart, float sizeEnd)
    {
        float size = Math.Max(sizeStart, sizeEnd);
        return size > 0 ? Math.Min(size * LinkedWidthPerSize, 0.6f) : Math.Clamp(size * LinkedWidthPerSize, 0.06f, 0.6f);
    }

    /// <summary>
    /// A linked emitter's strip is 2.2 times its particle size wide, as the original draws beams (scene beamwidth) and
    /// ribbons (scene linkedarea: size 0.5 draws 1.1 m).
    /// </summary>
    public const float LinkedWidthPerSize = 2.2f;

    /// <summary>The width of a linked emitter's ribbon at a particle size.</summary>
    public static float LinkedWidth(float size) => Math.Max(0, size) * LinkedWidthPerSize;

    /// <summary>The weather a WEATHER_* value shows: 0 clear, 1 rain, 2 snow (anything else is clear).</summary>
    public static int NormalizeWeather(int weather) => weather is 1 or 2 ? weather : 0;
}
