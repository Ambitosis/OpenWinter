using System.Globalization;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// One automatic NWNX_Events event as a game system raises it: its data, then the _BEFORE half (which subscribers
/// may skip or give a result), the engine's own work unless skipped, then the _AFTER half with the same data.
/// <see cref="Begin"/> returns null when nobody listens, so a hook costs one lookup:
/// <code>
/// var ev = NwnxEvent.Begin(world, "NWNX_ON_ITEM_EQUIP", creature)?.Add("ITEM", item).Add("SLOT", slot);
/// if (ev?.Before() == true) { ev.After(); return; }
/// ... the engine's work ...
/// ev?.After();
/// </code>
/// Data is text as NWNX writes it (nwnx_events_core): integers in decimal, floats with six decimals, objects as
/// ObjectToString writes them.
/// </summary>
public sealed class NwnxEvent
{
    readonly World _world;
    readonly string _name;
    readonly uint _self;
    readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

    /// <summary>A subscriber of the _BEFORE half called SkipEvent.</summary>
    public bool Skipped { get; private set; }
    /// <summary>The last SetEventResult of the _BEFORE half ("" when none).</summary>
    public string Result { get; private set; } = "";
    /// <summary>A subscriber of the _BEFORE half called SetEventResult.</summary>
    public bool HasResult { get; private set; }

    NwnxEvent(World world, string name, uint self) { _world = world; _name = name; _self = self; }

    /// <summary>The event <paramref name="name"/> (without _BEFORE / _AFTER) on <paramref name="self"/>, or null when nobody listens.</summary>
    public static NwnxEvent? Begin(World world, string name, GameObject? self) => Begin(world, name, self?.Id ?? GameObject.InvalidId);

    public static NwnxEvent? Begin(World world, string name, uint self) =>
        EventsPlugin.Listening(world, name) ? new NwnxEvent(world, name, self) : null;

    /// <summary>As <see cref="Begin(World, string, GameObject?)"/>, also null when <paramref name="whitelist"/>'s ID whitelist is on without <paramref name="id"/>.</summary>
    public static NwnxEvent? Begin(World world, string name, GameObject? self, string whitelist, int id) =>
        EventsPlugin.Listening(world, name) && EventsPlugin.Allows(world, whitelist, id) ? new NwnxEvent(world, name, self?.Id ?? GameObject.InvalidId) : null;

    public NwnxEvent Add(string tag, int value) { _data[tag] = value.ToString(CultureInfo.InvariantCulture); return this; }
    public NwnxEvent Add(string tag, bool value) { _data[tag] = value ? "1" : "0"; return this; }
    public NwnxEvent Add(string tag, float value) { _data[tag] = Float(value); return this; }
    public NwnxEvent Add(string tag, string value) { _data[tag] = value; return this; }
    public NwnxEvent Add(string tag, GameObject? value) { _data[tag] = Hex(value?.Id ?? GameObject.InvalidId); return this; }
    public NwnxEvent AddObject(string tag, uint id) { _data[tag] = Hex(id); return this; }
    public NwnxEvent Position(string prefix, System.Numerics.Vector3 p) =>
        Add(prefix + "X", p.X).Add(prefix + "Y", p.Y).Add(prefix + "Z", p.Z);
    public NwnxEvent Remove(string tag) { _data.Remove(tag); return this; }

    /// <summary>Runs the _BEFORE half. True when a subscriber skipped it.</summary>
    public bool Before()
    {
        var o = EventsPlugin.Run(_world, _name + "_BEFORE", _self, _data);
        Skipped = o.Skipped;
        Result = o.Result;
        HasResult = o.ResultSet;
        return Skipped;
    }

    /// <summary>Runs the _AFTER half.</summary>
    public void After() => EventsPlugin.Run(_world, _name + "_AFTER", _self, _data);

    /// <summary>Runs an event that has no _BEFORE / _AFTER halves (NWNX_ON_COMBAT_MODE_ON, NWNX_ON_CALENDAR_HOUR ...).</summary>
    public void Fire() => EventsPlugin.Run(_world, _name, _self, _data);

    /// <summary>Before, then <paramref name="work"/> unless skipped, then After.</summary>
    public static void Wrap(World world, string name, GameObject? self, Action<NwnxEvent>? data, Action work)
    {
        var ev = Begin(world, name, self);
        if (ev != null) data?.Invoke(ev);
        if (ev?.Before() != true) work();
        ev?.After();
    }

    public static string Hex(uint id) => id.ToString("x", CultureInfo.InvariantCulture);
    public static string Float(float f) => f.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>Automatic events OpenWinter raises, by base name. Subscribing to any other NWNX_ON_* event warns.</summary>
    static readonly HashSet<string> Emitted = new(StringComparer.Ordinal)
    {
        "NWNX_ON_EFFECT_APPLIED", "NWNX_ON_EFFECT_REMOVED",
    };

    /// <summary>Why an automatic event is never raised, or null when it is (or is a script-defined event).</summary>
    public static string? NotEmitted(string evt)
    {
        if (!evt.StartsWith("NWNX_ON_", StringComparison.Ordinal)) return null;
        if (evt.StartsWith("NWNX_ON_DM_", StringComparison.Ordinal) || evt.StartsWith("NWNX_ON_PARTY_", StringComparison.Ordinal)) return null;
        string b = evt.EndsWith("_BEFORE", StringComparison.Ordinal) ? evt[..^7] : evt.EndsWith("_AFTER", StringComparison.Ordinal) ? evt[..^6] : evt;
        return Emitted.Contains(b) ? null : "OpenWinter does not raise this automatic event; explicit SignalEvent still works";
    }
}
