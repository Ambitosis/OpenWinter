namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Phase selection for authored visual-effect model animations.</summary>
public static class EffectAnimation
{
    /// <summary>A cessation-only lasting effect remains dormant until it ends.</summary>
    public static bool WaitsForCessation(string phase, IEnumerable<string> animations)
    {
        if (phase != "duration") return false;
        var names = animations.ToArray();
        return names.Any(n => n.Equals("cessation", StringComparison.OrdinalIgnoreCase))
            && names.All(n => n.Equals("cessation", StringComparison.OrdinalIgnoreCase)
                || n.Equals("fade", StringComparison.OrdinalIgnoreCase));
    }
}
