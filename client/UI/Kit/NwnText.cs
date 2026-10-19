using System.Text;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// NWN's inline colour tokens: <c>&lt;cRGB&gt;text&lt;/c&gt;</c>, where R, G and B are single raw bytes
/// (strings arrive decoded as Windows-1252, so each byte is one character). Turned into
/// RichTextLabel BBCode for display, or stripped for plain labels.
/// </summary>
public static class NwnText
{
    /// <summary>The raw byte a Windows-1252-decoded character came from.</summary>
    static int ByteOf(char c)
    {
        if (c <= 0xFF) return c;
        var bytes = OpenWinter.Aurora.Text.Windows1252.GetBytes(new[] { c });
        return bytes.Length == 1 ? bytes[0] : 0xFF;
    }

    static bool IsToken(string s, int i) => i + 5 < s.Length && s[i] == '<' && (s[i + 1] == 'c' || s[i + 1] == 'C') && s[i + 5] == '>';

    /// <summary>Plain text with colour tokens removed.</summary>
    public static string Strip(string s)
    {
        if (s.IndexOf('<') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (IsToken(s, i)) { i += 5; continue; }
            if (string.CompareOrdinal(s, i, "</c>", 0, 4) == 0 || string.CompareOrdinal(s, i, "</C>", 0, 4) == 0) { i += 3; continue; }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>BBCode with colour tokens as [color] tags; '[' escaped. <paramref name="baseColor"/> wraps the whole string.</summary>
    public static string ToBbcode(string s, string? baseColor = null)
    {
        var sb = new StringBuilder(s.Length + 32);
        if (baseColor != null) sb.Append("[color=").Append(baseColor).Append(']');
        int open = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (IsToken(s, i))
            {
                sb.Append($"[color=#{ByteOf(s[i + 2]):x2}{ByteOf(s[i + 3]):x2}{ByteOf(s[i + 4]):x2}]");
                open++;
                i += 5;
                continue;
            }
            if (string.CompareOrdinal(s, i, "</c>", 0, 4) == 0 || string.CompareOrdinal(s, i, "</C>", 0, 4) == 0)
            {
                if (open > 0) { sb.Append("[/color]"); open--; }
                i += 3;
                continue;
            }
            if (s[i] == '[') sb.Append("[lb]");
            else sb.Append(s[i]);
        }
        while (open-- > 0) sb.Append("[/color]");
        if (baseColor != null) sb.Append("[/color]");
        return sb.ToString();
    }
}
