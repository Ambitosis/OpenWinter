namespace OpenWinter.Aurora.Formats;

/// <summary>INI-style text (tileset .set files, TXI-like key files). Names are case-insensitive.</summary>
public sealed class Ini
{
    readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);

    public static Ini Parse(byte[] buf)
    {
        var ini = new Ini();
        Dictionary<string, string>? section = null;
        foreach (var raw in Bin.Text.GetString(buf).Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';') continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                ini._sections[line[1..^1].Trim()] = section;
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq > 0 && section != null) section[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return ini;
    }

    public bool HasSection(string section) => _sections.ContainsKey(section);

    public string Get(string section, string key, string fallback = "") =>
        _sections.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) ? v : fallback;

    public int GetInt(string section, string key, int fallback = 0) =>
        int.TryParse(Get(section, key), out int v) ? v : fallback;

    public float GetFloat(string section, string key, float fallback = 0f) =>
        float.TryParse(Get(section, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

    public IReadOnlyDictionary<string, string> Section(string section) =>
        _sections.TryGetValue(section, out var s) ? s : new Dictionary<string, string>();
}
