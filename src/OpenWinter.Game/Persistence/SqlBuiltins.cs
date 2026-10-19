using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SQLitePCL;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// The extra SQL functions the game documents in SQLite_README.txt:
/// nwbufcompress/nwbufdecompress, base64encode/base64decode, sha1, xxh32, xxh64,
/// regexp (the REGEXP operator), regexp_match and regexp_replace.
/// </summary>
static class SqlBuiltins
{
    const int Deterministic = 0x800; // SQLITE_DETERMINISTIC
    const int Utf8 = 1;

    public static void Register(SqlDb db)
    {
        var h = db.Handle;
        raw.sqlite3_create_function(h, "nwbufcompress", 2, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
        {
            var magic = Text(a[0]);
            if (Encoding.ASCII.GetByteCount(magic) != 4) throw new ArgumentException("nwbufcompress: MAGIC must be exactly 4 bytes");
            raw.sqlite3_result_blob(ctx, CompressedBuffer.Wrap(magic, Blob(a[1])));
        }));
        raw.sqlite3_create_function(h, "nwbufdecompress", 2, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
        {
            var magic = Text(a[0]);
            if (Encoding.ASCII.GetByteCount(magic) != 4) throw new ArgumentException("nwbufdecompress: MAGIC must be exactly 4 bytes");
            raw.sqlite3_result_blob(ctx, CompressedBuffer.Unwrap(magic, Blob(a[1])));
        }));
        raw.sqlite3_create_function(h, "base64encode", 1, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_text(ctx, Convert.ToBase64String(Blob(a[0])))));
        raw.sqlite3_create_function(h, "base64decode", 1, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
        {
            var text = Text(a[0]);
            if (text.Length == 0) { raw.sqlite3_result_text(ctx, ""); return; }
            raw.sqlite3_result_blob(ctx, Convert.FromBase64String(text));
        }));
        raw.sqlite3_create_function(h, "sha1", 1, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_text(ctx, Convert.ToHexString(SHA1.HashData(Blob(a[0]))).ToLowerInvariant())));
        raw.sqlite3_create_function(h, "xxh32", 1, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_int64(ctx, (int)XxHash.Hash32(Blob(a[0])))));
        raw.sqlite3_create_function(h, "xxh64", 1, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_int64(ctx, (long)XxHash.Hash64(Blob(a[0])))));
        raw.sqlite3_create_function(h, "regexp", 2, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_int(ctx, Pattern(Text(a[0]), 0).IsMatch(Text(a[1])) ? 1 : 0)));
        raw.sqlite3_create_function(h, "regexp_match", 4, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_int(ctx, Pattern(Text(a[0]), raw.sqlite3_value_int(a[2])).IsMatch(Text(a[1])) ? 1 : 0)));
        raw.sqlite3_create_function(h, "regexp_replace", 5, Utf8 | Deterministic, null, (ctx, _, a) => Guard(ctx, () =>
            raw.sqlite3_result_text(ctx, Pattern(Text(a[0]), raw.sqlite3_value_int(a[3])).Replace(Text(a[1]), Text(a[2])))));
    }

    static void Guard(sqlite3_context ctx, Action body)
    {
        try { body(); }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidDataException or RegexParseException)
        {
            raw.sqlite3_result_error(ctx, e.Message);
        }
    }

    static string Text(sqlite3_value v) => raw.sqlite3_value_type(v) == raw.SQLITE_NULL ? "" : raw.sqlite3_value_text(v).utf8_to_string() ?? "";
    static byte[] Blob(sqlite3_value v) => raw.sqlite3_value_type(v) == raw.SQLITE_NULL ? Array.Empty<byte>() : raw.sqlite3_value_blob(v).ToArray();

    static readonly Dictionary<(string, int), Regex> _cache = new();

    /// <summary>ECMAScript-flavoured regex. Syntax flag bit 0 (REGEXP_ICASE-style) turns on case-insensitive matching.</summary>
    static Regex Pattern(string pattern, int syntaxFlags)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue((pattern, syntaxFlags), out var r)) return r;
            var opts = RegexOptions.ECMAScript | RegexOptions.CultureInvariant;
            if ((syntaxFlags & 1) != 0) opts |= RegexOptions.IgnoreCase;
            r = new Regex(pattern, opts, TimeSpan.FromSeconds(1));
            if (_cache.Count > 256) _cache.Clear();
            _cache[(pattern, syntaxFlags)] = r;
            return r;
        }
    }
}

/// <summary>
/// The game's compressed-buffer envelope ("NWCompressedBuf"): 4-byte magic, uint32 header
/// version (3), uint32 algorithm (0 none, 1 zlib, 2 zstd), uint32 uncompressed size, then the
/// payload. zlib payloads start with a uint32 sub-header version; zstd payloads with a uint32
/// sub-header version (1) and a uint32 dictionary id (0: none), then one zstd frame. The original
/// writes zstd (nwbufcompress, campaign objects, character databases: parity suite persist_objects),
/// and so does OpenWinter; all three algorithms are read.
/// </summary>
public static class CompressedBuffer
{
    public const int AlgorithmNone = 0, AlgorithmZlib = 1, AlgorithmZstd = 2;

    public static byte[] Wrap(string magic, byte[] data, int algorithm = AlgorithmZstd)
    {
        byte[] payload;
        if (algorithm == AlgorithmZstd)
        {
            using var z = new ZstdSharp.Compressor();
            var frame = z.Wrap(data);
            payload = new byte[8 + frame.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), 0);
            frame.CopyTo(payload.AsSpan(8));
        }
        else
        {
            algorithm = AlgorithmNone;
            payload = data;
        }
        var o = new byte[16 + payload.Length];
        Encoding.ASCII.GetBytes(magic.PadRight(4)[..4], 0, 4, o, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(8), (uint)algorithm);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(12), (uint)data.Length);
        payload.CopyTo(o, 16);
        return o;
    }

    public static byte[] Unwrap(string magic, byte[] buf)
    {
        if (buf.Length < 16) throw new InvalidDataException("compressed buffer too short");
        if (Encoding.ASCII.GetString(buf, 0, 4) != magic.PadRight(4)[..4]) throw new InvalidDataException("compressed buffer magic mismatch");
        uint algorithm = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(8));
        int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(12));
        switch (algorithm)
        {
            case AlgorithmNone:
                return buf.AsSpan(16, Math.Min(size, buf.Length - 16)).ToArray();
            case AlgorithmZlib:
            {
                // A uint32 sub-header version precedes the zlib stream.
                using var input = new MemoryStream(buf, 20, buf.Length - 20);
                using var z = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
                var output = new byte[size];
                z.ReadExactly(output);
                return output;
            }
            case AlgorithmZstd:
            {
                if (buf.Length < 24) throw new InvalidDataException("zstd buffer too short");
                if (BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(20)) != 0)
                    throw new InvalidDataException("zstd buffers with a dictionary are not supported");
                try
                {
                    using var z = new ZstdSharp.Decompressor();
                    var output = z.Unwrap(buf.AsSpan(24)).ToArray();
                    if (output.Length != size) throw new InvalidDataException($"zstd buffer holds {output.Length} bytes, header says {size}");
                    return output;
                }
                catch (ZstdSharp.ZstdException e) { throw new InvalidDataException("zstd buffer is damaged: " + e.Message, e); }
            }
            default:
                throw new InvalidDataException($"compressed buffer algorithm {algorithm} is unknown");
        }
    }
}

/// <summary>XXH32 and XXH64 (seed 0), from the public xxHash specification.</summary>
static class XxHash
{
    const uint P32_1 = 2654435761U, P32_2 = 2246822519U, P32_3 = 3266489917U, P32_4 = 668265263U, P32_5 = 374761393U;
    const ulong P64_1 = 11400714785074694791UL, P64_2 = 14029467366897019727UL, P64_3 = 1609587929392839161UL,
        P64_4 = 9650029242287828579UL, P64_5 = 2870177450012600261UL;

    public static uint Hash32(ReadOnlySpan<byte> d, uint seed = 0)
    {
        int i = 0, n = d.Length;
        uint h;
        if (n >= 16)
        {
            uint v1 = seed + P32_1 + P32_2, v2 = seed + P32_2, v3 = seed, v4 = seed - P32_1;
            for (; i <= n - 16; i += 16)
            {
                v1 = R32(v1, BinaryPrimitives.ReadUInt32LittleEndian(d[i..]));
                v2 = R32(v2, BinaryPrimitives.ReadUInt32LittleEndian(d[(i + 4)..]));
                v3 = R32(v3, BinaryPrimitives.ReadUInt32LittleEndian(d[(i + 8)..]));
                v4 = R32(v4, BinaryPrimitives.ReadUInt32LittleEndian(d[(i + 12)..]));
            }
            h = BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) + BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
        }
        else h = seed + P32_5;
        h += (uint)n;
        for (; i <= n - 4; i += 4) h = BitOperations.RotateLeft(h + BinaryPrimitives.ReadUInt32LittleEndian(d[i..]) * P32_3, 17) * P32_4;
        for (; i < n; i++) h = BitOperations.RotateLeft(h + d[i] * P32_5, 11) * P32_1;
        h ^= h >> 15; h *= P32_2; h ^= h >> 13; h *= P32_3; h ^= h >> 16;
        return h;
    }

    static uint R32(uint acc, uint input) => BitOperations.RotateLeft(acc + input * P32_2, 13) * P32_1;

    public static ulong Hash64(ReadOnlySpan<byte> d, ulong seed = 0)
    {
        int i = 0, n = d.Length;
        ulong h;
        if (n >= 32)
        {
            ulong v1 = seed + P64_1 + P64_2, v2 = seed + P64_2, v3 = seed, v4 = seed - P64_1;
            for (; i <= n - 32; i += 32)
            {
                v1 = R64(v1, BinaryPrimitives.ReadUInt64LittleEndian(d[i..]));
                v2 = R64(v2, BinaryPrimitives.ReadUInt64LittleEndian(d[(i + 8)..]));
                v3 = R64(v3, BinaryPrimitives.ReadUInt64LittleEndian(d[(i + 16)..]));
                v4 = R64(v4, BinaryPrimitives.ReadUInt64LittleEndian(d[(i + 24)..]));
            }
            h = BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) + BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
            h = Merge(h, v1); h = Merge(h, v2); h = Merge(h, v3); h = Merge(h, v4);
        }
        else h = seed + P64_5;
        h += (ulong)n;
        for (; i <= n - 8; i += 8) h = BitOperations.RotateLeft(h ^ R64(0, BinaryPrimitives.ReadUInt64LittleEndian(d[i..])), 27) * P64_1 + P64_4;
        for (; i <= n - 4; i += 4) h = BitOperations.RotateLeft(h ^ (BinaryPrimitives.ReadUInt32LittleEndian(d[i..]) * P64_1), 23) * P64_2 + P64_3;
        for (; i < n; i++) h = BitOperations.RotateLeft(h ^ (d[i] * P64_5), 11) * P64_1;
        h ^= h >> 33; h *= P64_2; h ^= h >> 29; h *= P64_3; h ^= h >> 32;
        return h;
    }

    static ulong R64(ulong acc, ulong input) => BitOperations.RotateLeft(acc + input * P64_2, 31) * P64_1;
    static ulong Merge(ulong acc, ulong v) => (acc ^ R64(0, v)) * P64_1 + P64_4;
}
