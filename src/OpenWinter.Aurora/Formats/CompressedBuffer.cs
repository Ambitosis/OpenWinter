using System.Buffers.Binary;
using System.Text;

namespace OpenWinter.Aurora.Formats;

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

    /// <summary>Largest decompressed size accepted, far above any stored object or character database.</summary>
    public const int MaxSize = 256 * 1024 * 1024;

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
        uint declared = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(12));
        if (algorithm != AlgorithmNone && declared > MaxSize) throw new InvalidDataException($"compressed buffer claims {declared} bytes");
        int size = (int)Math.Min(declared, int.MaxValue);
        switch (algorithm)
        {
            case AlgorithmNone:
                return buf.AsSpan(16, Math.Min(size, buf.Length - 16)).ToArray();
            case AlgorithmZlib:
            {
                // A uint32 sub-header version precedes the zlib stream.
                if (buf.Length < 20) throw new InvalidDataException("zlib buffer too short");
                using var input = new MemoryStream(buf, 20, buf.Length - 20);
                using var z = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
                // Grow with the data actually inflated rather than trusting the declared size up front.
                var output = new MemoryStream();
                var chunk = new byte[81920];
                while (output.Length < size)
                {
                    int n = z.Read(chunk, 0, (int)Math.Min(chunk.Length, size - output.Length));
                    if (n == 0) throw new InvalidDataException($"zlib buffer holds {output.Length} bytes, header says {size}");
                    output.Write(chunk, 0, n);
                }
                return output.ToArray();
            }
            case AlgorithmZstd:
            {
                if (buf.Length < 24) throw new InvalidDataException("zstd buffer too short");
                if (BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(20)) != 0)
                    throw new InvalidDataException("zstd buffers with a dictionary are not supported");
                try
                {
                    using var z = new ZstdSharp.Decompressor();
                    var output = z.Unwrap(buf.AsSpan(24), size).ToArray();
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
