using System.Buffers.Binary;
namespace OpenWinter.Aurora.Formats;

/// <summary>BioWare SSF V1.0: offset table followed by resref/strref records.</summary>
public sealed class SoundSet
{
    public readonly record struct Entry(string Sound, uint StringRef);
    public IReadOnlyList<Entry> Entries { get; }
    SoundSet(Entry[] entries) => Entries = entries;
    public Entry Get(int slot) => slot >= 0 && slot < Entries.Count ? Entries[slot] : new("", uint.MaxValue);
    public static SoundSet Read(byte[] data)
    {
        if (data.Length < 40 || !data.AsSpan(0, 8).SequenceEqual("SSF V1.0"u8))
            throw new InvalidDataException("Invalid SSF V1.0 header.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
        uint table = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
        if (table < 40 || table > data.Length || count > (data.Length - table) / 4)
            throw new InvalidDataException("Invalid SSF entry table.");
        var entries = new Entry[(int)count];
        for (int i = 0; i < entries.Length; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)table + i * 4));
            if (offset > data.Length || data.Length - offset < 20)
                throw new InvalidDataException("Invalid SSF data offset.");
            entries[i] = new(Bin.FixedString(data, (int)offset, 16), BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)offset + 16)));
        }
        return new(entries);
    }
}
