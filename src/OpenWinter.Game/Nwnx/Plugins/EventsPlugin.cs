using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Events: subscriptions (scripts and script chunks), event data, skipping, results, dispatch lists and ID
/// whitelists, for script-defined events (PushEventData + SignalEvent) and for the automatic NWNX_ON_* events game
/// systems raise through <see cref="NwnxEvent"/>.
/// <para>
/// Measured on NWNX (parity suite nwnx_events_core): subscribers run in the order they subscribed, a repeated
/// subscription is ignored, and resubscribing puts the subscriber last. Pushed data belongs to the next SignalEvent
/// (subscribed or not) and is gone after it; a tag pushed twice keeps its last value; a missing tag, or any read
/// outside an event, gives "". SignalEvent returns TRUE whatever the subscriptions. Events nest: a handler that
/// signals another event reads its own data and name again afterwards, and ExecuteScript from a handler sees the
/// event, while a DelayCommand does not. Toggling a dispatch list on or off empties it; it may be set up before the
/// subscription.
/// </para>
/// </summary>
public static class EventsPlugin
{
    const string P = "Events";
    internal const string KeyEvent = "nwnx:event", KeyDataPrefix = "nwnx:data:";

    sealed class Subscriber
    {
        public required string Name;
        public bool IsChunk;
        public bool Wrap;
        public NcsProgram? Program;
        public bool CompileFailed;
        public bool Same(string name, bool chunk, bool wrap) =>
            IsChunk == chunk && (chunk ? Name == name && Wrap == wrap : Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    sealed class Frame
    {
        public bool Skipped;
        public string Result = "";
        public bool ResultSet;
    }

    sealed class State
    {
        public readonly Dictionary<string, List<Subscriber>> Subscribers = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> PendingData = new(StringComparer.Ordinal);
        public readonly Stack<Frame> Running = new();
        /// <summary>Dispatch lists by (event, script or chunk); present while list mode is on.</summary>
        public readonly Dictionary<(string, string), HashSet<uint>> DispatchLists = new();
        public readonly Dictionary<string, HashSet<int>> WhitelistIds = new(StringComparer.Ordinal);
        /// <summary>Events whose ID whitelist is on (some are on by default).</summary>
        public readonly HashSet<string> WhitelistOn = new(StringComparer.Ordinal)
        {
            "NWNX_ON_HAS_FEAT", "NWNX_ON_RUN_EVENT_SCRIPT", "NWNX_ON_ITEMPROPERTY_EFFECT",
        };
        public int Count;
        /// <summary>Event names with a subscriber, also by base name (without _BEFORE / _AFTER), for cheap checks.</summary>
        public readonly HashSet<string> Live = new(StringComparer.Ordinal);

        public void Changed()
        {
            Live.Clear();
            foreach (var (evt, list) in Subscribers)
            {
                if (list.Count == 0) continue;
                Live.Add(evt);
                if (evt.EndsWith("_BEFORE", StringComparison.Ordinal)) Live.Add(evt[..^7]);
                else if (evt.EndsWith("_AFTER", StringComparison.Ordinal)) Live.Add(evt[..^6]);
            }
        }
    }

    static readonly ConditionalWeakTable<World, State> _states = new();
    static State Of(World w) => _states.GetValue(w, _ => new State());

    /// <summary>The result of one signalled event.</summary>
    public readonly record struct Outcome(bool Skipped, string Result, bool ResultSet);

    /// <summary>Whether any script or chunk is subscribed to <paramref name="evt"/>. Cheap: game systems check it first.</summary>
    public static bool Subscribed(World world, string evt) =>
        _states.TryGetValue(world, out var st) && st.Count > 0 && st.Subscribers.TryGetValue(evt, out var l) && l.Count > 0;

    /// <summary>Whether an event with this base name, or its _BEFORE / _AFTER halves, has a subscriber.</summary>
    public static bool Listening(World world, string baseName)
    {
        return _states.TryGetValue(world, out var st) && st.Count > 0 && st.Live.Contains(baseName);
    }

    /// <summary>False when <paramref name="whitelist"/>'s ID whitelist is on and <paramref name="id"/> isn't on it.</summary>
    public static bool Allows(World world, string whitelist, int id)
    {
        var st = Of(world);
        return !st.WhitelistOn.Contains(whitelist) || st.WhitelistIds.TryGetValue(whitelist, out var ids) && ids.Contains(id);
    }

    /// <summary>
    /// Runs every subscriber of <paramref name="evt"/> with OBJECT_SELF = <paramref name="target"/> and the given
    /// event data. Returns whether a subscriber called SkipEvent and the last SetEventResult value.
    /// </summary>
    public static (bool Skipped, string Result) Signal(World world, string evt, GameObject? target, IReadOnlyDictionary<string, string>? data = null)
    {
        var o = Run(world, evt, target?.Id ?? GameObject.InvalidId, data);
        return (o.Skipped, o.Result);
    }

    /// <summary>As <see cref="Signal"/>, with OBJECT_SELF given by id (OBJECT_INVALID allowed), and whether a result was set.</summary>
    public static Outcome Run(World world, string evt, uint self, IReadOnlyDictionary<string, string>? data)
    {
        if (world.Scripts is not { } host || !_states.TryGetValue(world, out var st)
            || !st.Subscribers.TryGetValue(evt, out var subs) || subs.Count == 0) return default;
        var ctx = new EventContext().Set(KeyEvent, evt);
        if (data != null) foreach (var kv in data) ctx.Set(KeyDataPrefix + kv.Key, kv.Value);
        var frame = new Frame();
        st.Running.Push(frame);
        try
        {
            foreach (var sub in subs.ToList())
            {
                if (st.DispatchLists.TryGetValue((evt, sub.Name), out var list) && !list.Contains(self)) continue;
                if (!sub.IsChunk) host.Run(sub.Name, self, ctx.Clone());
                else if (Chunk(host, sub) is { } program) host.Run(program, "!Chunk", self, ctx.Clone());
            }
            return new Outcome(frame.Skipped, frame.Result, frame.ResultSet);
        }
        finally { st.Running.Pop(); }
    }

    static NcsProgram? Chunk(ScriptHost host, Subscriber sub)
    {
        if (sub.Program != null || sub.CompileFailed) return sub.Program;
        string source = sub.Wrap ? "void main() {" + sub.Name + "}" : sub.Name;
        var ncs = Scripting.Compiler.NssCompiler.Compile(source, "Chunk", host.Definitions, host.ScriptSource, out var error, mainOnly: true);
        if (ncs == null)
        {
            sub.CompileFailed = true;
            host.Log($"[nwnx] NWNX_Events: script chunk did not compile: {error}");
            return null;
        }
        sub.Program = new NcsProgram("!Chunk", ncs);
        ScriptChunks.Attach(sub.Program, sub.Name);
        return sub.Program;
    }

    public static int SubscriberCount(World world, string evt) =>
        _states.TryGetValue(world, out var st) && st.Subscribers.TryGetValue(evt, out var l) ? l.Count : 0;

    static void Add(NwnxCall n, string evt, string name, bool chunk, bool wrap)
    {
        var st = Of(n.World);
        if (NwnxEvent.NotEmitted(evt) is { } reason) n.ReportUnsupported(reason, evt);
        if (!st.Subscribers.TryGetValue(evt, out var list)) st.Subscribers[evt] = list = new List<Subscriber>();
        if (list.Any(s => s.Same(name, chunk, wrap))) return;
        list.Add(new Subscriber { Name = name, IsChunk = chunk, Wrap = wrap });
        st.Count++;
        st.Changed();
    }

    static void Remove(NwnxCall n, string evt, Func<Subscriber, bool> match)
    {
        var st = Of(n.World);
        if (st.Subscribers.TryGetValue(evt, out var list)) st.Count -= list.RemoveAll(s => match(s));
        st.Changed();
    }

    [NwnxFunc(P, "SubscribeEvent")]
    static void Subscribe(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString();
        Add(n, evt, script, chunk: false, wrap: false);
    }
    [NwnxFunc(P, "UnsubscribeEvent")]
    static void Unsubscribe(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString();
        Remove(n, evt, s => s.Same(script, false, false));
    }
    [NwnxFunc(P, "SubscribeEventScriptChunk")]
    static void SubscribeChunk(NwnxCall n)
    {
        var evt = n.PopString(); var chunk = n.PopString(); bool wrap = n.PopInt() != 0;
        Add(n, evt, chunk, chunk: true, wrap);
    }
    [NwnxFunc(P, "UnsubscribeEventScriptChunk")]
    static void UnsubscribeChunk(NwnxCall n)
    {
        var evt = n.PopString(); var chunk = n.PopString(); bool wrap = n.PopInt() != 0;
        Remove(n, evt, s => s.Same(chunk, true, wrap));
    }
    /// <summary>Removes every subscription whose script name or chunk starts with the prefix, case-sensitively (nwnx_events_core).</summary>
    [NwnxFunc(P, "UnsubscribeAllStartingWith")]
    static void UnsubscribeAll(NwnxCall n)
    {
        var prefix = n.PopString();
        var st = Of(n.World);
        foreach (var list in st.Subscribers.Values)
            st.Count -= list.RemoveAll(s => s.Name.StartsWith(prefix, StringComparison.Ordinal));
        st.Changed();
    }
    [NwnxFunc(P, "PushEventData")]
    static void PushData(NwnxCall n)
    {
        var tag = n.PopString(); var data = n.PopString();
        Of(n.World).PendingData[tag] = data;
    }
    [NwnxFunc(P, "SignalEvent")]
    static void SignalEvent(NwnxCall n)
    {
        var evt = n.PopString(); var target = n.PopObjectId();
        var st = Of(n.World);
        var data = new Dictionary<string, string>(st.PendingData);
        st.PendingData.Clear();
        Run(n.World, evt, target, data);
        n.Push(1);
    }
    [NwnxFunc(P, "GetEventData")]
    static void GetData(NwnxCall n)
    {
        var tag = n.PopString();
        n.Push(n.Host?.CurrentEvent.Str(KeyDataPrefix + tag) ?? "");
    }
    [NwnxFunc(P, "GetCurrentEvent")] static void CurrentEvent(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Str(KeyEvent) ?? "");
    [NwnxFunc(P, "SkipEvent")]
    static void Skip(NwnxCall n)
    {
        if (Of(n.World).Running.TryPeek(out var top)) top.Skipped = true;
    }
    [NwnxFunc(P, "SetEventResult")]
    static void SetResult(NwnxCall n)
    {
        var result = n.PopString();
        if (Of(n.World).Running.TryPeek(out var top)) { top.Result = result; top.ResultSet = true; }
    }
    [NwnxFunc(P, "GetNumSubscribers")] static void NumSubscribers(NwnxCall n) => n.Push(SubscriberCount(n.World, n.PopString()));

    [NwnxFunc(P, "ToggleDispatchListMode")]
    static void ToggleDispatch(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString(); bool on = n.PopInt() != 0;
        var lists = Of(n.World).DispatchLists;
        if (on) lists[(evt, script)] = new HashSet<uint>();
        else lists.Remove((evt, script));
    }
    [NwnxFunc(P, "AddObjectToDispatchList")]
    static void AddToDispatch(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString(); uint obj = n.PopObjectId();
        if (Of(n.World).DispatchLists.TryGetValue((evt, script), out var list)) list.Add(obj);
    }
    [NwnxFunc(P, "RemoveObjectFromDispatchList")]
    static void RemoveFromDispatch(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString(); uint obj = n.PopObjectId();
        if (Of(n.World).DispatchLists.TryGetValue((evt, script), out var list)) list.Remove(obj);
    }

    [NwnxFunc(P, "ToggleIDWhitelist")]
    static void ToggleWhitelist(NwnxCall n)
    {
        var evt = n.PopString(); bool on = n.PopInt() != 0;
        var st = Of(n.World);
        if (on) st.WhitelistOn.Add(evt);
        else st.WhitelistOn.Remove(evt);
    }
    [NwnxFunc(P, "AddIDToWhitelist")]
    static void AddWhitelist(NwnxCall n)
    {
        var evt = n.PopString(); int id = n.PopInt();
        var ids = Of(n.World).WhitelistIds;
        if (!ids.TryGetValue(evt, out var set)) ids[evt] = set = new HashSet<int>();
        set.Add(id);
    }
    [NwnxFunc(P, "RemoveIDFromWhitelist")]
    static void RemoveWhitelist(NwnxCall n)
    {
        var evt = n.PopString(); int id = n.PopInt();
        if (Of(n.World).WhitelistIds.TryGetValue(evt, out var set)) set.Remove(id);
    }
}
