using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using JsonValue = System.Text.Json.Nodes.JsonValue;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// Turns live engine state into JSON by reflection, so every field any package adds to an
/// object, system or action is saved without per-field code. Encoding (read back by
/// <see cref="SnapshotReader"/>):
/// <list type="bullet">
/// <item>primitives and strings as JSON values; enums as numbers; non-finite floats as strings</item>
/// <item>classes and structs as objects of their fields; <c>"$t"</c> names the runtime type when it differs from the declared one</item>
/// <item>collections as arrays (dictionaries as [key, value] pairs); <c>{"$v": ..}</c> wraps a value that needs a <c>"$t"</c>, <c>"$cmp"</c> (key comparer) or <c>"$id"</c></item>
/// <item>game objects as <c>{"$obj": id}</c>; engine services as <c>{"$svc": name}</c>; shared instances as <c>"$id"</c> + <c>{"$ref": id}</c></item>
/// <item>GFF structs as <c>{"$gff": base64}</c>, json as <c>{"$json": text}</c>, scripts as <c>{"$prog": resref}</c>, delegates as <c>{"$del": [...]}</c></item>
/// </list>
/// </summary>
sealed class SnapshotWriter
{
    sealed class Tracked
    {
        public required int Id;
        public JsonNode? Node;
        public bool Aliased;
    }

    readonly World _world;
    readonly Dictionary<object, Tracked> _seen = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<GameObject, string> _keys = new(ReferenceEqualityComparer.Instance);
    readonly Queue<GameObject> _pending = new();
    int _nextRef = 1, _orphans;

    /// <summary>Every game object referenced so far, in the order they were first referenced.</summary>
    public readonly List<GameObject> Objects = new();
    public readonly SortedSet<string> Warnings = new(StringComparer.Ordinal);

    public SnapshotWriter(World world) { _world = world; }

    /// <summary>
    /// Writes one value on its own (an effect or an action inside an object's serialized state): game objects are
    /// written as their object id when registered and as null otherwise; they are not saved themselves.
    /// </summary>
    public bool Detached;

    /// <summary>Returned for values that are not saved (host resources); fields holding them are left out.</summary>
    static readonly JsonNode Omit = new JsonObject { ["$omit"] = true };

    public static bool IsOmitted(JsonNode? n) => ReferenceEquals(n, Omit);

    // ---------------------------------------------------------------- objects

    /// <summary>The key a game object is saved under: its id when registered with the world, otherwise a generated "u{n}".</summary>
    public string KeyOf(GameObject o)
    {
        if (_keys.TryGetValue(o, out var key)) return key;
        key = _world.Get(o.Id) == o && o.Id != 0 ? o.Id.ToString(CultureInfo.InvariantCulture) : "u" + (++_orphans).ToString(CultureInfo.InvariantCulture);
        _keys[o] = key;
        Objects.Add(o);
        _pending.Enqueue(o);
        return key;
    }

    public bool TryNextPending(out GameObject o) => _pending.TryDequeue(out o!);

    JsonNode? ObjRef(GameObject o)
    {
        if (Detached)
            return _world.Get(o.Id) == o && o.Id != 0 ? new JsonObject { ["$obj"] = o.Id.ToString(CultureInfo.InvariantCulture) } : null;
        var key = KeyOf(o);
        return new JsonObject { ["$obj"] = key };
    }

    // ---------------------------------------------------------------- values

    /// <summary>Encodes a value declared as <paramref name="declared"/>. Returns <see cref="Omit"/> for values that are not saved.</summary>
    public JsonNode? Write(object? v, Type declared, bool allowDelegates = false)
    {
        if (v == null) return null;
        if (v is GameObject go) return ObjRef(go);
        if (SnapshotTypes.ServiceName(v) is { } svc) return new JsonObject { ["$svc"] = svc };
        var t = v.GetType();
        if (v is Delegate d) return allowDelegates ? Poly(WriteDelegate(d), t, declared) : Omit;
        if (SnapshotTypes.IsSkipped(t) || t.IsPointer || v is IntPtr || v is UIntPtr) return Omit;

        if (t == typeof(string)) return JsonValue.Create((string)v);
        if (t.IsPrimitive || t.IsEnum || t == typeof(decimal)) return Poly(Primitive(v, t), t, declared);
        if (t.IsValueType) return Poly(Struct(v, t), t, declared);

        if (_seen.TryGetValue(v, out var seen))
        {
            seen.Aliased = true;
            return new JsonObject { ["$ref"] = seen.Id };
        }
        var tracked = new Tracked { Id = _nextRef++ };
        _seen[v] = tracked;
        var node = Poly(Reference(v, t), t, declared);
        tracked.Node = node;
        return node;
    }

    /// <summary>Adds the runtime type when it differs from the declared type: inside objects, or as a wrapper.</summary>
    static JsonNode? Poly(JsonNode? node, Type actual, Type declared)
    {
        if (actual == declared || Nullable.GetUnderlyingType(declared) == actual || node == null) return node;
        var name = SnapshotTypes.TypeName(actual);
        if (node is JsonObject o && !o.ContainsKey("$v") && !o.ContainsKey("$t")) { o["$t"] = name; return o; }
        return new JsonObject { ["$t"] = name, ["$v"] = node };
    }

    static JsonNode Primitive(object v, Type t)
    {
        if (t.IsEnum) return JsonValue.Create(Convert.ToInt64(v, CultureInfo.InvariantCulture));
        return v switch
        {
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            uint u => JsonValue.Create(u),
            long l => JsonValue.Create(l),
            ulong ul => JsonValue.Create(ul),
            short s => JsonValue.Create(s),
            ushort us => JsonValue.Create(us),
            byte by => JsonValue.Create(by),
            sbyte sb => JsonValue.Create(sb),
            char c => JsonValue.Create((int)c),
            float f => float.IsFinite(f) ? JsonValue.Create(f) : JsonValue.Create(f.ToString("R", CultureInfo.InvariantCulture)),
            double d => double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(d.ToString("R", CultureInfo.InvariantCulture)),
            decimal m => JsonValue.Create(m),
            _ => JsonValue.Create(Convert.ToInt64(v, CultureInfo.InvariantCulture)),
        };
    }

    JsonNode Struct(object v, Type t)
    {
        switch (v)
        {
            case Vector3 v3: return new JsonArray(F(v3.X), F(v3.Y), F(v3.Z));
            case Vector2 v2: return new JsonArray(F(v2.X), F(v2.Y));
            case Cell cell:
                // Script stack cell: the int (or float bits / object id) and, for strings and engine types, the reference.
                if (cell.R == null) return JsonValue.Create(cell.I);
                return new JsonArray(JsonValue.Create(cell.I), Write(cell.R, typeof(object)) is var r && !IsOmitted(r) ? r : null);
        }
        return Fields(v, t, closure: false);
    }

    static JsonNode F(float f) => float.IsFinite(f) ? JsonValue.Create(f) : JsonValue.Create(f.ToString("R", CultureInfo.InvariantCulture));

    JsonNode? Reference(object v, Type t)
    {
        switch (v)
        {
            case byte[] bytes: return JsonValue.Create(Convert.ToBase64String(bytes));
            case bool[] bits: return JsonValue.Create(string.Concat(bits.Select(b => b ? '1' : '0')));
            case GffStruct gff: return new JsonObject { ["$gff"] = Convert.ToBase64String(Gff.Write(gff, "GFF ")) };
            case JsonNode json: return new JsonObject { ["$json"] = json.ToJsonString() };
            case NcsProgram program:
            {
                var o = new JsonObject { ["$prog"] = program.Name };
                if (ScriptChunks.Of(program) is { Length: > 0 } chunk) o["chunk"] = chunk;
                return o;
            }
            case Array array:
            {
                var elem = t.GetElementType()!;
                var a = new JsonArray();
                foreach (var item in array) a.Add(Element(item, elem));
                return a;
            }
            case IDictionary dict:
            {
                var (keyType, valueType) = DictionaryTypes(t);
                var a = new JsonArray();
                foreach (DictionaryEntry e in dict)
                {
                    var k = Element(e.Key, keyType);
                    a.Add(new JsonArray(k, Element(e.Value, valueType)));
                }
                return Compared(a, t, v);
            }
        }
        if (SnapshotTypes.IsSequence(t) && v is IEnumerable seq)
        {
            var elem = t.GetGenericArguments()[0];
            var a = new JsonArray();
            foreach (var item in seq) a.Add(Element(item, elem));
            return Compared(a, t, v);
        }
        return Fields(v, t, SnapshotTypes.IsCompilerGenerated(t));
    }

    JsonNode? Element(object? item, Type declared)
    {
        var n = Write(item, declared);
        return IsOmitted(n) ? null : n;
    }

    static JsonNode Compared(JsonArray a, Type t, object collection)
    {
        if (!t.IsGenericType) return a;
        var comparer = t.GetProperty("Comparer")?.GetValue(collection) ?? t.GetProperty("KeyComparer")?.GetValue(collection);
        return SnapshotTypes.ComparerName(comparer) is { } name ? new JsonObject { ["$cmp"] = name, ["$v"] = a } : a;
    }

    public static (Type Key, Type Value) DictionaryTypes(Type t)
    {
        for (var x = t; x != null; x = x.BaseType)
            if (x.IsGenericType && x.GetGenericArguments().Length == 2) return (x.GetGenericArguments()[0], x.GetGenericArguments()[1]);
        var iface = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        return iface != null ? (iface.GetGenericArguments()[0], iface.GetGenericArguments()[1]) : (typeof(object), typeof(object));
    }

    /// <summary>All saved fields of an object (or boxed struct).</summary>
    public JsonObject Fields(object v, Type t, bool closure)
    {
        var o = new JsonObject();
        foreach (var (key, f) in SnapshotTypes.Fields(t))
        {
            var ft = f.FieldType;
            if (typeof(Delegate).IsAssignableFrom(ft) && !closure) continue;
            if (SnapshotTypes.IsSkipped(ft)) continue;
            var node = Write(f.GetValue(v), ft, allowDelegates: closure);
            if (IsOmitted(node)) continue;
            o[key] = node;
        }
        return o;
    }

    JsonNode WriteDelegate(Delegate d)
    {
        var list = new JsonArray();
        foreach (var part in d.GetInvocationList())
        {
            var m = part.Method;
            if (m.DeclaringType == null) { Warnings.Add($"delegate {m.Name} has no declaring type"); continue; }
            var entry = new JsonObject
            {
                ["type"] = SnapshotTypes.TypeName(m.DeclaringType),
                ["name"] = m.Name,
                ["params"] = new JsonArray(m.GetParameters().Select(p => (JsonNode?)JsonValue.Create(SnapshotTypes.TypeName(p.ParameterType))).ToArray()),
            };
            if (part.Target != null)
            {
                var target = Write(part.Target, typeof(object), allowDelegates: true);
                if (IsOmitted(target)) { Warnings.Add($"delegate {m.DeclaringType.Name}.{m.Name}: target not saveable"); continue; }
                entry["target"] = target;
            }
            list.Add(entry);
        }
        return new JsonObject { ["$del"] = list };
    }

    /// <summary>
    /// Marks instances referenced more than once so the reader shares them again: objects get an "$id"
    /// property; arrays and values are wrapped in {"$id", "$v"} where they first appeared.
    /// </summary>
    public void FinishReferences()
    {
        foreach (var tr in _seen.Values)
        {
            if (!tr.Aliased || tr.Node == null) continue;
            if (tr.Node is JsonObject o) { o["$id"] = tr.Id; continue; }
            var parent = tr.Node.Parent;
            var wrapper = new JsonObject();
            switch (parent)
            {
                case JsonObject po:
                {
                    var key = po.First(kv => ReferenceEquals(kv.Value, tr.Node)).Key;
                    po[key] = null;
                    wrapper["$id"] = tr.Id;
                    wrapper["$v"] = tr.Node;
                    po[key] = wrapper;
                    break;
                }
                case JsonArray pa:
                {
                    int i = 0;
                    while (i < pa.Count && !ReferenceEquals(pa[i], tr.Node)) i++;
                    if (i == pa.Count) break;
                    pa[i] = null;
                    wrapper["$id"] = tr.Id;
                    wrapper["$v"] = tr.Node;
                    pa[i] = wrapper;
                    break;
                }
                default:
                    Warnings.Add($"shared value #{tr.Id} at the top level cannot be marked");
                    break;
            }
        }
    }
}
