using System.Buffers.Binary;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// The horizontal metrics of a TrueType face (head, hhea, hmtx and a Unicode cmap, format 4 or 12): each
/// character's unhinted advance in font units. The classic interface lays out the game's TrueType faces with
/// these when it has to round each character's advance the way the original does (docs/CLASSIC_UI.md).
/// Read from the public OpenType table layout.
/// </summary>
public sealed class TrueTypeMetrics
{
    public int UnitsPerEm { get; private set; } = 1000;
    readonly Dictionary<int, int> _glyphOf = new();
    ushort[] _advances = Array.Empty<ushort>();

    /// <summary>The metrics of a font file, or null when it has none of the tables needed.</summary>
    public static TrueTypeMetrics? Read(ReadOnlySpan<byte> data)
    {
        try
        {
            var m = new TrueTypeMetrics();
            return m.Load(data) ? m : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
        catch (IndexOutOfRangeException) { return null; }
    }

    /// <summary>The character's advance in font units (the first glyph's, usually .notdef, when it has no glyph).</summary>
    public int AdvanceUnits(int codepoint)
    {
        int g = _glyphOf.TryGetValue(codepoint, out int v) ? v : 0;
        if (_advances.Length == 0) return 0;
        return _advances[Math.Clamp(g, 0, _advances.Length - 1)];
    }

    /// <summary>The character's unhinted advance in pixels at a pixel size (em height).</summary>
    public float Advance(int codepoint, float pixelSize) => AdvanceUnits(codepoint) * pixelSize / UnitsPerEm;

    bool Load(ReadOnlySpan<byte> d)
    {
        int tables = BinaryPrimitives.ReadUInt16BigEndian(d[4..]);
        int head = -1, hhea = -1, hmtx = -1, cmap = -1;
        for (int i = 0; i < tables; i++)
        {
            var rec = d.Slice(12 + 16 * i, 16);
            int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(rec[8..]);
            switch (System.Text.Encoding.ASCII.GetString(rec[..4]))
            {
                case "head": head = offset; break;
                case "hhea": hhea = offset; break;
                case "hmtx": hmtx = offset; break;
                case "cmap": cmap = offset; break;
            }
        }
        if (head < 0 || hhea < 0 || hmtx < 0 || cmap < 0) return false;
        UnitsPerEm = Math.Max(1, (int)BinaryPrimitives.ReadUInt16BigEndian(d[(head + 18)..]));
        int metrics = BinaryPrimitives.ReadUInt16BigEndian(d[(hhea + 34)..]);
        _advances = new ushort[metrics];
        for (int i = 0; i < metrics; i++) _advances[i] = BinaryPrimitives.ReadUInt16BigEndian(d[(hmtx + 4 * i)..]);
        // Glyphs past numberOfHMetrics share the last advance (AdvanceUnits clamps to it).
        return ReadCmap(d, cmap);
    }

    bool ReadCmap(ReadOnlySpan<byte> d, int cmap)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(d[(cmap + 2)..]);
        int best = -1, bestRank = -1;
        for (int i = 0; i < count; i++)
        {
            var rec = d[(cmap + 4 + 8 * i)..];
            int platform = BinaryPrimitives.ReadUInt16BigEndian(rec), encoding = BinaryPrimitives.ReadUInt16BigEndian(rec[2..]);
            int offset = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(rec[4..]);
            int format = BinaryPrimitives.ReadUInt16BigEndian(d[offset..]);
            int rank = (platform, encoding, format) switch
            {
                (3, 10, 12) or (0, 4, 12) or (0, 6, 12) => 3,
                (3, 1, 4) => 2,
                (0, _, 4) => 1,
                _ => -1,
            };
            if (rank > bestRank) { best = offset; bestRank = rank; }
        }
        if (best < 0) return false;
        // Overlapping or repeated ranges in a malformed table cannot map more than all of Unicode twice.
        long budget = 2 * 0x110000L;
        if (BinaryPrimitives.ReadUInt16BigEndian(d[best..]) == 12)
        {
            int groups = (int)BinaryPrimitives.ReadUInt32BigEndian(d[(best + 12)..]);
            for (int i = 0; i < groups; i++)
            {
                var g = d[(best + 16 + 12 * i)..];
                int start = (int)BinaryPrimitives.ReadUInt32BigEndian(g), end = (int)BinaryPrimitives.ReadUInt32BigEndian(g[4..]);
                int glyph = (int)BinaryPrimitives.ReadUInt32BigEndian(g[8..]);
                // Only the planes a game's text can hold; skip absurd ranges.
                if (end < start || (long)end - start > 0x10000) continue;
                if ((budget -= end - start + 1) < 0) break;
                for (int c = start; c <= end; c++) _glyphOf[c] = glyph + (c - start);
            }
            return true;
        }
        int segs = BinaryPrimitives.ReadUInt16BigEndian(d[(best + 6)..]) / 2;
        int ends = best + 14, starts = ends + 2 * segs + 2, deltas = starts + 2 * segs, ranges = deltas + 2 * segs;
        for (int s = 0; s < segs; s++)
        {
            int end = BinaryPrimitives.ReadUInt16BigEndian(d[(ends + 2 * s)..]);
            int start = BinaryPrimitives.ReadUInt16BigEndian(d[(starts + 2 * s)..]);
            short delta = BinaryPrimitives.ReadInt16BigEndian(d[(deltas + 2 * s)..]);
            int rangeAt = ranges + 2 * s;
            int range = BinaryPrimitives.ReadUInt16BigEndian(d[rangeAt..]);
            if (end >= start && (budget -= end - start + 1) < 0) break;
            for (int c = start; c <= end && c != 0xFFFF; c++)
            {
                int glyph;
                if (range == 0) glyph = (c + delta) & 0xFFFF;
                else
                {
                    glyph = BinaryPrimitives.ReadUInt16BigEndian(d[(rangeAt + range + 2 * (c - start))..]);
                    if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                }
                if (glyph != 0) _glyphOf[c] = glyph;
            }
        }
        return true;
    }
}
