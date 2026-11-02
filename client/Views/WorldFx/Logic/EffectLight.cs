namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>
/// The original's effect lights, measured on the floor under authored lights (visual scene lightfalloff): a light of
/// radius R and multiplier m lights a surface d away with K (R m)^2 / d^2 times the cosine of the angle it meets the
/// surface at, fading to nothing at twice R m. The multiplier stretches the light rather than brightening it (radius 4
/// at multiplier 2 lit the floor exactly as radius 8 at 1). Godot's omni falloff with a decay of 2 and a range of
/// 2 R m has this shape; the original only softens it within a tenth of R m.
/// </summary>
public static class EffectLight
{
    /// <summary>Range as a multiple of radius times multiplier, the falloff exponent and K.</summary>
    public const float Reach = 2f, Decay = 2f, Strength = 0.024f;

    /// <summary>The omni light's range and energy for a light's radius and multiplier.</summary>
    public static (float Range, float Energy) For(float radius, float multiplier)
    {
        float reach = Math.Max(0f, radius) * Math.Clamp(multiplier, 0f, 3f);
        return (Math.Max(0.05f, Reach * reach), Strength * reach * reach);
    }

    /// <summary>A keyed value at a time, linear between keys and held before the first and after the last.</summary>
    public static float Keyed(float[] times, float[] values, float time, float fallback)
    {
        int count = Math.Min(times.Length, values.Length);
        if (count == 0) return fallback;
        if (time <= times[0]) return values[0];
        for (int k = 1; k < count; k++)
            if (time <= times[k])
                return times[k] > times[k - 1] ? values[k - 1] + (values[k] - values[k - 1]) * (time - times[k - 1]) / (times[k] - times[k - 1]) : values[k];
        return values[count - 1];
    }
}
