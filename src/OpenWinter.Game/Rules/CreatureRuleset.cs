using System.Globalization;
using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Rules;

/// <summary>
/// ruleset.2da (EE): named rule constants modules may override (Toughness hit points, size AC,
/// tumble ranks per AC point, save feat bonuses, ...). Missing entries fall back to the original values.
/// </summary>
public static class Ruleset
{
    static readonly ConditionalWeakTable<TwoDA, Dictionary<string, float>> Cache = new();

    public static float Float(World w, string label, float fallback)
    {
        var table = w.Resources.TwoDA("ruleset");
        var map = Cache.GetValue(table, Build);
        return map.TryGetValue(label, out var v) ? v : fallback;
    }

    public static int Int(World w, string label, int fallback) => (int)MathF.Round(Float(w, label, fallback));

    static Dictionary<string, float> Build(TwoDA t)
    {
        var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int r = 0; r < t.RowCount; r++)
        {
            var label = t.Get(r, "Label");
            if (label.Length == 0) continue;
            var raw = t.Get(r, "Value").TrimEnd('f', 'F');
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) map[label] = v;
        }
        return map;
    }
}
