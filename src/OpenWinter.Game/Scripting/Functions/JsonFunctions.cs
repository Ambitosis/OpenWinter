using System.Text.Json.Nodes;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// EE json type: construction, access and mutation. A json variable refers to a value (assigning it shares
/// the value, which the *Inplace functions change); the other functions return changed copies. Behaviour and
/// error texts follow the original (see <see cref="JsonCore"/>).
/// </summary>
static class JsonFunctions
{
    // JSON_TYPE_* constants.
    const int TypeNull = 0, TypeObject = 1, TypeArray = 2, TypeString = 3, TypeInteger = 4, TypeFloat = 5, TypeBool = 6;

    public const string NotAnObject = "[json.exception.type_error.302] not an object";
    public const string NotAnArray = "[json.exception.type_error.302] not an array";
    public const string NotAnArrayOrObject = "[json.exception.type_error.302] not an array or object";
    public const string OutOfRange = "[json.exception.out_of_range.401] index out of range";

    /// <summary>Parses json text (not script text: no codepage conversion).</summary>
    public static JsonValue Parse(string text)
    {
        try { return new JsonValue(JsonCore.Parse(text)); }
        catch (JsonCore.ParseException e) { return new JsonValue(null) { Error = e.Message }; }
    }

    /// <summary>Parses a script string (Windows-1252 bytes).</summary>
    public static JsonValue ParseScript(string text) => Parse(JsonCore.FromScript(text));

    public static string Dump(JsonValue v) => JsonCore.Dump(v.Node);

    public static bool DeepEquals(JsonValue? a, JsonValue? b) => JsonCore.Equal(a?.Node, b?.Node);

    public static JsonNode? Copy(JsonNode? n) => n?.DeepClone();
    static JsonValue Wrap(JsonNode? n) => new(n);
    public static JsonValue Error(string message) => new(null) { Error = message };

    static int TypeOf(JsonNode? n) => JsonCore.Kind(n) switch
    {
        JsonKind.Null => TypeNull,
        JsonKind.Object => TypeObject,
        JsonKind.Array => TypeArray,
        JsonKind.String => TypeString,
        JsonKind.Bool => TypeBool,
        JsonKind.Float => TypeFloat,
        _ => TypeInteger,
    };

    /// <summary>A failed in-place change leaves the variable null, carrying the error.</summary>
    static object? Fail(JsonValue target, string message)
    {
        target.Node = null;
        target.Error = message;
        return null;
    }

    [NwFunc("JsonParse")] static object? JParse(Call c) => ParseScript(c.S(0));
    [NwFunc("JsonDump")] static object? JDump(Call c) => JsonCore.Dump(c.J(0).Node, c.I(1, -1));
    [NwFunc("JsonNull")] static object? JNull(Call c) => new JsonValue(null) { Error = c.S(0) };
    [NwFunc("JsonObject")] static object? JObject(Call c) => Wrap(new JsonObject());
    [NwFunc("JsonArray")] static object? JArray(Call c) => Wrap(new JsonArray());
    [NwFunc("JsonString")] static object? JString(Call c) => Wrap(System.Text.Json.Nodes.JsonValue.Create(JsonCore.FromScript(c.S(0))));
    [NwFunc("JsonInt")] static object? JInt(Call c) => Wrap(JsonCore.Int(c.I(0)));
    [NwFunc("JsonFloat")] static object? JFloat(Call c) => Wrap(JsonCore.Float(c.F(0)));
    [NwFunc("JsonBool")] static object? JBool(Call c) => Wrap(System.Text.Json.Nodes.JsonValue.Create(c.I(0) != 0));
    [NwFunc("JsonGetType")] static object? JType(Call c) => TypeOf(c.J(0).Node);
    [NwFunc("JsonGetError")] static object? JError(Call c) => JsonCore.ToScript(c.J(0).Error);
    /// <summary>Members of an object or array; 0 for null, 1 for any other value.</summary>
    [NwFunc("JsonGetLength")] static object? JLength(Call c) => c.J(0).Node switch { null => 0, JsonObject o => o.Count, JsonArray a => a.Count, _ => 1 };
    [NwFunc("JsonGetString")] static object? JGetString(Call c) =>
        JsonCore.Kind(c.J(0).Node) == JsonKind.String ? JsonCore.ToScript(JsonCore.Str(c.J(0).Node!)) : "";
    /// <summary>Integers wrap to 32 bits; floats truncate (out of range gives int.MinValue); bools are 0/1; anything else 0.</summary>
    [NwFunc("JsonGetInt")] static object? JGetInt(Call c)
    {
        var n = c.J(0).Node;
        return JsonCore.Kind(n) switch
        {
            JsonKind.Int or JsonKind.UInt or JsonKind.Bool => unchecked((int)JsonCore.Long(n!)),
            JsonKind.Float => JsonCore.Double(n!) is var d && d > -2147483649.0 && d < 2147483648.0 ? (int)d : int.MinValue,
            _ => 0,
        };
    }
    [NwFunc("JsonGetFloat")] static object? JGetFloat(Call c)
    {
        var n = c.J(0).Node;
        return JsonCore.Kind(n) switch
        {
            JsonKind.Int => (float)JsonCore.Long(n!),
            JsonKind.UInt => (float)JsonCore.ULong(n!),
            JsonKind.Float => (float)JsonCore.Double(n!),
            JsonKind.Bool => JsonCore.Bool(n!) ? 1f : 0f,
            _ => 0f,
        };
    }

    [NwFunc("JsonObjectKeys")] static object? JKeys(Call c) =>
        c.J(0).Node is JsonObject o
            ? Wrap(new JsonArray(JsonCore.Sorted(o).Select(kv => (JsonNode?)System.Text.Json.Nodes.JsonValue.Create(kv.Key)).ToArray()))
            : Error(NotAnObject);
    [NwFunc("JsonObjectGet")] static object? JObjGet(Call c) =>
        c.J(0).Node is JsonObject o && o.TryGetPropertyValue(JsonCore.FromScript(c.S(1)), out var v) ? Wrap(Copy(v))
            : Error("[json.exception.type_error.302] not an object or key not found");
    [NwFunc("JsonObjectSet")] static object? JObjSet(Call c)
    {
        if (Copy(c.J(0).Node) is not JsonObject o) return Error(NotAnObject);
        o[JsonCore.FromScript(c.S(1))] = Copy(c.J(2).Node);
        return Wrap(o);
    }
    [NwFunc("JsonObjectDel")] static object? JObjDel(Call c)
    {
        if (Copy(c.J(0).Node) is not JsonObject o) return Error(NotAnObject);
        o.Remove(JsonCore.FromScript(c.S(1)));
        return Wrap(o);
    }
    [NwFunc("JsonArrayGet")] static object? JArrGet(Call c) =>
        c.J(0).Node is not JsonArray a ? Error(NotAnArray) : c.I(1) >= 0 && c.I(1) < a.Count ? Wrap(Copy(a[c.I(1)])) : Error(OutOfRange);
    [NwFunc("JsonArraySet")] static object? JArrSet(Call c)
    {
        if (Copy(c.J(0).Node) is not JsonArray a) return Error(NotAnArray);
        if (c.I(1) < 0 || c.I(1) >= a.Count) return Error(OutOfRange);
        a[c.I(1)] = Copy(c.J(2).Node);
        return Wrap(a);
    }
    /// <summary>Index -1 appends; otherwise it must name an existing element, which the new one goes before.</summary>
    [NwFunc("JsonArrayInsert")] static object? JArrInsert(Call c)
    {
        if (Copy(c.J(0).Node) is not JsonArray a) return Error(NotAnArray);
        int at = c.I(2, -1);
        if (at == -1) a.Add(Copy(c.J(1).Node));
        else if (at < -1 || at >= a.Count) return Error(OutOfRange);
        else a.Insert(at, Copy(c.J(1).Node));
        return Wrap(a);
    }
    [NwFunc("JsonArrayDel")] static object? JArrDel(Call c)
    {
        if (Copy(c.J(0).Node) is not JsonArray a) return Error(NotAnArray);
        if (c.I(1) < 0 || c.I(1) >= a.Count) return Error(OutOfRange);
        a.RemoveAt(c.I(1));
        return Wrap(a);
    }

    [NwFunc("JsonObjectSetInplace")] static object? ObjSetInplace(Call c)
    {
        var t = c.J(0);
        if (t.Node is not JsonObject o) return Fail(t, NotAnObject);
        o[JsonCore.FromScript(c.S(1))] = Copy(c.J(2).Node);
        return null;
    }
    [NwFunc("JsonObjectDelInplace")] static object? ObjDelInplace(Call c)
    {
        var t = c.J(0);
        if (t.Node is not JsonObject o) return Fail(t, NotAnObject);
        o.Remove(JsonCore.FromScript(c.S(1)));
        return null;
    }
    [NwFunc("JsonArrayInsertInplace")] static object? ArrInsertInplace(Call c)
    {
        var t = c.J(0);
        if (t.Node is not JsonArray a) return Fail(t, NotAnArray);
        int at = c.I(2, -1);
        var value = Copy(c.J(1).Node);
        if (at == -1) a.Add(value);
        else if (at < -1 || at >= a.Count) return Fail(t, OutOfRange);
        else a.Insert(at, value);
        return null;
    }
    [NwFunc("JsonArraySetInplace")] static object? ArrSetInplace(Call c)
    {
        var t = c.J(0);
        if (t.Node is not JsonArray a) return Fail(t, NotAnArray);
        if (c.I(1) < 0 || c.I(1) >= a.Count) return Fail(t, OutOfRange);
        a[c.I(1)] = Copy(c.J(2).Node);
        return null;
    }
    [NwFunc("JsonArrayDelInplace")] static object? ArrDelInplace(Call c)
    {
        var t = c.J(0);
        if (t.Node is not JsonArray a) return Fail(t, NotAnArray);
        if (c.I(1) < 0 || c.I(1) >= a.Count) return Fail(t, OutOfRange);
        a.RemoveAt(c.I(1));
        return null;
    }

    /// <summary>The nth (from 0) element index or object key whose value compares with the needle as asked.</summary>
    [NwFunc("JsonFind")] static object? JFind(Call c)
    {
        var needle = c.J(1).Node;
        int nth = c.I(2), cond = c.I(3);
        bool Match(JsonNode? v)
        {
            int cmp = JsonCore.Compare(v, needle);
            return cond switch { 1 => cmp < 0, 2 => cmp <= 0, 3 => cmp > 0, 4 => cmp >= 0, _ => JsonCore.Equal(v, needle) };
        }
        switch (c.J(0).Node)
        {
            case JsonArray a:
                for (int i = 0; i < a.Count; i++)
                    if (Match(a[i]) && nth-- == 0) return Wrap(JsonCore.Int(i));
                return JsonValue.Null;
            case JsonObject o:
                foreach (var kv in JsonCore.Sorted(o))
                    if (Match(kv.Value) && nth-- == 0) return Wrap(System.Text.Json.Nodes.JsonValue.Create(kv.Key));
                return JsonValue.Null;
            default:
                return Error(NotAnArrayOrObject);
        }
    }

    /// <summary>RFC 7386 merge patch.</summary>
    [NwFunc("JsonMerge")] static object? JMerge(Call c)
    {
        static JsonNode? Merge(JsonNode? target, JsonNode? patch)
        {
            if (patch is not JsonObject p) return Copy(patch);
            var t = Copy(target) as JsonObject ?? new JsonObject();
            foreach (var kv in p)
            {
                if (kv.Value == null) t.Remove(kv.Key);
                else t[kv.Key] = Merge(t.TryGetPropertyValue(kv.Key, out var old) ? old : null, kv.Value);
            }
            return t;
        }
        return Wrap(Merge(c.J(0).Node, c.J(1).Node));
    }

    /// <summary>Malformed pointers report the parse error; anything not found is "value not found".</summary>
    [NwFunc("JsonPointer")] static object? JPointer(Call c)
    {
        List<string> tokens;
        try { tokens = JsonCore.ParsePointer(JsonCore.FromScript(c.S(1))); }
        catch (JsonCore.ParseException e) { return Error(e.Message); }
        try { return Wrap(Copy(JsonCore.At(c.J(0).Node, tokens, tokens.Count))); }
        catch (JsonCore.ParseException) { return Error("value not found"); }
    }
}
