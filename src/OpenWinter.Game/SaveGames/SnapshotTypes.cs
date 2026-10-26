using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// Type rules shared by <see cref="SnapshotWriter"/> and <see cref="SnapshotReader"/>: which
/// fields are state, which values are engine services (re-bound to the new world on load
/// rather than copied) and which are host resources that never go into a save.
/// </summary>
static partial class SnapshotTypes
{
    /// <summary>Host resources: never saved. Fields holding them (directly or inside a collection) keep the new world's value.</summary>
    static readonly HashSet<Type> Skipped = new()
    {
        typeof(PlayerSession), typeof(SqlDb), typeof(SqlStatement), typeof(MemorySource), typeof(Walkmesh), typeof(Pathfinder),
        typeof(Players.NuiWindow), typeof(TwoDA), typeof(TalkTable), typeof(Random), typeof(Type),
        typeof(Nwnx.Plugins.RisenholmRpc.Listener),
    };

    static readonly string[] SkippedNamespaces =
    {
        "System.Threading", "System.IO", "System.Diagnostics", "System.Reflection", "System.Net", "System.Runtime",
        "System.Linq", "SQLitePCL", "Microsoft.",
    };

    /// <summary>System types that are plain data.</summary>
    static readonly HashSet<Type> SystemData = new()
    {
        typeof(object), typeof(string), typeof(decimal), typeof(DateTime), typeof(TimeSpan), typeof(Guid),
        typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion), typeof(Matrix4x4), typeof(Plane),
    };

    static readonly HashSet<Type> SystemGenericData = new()
    {
        typeof(Nullable<>), typeof(List<>), typeof(Dictionary<,>), typeof(HashSet<>), typeof(SortedDictionary<,>), typeof(SortedSet<>),
        typeof(SortedList<,>), typeof(Stack<>), typeof(Queue<>), typeof(KeyValuePair<,>), typeof(LinkedList<>),
        typeof(ValueTuple<>), typeof(ValueTuple<,>), typeof(ValueTuple<,,>), typeof(ValueTuple<,,,>), typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>), typeof(ValueTuple<,,,,,,>), typeof(ValueTuple<,,,,,,,>),
        typeof(Tuple<>), typeof(Tuple<,>), typeof(Tuple<,,>), typeof(Tuple<,,,>),
        typeof(IList<>), typeof(IReadOnlyList<>), typeof(ICollection<>), typeof(IReadOnlyCollection<>), typeof(IEnumerable<>),
        typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>), typeof(ISet<>),
    };

    static readonly ConcurrentDictionary<Type, bool> _skipCache = new();

    /// <summary>True for host-resource types (and generic types or arrays built from them).</summary>
    public static bool IsSkipped(Type t) => _skipCache.GetOrAdd(t, ComputeSkipped);

    static bool ComputeSkipped(Type t)
    {
        if (t.IsArray) return IsSkipped(t.GetElementType()!);
        if (Skipped.Contains(t)) return true;
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            if (t.GetGenericArguments().Any(IsSkipped)) return true;
            if (def.Namespace?.StartsWith("System", StringComparison.Ordinal) == true && !SystemGenericData.Contains(def)
                && !typeof(Delegate).IsAssignableFrom(t)) return true;
            if (def == typeof(PriorityQueue<,>)) return true;
            return false;
        }
        if (t.IsPrimitive || t.IsEnum || SystemData.Contains(t)) return false;
        if (typeof(JsonNode).IsAssignableFrom(t)) return false;
        if (typeof(Delegate).IsAssignableFrom(t)) return false;
        var ns = t.Namespace ?? "";
        if (SkippedNamespaces.Any(p => ns.StartsWith(p, StringComparison.Ordinal))) return true;
        if (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)) return !t.IsInterface;
        return false;
    }

    /// <summary>Engine services: saved as a name and bound to the loading world's instance.</summary>
    public static string? ServiceName(object value) => value switch
    {
        World => "world",
        IWorldSystem s => "sys:" + s.GetType().FullName,
        ScriptHost => "host",
        ScriptVM => "vm",
        NssDefinitions => "nss",
        NssFunction f => "fn:" + f.Name,
        ResMan => "resman",
        ModelCache => "models",
        CreatureFactory => "creatures",
        ItemFactory => "items",
        AreaLoader => "loader",
        IWorldOutput => "output",
        _ => null,
    };

    public static object? ResolveService(World world, string name)
    {
        switch (name)
        {
            case "world": return world;
            case "host": return world.Scripts;
            case "vm": return world.Scripts?.VM;
            case "nss": return world.Scripts?.Definitions;
            case "resman": return world.Resources;
            case "models": return world.Models;
            case "creatures": return world.Creatures;
            case "items": return world.Items;
            case "loader": return world.Loader;
            case "output": return world.Output;
        }
        if (name.StartsWith("sys:", StringComparison.Ordinal))
            return world.Systems.FirstOrDefault(s => s.GetType().FullName == name[4..]);
        if (name.StartsWith("fn:", StringComparison.Ordinal) && world.Scripts?.Definitions.ByName.TryGetValue(name[3..], out var f) == true)
            return f;
        return null;
    }

    /// <summary>Instance fields of a type and its bases, keyed by name (qualified by the declaring type when a base reuses a name).</summary>
    public static IReadOnlyList<(string Key, FieldInfo Field)> Fields(Type t) => _fields.GetOrAdd(t, ComputeFields);

    static readonly ConcurrentDictionary<Type, (string, FieldInfo)[]> _fields = new();

    static (string, FieldInfo)[] ComputeFields(Type t)
    {
        var list = new List<(string, FieldInfo)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var level = t; level != null && level != typeof(object) && level != typeof(ValueType); level = level.BaseType)
            foreach (var f in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string key = seen.Add(f.Name) ? f.Name : level.Name + "." + f.Name;
                seen.Add(key);
                list.Add((key, f));
            }
        return list.ToArray();
    }

    public static IReadOnlyDictionary<string, FieldInfo> FieldMap(Type t) =>
        _fieldMaps.GetOrAdd(t, x => Fields(x).ToDictionary(p => p.Key, p => p.Field, StringComparer.Ordinal));

    static readonly ConcurrentDictionary<Type, Dictionary<string, FieldInfo>> _fieldMaps = new();

    /// <summary>Compiler-generated closure and state-machine classes (lambdas' captured variables).</summary>
    public static bool IsCompilerGenerated(Type t) =>
        t.Name.Contains('<') || t.IsDefined(typeof(CompilerGeneratedAttribute), false);

    // ---- type names ----

    static readonly Regex AssemblyDetails = new(@", (Version|Culture|PublicKeyToken)=[^,\]]+", RegexOptions.Compiled);
    static readonly ConcurrentDictionary<Type, string> _names = new();
    static readonly ConcurrentDictionary<string, Type?> _types = new(StringComparer.Ordinal);

    public static string TypeName(Type t) => _names.GetOrAdd(t, x => AssemblyDetails.Replace(x.AssemblyQualifiedName ?? x.FullName ?? x.Name, ""));

    public static Type? ResolveType(string name) => _types.GetOrAdd(name, n => Type.GetType(n, throwOnError: false));

    /// <summary>String comparers recognised for Dictionary/HashSet keys.</summary>
    public static string? ComparerName(object? comparer)
    {
        if (comparer == null) return null;
        if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)) return "OrdinalIgnoreCase";
        if (ReferenceEquals(comparer, StringComparer.Ordinal)) return null;
        if (ReferenceEquals(comparer, StringComparer.InvariantCultureIgnoreCase)) return "InvariantCultureIgnoreCase";
        if (ReferenceEquals(comparer, StringComparer.InvariantCulture)) return "InvariantCulture";
        if (ReferenceEquals(comparer, StringComparer.CurrentCultureIgnoreCase)) return "CurrentCultureIgnoreCase";
        if (ReferenceEquals(comparer, ReferenceEqualityComparer.Instance)) return "Reference";
        return null;
    }

    public static object? ComparerByName(string? name) => name switch
    {
        "OrdinalIgnoreCase" => StringComparer.OrdinalIgnoreCase,
        "InvariantCultureIgnoreCase" => StringComparer.InvariantCultureIgnoreCase,
        "InvariantCulture" => StringComparer.InvariantCulture,
        "CurrentCultureIgnoreCase" => StringComparer.CurrentCultureIgnoreCase,
        "Reference" => ReferenceEqualityComparer.Instance,
        _ => null,
    };

    static readonly HashSet<Type> Sequences = new()
    {
        typeof(List<>), typeof(HashSet<>), typeof(SortedSet<>), typeof(Stack<>), typeof(Queue<>), typeof(LinkedList<>),
    };

    /// <summary>Generic collections saved as a JSON array of their items (dictionaries and arrays are handled separately).</summary>
    public static bool IsSequence(Type t) => t.IsGenericType && Sequences.Contains(t.GetGenericTypeDefinition());
}
