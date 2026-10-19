using System.Text.Json.Nodes;
using JNode = System.Text.Json.Nodes.JsonNode;
using JValue = System.Text.Json.Nodes.JsonValue;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>EE json: RFC 6902 patch and diff, array transforms, ranges and set operations, as the original does them.</summary>
public static class JsonExtraFunctions
{
    // JSON_ARRAY_* and JSON_SET_* constants.
    const int SortAscending = 1, SortDescending = 2, Shuffle = 3, Reverse = 4, Unique = 5, Coalesce = 6;
    const int SetSubset = 1, SetUnion = 2, SetIntersect = 3, SetDifference = 4, SetSymmetricDifference = 5;

    static JsonValue Error(string message) => new(null) { Error = message };
    static JNode? Copy(JNode? n) => n?.DeepClone();

    /// <summary>Structural equality; numbers compare by value (4 == 4.0).</summary>
    public static bool Equal(JNode? a, JNode? b) => JsonCore.Equal(a, b);

    /// <summary>The original's total order: null &lt; bool &lt; number &lt; object &lt; array &lt; string.</summary>
    public static int Compare(JNode? a, JNode? b) => JsonCore.Compare(a, b);

    // ---- RFC 6902 patch -------------------------------------------------------

    static JsonCore.ParseException PatchError(int code, string message) => new($"[json.exception.parse_error.{code}] parse error: {message}");

    /// <summary>Applies an RFC 6902 patch to a copy of the document; errors carry the original's messages.</summary>
    public static JNode? Patch(JNode? doc, JNode? patch)
    {
        if (patch is not JsonArray ops) throw PatchError(104, "JSON patch must be an array of objects");
        doc = Copy(doc);
        foreach (var opNode in ops)
        {
            if (opNode is not JsonObject op) throw PatchError(104, "JSON patch must be an array of objects");
            JNode? Member(string opName, string member, bool asString)
            {
                string what = opName == "op" ? "operation" : $"operation '{opName}'";
                if (!op.TryGetPropertyValue(member, out var v)) throw PatchError(105, $"{what} must have member '{member}'");
                if (asString && JsonCore.Kind(v) != JsonKind.String) throw PatchError(105, $"{what} must have string member '{member}'");
                return v;
            }
            string name = JsonCore.Str(Member("op", "op", true)!);
            var path = JsonCore.ParsePointer(JsonCore.Str(Member(name, "path", true)!));
            switch (name)
            {
                case "add":
                    doc = Add(doc, path, Copy(Member(name, "value", false)));
                    break;
                case "remove":
                    Remove(doc, path);
                    break;
                case "replace":
                {
                    var value = Copy(Member(name, "value", false));
                    JsonCore.At(doc, path, path.Count);
                    doc = SetAt(doc, path, value);
                    break;
                }
                case "move":
                {
                    var from = JsonCore.ParsePointer(JsonCore.Str(Member(name, "from", true)!));
                    var v = Copy(JsonCore.At(doc, from, from.Count));
                    Remove(doc, from);
                    doc = Add(doc, path, v);
                    break;
                }
                case "copy":
                {
                    var from = JsonCore.ParsePointer(JsonCore.Str(Member(name, "from", true)!));
                    doc = Add(doc, path, Copy(JsonCore.At(doc, from, from.Count)));
                    break;
                }
                case "test":
                {
                    var expected = Member(name, "value", false);
                    bool ok;
                    try { ok = JsonCore.Equal(JsonCore.At(doc, path, path.Count), expected); }
                    catch (JsonCore.ParseException) { ok = false; }
                    if (!ok) throw new JsonCore.ParseException("[json.exception.other_error.501] unsuccessful: " + JsonCore.Dump(op));
                    break;
                }
                default:
                    throw PatchError(105, $"operation value '{name}' is invalid");
            }
        }
        return doc;
    }

    /// <summary>Replaces the value a pointer names (which must exist).</summary>
    static JNode? SetAt(JNode? doc, List<string> path, JNode? value)
    {
        if (path.Count == 0) return value;
        switch (JsonCore.At(doc, path, path.Count - 1))
        {
            case JsonObject o: o[path[^1]] = value; break;
            case JsonArray a: a[JsonCore.ArrayIndex(path[^1])] = value; break;
        }
        return doc;
    }

    static JNode? Add(JNode? doc, List<string> path, JNode? value)
    {
        if (path.Count == 0) return value;
        // The first token must exist (unless it is the one being added); deeper missing parents become objects.
        if (path.Count > 1) JsonCore.At(doc, path, 1);
        var parent = doc;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var t = path[i];
            switch (parent)
            {
                case JsonObject o:
                    if (!o.TryGetPropertyValue(t, out var next) || next == null) { next = new JsonObject(); o[t] = next; }
                    parent = next;
                    break;
                case JsonArray a:
                    int idx = JsonCore.ArrayIndex(t);
                    if (idx >= a.Count) throw new JsonCore.ParseException($"[json.exception.out_of_range.401] array index {idx} is out of range");
                    parent = a[idx] ?? (a[idx] = new JsonObject());
                    break;
                default:
                    throw new JsonCore.ParseException($"[json.exception.out_of_range.404] unresolved reference token '{t}'");
            }
        }
        var last = path[^1];
        switch (parent)
        {
            case JsonObject o: o[last] = value; break;
            case JsonArray a:
                if (last == "-") a.Add(value);
                else
                {
                    int idx = JsonCore.ArrayIndex(last);
                    if (idx > a.Count) throw new JsonCore.ParseException($"[json.exception.out_of_range.401] array index {idx} is out of range");
                    a.Insert(idx, value);
                }
                break;
            default:
                throw new JsonCore.ParseException($"[json.exception.type_error.313] invalid value to unflatten");
        }
        return doc;
    }

    static void Remove(JNode? doc, List<string> path)
    {
        if (path.Count == 0) throw new JsonCore.ParseException("[json.exception.out_of_range.405] JSON pointer has no parent");
        var parent = JsonCore.At(doc, path, path.Count - 1);
        var last = path[^1];
        switch (parent)
        {
            case JsonObject o:
                if (!o.Remove(last)) throw new JsonCore.ParseException($"[json.exception.out_of_range.403] key '{last}' not found");
                break;
            case JsonArray a:
                int idx = JsonCore.ArrayIndex(last);
                if (idx >= a.Count) throw new JsonCore.ParseException($"[json.exception.out_of_range.401] array index {idx} is out of range");
                a.RemoveAt(idx);
                break;
            default:
                throw new JsonCore.ParseException($"[json.exception.out_of_range.404] unresolved reference token '{last}'");
        }
    }

    [NwFunc("JsonPatch")] static object? JPatch(Call c)
    {
        try { return new JsonValue(Patch(c.J(0).Node, c.J(1).Node)); }
        catch (JsonCore.ParseException e) { return Error(e.Message); }
    }

    // ---- diff ---------------------------------------------------------------------

    /// <summary>A patch turning <paramref name="a"/> into <paramref name="b"/>, built the way the original builds it.</summary>
    public static JsonArray Diff(JNode? a, JNode? b)
    {
        var ops = new JsonArray();
        Diff(a, b, "", ops);
        return ops;
    }

    static JsonObject Op(string op, string path, JNode? value = null, bool withValue = false)
    {
        var o = new JsonObject { ["op"] = op, ["path"] = path };
        if (withValue) o["value"] = Copy(value);
        return o;
    }

    static void Diff(JNode? a, JNode? b, string path, JsonArray ops)
    {
        if (JsonCore.Equal(a, b)) return;
        JsonKind ka = JsonCore.Kind(a), kb = JsonCore.Kind(b);
        if (ka != kb) { ops.Add(Op("replace", path, b, true)); return; }
        if (a is JsonArray xa && b is JsonArray xb)
        {
            int i = 0;
            for (; i < xa.Count && i < xb.Count; i++) Diff(xa[i], xb[i], path + "/" + i, ops);
            // Removals go in from the end, so earlier indices stay valid.
            int at = ops.Count;
            for (; i < xa.Count; i++) ops.Insert(at, Op("remove", path + "/" + i));
            for (; i < xb.Count; i++) ops.Add(Op("add", path + "/-", xb[i], true));
            return;
        }
        if (a is JsonObject oa && b is JsonObject ob)
        {
            foreach (var kv in JsonCore.Sorted(oa))
            {
                var p = path + "/" + JsonCore.EscapeToken(kv.Key);
                if (ob.TryGetPropertyValue(kv.Key, out var bv)) Diff(kv.Value, bv, p, ops);
                else ops.Add(Op("remove", p));
            }
            foreach (var kv in JsonCore.Sorted(ob))
                if (!oa.ContainsKey(kv.Key)) ops.Add(Op("add", path + "/" + JsonCore.EscapeToken(kv.Key), kv.Value, true));
            return;
        }
        ops.Add(Op("replace", path, b, true));
    }

    [NwFunc("JsonDiff")] static object? JDiff(Call c) => new JsonValue(Diff(c.J(0).Node, c.J(1).Node));

    // ---- arrays -------------------------------------------------------------------

    [NwFunc("JsonArrayTransform")] static object? JTransform(Call c)
    {
        if (c.J(0).Node is not JsonArray src) return Error(JsonFunctions.NotAnArray);
        var items = src.Select(Copy).ToList();
        switch (c.I(1))
        {
            case SortAscending: items.Sort(Compare); break;
            case SortDescending: items.Sort((x, y) => Compare(y, x)); break;
            case Shuffle:
                for (int i = items.Count - 1; i > 0; i--)
                {
                    int j = Random.Shared.Next(i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }
                break;
            case Reverse: items.Reverse(); break;
            case Unique:
            {
                // The first of equal values stays (1 and 1.0 are equal).
                var kept = new List<JNode?>();
                foreach (var it in items)
                    if (!kept.Any(k => Equal(k, it))) kept.Add(it);
                items = kept;
                break;
            }
            case Coalesce:
                return new JsonValue(items.FirstOrDefault(i => i != null));
            default: return JsonValue.Null;
        }
        return new JsonValue(new JsonArray(items.ToArray()));
    }

    /// <summary>
    /// Elements begin..end inclusive. A negative begin means 0, a negative end counts back from the end (no further
    /// than the first element), both are clamped to the last element; an end just before begin gives [], further back
    /// fails the way the original does.
    /// </summary>
    [NwFunc("JsonArrayGetRange")] static object? JRange(Call c)
    {
        if (c.J(0).Node is not JsonArray src) return Error(JsonFunctions.NotAnArray);
        var result = new JsonArray();
        if (src.Count == 0) return new JsonValue(result);
        int begin = Math.Min(Math.Max(0, c.I(1)), src.Count - 1), end = c.I(2);
        if (end < 0) end = Math.Max(0, src.Count + end);
        end = Math.Min(end, src.Count - 1);
        if (end < begin - 1) return Error("vector<T> too long"); // one before begin is simply empty
        for (int i = begin; i <= end; i++) result.Add(Copy(src[i]));
        return new JsonValue(result);
    }

    /// <summary>Set operations on the arrays as sorted sets (results come out sorted, without duplicates); SUBSET asks whether o is within v.</summary>
    [NwFunc("JsonSetOp")] static object? JSetOp(Call c)
    {
        if (c.J(0).Node is not JsonArray va || c.J(2).Node is not JsonArray oa) return Error(JsonFunctions.NotAnArrayOrObject);
        static List<JNode?> Set(JsonArray a)
        {
            var list = a.Select(Copy).ToList();
            list.Sort(Compare);
            var unique = new List<JNode?>();
            foreach (var x in list)
                if (unique.Count == 0 || Compare(unique[^1], x) != 0) unique.Add(x);
            return unique;
        }
        var v = Set(va);
        var o = Set(oa);
        bool In(List<JNode?> s, JNode? x) => s.BinarySearch(x, Comparer<JNode?>.Create(Compare)) >= 0;
        static JsonValue Arr(IEnumerable<JNode?> xs) => new(new JsonArray(xs.Select(Copy).ToArray()));
        return c.I(1) switch
        {
            SetSubset => new JsonValue(JValue.Create(o.All(x => In(v, x)))),
            SetUnion => Arr(v.Concat(o.Where(x => !In(v, x))).OrderBy(x => x, Comparer<JNode?>.Create(Compare))),
            SetIntersect => Arr(v.Where(x => In(o, x))),
            SetDifference => Arr(v.Where(x => !In(o, x))),
            SetSymmetricDifference => Arr(v.Where(x => !In(o, x)).Concat(o.Where(x => !In(v, x))).OrderBy(x => x, Comparer<JNode?>.Create(Compare))),
            _ => JsonValue.Null,
        };
    }
}
