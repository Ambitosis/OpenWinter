using System.Globalization;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// Plugin values kept on an object that can travel with it in its GFF (NWNX_Object_SetInt/String/Float with bPersist,
/// and the plugins' own persisted settings). They are stored in the object's plugin state under "pos:" + key, and the
/// persisted ones are written in the object's "NWNX_POS" field as NWNX:EE writes it (parity suite
/// nwnx_object_serialize): "[INTMAP:n]" then n entries "&lt;len&gt;key = value;", the same for "[FLTMAP:n]", and
/// "[STRMAP:n]" with entries "&lt;len&gt;key = &lt;len&gt;value;"; empty maps are left out and entries are newest first.
/// </summary>
public static class NwnxPersistence
{
    public const string Field = "NWNX_POS";
    const string Prefix = "pos:", Persisted = "posp:";
    // Set once the object's persisted values are known here (set by a plugin or read from its NWNX_POS).
    const string Owned = "pos!owned";

    public static NwnxValue? Get(NwnxRegistry r, uint id, string key) =>
        r.HasState(id) && r.ObjectState(id).TryGetValue(Prefix + key, out var v) ? v : null;

    public static void Set(NwnxRegistry r, uint id, string key, NwnxValue value, bool persist)
    {
        var state = r.ObjectState(id);
        state.Remove(Prefix + key); // a new value is the newest entry
        state[Prefix + key] = value;
        if (persist)
        {
            state[Persisted + key] = NwnxValue.Int(1);
            state[Owned] = NwnxValue.Int(1);
        }
        else state.Remove(Persisted + key);
    }

    public static bool Delete(NwnxRegistry r, uint id, string key)
    {
        if (!r.HasState(id)) return false;
        var state = r.ObjectState(id);
        state.Remove(Persisted + key);
        return state.Remove(Prefix + key);
    }

    /// <summary>Every stored key (persisted or not), oldest first.</summary>
    public static IEnumerable<string> Keys(NwnxRegistry r, uint id) =>
        r.HasState(id) ? r.ObjectState(id).Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).Select(k => k[Prefix.Length..]).ToList() : [];

    /// <summary>The NWNX_POS text for an object's persisted values ("" when it has none).</summary>
    public static string Write(NwnxRegistry r, uint id)
    {
        if (!r.HasState(id)) return "";
        var state = r.ObjectState(id);
        var entries = state.Where(kv => kv.Key.StartsWith(Prefix, StringComparison.Ordinal) && state.ContainsKey(Persisted + kv.Key[Prefix.Length..]))
            .Select(kv => (Key: kv.Key[Prefix.Length..], kv.Value)).Reverse().ToList();
        var sb = new StringBuilder();
        void Map(string name, NwnxType type, Func<NwnxValue, string> text)
        {
            var list = entries.Where(e => e.Value.Type == type).ToList();
            if (list.Count == 0) return;
            sb.Append('[').Append(name).Append(':').Append(list.Count).Append(']');
            foreach (var (key, value) in list) sb.Append('<').Append(Length(key)).Append('>').Append(key).Append(" = ").Append(text(value)).Append(';');
        }
        Map("INTMAP", NwnxType.Int, v => ((int)v.Value!).ToString(CultureInfo.InvariantCulture));
        Map("FLTMAP", NwnxType.Float, v => FormatFloat((float)v.Value!));
        Map("STRMAP", NwnxType.String, v => { var s = (string)v.Value!; return "<" + Length(s) + ">" + s; });
        return sb.ToString();
    }

    /// <summary>Restores persisted values from NWNX_POS text, replacing those the object had. False if it is malformed.</summary>
    public static bool Read(NwnxRegistry r, uint id, string text)
    {
        var parsed = new List<(string Key, NwnxValue Value)>();
        int at = 0;
        while (at < text.Length)
        {
            if (text[at] != '[') return false;
            int colon = text.IndexOf(':', at), close = text.IndexOf(']', at);
            if (colon < 0 || close < colon || !int.TryParse(text.AsSpan(colon + 1, close - colon - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) return false;
            string map = text[(at + 1)..colon];
            at = close + 1;
            for (int i = 0; i < count; i++)
            {
                if (ReadCounted(text, ref at) is not { } key || !text.AsSpan(at).StartsWith(" = ")) return false;
                at += 3;
                NwnxValue value;
                if (map == "STRMAP")
                {
                    if (ReadCounted(text, ref at) is not { } s) return false;
                    value = NwnxValue.String(s);
                }
                else
                {
                    int end = text.IndexOf(';', at);
                    if (end < 0) return false;
                    var number = text[at..end];
                    at = end;
                    if (map == "INTMAP" && int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) value = NwnxValue.Int(n);
                    else if (map == "FLTMAP" && float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) value = NwnxValue.Float(f);
                    else return false;
                }
                if (at >= text.Length || text[at] != ';') return false;
                at++;
                parsed.Add((key, value));
            }
        }
        foreach (var key in Keys(r, id).Where(k => r.ObjectState(id).ContainsKey(Persisted + k)).ToList()) Delete(r, id, key);
        // Entries are written newest first.
        for (int i = parsed.Count - 1; i >= 0; i--) Set(r, id, parsed[i].Key, parsed[i].Value, persist: true);
        r.ObjectState(id)[Owned] = NwnxValue.Int(1);
        return true;
    }

    /// <summary>Writes the field on a serialized object when it has persisted values.</summary>
    public static void WriteField(World world, GameObject o, GffStruct s, bool always = false)
    {
        if (world.Systems.OfType<NwnxRegistry>().FirstOrDefault() is not { } r) return;
        // A field this engine never read or wrote (one it could not parse, say) is kept as it was.
        if (!r.HasState(o.Id) || !r.ObjectState(o.Id).ContainsKey(Owned))
        {
            if (always && !s.Has(Field)) s.SetString(Field, "");
            return;
        }
        s.SetString(Field, Write(r, o.Id));
    }

    /// <summary>Restores the field of a deserialized object.</summary>
    public static void ReadField(World world, GameObject o, GffStruct s)
    {
        if (s.GetString(Field) is not { Length: > 0 } text || world.Systems.OfType<NwnxRegistry>().FirstOrDefault() is not { } r) return;
        Read(r, o.Id, text);
    }

    static string? ReadCounted(string text, ref int at)
    {
        if (at >= text.Length || text[at] != '<') return null;
        int close = text.IndexOf('>', at);
        if (close < 0 || !int.TryParse(text.AsSpan(at + 1, close - at - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int len)) return null;
        int start = close + 1;
        // Lengths count the string's bytes in the game's single-byte encoding.
        if (start + len > text.Length) return null;
        at = start + len;
        return text.Substring(start, len);
    }

    static int Length(string s) => s.Length;

    /// <summary>Six significant digits without trailing zeros (2.5, 0.1, 1e+07), as NWNX:EE writes floats.</summary>
    static string FormatFloat(float f)
    {
        var s = f.ToString("G6", CultureInfo.InvariantCulture);
        int e = s.IndexOf('E');
        if (e < 0) return s;
        string mantissa = s[..e], exp = s[(e + 1)..];
        char sign = exp[0] == '-' ? '-' : '+';
        exp = exp.TrimStart('+', '-').PadLeft(2, '0');
        return mantissa + "e" + sign + exp;
    }
}
