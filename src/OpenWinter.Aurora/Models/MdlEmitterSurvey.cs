using System.Buffers.Binary;

namespace OpenWinter.Aurora.Models;

/// <summary>Raw emitter node contents for the emitter inventory diagnostic (every header field and controller).</summary>
public sealed class RawEmitter
{
    public string Node = "";
    /// <summary>Header fields by name, as text (binary: from the emitter header; text models: every property line).</summary>
    public readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Geometry controllers: id (binary) or name (text) to (rows, first row's values).</summary>
    public readonly Dictionary<string, (int Rows, float[] Values)> Controllers = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Controllers keyed by animations: (animation, controller, rows, key times, key values row by row).</summary>
    public readonly List<(string Animation, string Controller, int Rows, float[] Times, float[][] Values)> Animated = new();
}

public sealed partial class MdlBinaryReader
{
    /// <summary>Every emitter node of a compiled model with all of its header fields and controllers.</summary>
    public static List<RawEmitter> SurveyEmitters(byte[] buf)
    {
        var r = new MdlBinaryReader(buf);
        var result = new Dictionary<string, RawEmitter>(StringComparer.OrdinalIgnoreCase);
        r.SurveyNode((int)r.U(0x48), result, 0);
        int animPtr = (int)r.U(0x78), animCount = (int)r.U(0x7C);
        for (int i = 0; i < animCount && i < 512; i++)
        {
            int a = (int)r.U(animPtr + i * 4);
            r.SurveyAnimNode((int)r.U(a + 0x48), r.S(a + 0x08, 64), result, 0);
        }
        return result.Values.ToList();
    }

    void SurveyNode(int o, Dictionary<string, RawEmitter> into, int depth)
    {
        if (depth > 128 || o <= 0 || 12 + o + 0x70 > _b.Length) return;
        uint flags = U(o + 0x6C);
        if ((flags & NodeEmitter) != 0 && 12 + o + 0x148 <= _b.Length)
        {
            var e = new RawEmitter { Node = S(o + 0x20, 32) };
            e.Fields["deadspace"] = F(o + 0x70).ToString("0.###");
            e.Fields["blastRadius"] = F(o + 0x74).ToString("0.###");
            e.Fields["blastLength"] = F(o + 0x78).ToString("0.###");
            e.Fields["xgrid"] = U(o + 0x7C).ToString();
            e.Fields["ygrid"] = U(o + 0x80).ToString();
            e.Fields["spawntype"] = U(o + 0x84).ToString();
            e.Fields["update"] = S(o + 0x88, 32);
            e.Fields["render"] = S(o + 0xA8, 32);
            e.Fields["blend"] = S(o + 0xC8, 32);
            e.Fields["texture"] = S(o + 0xE8, 64);
            e.Fields["chunkName"] = S(o + 0x128, 16);
            e.Fields["twosidedtex"] = U(o + 0x138).ToString();
            e.Fields["loop"] = U(o + 0x13C).ToString();
            e.Fields["renderorder"] = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(12 + o + 0x140)).ToString();
            e.Fields["byte142"] = _b[12 + o + 0x142].ToString();
            e.Fields["byte143"] = _b[12 + o + 0x143].ToString();
            uint ef = U(o + 0x144);
            e.Fields["flags"] = "0x" + ef.ToString("X");
            foreach (var (bit, name) in EmitterFlagNames)
                e.Fields[name] = (ef & bit) != 0 ? "1" : "0";
            foreach (var (type, k) in Controllers(o))
                e.Controllers[ControllerName(type)] = (k.Times.Length, k.Values.Take(k.Width).ToArray());
            into.TryAdd(e.Node, e);
        }
        int children = (int)U(o + 0x48), count = (int)U(o + 0x4C);
        for (int i = 0; i < count && i < 4096; i++) SurveyNode((int)U(children + i * 4), into, depth + 1);
    }

    void SurveyAnimNode(int o, string anim, Dictionary<string, RawEmitter> into, int depth)
    {
        if (depth > 128 || o <= 0 || 12 + o + 0x70 > _b.Length) return;
        if (into.TryGetValue(S(o + 0x20, 32), out var e))
            foreach (var (type, k) in Controllers(o))
                e.Animated.Add((anim, ControllerName(type), k.Times.Length, k.Times,
                    Enumerable.Range(0, k.Times.Length).Select(r => k.Values.Skip(r * k.Width).Take(k.Width).ToArray()).ToArray()));
        int children = (int)U(o + 0x48), count = (int)U(o + 0x4C);
        for (int i = 0; i < count && i < 4096; i++) SurveyAnimNode((int)U(children + i * 4), anim, into, depth + 1);
    }

    /// <summary>Emitter header flag bits (public community format descriptions).</summary>
    internal static readonly (uint Bit, string Name)[] EmitterFlagNames =
    {
        (0x0001, "p2p"), (0x0002, "p2p_sel"), (0x0004, "affectedByWind"), (0x0008, "m_isTinted"), (0x0010, "bounce"),
        (0x0020, "random"), (0x0040, "inherit"), (0x0080, "inheritvel"), (0x0100, "inherit_local"), (0x0200, "splat"),
        (0x0400, "inherit_part"), (0x0800, "flag800"), (0x1000, "flag1000"), (0x2000, "flag2000"), (0x4000, "flag4000"), (0x8000, "flag8000"),
    };

    /// <summary>Emitter controller names by id (public community format descriptions).</summary>
    internal static readonly Dictionary<int, string> EmitterControllerNames = new()
    {
        [8] = "position", [20] = "orientation", [36] = "scale",
        [80] = "alphaEnd", [84] = "alphaStart", [88] = "birthrate", [92] = "bounce_co", [96] = "colorEnd",
        [108] = "colorStart", [120] = "combinetime", [124] = "drag", [128] = "fps", [132] = "frameEnd",
        [136] = "frameStart", [140] = "grav", [144] = "lifeExp", [148] = "mass", [152] = "p2p_bezier2",
        [156] = "p2p_bezier3", [160] = "particleRot", [164] = "randvel", [168] = "sizeStart", [172] = "sizeEnd",
        [176] = "sizeStart_y", [180] = "sizeEnd_y", [184] = "spread", [188] = "threshold", [192] = "velocity",
        [196] = "xsize", [200] = "ysize", [204] = "blurlength", [208] = "lightningDelay", [212] = "lightningRadius",
        [216] = "lightningScale", [220] = "lightningSubDiv", [224] = "detonate",
        [464] = "alphaMid", [468] = "colorMid", [480] = "percentStart", [481] = "percentMid", [482] = "percentEnd",
        [484] = "sizeMid", [488] = "sizeMid_y",
    };

    /// <summary>Controllers keyed by animations on every node: (node kind, controller, key rows).</summary>
    public static List<(string Kind, string Controller, int Rows)> SurveyAnimatedControllers(byte[] buf)
    {
        var r = new MdlBinaryReader(buf);
        var result = new List<(string, string, int)>();
        int animPtr = (int)r.U(0x78), animCount = (int)r.U(0x7C);
        for (int i = 0; i < animCount && i < 512; i++) r.AnimControllers((int)r.U((int)r.U(animPtr + i * 4) + 0x48), result, 0);
        return result;
    }

    void AnimControllers(int o, List<(string, string, int)> into, int depth)
    {
        if (depth > 128 || o <= 0 || 12 + o + 0x70 > _b.Length) return;
        uint flags = U(o + 0x6C);
        string kind = (flags & NodeEmitter) != 0 ? "emitter" : (flags & NodeLight) != 0 ? "light" : (flags & NodeMesh) != 0 ? "mesh" : "dummy";
        foreach (var (type, k) in Controllers(o))
            into.Add((kind, kind == "mesh" ? MeshControllerName(type) : kind == "emitter" ? ControllerName(type) : "ctrl" + type, k.Times.Length));
        int children = (int)U(o + 0x48), count = (int)U(o + 0x4C);
        for (int i = 0; i < count && i < 4096; i++) AnimControllers((int)U(children + i * 4), into, depth + 1);
    }

    static string MeshControllerName(int type) => type switch
    {
        8 => "position",
        20 => "orientation",
        36 => "scale",
        100 => "selfillumcolor",
        128 => "alpha",
        132 => "alpha",
        _ => "ctrl" + type,
    };

    static string ControllerName(int type) => EmitterControllerNames.TryGetValue(type, out var n) ? n : "ctrl" + type;
}

public sealed partial class MdlAsciiReader
{
    /// <summary>Every emitter node of a text model with its properties and the controllers its animations key.</summary>
    public static List<RawEmitter> SurveyEmitters(byte[] buf)
    {
        var r = new MdlAsciiReader(Bin.Text.GetString(buf));
        var result = new Dictionary<string, RawEmitter>(StringComparer.OrdinalIgnoreCase);
        string anim = "";
        while (r._i < r._lines.Length)
        {
            var t = r.NextTokens();
            if (t.Length == 0) break;
            var key = t[0].ToLowerInvariant();
            if (key == "newanim") anim = t.Length > 1 ? t[1] : "";
            else if (key == "doneanim") anim = "";
            else if (key == "node" && anim.Length == 0)
            {
                var n = r.ReadNode(t);
                if (n.Type != "emitter") continue;
                var e = new RawEmitter { Node = n.Name };
                foreach (var (k, v) in n.Props)
                {
                    if (k.Equals("parent", StringComparison.OrdinalIgnoreCase)) continue;
                    var nums = v.Skip(1).Select(x => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? (float?)f : null).ToArray();
                    if (nums.Length > 0 && nums.All(x => x != null)) e.Controllers[k] = (1, nums.Select(x => x!.Value).ToArray());
                    else e.Fields[k] = string.Join(" ", v.Skip(1));
                }
                result.TryAdd(e.Node, e);
            }
            else if (key == "node" && t.Length > 2 && result.TryGetValue(t[2], out var target))
            {
                while (r._i < r._lines.Length)
                {
                    var l = r.NextTokens();
                    if (l.Length == 0 || l[0].Equals("endnode", StringComparison.OrdinalIgnoreCase)) break;
                    if (!l[0].EndsWith("key", StringComparison.OrdinalIgnoreCase)) continue;
                    var keyRows = r.KeyRows(l).ToList();
                    target.Animated.Add((anim, l[0][..^3], keyRows.Count, keyRows.Select(k => P(k[0])).ToArray(), keyRows.Select(k => k.Skip(1).Select(P).ToArray()).ToArray()));
                }
            }
        }
        return result.Values.ToList();
    }
}
