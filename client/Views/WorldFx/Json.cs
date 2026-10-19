using System.Text.Json;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>Lenient readers for notify payload fields: missing or mistyped fields give the fallback.</summary>
static class J
{
    public static float F(JsonElement d, string name, float fallback = 0f) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle()
        : d.TryGetProperty(name, out v) && v.ValueKind is JsonValueKind.True ? 1f : fallback;

    public static int I(JsonElement d, string name, int fallback = 0) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? (v.TryGetInt32(out var i) ? i : v.TryGetInt64(out var l) ? unchecked((int)l) : (int)v.GetDouble())
            : d.TryGetProperty(name, out v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? (v.GetBoolean() ? 1 : 0) : fallback;

    public static long L(JsonElement d, string name, long fallback = 0) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : fallback;

    public static uint U(JsonElement d, string name, uint fallback = 0) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetUInt32(out var u) ? u : fallback;

    public static bool B(JsonElement d, string name, bool fallback = false) =>
        d.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.GetDouble() != 0,
            _ => fallback,
        } : fallback;

    public static string S(JsonElement d, string name, string fallback = "") =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    public static bool Has(JsonElement d, string name) => d.ValueKind == JsonValueKind.Object && d.TryGetProperty(name, out _);

    /// <summary>An [x, y, z] array.</summary>
    public static Vector3? V(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array || v.GetArrayLength() < 3) return null;
        return new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle());
    }

    public static float[] Floats(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetSingle()).ToArray()
            : Array.Empty<float>();

    /// <summary>0xRRGGBB as a Godot colour.</summary>
    public static Color Rgb(int rgb) => Color.Color8((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
