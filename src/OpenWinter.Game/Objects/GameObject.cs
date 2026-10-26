using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;

namespace OpenWinter.Game.Objects;

/// <summary>Object types as NWScript sees them (OBJECT_TYPE_* bit values).</summary>
[Flags]
public enum ObjectType
{
    Invalid = 0,
    Creature = 1,
    Item = 2,
    Trigger = 4,
    Door = 8,
    AreaOfEffect = 16,
    Waypoint = 32,
    Placeable = 64,
    Store = 128,
    Encounter = 256,
    // Engine-internal kinds (not in OBJECT_TYPE_*).
    Area = 0x1000,
    Module = 0x2000,
    Sound = 0x4000,
}

/// <summary>Local variables (Get/SetLocal*), stored per type like the original engine.</summary>
public sealed class LocalVariables
{
    public readonly Dictionary<string, int> Ints = new();
    public readonly Dictionary<string, float> Floats = new();
    public readonly Dictionary<string, string> Strings = new();
    public readonly Dictionary<string, uint> Objects = new();
    public readonly Dictionary<string, Location> Locations = new();
    public readonly JsonLocals Jsons = new();

    // When each name first got a value, for listing variables newest first (NWNX_Object_GetLocalVariable).
    Dictionary<string, long>? _order;
    long _sequence;

    /// <summary>
    /// Records a set or delete of <paramref name="name"/>: a name that just got its first value becomes the newest;
    /// one left with no value (all deleted or set to the type's default) is forgotten.
    /// </summary>
    public void Touch(string name)
    {
        if (TypeOf(name) == 0) _order?.Remove(name);
        else (_order ??= new(StringComparer.Ordinal)).TryAdd(name, ++_sequence);
    }

    /// <summary>
    /// The lowest local type (1 int, 2 float, 3 string, 4 object, 5 location, 6 json) holding a value other than the
    /// type's default under <paramref name="name"/>, or 0 when none does.
    /// </summary>
    public int TypeOf(string name)
    {
        if (Ints.TryGetValue(name, out int i) && i != 0) return 1;
        if (Floats.TryGetValue(name, out float f) && f != 0) return 2;
        if (Strings.TryGetValue(name, out var s) && s.Length > 0) return 3;
        if (Objects.TryGetValue(name, out uint o) && o != GameObject.InvalidId) return 4;
        if (Locations.TryGetValue(name, out var l) && l.Area != GameObject.InvalidId) return 5;
        if (Jsons.GetValue(name) is { Node: not null }) return 6;
        return 0;
    }

    /// <summary>Names holding a value, newest first. Names never recorded with <see cref="Touch"/> come last.</summary>
    public List<(string Name, int Type)> NewestFirst()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(IEnumerable<string> keys) { foreach (var k in keys) if (seen.Add(k)) names.Add(k); }
        Add(Ints.Keys); Add(Floats.Keys); Add(Strings.Keys); Add(Objects.Keys); Add(Locations.Keys); Add(Jsons.Keys);
        return names.Select(n => (Name: n, Type: TypeOf(n)))
            .Where(v => v.Type != 0)
            .OrderByDescending(v => _order != null && _order.TryGetValue(v.Name, out long at) ? at : long.MinValue)
            .ToList();
    }

    /// <summary>
    /// Adds the variables of a VarTable. Entry types: 1 int, 2 float, 3 string, 4 object (id), 5 location (a struct of
    /// Area, PositionX/Y/Z, OrientationX/Y/Z), 7 json (a struct holding the text in "JSON"; the original's save games and
    /// ObjectToJson) and 6, json as plain text in "Value" (what OpenWinter wrote before).
    /// </summary>
    public void LoadFrom(GffStruct owner)
    {
        foreach (var v in owner.GetList("VarTable"))
        {
            string name = v.GetString("Name");
            switch (v.GetInt("Type"))
            {
                case 1: Ints[name] = (int)v.GetInt("Value"); break;
                case 2: Floats[name] = v.GetFloat("Value"); break;
                case 3: Strings[name] = v.GetString("Value"); break;
                case 4: Objects[name] = (uint)v.GetInt("Value"); break;
                case 5:
                    if (v.GetStruct("Value") is { } l)
                    {
                        float deg = MathF.Atan2(l.GetFloat("OrientationY"), l.GetFloat("OrientationX")) * 180f / MathF.PI;
                        if (deg < 0) deg += 360f;
                        Locations[name] = new Location((uint)l.GetInt("Area", GameObject.InvalidId),
                            new Vector3(l.GetFloat("PositionX"), l.GetFloat("PositionY"), l.GetFloat("PositionZ")), deg);
                    }
                    break;
                case 6: Jsons[name] = v.GetString("Value"); break;
                case 7: Jsons[name] = v.GetStruct("Value")?.GetString("JSON") ?? "null"; break;
            }
        }
        // The original writes the newest variable first.
        var list = owner.GetList("VarTable");
        for (int i = list.Count - 1; i >= 0; i--) Touch(list[i].GetString("Name"));
    }
}

/// <summary>
/// Local json variables. Scripts share the stored value (GetLocalJson returns the same value SetLocalJson
/// stored, so in-place edits show through); everything else sees each value as its json text.
/// </summary>
public sealed class JsonLocals : IEnumerable<KeyValuePair<string, string>>
{
    readonly Dictionary<string, Scripting.JsonValue> _values = new();

    public string this[string name]
    {
        get => Scripting.JsonCore.Dump(_values[name].Node);
        set => _values[name] = Scripting.Functions.JsonFunctions.Parse(value);
    }

    public Scripting.JsonValue? GetValue(string name) => _values.GetValueOrDefault(name);
    public void SetValue(string name, Scripting.JsonValue value) => _values[name] = value;
    public bool TryGetValue(string name, out string text)
    {
        bool found = _values.TryGetValue(name, out var v);
        text = found ? Scripting.JsonCore.Dump(v!.Node) : "";
        return found;
    }
    public bool ContainsKey(string name) => _values.ContainsKey(name);
    public IEnumerable<string> Keys => _values.Keys;
    public int Count => _values.Count;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
        _values.Select(kv => new KeyValuePair<string, string>(kv.Key, Scripting.JsonCore.Dump(kv.Value.Node))).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A point in an area with a facing (NWScript location).</summary>
public readonly record struct Location(uint Area, Vector3 Position, float FacingDegrees)
{
    public static readonly Location Invalid = new(GameObject.InvalidId, Vector3.Zero, 0);
}

/// <summary>Base of everything NWScript can hold an object reference to.</summary>
public abstract partial class GameObject
{
    /// <summary>OBJECT_INVALID.</summary>
    public const uint InvalidId = 0x7F000000;

    public uint Id { get; internal set; }
    public abstract ObjectType Type { get; }
    public string Tag = "";
    public string ResRef = "";
    public LocString Name = new();
    /// <summary>The name before the first SetName (GetName bOriginalName, and SetName "" restores it).</summary>
    public LocString? OriginalName;
    public string Description = "";
    public Area? Area { get; internal set; }
    public Vector3 Position;
    /// <summary>Radians, counter-clockwise from +X (east).</summary>
    public float Facing;
    public bool Plot;
    public readonly LocalVariables Locals = new();
    public readonly Dictionary<string, string> Scripts = new(StringComparer.OrdinalIgnoreCase);
    public bool IsDestroyed { get; internal set; }
    /// <summary>Being destroyed (World.Destroy is taking its items with it).</summary>
    public bool Destroying { get; internal set; }
    /// <summary>Effects currently applied (ApplyEffectToObject).</summary>
    public readonly List<Scripting.Effect> Effects = new();
    /// <summary>Queued actions (ActionX functions), front first.</summary>
    public readonly List<GameAction> Actions = new();
    /// <summary>SetCommandable: when false, new actions are refused.</summary>
    public bool Commandable = true;
    /// <summary>Game time of the next heartbeat (for creatures: of the last beat, see World.Beat).</summary>
    public double NextHeartbeat = -1;
    /// <summary>A creature's random share of its current heartbeat period, and whether that is its first beat.</summary>
    public double HeartbeatRoll;
    public bool HeartbeatFirst;
    /// <summary>A creature's next heartbeat is due at once (a condition such as confusion just landed: Rules.StateScripts).</summary>
    public bool HeartbeatNow;
    /// <summary>
    /// The last EventSpellCastAt the object received (GetLastSpellCaster / GetLastSpell / GetLastSpellHarmful outside
    /// its OnSpellCastAt script too; nwserver ai_queries). Not saved.
    /// </summary>
    public uint LastSpellCaster = InvalidId;
    public int LastSpellCastAt = -1;
    public bool LastSpellHarmful;

    /// <summary>
    /// NWScript GetFacing(): degrees, 0 = east, counter-clockwise. Placeables and doors keep their model's
    /// bearing in Facing; like the original, scripts see that bearing plus 90 degrees.
    /// </summary>
    public float FacingDegrees
    {
        get { float d = (Facing + BearingOffset) * 180f / MathF.PI % 360f; return d < 0 ? d + 360f : d; }
        set => Facing = value * MathF.PI / 180f - BearingOffset;
    }

    public float BearingOffset => this is Placeable or Door ? MathF.PI / 2 : 0;

    /// <summary>The model bearing for a placeable or door created facing <paramref name="facingRadians"/> (a script facing).</summary>
    public static float BearingFor(float facingRadians) => facingRadians - MathF.PI / 2;

    public Location Location => new(Area?.Id ?? InvalidId, Position, FacingDegrees);

    public string Script(string evt) => Scripts.TryGetValue(evt, out var s) ? s : "";

    public string DisplayName(TalkTable? tlk) => Name.Resolve(tlk);

    public override string ToString() => $"{Type} {Id:X} '{Tag}'";
}
