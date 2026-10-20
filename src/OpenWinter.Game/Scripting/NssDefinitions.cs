using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenWinter.Game.Scripting;

public enum NssType : byte
{
    Void, Int, Float, String, Object, Vector, Action,
    // Engine structures, in nwscript.nss ENGINE_STRUCTURE_n order.
    Effect, Event, Location, Talent, ItemProperty, SqlQuery, Cassowary, Json,
}

public sealed record NssParam(NssType Type, string Name, string? Default);

public sealed record NssFunction(int Index, string Name, NssType Returns, NssParam[] Params);

/// <summary>
/// Engine function signatures and constants from nwscript.nss. Function
/// indexes (the ACTION opcode's routine number) are declaration order.
/// </summary>
public sealed class NssDefinitions
{
    public readonly List<NssFunction> Functions = new();
    public readonly Dictionary<string, NssFunction> ByName = new(StringComparer.Ordinal);
    public readonly Dictionary<string, (NssType Type, string Value)> Constants = new(StringComparer.Ordinal);
    /// <summary>Engine structure names by index (effect, event, location, ...).</summary>
    public readonly List<string> EngineStructures = new();

    static readonly Regex Define = new(@"^#define\s+ENGINE_STRUCTURE_(\d+)\s+(\w+)", RegexOptions.Compiled);
    static readonly Regex Function = new(@"^\s*(\w+)\s+(\w+)\s*\(([^)]*)\)\s*;", RegexOptions.Compiled);
    static readonly Regex Constant = new(@"^\s*(int|float|string|object|vector|location|json)\s+(\w+)\s*=\s*(.+?)\s*;", RegexOptions.Compiled);

    public static NssDefinitions Parse(string source)
    {
        var d = new NssDefinitions();
        var structs = new SortedDictionary<int, string>();
        foreach (var raw in source.Replace("\r", "").Split('\n'))
        {
            var line = StripComment(raw);
            if (line.Length == 0) continue;
            var m = Define.Match(line);
            if (m.Success) { structs[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value; continue; }
            m = Function.Match(line);
            if (m.Success && TryType(m.Groups[1].Value, out var ret))
            {
                var ps = new List<NssParam>();
                foreach (var p in SplitParams(m.Groups[3].Value))
                {
                    var eq = p.IndexOf('=');
                    var decl = (eq >= 0 ? p[..eq] : p).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (decl.Length < 2 || !TryType(decl[0], out var pt)) continue;
                    ps.Add(new NssParam(pt, decl[1], eq >= 0 ? p[(eq + 1)..].Trim() : null));
                }
                var f = new NssFunction(d.Functions.Count, m.Groups[2].Value, ret, ps.ToArray());
                d.Functions.Add(f);
                d.ByName.TryAdd(f.Name, f);
                continue;
            }
            m = Constant.Match(line);
            if (m.Success && TryType(m.Groups[1].Value, out var ct))
                d.Constants.TryAdd(m.Groups[2].Value, (ct, m.Groups[3].Value));
        }
        d.EngineStructures.AddRange(structs.Values);
        return d;
    }

    static string StripComment(string line)
    {
        bool inString = false;
        for (int i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"') inString = !inString;
            if (!inString && line[i] == '/' && line[i + 1] == '/') return line[..i].Trim();
        }
        return line.Trim();
    }

    static IEnumerable<string> SplitParams(string s)
    {
        int depth = 0, start = 0;
        bool str = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"') str = !str;
            if (str) continue;
            if (c is '(' or '[') depth++;
            else if (c is ')' or ']') depth--;
            else if (c == ',' && depth == 0) { yield return s[start..i]; start = i + 1; }
        }
        if (s.Trim().Length > 0) yield return s[start..];
    }

    public static bool TryType(string name, out NssType type)
    {
        type = name switch
        {
            "void" => NssType.Void,
            "int" => NssType.Int,
            "float" => NssType.Float,
            "string" => NssType.String,
            "object" => NssType.Object,
            "vector" => NssType.Vector,
            "action" => NssType.Action,
            "effect" => NssType.Effect,
            "event" => NssType.Event,
            "location" => NssType.Location,
            "talent" => NssType.Talent,
            "itemproperty" => NssType.ItemProperty,
            "sqlquery" => NssType.SqlQuery,
            "cassowary" => NssType.Cassowary,
            "json" => NssType.Json,
            _ => (NssType)255,
        };
        return (byte)type != 255;
    }

    /// <summary>Integer value of an int constant (e.g. OBJECT_TYPE_CREATURE), for implementations that want names.</summary>
    public int Int(string name) =>
        Constants.TryGetValue(name, out var c) && c.Type == NssType.Int ? ParseInt(c.Value) : throw new KeyNotFoundException(name);

    static int ParseInt(string v)
    {
        v = v.Trim();
        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return (int)uint.Parse(v[2..], NumberStyles.HexNumber);
        return int.Parse(v, CultureInfo.InvariantCulture);
    }
}
