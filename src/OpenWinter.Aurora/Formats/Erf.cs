using System.Buffers.Binary;
using System.Text;

namespace OpenWinter.Aurora.Formats;

/// <summary>One resource stored in an ERF-family archive.</summary>
public sealed record ErfEntry(string ResRef, ushort ResType, byte[] Data);

/// <summary>
/// The ERF archive family (.erf, .hak, .mod, .sav, .nwm) per BioWare's published ERF
/// specification: a 160-byte header, optional localized description strings, a key
/// list (resref, resource id, type) and a resource list (offset, size), then the data.
/// V1.0 stores 16-character resrefs; V1.1 (NWN:EE) stores 32. <see cref="Write"/> picks
/// V1.0 unless a resref needs the longer form. Reading files on disk lazily is
/// <see cref="Resources.ErfSource"/>; this class reads and writes whole archives in memory.
/// </summary>
public static class Erf
{
    const int HeaderSize = 160;

    /// <summary>
    /// Builds an archive. <paramref name="fileType"/> is the 4-character type ("SAV ", "ERF ", "MOD ", "HAK ").
    /// Entries keep their order; resource ids are their indices. <paramref name="buildDate"/> fills the
    /// build year/day header fields (UTC now when null).
    /// </summary>
    public static byte[] Write(IReadOnlyList<ErfEntry> entries, string fileType, uint descriptionStrRef = 0xFFFFFFFF,
        IReadOnlyDictionary<int, string>? descriptions = null, DateTime? buildDate = null)
    {
        int resrefLen = entries.Any(e => e.ResRef.Length > 16) ? 32 : 16;
        string version = resrefLen == 32 ? "V1.1" : "V1.0";
        var date = buildDate ?? DateTime.UtcNow;

        var loc = new MemoryStream();
        foreach (var (lang, text) in (descriptions ?? new Dictionary<int, string>()).OrderBy(kv => kv.Key))
        {
            var bytes = Text.Windows1252.GetBytes(text);
            Span<byte> head = stackalloc byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(head, (uint)lang);
            BinaryPrimitives.WriteUInt32LittleEndian(head[4..], (uint)bytes.Length);
            loc.Write(head);
            loc.Write(bytes);
        }

        int keySize = resrefLen + 8;
        long locOffset = HeaderSize;
        long keyOffset = locOffset + loc.Length;
        long resOffset = keyOffset + (long)entries.Count * keySize;
        long dataOffset = resOffset + (long)entries.Count * 8;
        long total = dataOffset + entries.Sum(e => (long)e.Data.Length);
        if (total > uint.MaxValue) throw new InvalidOperationException("ERF archive larger than 4 GB");

        var buf = new byte[total];
        var span = buf.AsSpan();
        Encoding.ASCII.GetBytes(fileType.PadRight(4)[..4], span[..4]);
        Encoding.ASCII.GetBytes(version, span[4..8]);
        Le32(span, 8, (uint)(descriptions?.Count ?? 0));
        Le32(span, 12, (uint)loc.Length);
        Le32(span, 16, (uint)entries.Count);
        Le32(span, 20, (uint)locOffset);
        Le32(span, 24, (uint)keyOffset);
        Le32(span, 28, (uint)resOffset);
        Le32(span, 32, (uint)(date.Year - 1900));
        Le32(span, 36, (uint)(date.DayOfYear - 1));
        Le32(span, 40, descriptionStrRef);
        loc.ToArray().CopyTo(span[(int)locOffset..]);

        long at = dataOffset;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.ResRef.Length == 0) throw new ArgumentException("ERF entry with an empty resref");
            int k = (int)(keyOffset + (long)i * keySize);
            Encoding.ASCII.GetBytes(e.ResRef.ToLowerInvariant(), span.Slice(k, resrefLen));
            Le32(span, k + resrefLen, (uint)i);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(k + resrefLen + 4)..], e.ResType);
            int r = (int)(resOffset + (long)i * 8);
            Le32(span, r, (uint)at);
            Le32(span, r + 4, (uint)e.Data.Length);
            e.Data.CopyTo(span[(int)at..]);
            at += e.Data.Length;
        }
        return buf;
    }

    /// <summary>Reads every resource of an archive (in key-list order) and its 4-character file type.</summary>
    public static List<ErfEntry> Read(byte[] file, out string fileType)
    {
        if (file.Length < HeaderSize) throw new InvalidDataException("ERF: file too short");
        var b = file.AsSpan();
        fileType = Encoding.ASCII.GetString(b[..4]);
        string version = Encoding.ASCII.GetString(b[4..8]);
        if (version != "V1.0" && version != "V1.1") throw new InvalidDataException($"ERF: unsupported version '{version}'");
        int resrefLen = version == "V1.1" ? 32 : 16;
        int count = (int)Rd32(b, 16);
        long keyOffset = Rd32(b, 24), resOffset = Rd32(b, 28);
        int keySize = resrefLen + 8;
        if (keyOffset + (long)count * keySize > file.Length || resOffset + (long)count * 8 > file.Length)
            throw new InvalidDataException("ERF: key or resource list out of range");
        var list = new List<ErfEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int k = (int)(keyOffset + (long)i * keySize);
            string resref = Bin.FixedString(file, k, resrefLen);
            uint resId = Rd32(b, k + resrefLen);
            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(b[(k + resrefLen + 4)..]);
            if (resId >= count) throw new InvalidDataException($"ERF: resource id {resId} out of range");
            int r = (int)(resOffset + resId * 8L);
            long offset = Rd32(b, r), size = Rd32(b, r + 4);
            if (offset + size > file.Length) throw new InvalidDataException($"ERF: {resref} data out of range");
            list.Add(new ErfEntry(resref, type, b.Slice((int)offset, (int)size).ToArray()));
        }
        return list;
    }

    static void Le32(Span<byte> b, int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b[at..], v);
    static uint Rd32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
}
