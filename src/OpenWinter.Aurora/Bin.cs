using System.Text;

namespace OpenWinter.Aurora;

internal static class Bin
{
    /// <summary>Windows-1252 is the de-facto text encoding of Aurora data.</summary>
    public static readonly Encoding Text = CreateText();

    static Encoding CreateText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    /// <summary>Reads a fixed-size NUL-padded field, lowercased-insensitive callers decide casing.</summary>
    public static string FixedString(ReadOnlySpan<byte> buf, int offset, int size)
    {
        if (offset < 0 || offset >= buf.Length) return "";
        var span = buf.Slice(offset, Math.Min(size, buf.Length - offset));
        int end = span.IndexOf((byte)0);
        if (end >= 0) span = span[..end];
        return Text.GetString(span);
    }
}
