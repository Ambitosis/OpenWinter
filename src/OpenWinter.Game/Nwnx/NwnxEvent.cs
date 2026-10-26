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

    /// <summary>Runs an event that has no _BEFORE / _AFTER halves (NWNX_ON_COMBAT_MODE_ON, NWNX_ON_CALENDAR_HOUR ...). True when skipped.</summary>
    public bool Fire()
    {
        var o = EventsPlugin.Run(_world, _name, _self, _data);
        Skipped = o.Skipped;
        Result = o.Result;
        HasResult = o.ResultSet;
        return Skipped;
    }

    /// <summary>Runs the event's _AFTER half when disposed (handlers with several ways out).</summary>
    public readonly struct AfterScope : IDisposable
    {
        readonly NwnxEvent? _ev;
        public AfterScope(NwnxEvent? ev) => _ev = ev;
        public void Dispose() => _ev?.After();
    }

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
        "NWNX_ON_EFFECT_APPLIED", "NWNX_ON_EFFECT_REMOVED", "NWNX_ON_HEAL",
        "NWNX_ON_ITEM_ACQUIRE", "NWNX_ON_INVENTORY_ADD_ITEM", "NWNX_ON_INVENTORY_REMOVE_ITEM", "NWNX_ON_VALIDATE_ITEM_EQUIP",
        "NWNX_ON_ITEM_EQUIP", "NWNX_ON_ITEM_UNEQUIP", "NWNX_ON_INVENTORY_ADD_GOLD", "NWNX_ON_INVENTORY_REMOVE_GOLD",
        "NWNX_ON_ITEM_DESTROY_OBJECT", "NWNX_ON_VALIDATE_USE_ITEM", "NWNX_ON_USE_ITEM",
        "NWNX_ON_ITEMPROPERTY_EFFECT_APPLIED", "NWNX_ON_ITEMPROPERTY_EFFECT_REMOVED",
        "NWNX_ON_INPUT_CAST_SPELL", "NWNX_ON_BROADCAST_CAST_SPELL", "NWNX_ON_DECREMENT_SPELL_COUNT", "NWNX_ON_CAST_SPELL",
        "NWNX_ON_SPELL_INTERRUPTED", "NWNX_ON_SPELL_FAILED",
        "NWNX_ON_STEALTH_ENTER", "NWNX_ON_STEALTH_EXIT", "NWNX_ON_DETECT_ENTER", "NWNX_ON_DETECT_EXIT", "NWNX_ON_COMBAT_MODE_ON",
        "NWNX_ON_COMBAT_MODE_OFF", "NWNX_ON_HAS_FEAT", "NWNX_ON_DECREMENT_REMAINING_FEAT_USES", "NWNX_ON_USE_FEAT", "NWNX_ON_USE_SKILL",
        "NWNX_ON_ADD_ASSOCIATE", "NWNX_ON_REMOVE_ASSOCIATE", "NWNX_ON_SET_NPC_FACTION_REPUTATION", "NWNX_ON_SET_EXPERIENCE",
        "NWNX_ON_LEVEL_DOWN", "NWNX_ON_LEVEL_UP_AUTOMATIC", "NWNX_ON_OBJECT_LOCK", "NWNX_ON_OBJECT_UNLOCK", "NWNX_ON_POLYMORPH",
        "NWNX_ON_UNPOLYMORPH", "NWNX_ON_RUN_EVENT_SCRIPT", "NWNX_ON_CALENDAR_HOUR", "NWNX_ON_CALENDAR_DAY", "NWNX_ON_CALENDAR_MONTH",
        "NWNX_ON_CALENDAR_YEAR", "NWNX_ON_CALENDAR_DAWN", "NWNX_ON_CALENDAR_DUSK", "NWNX_ON_CREATURE_JUMP_TO_POINT",
        "NWNX_ON_CREATURE_JUMP_TO_OBJECT", "NWNX_ON_CREATURE_TILE_CHANGE", "NWNX_ON_MATERIALCHANGE",
        "NWNX_ON_ATTACK_TARGET_CHANGE", "NWNX_ON_START_COMBAT_ROUND", "NWNX_ON_BROADCAST_ATTACK_OF_OPPORTUNITY",
        "NWNX_ON_COMBAT_ATTACK_OF_OPPORTUNITY", "NWNX_ON_DISARM", "NWNX_ON_ITEM_DECREMENT_STACKSIZE", "NWNX_ON_ITEM_AMMO_RELOAD",
        "NWNX_ON_INVENTORY_OPEN", "NWNX_ON_INPUT_DROP_ITEM", "NWNX_ON_STORE_REQUEST_BUY", "NWNX_ON_STORE_REQUEST_SELL",
        "NWNX_ON_ITEM_PAY_TO_IDENTIFY", "NWNX_ON_CHARACTER_SHEET_PERMITTED", "NWNX_ON_CHARACTER_SHEET_OPEN", "NWNX_ON_JOURNAL_OPEN",
        "NWNX_ON_EXAMINE_OBJECT", "NWNX_ON_ITEM_SPLIT", "NWNX_ON_INPUT_TOGGLE_PAUSE", "NWNX_ON_PVP_ATTITUDE_CHANGE",
        "NWNX_ON_QUICKBAR_SET_BUTTON", "NWNX_ON_QUICKCHAT", "NWNX_ON_MAP_PIN_ADD_PIN", "NWNX_ON_MAP_PIN_CHANGE_PIN",
        "NWNX_ON_MAP_PIN_DESTROY_PIN", "NWNX_ON_POSSESS_FAMILIAR", "NWNX_ON_UNPOSSESS_FAMILIAR", "NWNX_ON_CLIENT_LEVEL_UP_BEGIN",
        "NWNX_ON_LEVEL_UP", "NWNX_ON_INPUT_ATTACK_OBJECT", "NWNX_ON_PLACEABLE_OPEN", "NWNX_ON_PLACEABLE_CLOSE", "NWNX_ON_OBJECT_USE",
        "NWNX_ON_CLIENT_CONNECT", "NWNX_ON_CLIENT_DISCONNECT", "NWNX_ON_CHECK_STICKY_PLAYER_NAME_RESERVED", "NWNX_ON_SERVER_SEND_AREA",
        "NWNX_ON_SERVER_CHARACTER_SAVE", "NWNX_ON_COMBAT_ENTER", "NWNX_ON_COMBAT_EXIT", "NWNX_ON_INPUT_WALK_TO_WAYPOINT",
        "NWNX_ON_INPUT_EMOTE", "NWNX_ON_INPUT_KEYBOARD", "NWNX_ON_BARTER_START", "NWNX_ON_BARTER_ADD_ITEM", "NWNX_ON_BARTER_END",
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
