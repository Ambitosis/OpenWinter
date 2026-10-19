using System.Globalization;
using System.Text.Json.Nodes;

namespace OpenWinter.Client.UI.Nui.Model;

// Godot-free NUI model code (also compiled into the test project).

public readonly record struct NuiRect(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;
    public NuiRect Deflate(float d) => new(X + d, Y + d, MathF.Max(0, W - 2 * d), MathF.Max(0, H - 2 * d));
    public JsonObject ToJson() => new() { ["x"] = X, ["y"] = Y, ["w"] = W, ["h"] = H };
}

public readonly record struct NuiVec(float X, float Y)
{
    public JsonObject ToJson() => new() { ["x"] = X, ["y"] = Y };
}

public readonly record struct NuiColor(int R, int G, int B, int A = 255)
{
    public JsonObject ToJson() => new() { ["r"] = R, ["g"] = G, ["b"] = B, ["a"] = A };
}

/// <summary>
/// One window's bind store, and the rules for reading NUI property values: a
/// property is either a static JSON value or <c>{"bind": "name", ...}</c>
/// (NuiBind), and string properties may be <c>{"strref": n}</c> (NuiStrRef).
/// Inside a list row template every bind holding an array yields its element
/// for that row.
/// </summary>
public sealed class NuiBinds
{
    public const int NumberFlagHex = 0x1, TextFlagLower = 0x1, TextFlagUpper = 0x2;

    readonly Dictionary<string, JsonNode?> _values = new(StringComparer.Ordinal);

    /// <summary>Resolves NuiStrRef values (the client's talk table).</summary>
    public Func<uint, string>? StrRef;

    /// <summary>Raised after a bind value changes (from the server or the user).</summary>
    public event Action<string>? Changed;

    public IReadOnlyDictionary<string, JsonNode?> Values => _values;

    public JsonNode? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;

    public void Set(string name, JsonNode? value)
    {
        _values[name] = value?.DeepClone();
        Changed?.Invoke(name);
    }

    public static bool IsBind(JsonNode? prop, out string name)
    {
        name = "";
        if (prop is JsonObject o && o.TryGetPropertyValue("bind", out var b) && b is JsonValue v && v.TryGetValue<string>(out var s))
        {
            name = s;
            return true;
        }
        return false;
    }

    /// <summary>The effective value of a property: the bind's value (its row element in a list), or the static value.</summary>
    public JsonNode? Resolve(JsonNode? prop, int row = -1)
    {
        if (!IsBind(prop, out var name)) return prop;
        var v = Get(name);
        if (row >= 0 && v is JsonArray a) return row < a.Count ? a[row] : null;
        return v;
    }

    public string Text(JsonNode? prop, int row = -1, string fallback = "")
    {
        var v = Resolve(prop, row);
        string s;
        switch (v)
        {
            case null: return fallback;
            case JsonObject o when o.TryGetPropertyValue("strref", out var sr):
                s = Number(sr) is double d && StrRef != null ? StrRef((uint)d) : "";
                break;
            case JsonValue jv when jv.TryGetValue<string>(out var str):
                s = str;
                break;
            case JsonValue jv when jv.TryGetValue<bool>(out var bl):
                s = bl ? "true" : "false";
                break;
            case JsonValue jv when Number(jv) is double num:
                s = FormatNumber(jv, num, prop as JsonObject);
                break;
            default:
                s = v.ToJsonString();
                break;
        }
        if (prop is JsonObject p && IsBind(prop, out _) && p["text_flags"] is JsonValue tf && tf.TryGetValue<int>(out var flags))
        {
            if ((flags & TextFlagUpper) != 0) s = s.ToUpperInvariant();
            else if ((flags & TextFlagLower) != 0) s = s.ToLowerInvariant();
        }
        return s;
    }

    static string FormatNumber(JsonValue raw, double num, JsonObject? bind)
    {
        int flags = 0, precision = 0;
        if (bind != null)
        {
            if (bind["number_flags"] is JsonValue f && f.TryGetValue<int>(out var fi)) flags = fi;
            if (bind["number_precision"] is JsonValue p && p.TryGetValue<int>(out var pi)) precision = pi;
        }
        bool integral = IsInteger(raw);
        if ((flags & NumberFlagHex) != 0) return ((long)num).ToString("x", CultureInfo.InvariantCulture);
        if (precision > 0) return num.ToString("F" + precision, CultureInfo.InvariantCulture);
        if (integral) return ((long)num).ToString(CultureInfo.InvariantCulture);
        return num.ToString("0.######", CultureInfo.InvariantCulture);
    }

    static bool IsInteger(JsonValue v) =>
        v.GetValueKind() == System.Text.Json.JsonValueKind.Number && !v.ToJsonString().Contains('.') && !v.ToJsonString().Contains('e') && !v.ToJsonString().Contains('E');

    public static double? Number(JsonNode? v)
    {
        if (v is not JsonValue jv) return null;
        if (jv.TryGetValue<double>(out var d)) return d;
        if (jv.TryGetValue<int>(out var i)) return i;
        if (jv.TryGetValue<long>(out var l)) return l;
        if (jv.TryGetValue<float>(out var f)) return f;
        if (jv.TryGetValue<bool>(out var b)) return b ? 1 : 0;
        if (jv.GetValueKind() == System.Text.Json.JsonValueKind.Number &&
            double.TryParse(jv.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    public bool Bool(JsonNode? prop, int row = -1, bool fallback = false)
    {
        var v = Resolve(prop, row);
        if (v is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var b)) return b;
            if (Number(jv) is double d) return d != 0;
        }
        return fallback;
    }

    public double Float(JsonNode? prop, int row = -1, double fallback = 0) => Number(Resolve(prop, row)) ?? fallback;

    public int Int(JsonNode? prop, int row = -1, int fallback = 0) => Number(Resolve(prop, row)) is double d ? (int)Math.Round(d) : fallback;

    public NuiColor? Color(JsonNode? prop, int row = -1) => ToColor(Resolve(prop, row));

    public static NuiColor? ToColor(JsonNode? v)
    {
        if (v is not JsonObject o) return null;
        int C(string k, int def) => Number(o[k]) is double d ? Math.Clamp((int)Math.Round(d), 0, 255) : def;
        return new NuiColor(C("r", 0), C("g", 0), C("b", 0), C("a", 255));
    }

    public NuiRect? Rect(JsonNode? prop, int row = -1) => ToRect(Resolve(prop, row));

    public static NuiRect? ToRect(JsonNode? v)
    {
        if (v is not JsonObject o) return null;
        float F(string k) => (float)(Number(o[k]) ?? 0);
        return new NuiRect(F("x"), F("y"), F("w"), F("h"));
    }

    public NuiVec? Vec(JsonNode? prop, int row = -1) => ToVec(Resolve(prop, row));

    public static NuiVec? ToVec(JsonNode? v)
    {
        if (v is not JsonObject o) return null;
        return new NuiVec((float)(Number(o["x"]) ?? 0), (float)(Number(o["y"]) ?? 0));
    }

    /// <summary>Float array (draw list points, chart data).</summary>
    public static float[] Floats(JsonNode? v) =>
        v is JsonArray a ? a.Select(x => (float)(Number(x) ?? 0)).ToArray() : Array.Empty<float>();

    /// <summary>
    /// The full bind value after the user changed it: the new value, or for a
    /// list row a copy of the bind's array with that row's element replaced.
    /// </summary>
    public JsonNode? WithValue(string name, int row, JsonNode? value)
    {
        if (row < 0 || Get(name) is not JsonArray a) return value?.DeepClone();
        var copy = (JsonArray)a.DeepClone();
        while (copy.Count <= row) copy.Add(null);
        copy[row] = value?.DeepClone();
        return copy;
    }

    /// <summary>Bind names referenced by a NUI JSON fragment, skipping the given object keys (child elements).</summary>
    public static IEnumerable<string> BindNames(JsonNode? node, params string[] skipKeys)
    {
        switch (node)
        {
            case JsonObject o:
                if (IsBind(o, out var name))
                {
                    yield return name;
                    yield break;
                }
                foreach (var kv in o)
                {
                    if (skipKeys.Contains(kv.Key)) continue;
                    foreach (var n in BindNames(kv.Value)) yield return n;
                }
                break;
            case JsonArray a:
                foreach (var item in a)
                    foreach (var n in BindNames(item)) yield return n;
                break;
        }
    }
}
