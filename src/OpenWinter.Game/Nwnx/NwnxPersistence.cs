using System.Globalization;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>An object's plugin values (NWNX_POS), newest first; persisted ones travel in its GFF.</summary>
public sealed class NwnxPluginValues
{
    internal readonly List<(string Key, NwnxValue Value, bool Persist)> Entries = new();
    /// <summary>An NWNX_POS this engine couldn't read: kept as it was until a plugin sets a value.</summary>
    internal string? Unread;
}

/// <summary>
/// Plugin values kept on an object that travel with it in its GFF: NWNX_Object_SetInt/String/Float with bPersist and
/// the plugins' own persisted settings (NWNX_Creature, NWNX_Item, ...), one store for every plugin. The persisted ones
/// are written in the object's "NWNX_POS" field as NWNX:EE writes it (parity suites nwnx_object_serialize and
/// nwnx_creature_persist): "[INTMAP:n]" then n entries "&lt;len&gt;key = value;", the same for "[FLTMAP:n]", and
/// "[STRMAP:n]" with entries "&lt;len&gt;key = &lt;len&gt;value;"; empty maps are left out and entries are newest
/// first. An object built from a GFF (a character logging in, a saved game, a copy, a deserialized object) reads its
/// field the first time a value is asked for, and every save path writes it back.
/// </summary>
public static class NwnxPersistence
{
    public const string Field = "NWNX_POS";

    /// <summary>The object's values, read from the GFF it was built from on first use.</summary>
    public static NwnxPluginValues Values(GameObject o)
    {
        if (o.PluginValues is { } v) return v;
        o.PluginValues = v = new NwnxPluginValues();
        if (TemplateAccess.TemplateOf(o)?.GetString(Field) is { Length: > 0 } text) Load(v, text);
        return v;
    }

    public static NwnxValue? Get(GameObject o, string key)
    {
        foreach (var e in Values(o).Entries) if (e.Key == key) return e.Value;
        return null;
    }

    public static void Set(GameObject o, string key, NwnxValue value, bool persist)
    {
        var v = Values(o);
        v.Entries.RemoveAll(e => e.Key == key);
        v.Entries.Insert(0, (key, value, persist)); // a new value is the newest entry
        v.Unread = null;
    }

    public static bool Delete(GameObject o, string key)
    {
        var v = Values(o);
        if (v.Entries.RemoveAll(e => e.Key == key) == 0) return false;
        v.Unread = null;
        return true;
    }

    /// <summary>Every stored key (persisted or not), newest first.</summary>
    public static List<string> Keys(GameObject o) => Values(o).Entries.Select(e => e.Key).ToList();

    /// <summary>The NWNX_POS text for an object's persisted values ("" when it has none).</summary>
    public static string Write(GameObject o)
    {
        var v = Values(o);
        if (v.Unread != null) return v.Unread;
        var sb = new StringBuilder();
        void Map(string name, NwnxType type, Func<NwnxValue, string> text)
        {
            var list = v.Entries.Where(e => e.Persist && e.Value.Type == type).ToList();
            if (list.Count == 0) return;
            sb.Append('[').Append(name).Append(':').Append(list.Count).Append(']');
            foreach (var (key, value, _) in list) sb.Append('<').Append(key.Length).Append('>').Append(key).Append(" = ").Append(text(value)).Append(';');
        }
        Map("INTMAP", NwnxType.Int, x => ((int)x.Value!).ToString(CultureInfo.InvariantCulture));
        Map("FLTMAP", NwnxType.Float, x => FormatFloat((float)x.Value!));
        Map("STRMAP", NwnxType.String, x => { var s = (string)x.Value!; return "<" + s.Length + ">" + s; });
        return sb.ToString();
    }

    /// <summary>Replaces the object's persisted values with NWNX_POS text. False (nothing changed) if it is malformed.</summary>
    public static bool Read(GameObject o, string text)
    {
        if (Parse(text) is not { } parsed) return false;
        var v = Values(o);
        v.Entries.RemoveAll(e => e.Persist);
        v.Entries.InsertRange(0, parsed.Select(p => (p.Key, p.Value, true)));
        v.Unread = null;
        return true;
    }

    static void Load(NwnxPluginValues v, string text)
    {
        if (Parse(text) is { } parsed) v.Entries.AddRange(parsed.Select(p => (p.Key, p.Value, true)));
        else v.Unread = text;
    }

    /// <summary>Writes the field into a serialized object when it has (or had) plugin values.</summary>
    public static void WriteField(GameObject o, GffStruct s, bool always = false)
    {
        // An object nothing set values on keeps whatever field its GFF had (or none).
        if (o.PluginValues == null && TemplateAccess.TemplateOf(o)?.Has(Field) != true)
        {
            if (always && !s.Has(Field)) s.SetString(Field, "");
            return;
        }
        // The original always writes the field once it has one, empty when every value is gone.
        s.SetString(Field, Write(o));
    }

    /// <summary>Restores the field of an object built from a struct it doesn't keep (waypoints, triggers, sounds).</summary>
    public static void ReadField(GameObject o, GffStruct s)
    {
        if (TemplateAccess.TemplateOf(o) != null || s.GetString(Field) is not { Length: > 0 } text) return;
        var v = o.PluginValues ??= new NwnxPluginValues();
        if (!Read(o, text)) v.Unread = text;
    }

    /// <summary>The entries of NWNX_POS text, newest first, or null when it is malformed.</summary>
    public static List<(string Key, NwnxValue Value)>? Parse(string text)
    {
        var parsed = new List<(string Key, NwnxValue Value)>();
        int at = 0;
        while (at < text.Length)
        {
            if (text[at] != '[') return null;
            int colon = text.IndexOf(':', at), close = text.IndexOf(']', at);
            if (colon < 0 || close < colon || !int.TryParse(text.AsSpan(colon + 1, close - colon - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) return null;
            string map = text[(at + 1)..colon];
            if (map is not ("INTMAP" or "FLTMAP" or "STRMAP")) return null;
            at = close + 1;
            for (int i = 0; i < count; i++)
            {
                if (ReadCounted(text, ref at) is not { } key || !text.AsSpan(at).StartsWith(" = ")) return null;
                at += 3;
                NwnxValue value;
                if (map == "STRMAP")
                {
                    if (ReadCounted(text, ref at) is not { } s) return null;
                    value = NwnxValue.String(s);
                }
                else
                {
                    int end = text.IndexOf(';', at);
                    if (end < 0) return null;
                    var number = text[at..end];
                    at = end;
                    if (map == "INTMAP" && int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) value = NwnxValue.Int(n);
                    else if (map == "FLTMAP" && float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) value = NwnxValue.Float(f);
                    else return null;
                }
                if (at >= text.Length || text[at] != ';') return null;
                at++;
                parsed.Add((key, value));
            }
        }
        return parsed;
    }

    static string? ReadCounted(string text, ref int at)
    {
        if (at >= text.Length || text[at] != '<') return null;
        int close = text.IndexOf('>', at);
        if (close < 0 || !int.TryParse(text.AsSpan(at + 1, close - at - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int len)) return null;
        int start = close + 1;
        if (len < 0 || len > text.Length - start) return null;
        at = start + len;
        return text.Substring(start, len);
    }

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
