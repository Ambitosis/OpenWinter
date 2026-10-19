using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using JsonValue = System.Text.Json.Nodes.JsonValue;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// Reads values written by <see cref="SnapshotWriter"/>. Values are read into existing
/// instances where possible (the new world's systems, collections created by field
/// initializers), so fields that are not saved keep the new world's values and
/// collections keep their key comparers.
/// </summary>
sealed class SnapshotReader
{
    readonly World _world;
    readonly IReadOnlyDictionary<string, GameObject> _objects;
    readonly Dictionary<int, object> _refs = new();
    public readonly SortedSet<string> Warnings = new(StringComparer.Ordinal);

    public SnapshotReader(World world, IReadOnlyDictionary<string, GameObject> objects)
    {
        _world = world;
        _objects = objects;
    }

    public T? Read<T>(JsonNode? n, T? existing = default) => (T?)Read(n, typeof(T), existing);

    public object? Read(JsonNode? n, Type declared, object? existing)
    {
        if (Nullable.GetUnderlyingType(declared) is { } underlying)
            return n == null ? null : Read(n, underlying, null);
        if (n == null) return declared.IsValueType ? Activator.CreateInstance(declared) : null;

        if (n is JsonObject o)
        {
            if (o.TryGetPropertyValue("$obj", out var key)) return ResolveObject(key);
            if (o.TryGetPropertyValue("$svc", out var svc)) return SnapshotTypes.ResolveService(_world, svc!.GetValue<string>()) ?? Warn<object>($"service '{svc}' not available");
            if (o.TryGetPropertyValue("$ref", out var r))
            {
                int id = r!.GetValue<int>();
                return _refs.TryGetValue(id, out var shared) ? shared : Warn<object>($"shared value #{id} missing");
            }
            var actual = declared;
            if (o.TryGetPropertyValue("$t", out var tn))
            {
                var name = tn!.GetValue<string>();
                actual = SnapshotTypes.ResolveType(name)!;
                if (actual == null) return Default(declared, $"type '{name}' no longer exists");
            }
            int? refId = o.TryGetPropertyValue("$id", out var idNode) ? idNode!.GetValue<int>() : null;
            if (o.TryGetPropertyValue("$v", out var payload))
                return Payload(payload, actual, existing, o.TryGetPropertyValue("$cmp", out var cmp) ? cmp!.GetValue<string>() : null, refId);
            if (o.TryGetPropertyValue("$gff", out var gff)) return Register(refId, Gff.Read(Convert.FromBase64String(gff!.GetValue<string>())));
            if (o.TryGetPropertyValue("$json", out var json)) return Register(refId, JsonNode.Parse(json!.GetValue<string>()));
            if (o.TryGetPropertyValue("$prog", out var prog)) return Register(refId, Program(prog!.GetValue<string>()));
            if (o.TryGetPropertyValue("$del", out var del)) return Register(refId, Delegate(del as JsonArray, actual));

            if (actual.IsValueType)
            {
                var box = existing != null && existing.GetType() == actual ? existing : Create(actual);
                Populate(box, o);
                return box;
            }
            if (actual.IsAbstract || actual.IsInterface) return Default(declared, $"no concrete type for a {actual.Name}");
            var instance = existing != null && existing.GetType() == actual ? existing : Create(actual);
            Register(refId, instance);
            Populate(instance, o);
            return instance;
        }
        return Payload(n, declared, existing, null, null);
    }

    object? Default(Type declared, string warning)
    {
        Warnings.Add(warning);
        return declared.IsValueType ? Activator.CreateInstance(declared) : null;
    }

    T? Warn<T>(string warning) where T : class
    {
        Warnings.Add(warning);
        return null;
    }

    object? Register(int? id, object? value)
    {
        if (id is { } i && value != null) _refs[i] = value;
        return value;
    }

    GameObject? ResolveObject(JsonNode? key)
    {
        var k = key is JsonValue v && v.TryGetValue<string>(out var s) ? s : key?.ToJsonString() ?? "";
        if (_objects.TryGetValue(k, out var o)) return o;
        Warnings.Add($"object {k} missing");
        return null;
    }

    NcsProgram? Program(string name)
    {
        var p = _world.Scripts?.ProgramFor(name);
        if (p == null) Warnings.Add($"script '{name}' not found; closures using it are dropped");
        return p;
    }

    /// <summary>Fills an object's (or boxed struct's) fields from a saved field set.</summary>
    public void Populate(object target, JsonObject o)
    {
        var map = SnapshotTypes.FieldMap(target.GetType());
        foreach (var (key, node) in o)
        {
            if (key.StartsWith('$')) continue;
            if (!map.TryGetValue(key, out var f))
            {
                Warnings.Add($"{target.GetType().Name}.{key} no longer exists");
                continue;
            }
            try
            {
                var current = f.GetValue(target);
                var value = Read(node, f.FieldType, current);
                if (!ReferenceEquals(value, current) || f.FieldType.IsValueType) f.SetValue(target, value);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or JsonException
                or InvalidCastException or NotSupportedException or OverflowException or MissingMethodException or TargetInvocationException)
            {
                Warnings.Add($"{target.GetType().Name}.{key}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// A new instance for a saved value, built by a constructor when one can be called (so fields
    /// that are not saved, like a random generator, get their usual values): a parameterless one,
    /// or one whose parameters are optional or engine services of the loading world.
    /// </summary>
    object Create(Type t, object? comparer = null)
    {
        if (comparer != null)
        {
            var ctor = t.GetConstructors().FirstOrDefault(c => c.GetParameters() is [var p] && p.ParameterType.IsInstanceOfType(comparer));
            if (ctor != null) return ctor.Invoke(new[] { comparer });
        }
        if (t.IsValueType) return Activator.CreateInstance(t)!;
        if (t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null)
            return Activator.CreateInstance(t, nonPublic: true)!;
        foreach (var ctor in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(c => c.GetParameters().Length))
        {
            var ps = ctor.GetParameters();
            var args = new object?[ps.Length];
            bool ok = true;
            for (int i = 0; i < ps.Length && ok; i++)
            {
                if (Service(ps[i].ParameterType) is { } svc) args[i] = svc;
                else if (ps[i].IsOptional) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                else ok = false;
            }
            if (!ok) continue;
            try { return ctor.Invoke(args); }
            catch (TargetInvocationException) { }
        }
        return RuntimeHelpers.GetUninitializedObject(t);
    }

    object? Service(Type t)
    {
        if (t == typeof(World)) return _world;
        if (t == typeof(ScriptHost)) return _world.Scripts;
        if (t == typeof(Aurora.Resources.ResMan)) return _world.Resources;
        if (typeof(IWorldSystem).IsAssignableFrom(t)) return _world.Systems.FirstOrDefault(t.IsInstanceOfType);
        return null;
    }

    /// <summary>A value encoded as a JSON array or value (primitives, vectors, arrays, collections).</summary>
    object? Payload(JsonNode? n, Type t, object? existing, string? comparer, int? refId)
    {
        if (n == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
        if (n is JsonObject) return Register(refId, Read(n, t, existing));
        if (t == typeof(string)) return n.GetValue<string>();
        if (t == typeof(object))
        {
            if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s;
            return Default(t, "untyped value");
        }
        if (t.IsEnum) return Enum.ToObject(t, Number<long>(n));
        if (t.IsPrimitive || t == typeof(decimal)) return Primitive(n, t);
        if (t == typeof(Vector3) && n is JsonArray v3) return new Vector3(Float(v3[0]), Float(v3[1]), Float(v3[2]));
        if (t == typeof(Vector2) && n is JsonArray v2) return new Vector2(Float(v2[0]), Float(v2[1]));
        if (t == typeof(Cell))
        {
            if (n is JsonArray c) return new Cell { I = Number<int>(c[0]), R = c.Count > 1 ? Read(c[1], typeof(object), null) : null };
            return new Cell { I = Number<int>(n) };
        }
        if (t == typeof(byte[])) return Register(refId, Convert.FromBase64String(n.GetValue<string>()));
        if (t == typeof(bool[])) return Register(refId, n.GetValue<string>().Select(ch => ch == '1').ToArray());
        if (n is not JsonArray a) return Default(t, $"cannot read a {t.Name} from {n.ToJsonString()}");

        if (t.IsArray)
        {
            var elem = t.GetElementType()!;
            var array = Array.CreateInstance(elem, a.Count);
            Register(refId, array);
            for (int i = 0; i < a.Count; i++) array.SetValue(Read(a[i], elem, null), i);
            return array;
        }
        var cmp = SnapshotTypes.ComparerByName(comparer);
        if (typeof(IDictionary).IsAssignableFrom(t))
        {
            var dict = existing is IDictionary d && existing.GetType() == t ? d : (IDictionary)Create(t, cmp);
            dict.Clear();
            Register(refId, dict);
            var (kt, vt) = SnapshotWriter.DictionaryTypes(t);
            foreach (var pair in a)
            {
                if (pair is not JsonArray kv || kv.Count < 2) continue;
                var k = Read(kv[0], kt, null);
                if (k == null) { Warnings.Add($"{t.Name}: entry with a missing key dropped"); continue; }
                dict[k] = Read(kv[1], vt, null);
            }
            return dict;
        }
        if (SnapshotTypes.IsSequence(t))
        {
            var collection = existing != null && existing.GetType() == t ? existing : Create(t, cmp);
            t.GetMethod("Clear", Type.EmptyTypes)?.Invoke(collection, null);
            Register(refId, collection);
            var elem = t.GetGenericArguments()[0];
            var items = a.Select(x => Read(x, elem, null)).ToList();
            var def = t.GetGenericTypeDefinition();
            if (def == typeof(Stack<>)) items.Reverse();
            var add = t.GetMethod(def == typeof(Stack<>) ? "Push" : def == typeof(Queue<>) ? "Enqueue" : def == typeof(LinkedList<>) ? "AddLast" : "Add", new[] { elem })!;
            foreach (var item in items) add.Invoke(collection, new[] { item });
            return collection;
        }
        return Default(t, $"cannot read a {t.Name} from an array");
    }

    static T Number<T>(JsonNode? n) where T : struct
    {
        if (n is JsonValue v && v.TryGetValue<T>(out var x)) return x;
        return (T)Convert.ChangeType(n!.ToJsonString().Trim('"'), typeof(T), CultureInfo.InvariantCulture);
    }

    static float Float(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? float.Parse(s, CultureInfo.InvariantCulture) : Number<float>(n);
    static double Double(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? double.Parse(s, CultureInfo.InvariantCulture) : Number<double>(n);

    static object Primitive(JsonNode n, Type t)
    {
        if (t == typeof(bool)) return n.GetValue<bool>();
        if (t == typeof(float)) return Float(n);
        if (t == typeof(double)) return Double(n);
        if (t == typeof(char)) return (char)Number<int>(n);
        if (t == typeof(int)) return Number<int>(n);
        if (t == typeof(uint)) return Number<uint>(n);
        if (t == typeof(long)) return Number<long>(n);
        if (t == typeof(ulong)) return Number<ulong>(n);
        if (t == typeof(short)) return Number<short>(n);
        if (t == typeof(ushort)) return Number<ushort>(n);
        if (t == typeof(byte)) return Number<byte>(n);
        if (t == typeof(sbyte)) return Number<sbyte>(n);
        if (t == typeof(decimal)) return Number<decimal>(n);
        return Convert.ChangeType(n.ToJsonString(), t, CultureInfo.InvariantCulture);
    }

    Delegate? Delegate(JsonArray? parts, Type delegateType)
    {
        if (parts == null) return null;
        if (!typeof(Delegate).IsAssignableFrom(delegateType)) return Warn<Delegate>($"not a delegate type: {delegateType.Name}");
        Delegate? result = null;
        foreach (var p in parts.OfType<JsonObject>())
        {
            string typeName = p["type"]!.GetValue<string>(), name = p["name"]!.GetValue<string>();
            var owner = SnapshotTypes.ResolveType(typeName);
            var paramTypes = (p["params"] as JsonArray)?.Select(x => SnapshotTypes.ResolveType(x!.GetValue<string>())).ToArray() ?? Array.Empty<Type?>();
            var method = owner == null || paramTypes.Any(x => x == null) ? null
                : owner.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, paramTypes!, null);
            if (method == null) { Warnings.Add($"delegate {typeName}.{name} no longer exists; dropped"); continue; }
            object? target = p.TryGetPropertyValue("target", out var tn) ? Read(tn, typeof(object), null) : null;
            if (!method.IsStatic && target == null) { Warnings.Add($"delegate {owner!.Name}.{name}: target missing; dropped"); continue; }
            try
            {
                var part = method.IsStatic ? System.Delegate.CreateDelegate(delegateType, method) : System.Delegate.CreateDelegate(delegateType, target, method);
                result = System.Delegate.Combine(result, part);
            }
            catch (ArgumentException e) { Warnings.Add($"delegate {owner!.Name}.{name}: {e.Message}"); }
        }
        return result;
    }
}
