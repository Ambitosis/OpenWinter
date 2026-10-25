using System.Buffers.Binary;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// TLK V3.0 talk tables. StrRefs with bit 24 set index the module's custom
/// table; the rest index the base game table.
/// </summary>
public sealed class TalkTable
{
    public const uint CustomFlag = 0x01000000;

    readonly byte[]? _base;
    readonly byte[]? _custom;

    public TalkTable(byte[]? baseTable, byte[]? customTable)
    {
        _base = Valid(baseTable) ? baseTable : null;
        _custom = Valid(customTable) ? customTable : null;
    }

    static bool Valid(byte[]? b) => b != null && b.Length >= 20 && b[0] == (byte)'T' && b[1] == (byte)'L' && b[2] == (byte)'K';

    public string Get(uint strref)
    {
        if (strref == 0xFFFFFFFF) return "";
        if ((strref & CustomFlag) != 0) return Lookup(_custom, strref & 0x00FFFFFF);
        return Lookup(_base, strref);
    }

    /// <summary>Sound resref attached to an entry, if any.</summary>
    public string Sound(uint strref)
    {
        var table = (strref & CustomFlag) != 0 ? _custom : _base;
        uint index = strref & 0x00FFFFFF;
        if (table == null || !HasEntry(table, index)) return "";
        int e = 20 + (int)index * 40;
        return Bin.FixedString(table, e + 4, 16);
    }

    static uint Count(byte[] t) => BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(12));

    /// <summary>The entry is within the declared count and its 40 bytes lie inside the table.</summary>
    static bool HasEntry(byte[] t, uint index) => index < Count(t) && 20 + index * 40L + 40 <= t.Length;

    static string Lookup(byte[]? t, uint index)
    {
        if (t == null || !HasEntry(t, index)) return "";
        int e = 20 + (int)index * 40;
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(e));
        if ((flags & 1) == 0) return "";
        long strings = BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(16));
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(e + 28));
        long size = BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(e + 32));
        if (strings + offset + size > t.Length) return "";
        return Bin.Text.GetString(t, (int)(strings + offset), (int)size);
    }
}
