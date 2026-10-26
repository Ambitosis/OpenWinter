using System.Globalization;
using OpenWinter.Aurora.Formats;

/// <summary>
/// "exports": ["nwexp1.are", ...] in suite.json: files each engine wrote to its user directory's nwnx folder
/// (NWNX_Area_ExportARE / ExportGIT) are turned into readings and compared like the script's own. A file becomes
/// its field labels and types (the structure), every scalar value, the tiles, the variables, and per object list the
/// count and the objects' tags (sorted: the order isn't compared). Left out: object ids, a UUID's value (only whether
/// there is one), NWNX's own NWNX_* fields, and the objects' own fields (each engine's object format; written as
/// uncompared RAW lines).
/// </summary>
static class ParityExports
{
    public static List<string> Readings(string folder, IEnumerable<string> files)
    {
        var lines = new List<string>();
        foreach (var name in files)
        {
            string key = "file." + name;
            var path = Path.Combine(folder, name);
            if (!File.Exists(path)) { lines.Add($"PARITY|{key}|missing"); continue; }
            GffStruct root;
            try { root = Gff.Read(File.ReadAllBytes(path)); }
            catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException) { lines.Add($"PARITY|{key}|unreadable"); continue; }
            Struct(lines, key, root);
        }
        return lines;
    }

    static void Struct(List<string> lines, string key, GffStruct s)
    {
        lines.Add($"PARITY|{key}.fields|" + string.Join(",", Own(s.Fields).Select(f => f.Label + ":" + f.Type).Order(StringComparer.Ordinal)));
        foreach (var f in Own(s.Fields))
        {
            string k = key + "." + f.Label;
            switch (f.Value)
            {
                case GffStruct child: Struct(lines, k, child); break;
                case List<GffStruct> list when f.Label == "Tile_List":
                    lines.Add($"PARITY|{k}|" + string.Join(" ", list.Select(t => string.Join("/", t.Fields.Select(tf => Value(tf.Value))))));
                    lines.Add($"PARITY|{k}.fields|" + string.Join(",", list.FirstOrDefault()?.Fields.Select(tf => tf.Label + ":" + tf.Type) ?? []));
                    break;
                case List<GffStruct> list when f.Label == "VarTable":
                    lines.Add($"PARITY|{k}|" + string.Join(" ", list.Select(v => $"{v.GetString("Name")}/{v.GetInt("Type")}/{Value(v["Value"])}")));
                    break;
                case List<GffStruct> list:
                    lines.Add($"PARITY|{k}.count|{list.Count}");
                    lines.Add($"PARITY|{k}.tags|" + string.Join(" ", list.Select(o => o.GetString("Tag")).Order(StringComparer.Ordinal)));
                    lines.Add($"RAW|{k}.fields|" + string.Join(",", list.SelectMany(o => Own(o.Fields).Select(of => of.Label + ":" + of.Type)).Distinct().Order(StringComparer.Ordinal)));
                    break;
                case string uuid when f.Label == "UUID":
                    lines.Add($"PARITY|{k}|" + (uuid.Length > 0 ? "set" : ""));
                    break;
                default:
                    if (f.Label != "ObjectId") lines.Add($"PARITY|{k}|{Value(f.Value)}");
                    break;
            }
        }
    }

    static IEnumerable<GffField> Own(IEnumerable<GffField> fields) => fields.Where(f => !f.Label.StartsWith("NWNX_", StringComparison.Ordinal));

    static string Value(object? v) => v switch
    {
        null => "",
        float x => x.ToString("0.###", CultureInfo.InvariantCulture),
        double x => x.ToString("0.###", CultureInfo.InvariantCulture),
        LocString l => $"{l.StrRef}:{string.Join(";", l.Strings.Select(p => p.Key + "=" + p.Value))}",
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };
}
