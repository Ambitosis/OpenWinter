using System.Buffers.Binary;
using System.Globalization;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Textures;

/// <summary>Decodes Aurora texture files into neutral TextureData.</summary>
public static class TextureDecoder
{
    public static TextureData? DecodeTga(byte[] b)
    {
        if (b.Length < 18) return null;
        int idLen = b[0], cmapType = b[1], type = b[2];
        int cmapLen = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(5));
        int cmapBits = b[7];
        int w = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(12));
        int h = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(14));
        int bpp = b[16];
        bool topDown = (b[17] & 0x20) != 0;
        if (w == 0 || h == 0 || w > 16384 || h > 16384) return null;
        int p = 18 + idLen;
        if (p > b.Length) return null;
        byte[]? palette = null;
        if (cmapType == 1)
        {
            int entry = (cmapBits + 7) / 8;
            palette = b.AsSpan(p, Math.Min(cmapLen * entry, b.Length - p)).ToArray();
            p += cmapLen * entry;
        }
        int bytesPer = (bpp + 7) / 8;
        bool rle = type is 9 or 10 or 11;
        bool gray = type is 3 or 11;
        bool mapped = type is 1 or 9;
        if (!(type is 1 or 2 or 3 or 9 or 10 or 11)) return null;
        // Pixel sizes the decoder reads: an 8-bit palette index (with a palette), 8/16-bit grey, 16/24/32-bit colour.
        if (mapped ? bytesPer != 1 || palette == null : gray ? bytesPer is < 1 or > 2 : bytesPer is < 2 or > 4) return null;
        // Reject sizes the data cannot fill: an RLE packet expands to at most 128 pixels.
        long remaining = b.Length - p;
        if (remaining <= 0 || (long)w * h > (rle ? remaining * 128 : remaining / bytesPer)) return null;

        var outp = new byte[w * h * 4];
        bool hasAlpha = false;
        int total = w * h;
        int pixel = 0;
        var px = new byte[4];

        void Put(int index, ReadOnlySpan<byte> src)
        {
            int x = index % w, y = index / w;
            int row = topDown ? y : h - 1 - y;
            int o = (row * w + x) * 4;
            if (mapped && palette != null)
            {
                int pi = src[0] * ((cmapBits + 7) / 8);
                if (pi + 2 < palette.Length)
                {
                    outp[o] = palette[pi + 2]; outp[o + 1] = palette[pi + 1]; outp[o + 2] = palette[pi];
                    outp[o + 3] = cmapBits == 32 && pi + 3 < palette.Length ? palette[pi + 3] : (byte)255;
                }
            }
            else if (gray)
            {
                outp[o] = outp[o + 1] = outp[o + 2] = src[0];
                outp[o + 3] = bytesPer > 1 ? src[1] : (byte)255;
            }
            else if (bytesPer == 2)
            {
                int v = src[0] | (src[1] << 8);
                outp[o] = (byte)(((v >> 10) & 31) * 255 / 31);
                outp[o + 1] = (byte)(((v >> 5) & 31) * 255 / 31);
                outp[o + 2] = (byte)((v & 31) * 255 / 31);
                outp[o + 3] = 255;
            }
            else
            {
                outp[o] = src[2]; outp[o + 1] = src[1]; outp[o + 2] = src[0];
                outp[o + 3] = bytesPer == 4 ? src[3] : (byte)255;
            }
            if (outp[o + 3] < 255) hasAlpha = true;
        }

        if (!rle)
        {
            for (; pixel < total && p + bytesPer <= b.Length; pixel++, p += bytesPer) Put(pixel, b.AsSpan(p, bytesPer));
        }
        else
        {
            while (pixel < total && p < b.Length)
            {
                int hdr = b[p++];
                int count = (hdr & 0x7F) + 1;
                if ((hdr & 0x80) != 0)
                {
                    if (p + bytesPer > b.Length) break;
                    b.AsSpan(p, bytesPer).CopyTo(px);
                    p += bytesPer;
                    for (int i = 0; i < count && pixel < total; i++) Put(pixel++, px.AsSpan(0, bytesPer));
                }
                else
                {
                    for (int i = 0; i < count && pixel < total && p + bytesPer <= b.Length; i++, p += bytesPer) Put(pixel++, b.AsSpan(p, bytesPer));
                }
            }
        }
        return new TextureData { Width = w, Height = h, Format = TextureFormat.Rgba8, Data = outp, HasAlpha = hasAlpha };
    }

    public static TextureData? DecodeDds(byte[] b)
    {
        if (b.Length > 128 && b[0] == 'D' && b[1] == 'D' && b[2] == 'S' && b[3] == ' ') return DecodeStandardDds(b);
        // BioWare DDS: width, height, channels (3 = DXT1, 4 = DXT5), level-0 size, alpha mean; then the mip chain.
        if (b.Length < 20) return null;
        int w = (int)BinaryPrimitives.ReadUInt32LittleEndian(b);
        int h = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4));
        int channels = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8));
        if (w <= 0 || h <= 0 || w > 16384 || h > 16384) return null;
        var format = channels == 3 ? TextureFormat.Dxt1 : TextureFormat.Dxt5;
        return Compressed(b.AsSpan(20), w, h, format, int.MaxValue, channels == 4);
    }

    static TextureData? DecodeStandardDds(byte[] b)
    {
        int h = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(12));
        int w = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(16));
        int mips = Math.Max(1, (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(28)));
        if (w <= 0 || h <= 0 || w > 16384 || h > 16384) return null;
        uint pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(80));
        string fourcc = System.Text.Encoding.ASCII.GetString(b, 84, 4);
        if ((pfFlags & 0x4) != 0)
        {
            if (fourcc is "ATI1" or "BC4U") return SingleChannel(b.AsSpan(128), w, h);
            // The extended header: DXGI_FORMAT_BC4_UNORM (80) or BC4_TYPELESS (79), data after 20 more bytes.
            if (fourcc == "DX10" && b.Length > 148 && BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(128)) is 79 or 80)
                return SingleChannel(b.AsSpan(148), w, h);
            var fmt = fourcc switch { "DXT1" => TextureFormat.Dxt1, "DXT3" => TextureFormat.Dxt3, "DXT5" => TextureFormat.Dxt5, _ => (TextureFormat?)null };
            return fmt == null ? null : Compressed(b.AsSpan(128), w, h, fmt.Value, mips, fmt != TextureFormat.Dxt1);
        }
        int bits = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(88));
        uint rm = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(92)), gm = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(96));
        uint bm = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(100)), am = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(104));
        int bpp = bits / 8;
        if (bpp is < 1 or > 4 || 128 + (long)w * h * bpp > b.Length) return null;
        var outp = new byte[w * h * 4];
        bool alpha = false;
        for (int i = 0; i < w * h; i++)
        {
            uint v = 0;
            for (int k = 0; k < bpp; k++) v |= (uint)b[128 + i * bpp + k] << (8 * k);
            outp[i * 4] = Channel(v, rm); outp[i * 4 + 1] = Channel(v, gm); outp[i * 4 + 2] = Channel(v, bm);
            outp[i * 4 + 3] = am == 0 ? (byte)255 : Channel(v, am);
            if (outp[i * 4 + 3] < 255) alpha = true;
        }
        return new TextureData { Width = w, Height = h, Format = TextureFormat.Rgba8, Data = outp, HasAlpha = alpha, FlipV = true };
    }

    /// <summary>One-channel block compression (BC4, "ATI1"): level 0 as grey, opaque pixels.</summary>
    static TextureData? SingleChannel(ReadOnlySpan<byte> data, int w, int h)
    {
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        if (data.Length < bw * bh * 8) return null;
        var outp = new byte[w * h * 4];
        Span<byte> values = stackalloc byte[8];
        for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                var block = data.Slice((by * bw + bx) * 8, 8);
                int a0 = block[0], a1 = block[1];
                values[0] = (byte)a0;
                values[1] = (byte)a1;
                if (a0 > a1)
                    for (int i = 1; i < 7; i++) values[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
                else
                {
                    for (int i = 1; i < 5; i++) values[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                    values[6] = 0;
                    values[7] = 255;
                }
                ulong bits = 0;
                for (int i = 0; i < 6; i++) bits |= (ulong)block[2 + i] << (8 * i);
                for (int py = 0; py < 4; py++)
                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px, y = by * 4 + py;
                        if (x >= w || y >= h) continue;
                        byte v = values[(int)((bits >> (3 * (py * 4 + px))) & 7)];
                        int o = (y * w + x) * 4;
                        outp[o] = outp[o + 1] = outp[o + 2] = v;
                        outp[o + 3] = 255;
                    }
            }
        return new TextureData { Width = w, Height = h, Format = TextureFormat.Rgba8, Data = outp, FlipV = true };
    }

    static byte Channel(uint v, uint mask)
    {
        if (mask == 0) return 0;
        int shift = BitOperations(mask);
        uint max = mask >> shift;
        return (byte)(((v & mask) >> shift) * 255 / Math.Max(1u, max));
    }

    static int BitOperations(uint mask) => System.Numerics.BitOperations.TrailingZeroCount(mask);

    static TextureData? Compressed(ReadOnlySpan<byte> data, int w, int h, TextureFormat format, int maxMips, bool maybeAlpha)
    {
        int block = format == TextureFormat.Dxt1 ? 8 : 16;
        int size = 0, levels = 0, lw = w, lh = h;
        while (levels < maxMips)
        {
            int level = Math.Max(1, (lw + 3) / 4) * Math.Max(1, (lh + 3) / 4) * block;
            if (size + level > data.Length) break;
            size += level;
            levels++;
            if (lw == 1 && lh == 1) break;
            lw = Math.Max(1, lw / 2);
            lh = Math.Max(1, lh / 2);
        }
        if (levels == 0) return null;
        bool complete = lw == 1 && lh == 1;
        if (!complete) levels = 1; // partial chains are not usable by GPUs; keep only level 0
        size = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * block;
        if (levels > 1)
        {
            size = 0; lw = w; lh = h;
            for (int i = 0; i < levels; i++)
            {
                size += Math.Max(1, (lw + 3) / 4) * Math.Max(1, (lh + 3) / 4) * block;
                lw = Math.Max(1, lw / 2); lh = Math.Max(1, lh / 2);
            }
        }
        var bytes = data[..size].ToArray();
        return new TextureData
        {
            Width = w, Height = h, Format = format, MipCount = levels, Data = bytes,
            HasAlpha = maybeAlpha && format != TextureFormat.Dxt1 && Dxt5HasAlpha(bytes, w, h, format),
            FlipV = true,
        };
    }

    /// <summary>True if any DXT3/5 block in level 0 has alpha below 255.</summary>
    static bool Dxt5HasAlpha(byte[] data, int w, int h, TextureFormat format)
    {
        int blocks = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4);
        for (int i = 0; i < blocks; i++)
        {
            int o = i * 16;
            if (format == TextureFormat.Dxt5)
            {
                byte a0 = data[o], a1 = data[o + 1];
                if (a0 < 250 || a1 < 250) return true;
                if (a0 <= a1)
                {
                    // 6-alpha mode: code 6 is explicit transparent.
                    ulong bits = 0;
                    for (int k = 0; k < 6; k++) bits |= (ulong)data[o + 2 + k] << (8 * k);
                    for (int k = 0; k < 16; k++)
                        if (((bits >> (3 * k)) & 7) == 6) return true;
                }
            }
            else
            {
                for (int k = 0; k < 8; k++) if (data[o + k] != 0xFF) return true;
            }
        }
        return false;
    }

    /// <summary>PLT V1: per pixel a shade and a palette layer index.</summary>
    public static PaletteTextureData? DecodePlt(byte[] b)
    {
        if (b.Length < 24) return null;
        int w = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(16));
        int h = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(20));
        if (w <= 0 || h <= 0 || 24 + (long)w * h * 2 > b.Length) return null;
        var plt = new PaletteTextureData { Width = w, Height = h, Shades = new byte[w * h], Layers = new byte[w * h] };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int src = 24 + ((h - 1 - y) * w + x) * 2; // stored bottom-up
                plt.Shades[y * w + x] = b[src];
                plt.Layers[y * w + x] = b[src + 1];
            }
        return plt;
    }

    public static TextureInfo ParseTxi(byte[] b)
    {
        var info = new TextureInfo { HasInfo = true };
        foreach (var raw in Bin.Text.GetString(b).Replace("\r", "").Split('\n'))
        {
            var t = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 2) continue;
            string v = t[1];
            switch (t[0].ToLowerInvariant())
            {
                case "blending": info.Blending = v.ToLowerInvariant(); break;
                case "decal": info.Decal = v != "0"; break;
                case "envmaptexture": info.EnvironmentMap = v; break;
                case "bumpmaptexture": info.BumpMap = v; break;
                case "proceduretype": info.Procedure = v.ToLowerInvariant(); info.ProcedureType = v.Equals("cycle", StringComparison.OrdinalIgnoreCase) ? 1 : int.TryParse(v, out int pt) ? pt : 0; break;
                case "bumpyshinytexture": info.BumpyShiny = v; break;
                case "numx": info.NumX = int.TryParse(v, out int nx) ? nx : 1; break;
                case "numy": info.NumY = int.TryParse(v, out int ny) ? ny : 1; break;
                case "fps": info.Fps = float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0; break;
                case "ttfcontrast": info.TtfContrast = FontValue(v, 1, 0.01f, 10); break;
                case "ttfoutlineopacity": info.TtfOutlineOpacity = FontValue(v, 0.35f, 0, 1); break;
                case "ttfoutlinebrightness": info.TtfOutlineBrightness = FontValue(v, 0, 0, 1); break;
                case "spacingr": info.FontSpacingR = FontValue(v, 0, -1, 1); break;
            }
        }
        return info;
    }

    static float FontValue(string value, float fallback, float min, float max) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f)
            ? Math.Clamp(f, min, max) : fallback;
}
