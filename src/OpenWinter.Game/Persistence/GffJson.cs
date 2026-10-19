using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// GFF to and from the EE "GFF json" form (the shape ObjectToJson/TemplateToJson
/// emit and the neverwinter.nim tools read/write):
/// <c>{"__data_type":"UTI ","Field":{"type":"byte","value":1},...}</c>; nested structs
/// carry <c>"__struct_id"</c>, void data uses <c>"value64"</c> (base64), localised
/// strings are <c>{"id":strref,"0":"text"}</c>. Fields are emitted in case-insensitive
/// label order, like the reference tools.
/// </summary>
public static class GffJson
{
    static readonly Dictionary<GffType, string> Names = new()
    {
        [GffType.Byte] = "byte", [GffType.Char] = "char", [GffType.Word] = "word", [GffType.Short] = "short",
        [GffType.DWord] = "dword", [GffType.Int] = "int", [GffType.DWord64] = "dword64", [GffType.Int64] = "int64",
        [GffType.Float] = "float", [GffType.Double] = "double", [GffType.ExoString] = "cexostring", [GffType.ResRef] = "resref",
        [GffType.LocString] = "cexolocstring", [GffType.Void] = "void", [GffType.Struct] = "struct", [GffType.List] = "list",
        [GffType.Orientation] = "orientation", [GffType.Vector] = "vector",
    };
    static readonly Dictionary<string, GffType> Types = Names.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    static readonly Comparison<string> LabelOrder = (a, b) =>
    {
        int c = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : string.CompareOrdinal(a, b);
    };

    /// <summary>Root struct to json. <paramref name="fileType"/> is the 4-character GFF type ("UTI ", "BIC ", ...).</summary>
    public static JsonObject ToJson(GffStruct root, string fileType)
    {
        var o = new JsonObject { ["__data_type"] = fileType.PadRight(4)[..4] };
        Fields(root, o);
        return o;
    }

    static JsonObject StructToJson(GffStruct s)
    {
        var o = new JsonObject { ["__struct_id"] = s.StructType };
        Fields(s, o);
        return o;
    }

    static void Fields(GffStruct s, JsonObject into)
    {
        var fields = s.Fields.ToList();
        fields.Sort((a, b) => LabelOrder(a.Label, b.Label));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            if (!seen.Add(f.Label)) continue;
            var field = new JsonObject();
            // Struct fields repeat the struct id beside the type, like the reference tools.
            if (f.Type == GffType.Struct) field["__struct_id"] = (f.Value as GffStruct)?.StructType ?? 0;
            field["type"] = Names.GetValueOrDefault(f.Type, "byte");
            if (f.Type == GffType.Void) field["value64"] = Convert.ToBase64String(f.Value as byte[] ?? Array.Empty<byte>());
            else field["value"] = Value(f);
            into[f.Label] = field;
        }
    }

    static JsonNode? Value(GffField f) => f.Type switch
    {
        GffType.Byte or GffType.Word or GffType.DWord or GffType.Char or GffType.Short or GffType.Int =>
            JsonValue.Create(Convert.ToInt64(f.Value, CultureInfo.InvariantCulture)),
        GffType.DWord64 => JsonValue.Create(Convert.ToUInt64(f.Value, CultureInfo.InvariantCulture)),
        GffType.Int64 => JsonValue.Create(Convert.ToInt64(f.Value, CultureInfo.InvariantCulture)),
        // Floats are written as the double the float widens to, like the reference tools.
        GffType.Float => JsonValue.Create((double)Convert.ToSingle(f.Value, CultureInfo.InvariantCulture)),
        GffType.Double => JsonValue.Create(Convert.ToDouble(f.Value, CultureInfo.InvariantCulture)),
        GffType.ExoString or GffType.ResRef => JsonValue.Create(f.Value as string ?? ""),
        GffType.LocString => Loc(f.Value as LocString ?? new LocString()),
        GffType.Struct => StructToJson(f.Value as GffStruct ?? new GffStruct()),
        GffType.List => new JsonArray(((f.Value as List<GffStruct>) ?? new()).Select(x => (JsonNode?)StructToJson(x)).ToArray()),
        GffType.Vector when f.Value is Vector3 v => new JsonArray(v.X, v.Y, v.Z),
        GffType.Orientation when f.Value is Quaternion q => new JsonArray(q.X, q.Y, q.Z, q.W),
        _ => null,
    };

    static JsonObject Loc(LocString l)
    {
        var o = new JsonObject();
        foreach (var kv in l.Strings) o[kv.Key.ToString(CultureInfo.InvariantCulture)] = kv.Value;
        if (l.StrRef >= 0) o["id"] = l.StrRef;
        return o;
    }

    /// <summary>Json to a root struct. Throws <see cref="FormatException"/> on malformed input.</summary>
    public static GffStruct FromJson(JsonNode? node, out string fileType)
    {
        if (node is not JsonObject o) throw new FormatException("expected a json object");
        fileType = Str(o["__data_type"]) is { Length: > 0 } t ? t.PadRight(4)[..4] : "GFF ";
        var s = new GffStruct(0xFFFFFFFF);
        ReadFields(o, s);
        return s;
    }

    static GffStruct StructFromJson(JsonNode? node) => StructFromJson(node, null);

    static GffStruct StructFromJson(JsonNode? node, JsonNode? outerId)
    {
        if (node is not JsonObject o) throw new FormatException("expected a struct object");
        uint id = (uint)Num(o["__struct_id"] ?? outerId);
        var s = new GffStruct(id);
        ReadFields(o, s);
        return s;
    }

    static void ReadFields(JsonObject o, GffStruct into)
    {
        foreach (var (label, node) in o)
        {
            if (label.StartsWith("__", StringComparison.Ordinal)) continue;
            if (node is not JsonObject field || Str(field["type"]) is not { Length: > 0 } typeName || !Types.TryGetValue(typeName, out var type))
                throw new FormatException($"field {label}: missing or unknown type");
            object value = type switch
            {
                GffType.Byte => (byte)Num(field["value"]),
                GffType.Char => (sbyte)Num(field["value"]),
                GffType.Word => (ushort)Num(field["value"]),
                GffType.Short => (short)Num(field["value"]),
                GffType.DWord => (uint)Num(field["value"]),
                GffType.Int => (int)Num(field["value"]),
                GffType.DWord64 => (ulong)Num(field["value"]),
                GffType.Int64 => Num(field["value"]),
                GffType.Float => (float)Real(field["value"]),
                GffType.Double => Real(field["value"]),
                GffType.ExoString or GffType.ResRef => Str(field["value"]),
                GffType.LocString => LocFromJson(field["value"]),
                GffType.Void => Convert.FromBase64String(field["value64"] != null ? Str(field["value64"]) : Str(field["value"])),
                GffType.Struct => StructFromJson(field["value"], field["__struct_id"]),
                GffType.List => (field["value"] as JsonArray ?? new JsonArray()).Select(x => StructFromJson(x, null)).ToList(),
                GffType.Vector => field["value"] is JsonArray a && a.Count >= 3 ? new Vector3((float)Real(a[0]), (float)Real(a[1]), (float)Real(a[2])) : Vector3.Zero,
                GffType.Orientation => field["value"] is JsonArray q && q.Count >= 4
                    ? new Quaternion((float)Real(q[0]), (float)Real(q[1]), (float)Real(q[2]), (float)Real(q[3])) : Quaternion.Identity,
                _ => 0,
            };
            into.Fields.Add(new GffField(label, type, value));
        }
    }

    static LocString LocFromJson(JsonNode? node)
    {
        var l = new LocString();
        if (node is not JsonObject o) return l;
        foreach (var (key, v) in o)
        {
            if (key == "id") l.StrRef = (int)Num(v);
            else if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lang)) l.Strings[lang] = Str(v);
        }
        return l;
    }

    // Values may be parsed (JsonElement-backed) or built in memory (typed); go through the json text so both work.
    static long Num(JsonNode? n)
    {
        if (n is not JsonValue v) return 0;
        switch (v.GetValueKind())
        {
            case System.Text.Json.JsonValueKind.Number:
                var text = v.ToJsonString();
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
                if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u)) return unchecked((long)u);
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (long)d : 0;
            case System.Text.Json.JsonValueKind.True: return 1;
            default: return 0;
        }
    }

    static double Real(JsonNode? n) =>
        n is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number
        && double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    static string Str(JsonNode? n) => n is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String ? v.GetValue<string>() : "";

    /// <summary>The GFF file type for a resource type id ("UTC ", "UTI ", ...).</summary>
    public static string FileTypeFor(ushort resType) =>
        (Aurora.Resources.ResType.Extension(resType) is { Length: > 0 } ext ? ext.ToUpperInvariant() : "GFF").PadRight(4)[..4];
}
