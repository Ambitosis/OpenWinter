namespace OpenWinter.Net.Nwn.Wire;

/// <summary>Field types of the NWN:EE message layer. Over WSNetLayer every field is preceded by its type as one byte.</summary>
public enum NwnWireType : byte
{
    Bits = 1,
    Bool = 2,
    Byte = 3,
    Char = 4,
    Word = 5,
    Short = 6,
    Dword = 7,
    Int = 8,
    Dword64 = 9,
    Int64 = 10,
    Float = 11,
    Double = 12,
    ResRef = 13,
    String = 14,
    VoidPtr = 15,
    Json = 16,
    ObjectId = 17,
    LocString = 18,
}

/// <summary>Which way a message travels.</summary>
public enum NwnDirection : byte
{
    Both,
    ClientToServer,
    ServerToClient,
}

/// <summary>How a float is quantised on a bit-packed transport. WSNetLayer always sends full floats.</summary>
public readonly record struct NwnFloatEncoding(float Multiplier, float Min, float Max, int Bits)
{
    public static NwnFloatEncoding Quantised(float multiplier, int bits) => new(multiplier, 0, 0, bits);
    public static NwnFloatEncoding Ranged(float min, float max, int bits) => new(0, min, max, bits);
}

/// <summary>A localized string field: a talk table reference, or literal text.</summary>
public readonly record struct NwnLocString(bool IsStrRef, byte Gender, uint StrRef, string Text)
{
    public const uint NoStrRef = 0xFFFFFFFF;
    public static NwnLocString FromStrRef(uint strRef, byte gender = 0) => new(true, gender, strRef, "");
    public static NwnLocString FromText(string text) => new(false, 0, NoStrRef, text ?? "");
}

/// <summary>A malformed or unexpected field while reading a message.</summary>
public sealed class NwnWireException(string message) : Exception(message);

/// <summary>Game data the message layer needs while decoding: some field counts and branches depend on 2DA tables.</summary>
public interface INwnContext
{
    int TwoDARowCount(string table);
    string TwoDA(string table, int row, string column);
    /// <summary>True when the remote peer's version is at least build.patch.postfix (same build only).</summary>
    bool Satisfies(int build, int patch, int postfix);
    /// <summary>Whether each trigger is a trap: trigger updates are laid out differently for traps.</summary>
    Dictionary<uint, bool> TriggerIsTrap { get; }
}

/// <summary>A struct within a message.</summary>
public interface INwnStruct
{
    void Read(NwnReader r);
    void Write(NwnWriter w);
}

/// <summary>A message of the NWN:EE protocol, identified by major, minor and direction.</summary>
public abstract class NwnMessage : INwnStruct
{
    public abstract byte Major { get; }
    public abstract byte Minor { get; }
    public abstract NwnDirection Direction { get; }
    public abstract void Read(NwnReader r);
    public abstract void Write(NwnWriter w);
    public override string ToString() => $"{GetType().Name} {Major:X2}.{Minor:X2}";
}

/// <summary>Field-condition helpers for generated code. Conditions compare earlier fields of the same struct.</summary>
public static class NwnCond
{
    /// <summary>A bool, integer or enum field as a number; null when the field is absent.</summary>
    public static long? L<T>(T value) => value switch
    {
        null => null,
        bool b => b ? 1 : 0,
        Enum e => Convert.ToInt64(e),
        ulong u => unchecked((long)u),
        IConvertible c => c.ToInt64(null),
        _ => throw new NwnWireException($"field of type {typeof(T).Name} can't be compared"),
    };

    public static bool Flag<T>(T value, long flag) => ((L(value) ?? 0) & flag) != 0;
    public static bool In<T>(T value, params long[] set) => L(value) is { } v && Array.IndexOf(set, v) >= 0;
    public static bool NotIn<T>(T value, params long[] set) => !In(value, set);
    public static bool InSet(string value, params string[] set) => Array.IndexOf(set, value) >= 0;
    public static int Row<T>(T value) => (int)(L(value) ?? throw new NwnWireException("2DA row field is absent"));

    /// <summary>Whether an optional field is written (the wire's leading presence flag).</summary>
    public static bool Truthy<T>(T value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        float f => f != 0,
        double d => d != 0,
        System.Collections.ICollection c => c.Count > 0,
        Enum or IConvertible => L(value) != 0,
        _ => true,
    };
}
