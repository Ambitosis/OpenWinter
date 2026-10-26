using System.Globalization;
using System.Numerics;
using System.Reflection;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx;

/// <summary>Value kinds that travel over the NWNX argument stack.</summary>
public enum NwnxType { Int, Float, Object, String, Vector, Location, Effect, ItemProperty, Json, Action, Event, Talent, SqlQuery, Cassowary }

/// <summary>One typed stack entry.</summary>
public readonly record struct NwnxValue(NwnxType Type, object? Value)
{
    public static NwnxValue Int(int v) => new(NwnxType.Int, v);
    public static NwnxValue Float(float v) => new(NwnxType.Float, v);
    public static NwnxValue Object(uint v) => new(NwnxType.Object, v);
    public static NwnxValue String(string v) => new(NwnxType.String, v);

    /// <summary>A stable text form, used to key the generic Set/Get store.</summary>
    public override string ToString() => Value switch
    {
        uint o => o.ToString("x", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable fm => fm.ToString(null, CultureInfo.InvariantCulture),
        null => "",
        var v => v.ToString() ?? "",
    };
}

/// <summary>A LIFO stack of typed values: scripts push arguments, plugins pop them and push results.</summary>
public sealed class NwnxStack
{
    readonly List<NwnxValue> _items = new();

    public int Count => _items.Count;
    public void Push(NwnxValue v) => _items.Add(v);
    public void Clear() => _items.Clear();
    public NwnxValue? Peek() => _items.Count > 0 ? _items[^1] : null;

    public NwnxValue? Pop()
    {
        if (_items.Count == 0) return null;
        var v = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        return v;
    }

    /// <summary>Moves every entry into a new stack (same order) and empties this one.</summary>
    public NwnxStack Drain()
    {
        var s = new NwnxStack();
        s._items.AddRange(_items);
        _items.Clear();
        return s;
    }

    /// <summary>Entries from the top down.</summary>
    public IEnumerable<NwnxValue> TopDown()
    {
        for (int i = _items.Count - 1; i >= 0; i--) yield return _items[i];
    }
}

/// <summary>Marks a static <c>void M(NwnxCall n)</c> as the handler for an NWNX plugin function.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class NwnxFuncAttribute : Attribute
{
    public string Plugin { get; }
    public string Function { get; }
    /// <summary>Known missing engine behavior; the handler still preserves its existing ABI.</summary>
    public string? UnsupportedReason { get; set; }
    /// <param name="plugin">Plugin name with or without the "NWNX_" prefix.</param>
    public NwnxFuncAttribute(string plugin, string function) { Plugin = NwnxRegistry.PluginKey(plugin); Function = function; }
}

/// <summary>
/// One plugin call: the arguments the script pushed (popped in the reverse of
/// push order, like NWNX:EE) and the result stack the script pops from.
/// </summary>
public sealed class NwnxCall
{
    public required World World;
    public required NwnxRegistry Registry;
    public required string Plugin;
    public required string Function;
    public required NwnxStack Args;
    public required NwnxStack Results;
    /// <summary>The calling script, when called from NWScript.</summary>
    public ScriptHost? Host;
    public ScriptThread? Thread;

    /// <summary>Count each unsupported call, logging its limitation once per world (and per <paramref name="detail"/>, when given).</summary>
    public void ReportUnsupported(string reason, string? detail = null) => Registry.ReportUnsupported(Plugin, Function, reason, Thread?.Program.Name, detail);

    public NwnxValue? PopValue() => Args.Pop();

    public int PopInt() => Args.Pop() switch
    {
        { Value: int i } => i,
        { Value: float f } => (int)f,
        { Value: uint o } => unchecked((int)o),
        _ => 0,
    };
    public float PopFloat() => Args.Pop() switch
    {
        { Value: float f } => f,
        { Value: int i } => i,
        _ => 0f,
    };
    public uint PopObjectId() => Args.Pop() is { Value: uint o } ? o : GameObject.InvalidId;
    public GameObject? PopObject() => World.Get(PopObjectId());
    public T? PopObject<T>() where T : GameObject => PopObject() as T;
    public string PopString() => Args.Pop() switch
    {
        { Value: string s } => s,
        { Value: int i } => i.ToString(CultureInfo.InvariantCulture),
        { Value: float f } => f.ToString(CultureInfo.InvariantCulture),
        _ => "",
    };
    public Vector3 PopVector() => Args.Pop() is { Value: Vector3 v } ? v : Vector3.Zero;
    public Location PopLocation() => Args.Pop() is { Value: Location l } ? l : Location.Invalid;
    public Effect? PopEffect() => Args.Pop()?.Value as Effect;
    public JsonValue PopJson() => Args.Pop()?.Value as JsonValue ?? JsonValue.Null;

    public void Push(int v) => Results.Push(NwnxValue.Int(v));
    public void Push(bool v) => Results.Push(NwnxValue.Int(v ? 1 : 0));
    public void Push(float v) => Results.Push(NwnxValue.Float(v));
    public void PushObject(uint id) => Results.Push(NwnxValue.Object(id));
    public void Push(string v) => Results.Push(NwnxValue.String(v));
    public void Push(JsonValue v) => Results.Push(new NwnxValue(NwnxType.Json, v));
    public void Push(NwnxValue v) => Results.Push(v);

    /// <summary>Remaining arguments, from the top down (for generic handlers).</summary>
    public List<NwnxValue> PopAll()
    {
        var all = new List<NwnxValue>();
        while (Args.Pop() is { } v) all.Add(v);
        return all;
    }

    /// <summary>Per-object storage a plugin keeps beside the object (NWNX state that has no engine field yet).</summary>
    public Dictionary<string, NwnxValue> State(GameObject o) => Registry.ObjectState(o.Id);
    public Dictionary<string, NwnxValue> State(uint id) => Registry.ObjectState(id);
}

/// <summary>
/// Native NWNX bridge: the script-facing stack used by NWNXPush*/NWNXPop* and
/// the legacy "NWNXEE!ABIv2" local-variable ABI, and the C# implementations of
/// NWNX:EE plugin functions. Unknown Set*/Get* functions fall back to a
/// generic per-argument value store so paired setters and getters round-trip.
/// </summary>
public sealed class NwnxRegistry : IWorldSystem
{
    sealed record Handler(Action<NwnxCall> Invoke, string? UnsupportedReason);
    static readonly Dictionary<(string, string), Handler> Handlers = Discover();

    readonly World _world;
    readonly Dictionary<uint, Dictionary<string, NwnxValue>> _objectState = new();
    readonly Dictionary<string, NwnxValue> _generic = new(StringComparer.Ordinal);
    readonly HashSet<string> _missingLogged = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The stack scripts push to and pop from.</summary>
    public readonly NwnxStack Stack = new();
    /// <summary>NWNXGetIsAvailable. On by default: the bridge emulates the common plugins.</summary>
    public bool Available = true;
    public Action<string> Log = Console.WriteLine;
    /// <summary>
    /// Where plugins read their NWNX_* settings (NWNX:EE is configured through environment variables): the process
    /// environment unless a host substitutes its own.
    /// </summary>
    public Func<string, string?> EnvironmentVariable = Environment.GetEnvironmentVariable;
    /// <summary>A setting's value, or null when unset or empty.</summary>
    public string? Env(string name) => EnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    /// <summary>A true/false setting: "1", "y", "yes", "t" and "true" (any case) are true.</summary>
    public bool EnvBool(string name, bool fallback = false) => Env(name)?.Trim().ToLowerInvariant() switch
    {
        null => fallback,
        "1" or "y" or "yes" or "t" or "true" or "on" => true,
        _ => false,
    };
    /// <summary>An integer setting, or <paramref name="fallback"/> when unset or not a number.</summary>
    public int EnvInt(string name, int fallback = 0) =>
        int.TryParse(Env(name)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    // Plugin state that isn't per object.
    public readonly Dictionary<string, string> ConsoleCommands = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<(int Type, string Name), string> ResourceOverrides = new();
    public List<string>? ResRefIterator;
    public int ResRefIndex;

    /// <summary>Counts of unknown calls and known handlers with missing engine behavior.</summary>
    public readonly Dictionary<string, int> MissingCalls = new(StringComparer.OrdinalIgnoreCase);

    public NwnxRegistry(World world)
    {
        _world = world;
        world.ObjectRemoved += o => _objectState.Remove(o.Id);
    }

    readonly System.Collections.Concurrent.ConcurrentQueue<Action> _posted = new();

    /// <summary>Runs an action on the game thread at the next step: how plugins finish work done in the background.</summary>
    public void Post(Action action) => _posted.Enqueue(action);

    public void Step(float dt)
    {
        while (_posted.TryDequeue(out var action)) action();
    }

    /// <summary>"NWNX_Creature" and "Creature" both name the Creature plugin.</summary>
    public static string PluginKey(string plugin) =>
        (plugin.StartsWith("NWNX_", StringComparison.OrdinalIgnoreCase) ? plugin[5..] : plugin).ToLowerInvariant();

    static Dictionary<(string, string), Handler> Discover()
    {
        var map = new Dictionary<(string, string), Handler>();
        foreach (var type in typeof(NwnxRegistry).Assembly.GetTypes())
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var a in m.GetCustomAttributes<NwnxFuncAttribute>())
                    map[(a.Plugin, a.Function.ToLowerInvariant())] = new(m.CreateDelegate<Action<NwnxCall>>(), a.UnsupportedReason);
        return map;
    }

    public static bool HasHandler(string plugin, string function) => Handlers.ContainsKey((PluginKey(plugin), function.ToLowerInvariant()));
    public static bool HasPlugin(string plugin)
    {
        var key = PluginKey(plugin);
        return Handlers.Keys.Any(k => k.Item1 == key);
    }

    public Dictionary<string, NwnxValue> ObjectState(uint id)
    {
        if (!_objectState.TryGetValue(id, out var d)) _objectState[id] = d = new Dictionary<string, NwnxValue>(StringComparer.Ordinal);
        return d;
    }

    /// <summary>
    /// Runs a plugin function: the arguments currently on <see cref="Stack"/>
    /// are handed to the handler and the stack then holds its results.
    /// Returns false when no handler (native or generic) took the call.
    /// </summary>
    public bool Call(string plugin, string function, ScriptHost? host = null, ScriptThread? thread = null)
    {
        var call = new NwnxCall
        {
            World = _world, Registry = this, Plugin = PluginKey(plugin), Function = function,
            Args = Stack.Drain(), Results = Stack, Host = host, Thread = thread,
        };
        if (Handlers.TryGetValue((call.Plugin, function.ToLowerInvariant()), out var handler))
        {
            if (handler.UnsupportedReason is { } reason) call.ReportUnsupported(reason);
            handler.Invoke(call);
            return true;
        }
        call.ReportUnsupported("no handler; generic Set/Get storage only, no engine behavior");
        return Generic(call);
    }

    internal void ReportUnsupported(string plugin, string function, string reason, string? script, string? detail = null)
    {
        var name = $"NWNX_{PluginKey(plugin)}_{function}";
        MissingCalls[name] = MissingCalls.GetValueOrDefault(name) + 1;
        var label = detail is { Length: > 0 } ? $"{name} ({detail})" : name;
        if (_missingLogged.Add(label))
            Log($"[nwnx] not implemented: {label}: {reason}" + (script is { Length: > 0 } ? $" (in {script})" : ""));
    }

    /// <summary>SetX(args..., value) stores value under (plugin, X, args); GetX(args...) pushes it back.</summary>
    bool Generic(NwnxCall call)
    {
        var f = call.Function;
        var args = call.PopAll();
        if (f.StartsWith("Set", StringComparison.Ordinal) && args.Count >= 1)
        {
            var value = args[^1];
            _generic[GenericKey(call.Plugin, f[3..], args.Take(args.Count - 1))] = value;
            return true;
        }
        if (f.StartsWith("Get", StringComparison.Ordinal))
        {
            if (_generic.TryGetValue(GenericKey(call.Plugin, f[3..], args), out var v)) call.Push(v);
            return true;
        }
        return false;
    }

    static string GenericKey(string plugin, string name, IEnumerable<NwnxValue> args) =>
        plugin + "." + name.ToLowerInvariant() + "|" + string.Join("|", args.Select(a => a.ToString()));

    // ---- Legacy ABI: NWNX_PushArgument* = SetLocal*(OBJECT_INVALID, PUSH), NWNX_CallFunction = PlaySound(...!CALL), results via GetLocal*(OBJECT_INVALID, POP).

    public const string LegacyPush = "NWNXEE!ABIv2!X!Y!PUSH", LegacyPop = "NWNXEE!ABIv2!X!Y!POP";
    const string LegacyPrefix = "NWNXEE!ABIv2!", LegacyCallSuffix = "!CALL";

    /// <summary>
    /// PlaySound hook for "NWNXEE!ABIv2!plugin!function!CALL". Returns true if
    /// the sound name was an NWNX call (and must not be played).
    /// </summary>
    public bool TryLegacyCall(string soundName, ScriptHost? host = null, ScriptThread? thread = null)
    {
        if (!soundName.StartsWith(LegacyPrefix, StringComparison.Ordinal) || !soundName.EndsWith(LegacyCallSuffix, StringComparison.Ordinal)) return false;
        var parts = soundName[LegacyPrefix.Length..^LegacyCallSuffix.Length].Split('!');
        if (parts.Length < 2) return false;
        Call(parts[0], parts[1], host, thread);
        return true;
    }

    /// <summary>SetLocal*(OBJECT_INVALID, NWNX_PUSH, value) hook. Returns true if it was a push.</summary>
    public bool TryLegacyPush(uint obj, string varName, NwnxValue value)
    {
        if (obj != GameObject.InvalidId || varName != LegacyPush) return false;
        Stack.Push(value);
        return true;
    }

    /// <summary>GetLocal*(OBJECT_INVALID, NWNX_POP) hook. Returns true (and the popped value) if it was a pop.</summary>
    public bool TryLegacyPop(uint obj, string varName, NwnxType type, out object? value)
    {
        value = null;
        if (obj != GameObject.InvalidId || varName != LegacyPop) return false;
        value = PopAs(type);
        return true;
    }

    /// <summary>Pops the top of <see cref="Stack"/> converted to the requested kind (default value if empty or incompatible).</summary>
    public object? PopAs(NwnxType type)
    {
        var top = Stack.Pop();
        object? v = top?.Value;
        return type switch
        {
            NwnxType.Int => v switch { int i => i, float f => (int)f, uint o => unchecked((int)o), _ => 0 },
            NwnxType.Float => v switch { float f => f, int i => (float)i, _ => 0f },
            NwnxType.Object => v is uint o ? o : GameObject.InvalidId,
            NwnxType.String => v switch { string s => s, int i => i.ToString(CultureInfo.InvariantCulture), float f => f.ToString(CultureInfo.InvariantCulture), _ => "" },
            NwnxType.Vector => v is Vector3 vec ? vec : Vector3.Zero,
            NwnxType.Location => v is Location l ? l : Location.Invalid,
            _ => top is { } t && t.Type == type ? v : null,
        };
    }
}
