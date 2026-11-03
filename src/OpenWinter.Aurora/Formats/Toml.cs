using System.Globalization;
using System.Text;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// The TOML NWN:EE writes for settings.tml: <c>[table]</c> headers and <c>key = value</c> lines. Values come back as
/// text by dotted path ("server.vault.mode"), strings unquoted. Arrays of tables (<c>[[...]]</c>) are skipped.
/// </summary>
public static class Toml
{
    public static Dictionary<string, string> Read(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? table = "";
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("[[", StringComparison.Ordinal))
            {
                table = null;
                continue;
            }
            if (line[0] == '[')
            {
                int end = ClosingBracket(line);
                table = end > 0 ? Path(line[1..end]) : null;
                continue;
            }
            if (table == null) continue;
            int eq = KeyEnd(line);
            if (eq <= 0) continue;
            var key = Path(line[..eq]);
            if (key == null || Value(line[(eq + 1)..].Trim()) is not { } value) continue;
            values[table.Length > 0 ? table + "." + key : key] = value;
        }
        return values;
    }

    /// <summary>A value as text: a string's contents, or a number, boolean or array as written.</summary>
    static string? Value(string s)
    {
        if (s.Length == 0) return null;
        if (s[0] == '"') return BasicString(s, out _);
        if (s[0] == '\'')
        {
            int end = s.IndexOf('\'', 1);
            return end > 0 ? s[1..end] : null;
        }
        int hash = s.IndexOf('#');
        return (hash >= 0 ? s[..hash] : s).Trim();
    }

    static string? BasicString(string s, out int end)
    {
        var sb = new StringBuilder();
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"')
            {
                end = i;
                return sb.ToString();
            }
            if (c != '\\' || i + 1 >= s.Length)
            {
                sb.Append(c);
                continue;
            }
            char e = s[++i];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u' or 'U':
                    int n = e == 'u' ? 4 : 8;
                    if (i + n < s.Length && int.TryParse(s.AsSpan(i + 1, n), NumberStyles.HexNumber, null, out int cp) && cp is >= 0 and <= 0x10FFFF)
                    {
                        sb.Append(char.ConvertFromUtf32(cp));
                        i += n;
                    }
                    break;
                default: sb.Append(e); break;
            }
        }
        end = -1;
        return null;
    }

    /// <summary>Dotted key segments, quoted or bare, joined with '.'; null when malformed.</summary>
    static string? Path(string s)
    {
        var parts = new List<string>();
        int i = 0;
        while (true)
        {
            while (i < s.Length && s[i] is ' ' or '\t') i++;
            if (i >= s.Length) return null;
            if (s[i] == '"')
            {
                if (BasicString(s[i..], out int end) is not { } part) return null;
                parts.Add(part);
                i += end + 1;
            }
            else
            {
                int start = i;
                while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] is '-' or '_')) i++;
                if (i == start) return null;
                parts.Add(s[start..i]);
            }
            while (i < s.Length && s[i] is ' ' or '\t') i++;
            if (i >= s.Length) return string.Join('.', parts);
            if (s[i] != '.') return null;
            i++;
        }
    }

    static int ClosingBracket(string line)
    {
        bool quoted = false;
        for (int i = 1; i < line.Length; i++)
        {
            if (line[i] == '"' && line[i - 1] != '\\') quoted = !quoted;
            else if (line[i] == ']' && !quoted) return i;
        }
        return -1;
    }

    static int KeyEnd(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) quoted = !quoted;
            else if (line[i] == '=' && !quoted) return i;
        }
        return -1;
    }
}
