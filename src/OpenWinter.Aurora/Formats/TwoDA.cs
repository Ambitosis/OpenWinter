namespace OpenWinter.Aurora.Formats;

/// <summary>2DA V2.0 text table. Empty cells ("****") read back as "".</summary>
public sealed class TwoDA
{
    public static readonly TwoDA Empty = new();

    public string[] Columns { get; private set; } = Array.Empty<string>();
    readonly Dictionary<string, int> _colIndex = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string[]> _rows = new();

    public int RowCount => _rows.Count;

    public static TwoDA Parse(byte[] buf)
    {
        var t = new TwoDA();
        var lines = Bin.Text.GetString(buf).Replace("\r", "").Split('\n');
        bool header = false;
        for (int i = 1; i < lines.Length; i++)
        {
            var tokens = Tokenize(lines[i]);
            if (tokens.Count == 0) continue;
            if (!header)
            {
                if (tokens[0].StartsWith("DEFAULT", StringComparison.OrdinalIgnoreCase)) continue;
                t.Columns = tokens.ToArray();
                for (int c = 0; c < tokens.Count; c++) t._colIndex.TryAdd(tokens[c], c);
                header = true;
                continue;
            }
            if (!int.TryParse(tokens[0], out int index) || index < 0) index = t._rows.Count;
            var row = new string[t.Columns.Length];
            for (int c = 0; c < row.Length; c++)
            {
                var v = c + 1 < tokens.Count ? tokens[c + 1] : "";
                row[c] = v == "****" ? "" : v;
            }
            while (t._rows.Count < index) t._rows.Add(new string[t.Columns.Length]);
            if (index == t._rows.Count) t._rows.Add(row);
            else t._rows[index] = row;
        }
        return t;
    }

    public int ColumnIndex(string column) => _colIndex.TryGetValue(column, out int c) ? c : -1;

    public string Get(int row, string column)
    {
        int c = ColumnIndex(column);
        return Get(row, c);
    }

    public string Get(int row, int column)
    {
        if (row < 0 || row >= _rows.Count || column < 0) return "";
        var r = _rows[row];
        return column < r.Length ? r[column] ?? "" : "";
    }

    public int GetInt(int row, string column, int fallback = 0)
    {
        var v = Get(row, column);
        if (v.Length == 0) return fallback;
        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(v[2..], System.Globalization.NumberStyles.HexNumber, null, out int h)) return h;
        return int.TryParse(v, out int i) ? i : float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? (int)f : fallback;
    }

    public float GetFloat(int row, string column, float fallback = 0f)
    {
        var v = Get(row, column);
        return float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : fallback;
    }

    public bool IsEmpty(int row, string column) => Get(row, column).Length == 0;

    /// <summary>First row whose column equals the value (case-insensitive), or -1.</summary>
    public int Find(string column, string value)
    {
        int c = ColumnIndex(column);
        if (c < 0) return -1;
        for (int r = 0; r < _rows.Count; r++)
            if (string.Equals(Get(r, c), value, StringComparison.OrdinalIgnoreCase)) return r;
        return -1;
    }

    static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        int i = 0, n = line.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(line[i])) i++;
            if (i >= n) break;
            if (line[i] == '"')
            {
                int end = line.IndexOf('"', i + 1);
                if (end < 0) end = n;
                tokens.Add(line.Substring(i + 1, end - i - 1));
                i = end + 1;
            }
            else
            {
                int start = i;
                while (i < n && !char.IsWhiteSpace(line[i])) i++;
                tokens.Add(line[start..i]);
            }
        }
        return tokens;
    }
}
