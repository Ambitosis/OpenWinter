using System.Numerics;
using System.Reflection;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting;

/// <summary>Marks a static method <c>object? M(Call c)</c> as the implementation of an nwscript.nss function.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class NwFuncAttribute : Attribute
{
    public string Name { get; }
    public NwFuncAttribute(string name) { Name = name; }
}

/// <summary>Event data scripts read with GetEnteringObject, GetLastUsedBy, GetUserDefinedEventNumber, ...</summary>
public sealed class EventContext
{
    public readonly Dictionary<string, object> Values = new(StringComparer.Ordinal);
    public uint Obj(string key) => Values.TryGetValue(key, out var v) && v is uint u ? u : GameObject.InvalidId;
    public int Int(string key) => Values.TryGetValue(key, out var v) && v is int i ? i : 0;
    public string Str(string key) => Values.TryGetValue(key, out var v) && v is string s ? s : "";
    public EventContext Set(string key, object value) { Values[key] = value; return this; }
    /// <summary>Return synchronous event edits without leaking script identity or call parameters.</summary>
    public void CopyPayloadFrom(EventContext child)
    {
        foreach (var (key, value) in child.Values)
            if (key is not ("EventScriptType" or "DirectEventScriptType")
                && !key.StartsWith("param:", StringComparison.Ordinal)) Values[key] = value;
    }

    public EventContext Clone()
    {
        var c = new EventContext();
        foreach (var kv in Values) c.Values[kv.Key] = kv.Value;
        return c;
    }
}

/// <summary>One engine call: arguments in declaration order plus access to the world.</summary>
public sealed class Call
{
    public required ScriptHost Host;
    public required ScriptThread Thread;
    public required NssFunction Function;
    public required object?[] Args;

    public World World => Host.World;
    public uint SelfId => Thread.Self;
    public GameObject? Self => World.Get(Thread.Self);
    public EventContext Event => Host.CurrentEvent;

    public int I(int i, int fallback = 0) => i < Args.Length && Args[i] is int v ? v : fallback;
    public float F(int i, float fallback = 0) => i < Args.Length && Args[i] is float v ? v : fallback;
    public string S(int i) => i < Args.Length && Args[i] is string v ? v : "";
    public uint O(int i) => i < Args.Length && Args[i] is uint v ? v : GameObject.InvalidId;
    public GameObject? Obj(int i) => World.Get(O(i));
    public T? Obj<T>(int i) where T : GameObject => World.Get(O(i)) as T;
    public Vector3 V(int i) => i < Args.Length && Args[i] is Vector3 v ? v : Vector3.Zero;
    public Location L(int i) => i < Args.Length && Args[i] is Location v ? v : Location.Invalid;
    public Effect E(int i) => i < Args.Length && Args[i] is Effect v ? v : new Effect { Type = -1 };
    public ItemProperty IP(int i) => i < Args.Length && Args[i] is ItemProperty v ? v : new ItemProperty();
    public Talent T(int i) => i < Args.Length && Args[i] is Talent v ? v : new Talent { Type = -1 };
    public ScriptEvent Ev(int i) => i < Args.Length && Args[i] is ScriptEvent v ? v : new ScriptEvent();
    public JsonValue J(int i) => i < Args.Length && Args[i] is JsonValue v ? v : JsonValue.Null;
    public SqlQuery Q(int i) => i < Args.Length && Args[i] is SqlQuery v ? v : new SqlQuery();
    public Cassowary C(int i) => i < Args.Length && Args[i] is Cassowary v ? v : new Cassowary();
    public ScriptSituation? A(int i) => i < Args.Length ? Args[i] as ScriptSituation : null;

    /// <summary>The target object argument, defaulting to OBJECT_SELF when invalid is not meaningful.</summary>
    public GameObject? ObjOrSelf(int i) => World.Get(O(i)) ?? Self;
}

/// <summary>
/// Runs scripts against the world: binds engine functions, keeps the event
/// context stack, and schedules delayed and assigned commands.
/// </summary>
public sealed partial class ScriptHost : IScriptEngine
{
    readonly Func<Call, object?>?[] _impl;
    readonly Dictionary<int, string> _owners = new();
    /// <summary>Functions implemented in more than one place (a merge mistake: one silently wins).</summary>
    public readonly List<string> DuplicateImplementations = new();
    readonly HashSet<string> _missingLogged = new();
    readonly Dictionary<string, NcsProgram?> _programs = new(StringComparer.OrdinalIgnoreCase);
    readonly Stack<EventContext> _events = new();
    readonly PriorityQueue<(ScriptSituation Situation, uint Self), (double, long)> _scheduled = new();
    long _seq;
    int _depth;

    public World World { get; }
    public NssDefinitions Definitions { get; }
    public ScriptVM VM { get; }
    public Action<string> Log = Console.WriteLine;
    /// <summary>Called before every engine function call (diagnostics and tests).</summary>
    public Action<Call>? Trace;
    /// <summary>Counts of calls to functions without an implementation (for the coverage report).</summary>
    public readonly Dictionary<string, int> MissingCalls = new();
    public long ScriptsRun { get; private set; }
    /// <summary>SetScriptParam values waiting for the next ExecuteScript.</summary>
    public readonly Dictionary<string, string> PendingParams = new();

    public EventContext CurrentEvent => _events.Count > 0 ? _events.Peek() : Empty;
    static readonly EventContext Empty = new();

    public ScriptHost(World world, NssDefinitions definitions)
    {
        World = world;
        Definitions = definitions;
        VM = new ScriptVM(definitions, this);
        _impl = new Func<Call, object?>?[definitions.Functions.Count];
        foreach (var type in typeof(ScriptHost).Assembly.GetTypes())
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var attr in m.GetCustomAttributes<NwFuncAttribute>())
                {
                    if (!definitions.ByName.TryGetValue(attr.Name, out var f)) continue;
                    if (_impl[f.Index] != null && _owners.TryGetValue(f.Index, out var prior))
                        DuplicateImplementations.Add($"{attr.Name}: {prior} and {type.Name}.{m.Name}");
                    _impl[f.Index] = m.CreateDelegate<Func<Call, object?>>();
                    _owners[f.Index] = $"{type.Name}.{m.Name}";
                }
    }

    public int ImplementedCount => _impl.Count(f => f != null);
    public IEnumerable<string> Unimplemented => Definitions.Functions.Where(f => _impl[f.Index] == null).Select(f => f.Name);

    public static NssDefinitions LoadDefinitions(ResMan rm)
    {
        var bytes = rm.Read("nwscript", ResType.Nss) ?? throw new FileNotFoundException("nwscript.nss not found in resources");
        return NssDefinitions.Parse(OpenWinter.Aurora.Text.Windows1252.GetString(bytes));
    }

    NcsProgram? Program(string resref)
    {
        if (_programs.TryGetValue(resref, out var p)) return p;
        var bytes = World.Resources.Read(resref, ResType.Ncs);
        try { p = bytes == null ? null : new NcsProgram(resref.ToLowerInvariant(), bytes); }
        catch (InvalidDataException e) { Log($"[script error] {e.Message}"); p = null; }
        _programs[resref] = p;
        return p;
    }

    public bool Exists(string resref) => !string.IsNullOrEmpty(resref) && Program(resref) != null;

    /// <summary>Makes a compiled program available under a script name (CompileScript).</summary>
    public void Register(string resref, NcsProgram program) => _programs[resref] = program;

    /// <summary>Drops a loaded script, so its next run reads it from the resources again.</summary>
    public void Forget(string resref) => _programs.Remove(resref);

    /// <summary>Source of an include (or any script) as the compiler sees it, through the resource manager.</summary>
    public string? ScriptSource(string resref) =>
        World.Resources.Read(resref, ResType.Nss) is { } b ? OpenWinter.Aurora.Text.Windows1252.GetString(b) : null;

    /// <summary>Runs a script with OBJECT_SELF = <paramref name="self"/>. Returns the StartingConditional result (0 if none or on error).</summary>
    public int Run(string resref, uint self, EventContext? ev = null)
    {
        if (string.IsNullOrEmpty(resref)) return 0;
        var program = Program(resref);
        if (program == null) return 0;
        return Run(program, resref, self, ev);
    }

    /// <summary>True when the last script run by <see cref="Run(NcsProgram, string, uint, EventContext?)"/> stopped on an error.</summary>
    public bool LastRunFailed { get; private set; }

    /// <summary>Runs a loaded program (a module script, or a compiled chunk) as <paramref name="name"/>.</summary>
    public int Run(NcsProgram program, string name, uint self, EventContext? ev = null)
    {
        LastRunFailed = false;
        // Scripts nest at most 8 deep (recursion levels 0-7); deeper ExecuteScripts quietly do nothing.
        if (_depth >= MaxRecursion) return 0;
        _events.Push(ev ?? CurrentEvent.Clone());
        _depth++;
        try
        {
            ScriptsRun++;
            return VM.Run(program, self);
        }
        catch (ScriptException e)
        {
            LastRunFailed = true;
            ReportError(name, self, e.Message);
            if (e.AbortsCallers && _depth > 1) throw;
            return 0;
        }
        finally
        {
            _depth--;
            _events.Pop();
        }
    }

    /// <summary>Runs a stored action now.</summary>
    public void Run(ScriptSituation s, uint self, EventContext? ev = null)
    {
        _events.Push(ev ?? CurrentEvent.Clone());
        _depth++;
        try { VM.Run(s, self); }
        catch (ScriptException e)
        {
            ReportError(s.ScriptName, self, e.Message);
            if (e.AbortsCallers && _depth > 1) throw;
        }
        finally { _depth--; _events.Pop(); }
    }

    /// <summary>Logs an aborted script in the original's format ("Script error: name OID: ..., Tag: ..., ERROR: ...").</summary>
    void ReportError(string script, uint self, string message) =>
        Log($"[script error] Script error: {script} OID: {self:x8}, Tag: {World.Get(self)?.Tag ?? ""}, ERROR: {message}");

    /// <summary>
    /// Schedules a stored action (DelayCommand; AssignCommand uses zero delay so it runs after the current script).
    /// Zero delays run in the order given; real delays that fall due together run newest first, as in the original.
    /// </summary>
    public void Schedule(double delaySeconds, ScriptSituation s, uint self) =>
        _scheduled.Enqueue((s, self), (World.GameTime + Math.Max(0, delaySeconds), _seq = World.NextEventSequence(delaySeconds)));

    /// <summary>The due time and order of the next command, if it is due (the world runs it in turn with engine events).</summary>
    public bool TryPeekDue(out (double, long) key) => _scheduled.TryPeek(out _, out key) && key.Item1 <= World.GameTime;

    /// <summary>Runs the next queued command.</summary>
    public void RunNext()
    {
        var item = _scheduled.Dequeue();
        if (World.Get(item.Self) is not { } owner) return; // owner destroyed: the command is dropped
        World.SpawnIfPending(owner);
        Run(item.Situation, item.Self);
    }

    public int PendingCommands => _scheduled.Count;

    public const int MaxRecursion = 8;

    object? IScriptEngine.Call(ScriptThread thread, NssFunction function, object?[] args)
    {
        if (function.Index < 0)
        {
            // Engine-structure constant: json literals (text), otherwise the type's empty value (LOCATION_INVALID).
            if (args.Length > 1 && args[1] is string text) return Functions.JsonFunctions.ParseScript(text);
            return args.Length > 0 && args[0] is int type ? DefaultEngineValue((NssType)((int)NssType.Effect + (type - 0x10))) : null;
        }
        var impl = _impl[function.Index];
        if (Trace != null) Trace(new Call { Host = this, Thread = thread, Function = function, Args = args });
        if (impl == null)
        {
            MissingCalls[function.Name] = MissingCalls.GetValueOrDefault(function.Name) + 1;
            if (_missingLogged.Add(function.Name)) Log($"[script] not implemented: {function.Name} (in {thread.Program.Name})");
            return DefaultResult(function.Returns);
        }
        if (World.Profile is not { } profile) return Invoke(impl, thread, function, args);
        long start = global::System.Diagnostics.Stopwatch.GetTimestamp();
        var result = Invoke(impl, thread, function, args);
        string key = "fn " + function.Name;
        profile[key] = profile.GetValueOrDefault(key) + (global::System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / global::System.Diagnostics.Stopwatch.Frequency;
        return result;
    }

    /// <summary>A failing engine function aborts the calling script instead of taking the server down.</summary>
    object? Invoke(Func<Call, object?> impl, ScriptThread thread, NssFunction function, object?[] args)
    {
        try { return impl(new Call { Host = this, Thread = thread, Function = function, Args = args }); }
        catch (Exception e) when (e is not ScriptException and not ScriptThread.AbortSignal)
        {
            throw new ScriptException($"{function.Name}: {e.GetType().Name}: {e.Message}");
        }
    }

    object? DefaultResult(NssType t) => t switch
    {
        NssType.Int => 0,
        NssType.Float => 0f,
        NssType.String => "",
        NssType.Object => GameObject.InvalidId,
        NssType.Vector => Vector3.Zero,
        NssType.Void => null,
        _ => DefaultEngineValue(t),
    };

    public object DefaultEngineValue(NssType type) => type switch
    {
        NssType.Effect => new Effect { Type = -1 },
        NssType.Event => new ScriptEvent(),
        NssType.Location => Location.Invalid,
        NssType.Talent => new Talent { Type = -1 },
        NssType.ItemProperty => new ItemProperty(),
        NssType.SqlQuery => new SqlQuery(),
        NssType.Cassowary => new Cassowary(),
        NssType.Json => JsonValue.Null,
        _ => 0,
    };

    public bool EngineEquals(NssType type, object? a, object? b) => type switch
    {
        NssType.Location => a is Location la && b is Location lb && la == lb,
        NssType.Json => Functions.JsonFunctions.DeepEquals(a as JsonValue, b as JsonValue),
        _ => ReferenceEquals(a, b),
    };
}
