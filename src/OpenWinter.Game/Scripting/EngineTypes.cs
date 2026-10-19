using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting;

/// <summary>An effect (EffectX constructors produce these; ApplyEffectToObject consumes them).</summary>
public sealed partial class Effect
{
    public int Type;
    public int SubType;
    public int DurationType;
    public float Duration;
    public uint Creator = GameObject.InvalidId;
    public int SpellId = -1;
    public int CasterLevel;
    public string Tag = "";
    public int[] Ints = new int[8];
    public float[] Floats = new float[4];
    public string[] Strings = new string[4] { "", "", "", "" };
    public uint[] Objects = new uint[4] { GameObject.InvalidId, GameObject.InvalidId, GameObject.InvalidId, GameObject.InvalidId };
    public Vector3[] Vectors = new Vector3[2];
    public bool IgnoreImmunity;
    public bool IsLinked => Linked.Count > 0;
    public readonly List<Effect> Linked = new();
    /// <summary>Game time (seconds) the effect expires, for temporary effects.</summary>
    public double ExpiresAt;
    public long UniqueId;

    public Effect Clone()
    {
        var e = (Effect)MemberwiseClone();
        e.Ints = (int[])Ints.Clone();
        e.Floats = (float[])Floats.Clone();
        e.Strings = (string[])Strings.Clone();
        e.Objects = (uint[])Objects.Clone();
        e.Vectors = (Vector3[])Vectors.Clone();
        return e;
    }
}

/// <summary>A script event (EventUserDefined, EventSpellCastAt, ...).</summary>
public sealed class ScriptEvent
{
    public int Type;
    public int UserDefinedNumber;
    public uint Caster = GameObject.InvalidId;
    public int SpellId;
    public bool Harmful;
    public uint Target = GameObject.InvalidId;
    public Vector3 Position;
    public Location Location = Location.Invalid;
}

public sealed class Talent
{
    public int Type;
    public int Id;
    public int Category;
    /// <summary>A potion talent's item and cast-spell property (GetCreatureTalent* found it in the inventory).</summary>
    public uint Item = GameObject.InvalidId;
    public ItemProperty? Property;
    /// <summary>Metamagic of the memorised slot a spell talent came from.</summary>
    public int Meta;
    public bool Valid => Type >= 0;
}

public sealed class ItemProperty
{
    public int Type = -1;
    public int SubType;
    public int CostTable;
    public int CostTableValue;
    public int Param1;
    public int Param1Value;
    public int DurationType;
    public float Duration;
    public double ExpiresAt;
    public int ChanceToAppear = 100;
    public bool Useable = true;
    public int UsesPerDay = 255;
    public string Tag = "";
    public uint Creator = GameObject.InvalidId;
    /// <summary>
    /// Identity kept by copies: a property added to an item shares it with the script's variable, so
    /// RemoveItemProperty works with either, but not with an equal property built separately.
    /// </summary>
    public long Id = System.Threading.Interlocked.Increment(ref _nextId);
    static long _nextId;
    public bool Valid => Type >= 0;

    public ItemProperty Clone() => (ItemProperty)MemberwiseClone();
}

/// <summary>A prepared SQL statement handle (sqlquery).</summary>
public sealed class SqlQuery
{
    public object? Statement;
    public string Error = "";
    /// <summary>The SQL text and database name, for error messages.</summary>
    public string Sql = "", Db = "(unknown db)";
    /// <summary>Stepped since it was prepared or reset (binding is then refused).</summary>
    public bool Executed;
    /// <summary>The last step returned no row: further steps return false until a reset.</summary>
    public bool Done;
}

/// <summary>Constraint solver handle (cassowary). Minimal for now.</summary>
public sealed class Cassowary
{
    public readonly Dictionary<string, float> Values = new();
}

/// <summary>json values wrap System.Text.Json nodes; null node is JSON null.</summary>
public sealed class JsonValue
{
    public JsonNode? Node;
    public string Error = "";
    public JsonValue(JsonNode? node) { Node = node; }
    public static JsonValue Null => new(null);
}
