using System.Globalization;
using OpenWinter.Aurora.Resources;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// A font of the original interface. Bitmap fonts are a texture plus a TXI listing each character's
/// rectangle (upperleftcoords / lowerrightcoords in texture coordinates) and the cell height (fontheight,
/// in interface units); TrueType fonts (fnt_default) are used as they are. Character codes are Windows-1252.
/// </summary>
public sealed class GuiFont
{
    public const string DefaultName = "fnt_galahad14";

    public string Name { get; private init; } = "";
    /// <summary>Line height in pixels.</summary>
    public float Height { get; private init; }
    /// <summary>Baseline from the top of the line, in pixels.</summary>
    public float Baseline { get; private init; }
    float _spacingR, _spacingB;
    Texture2D? _texture;
    Rect2[] _src = Array.Empty<Rect2>();
    float[] _advance = Array.Empty<float>();
    Font? _ttf;
    int _ttfSize;
    /// <summary>Extra advance per character for a substituted TrueType face (see <see cref="TrueTypeFor"/>).</summary>
    float _tracking;

    public bool IsBitmap => _texture != null;

    static readonly System.Text.Encoding Cp1252 = CodePages();

    static System.Text.Encoding CodePages()
    {
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(1252);
        }
        catch (Exception) { return System.Text.Encoding.Latin1; }
    }

    /// <summary>
    /// Bitmap fonts the current original draws with one of its TrueType faces instead (measured on its screens:
    /// layout captions in fnt_galahad14 come out in fnt_maintext at about 16 px, e.g. "Load Game" 77 px wide).
    /// </summary>
    static readonly Dictionary<string, (string Face, int Size)> TrueTypeFor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fnt_galahad14"] = ("fnt_maintext", 16),
    };

    /// <summary>
    /// The original lays the substituted face's characters out wider than Godot does at 16 px: ink widths on its
    /// creation and main-menu screens (menudrive captures c02, c05, c22, h00) are "Select the Gender
    /// of your Character" 263 px, "Character Generation" 148, "Recommended" 97, "Alignment" 67, "Multiplayer" 74,
    /// "Load" 30, where Godot drew 246, 142, 91, 64, 72 and 29. Laying the characters out at their exact advances
    /// plus half a pixel each matches them within a pixel or two.
    /// </summary>
    const float SubstituteTracking = 0.5f;

    internal static GuiFont? Load(ClassicAssets assets, string name)
    {
        var res = assets.Content.Resources;
        string resref = name.ToLowerInvariant();
        if (TrueTypeFor.TryGetValue(resref, out var sub) && TrueType(res, name, sub.Face, sub.Size, SubstituteTracking) is { } substituted) return substituted;
        var txiBytes = res.Read(resref, ResType.Txi);
        var txi = txiBytes == null ? "" : System.Text.Encoding.ASCII.GetString(txiBytes);
        if (txi.Contains("upperleftcoords", StringComparison.OrdinalIgnoreCase) && assets.Texture(resref) is { } tex)
            return Bitmap(name, tex, txi);
        return TrueType(res, name, resref, 14);
    }

    static GuiFont? TrueType(OpenWinter.Aurora.Resources.ResMan res, string name, string face, int size, float tracking = -1)
    {
        if (res.Read(face, ResType.Ttf) is not { Length: > 0 } ttf) return null;
        try
        {
            var file = new FontFile { Data = ttf, Antialiasing = TextServer.FontAntialiasing.Gray, Hinting = TextServer.Hinting.Light };
            // A tracked face is laid out a character at a time at fractional advances.
            if (tracking >= 0) file.SubpixelPositioning = TextServer.SubpixelPositioning.OneQuarter;
            return new GuiFont { Name = name, _ttf = file, _ttfSize = size, _tracking = tracking, Height = file.GetHeight(size), Baseline = file.GetAscent(size) };
        }
        catch (Exception) { return null; }
    }

    /// <summary>Godot's own font, when the game has none.</summary>
    internal static GuiFont Fallback()
    {
        var f = ThemeDB.FallbackFont;
        return new GuiFont { Name = "fallback", _ttf = f, _ttfSize = 14, Height = f.GetHeight(14), Baseline = f.GetAscent(14) };
    }

    static GuiFont Bitmap(string name, Texture2D tex, string txi)
    {
        var lines = txi.Split('\n').Select(l => l.Trim()).ToArray();
        float Value(string key, float fallback)
        {
            foreach (var l in lines)
                if (l.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) && float.TryParse(l[(key.Length + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
            return fallback;
        }
        (float U, float V)[] Coords(string key)
        {
            int at = Array.FindIndex(lines, l => l.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            if (at < 0) return Array.Empty<(float, float)>();
            var parts = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int n = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 0;
            var result = new (float, float)[Math.Max(0, Math.Min(n, lines.Length - at - 1))];
            for (int i = 0; i < result.Length; i++)
            {
                var xyz = lines[at + 1 + i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                float u = xyz.Length > 0 && float.TryParse(xyz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0;
                float v = xyz.Length > 1 && float.TryParse(xyz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 0;
                result[i] = (u, v);
            }
            return result;
        }
        var ul = Coords("upperleftcoords");
        var lr = Coords("lowerrightcoords");
        float texUnits = Value("texturewidth", tex.GetWidth() / GuiModel.PixelsPerUnit);
        float scale = texUnits * GuiModel.PixelsPerUnit; // pixels on screen per 1.0 of texture coordinate
        int count = Math.Min(ul.Length, lr.Length);
        var src = new Rect2[count];
        var advance = new float[count];
        float w = tex.GetWidth(), h = tex.GetHeight();
        for (int i = 0; i < count; i++)
        {
            // Texture coordinates are bottom-up; the image is top-down.
            float x0 = ul[i].U * w, x1 = lr[i].U * w;
            float y0 = (1 - ul[i].V) * h, y1 = (1 - lr[i].V) * h;
            src[i] = new Rect2(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
            advance[i] = (lr[i].U - ul[i].U) * scale;
        }
        return new GuiFont
        {
            Name = name, _texture = tex, _src = src, _advance = advance,
            Height = Value("fontheight", 0.17f) * GuiModel.PixelsPerUnit,
            Baseline = Value("baselineheight", 0.14f) * GuiModel.PixelsPerUnit,
            _spacingR = Value("spacingR", 0) * GuiModel.PixelsPerUnit,
            _spacingB = Value("spacingB", 0) * GuiModel.PixelsPerUnit,
        };
    }

    /// <summary>Distance between lines in pixels.</summary>
    public float LineHeight => Height + _spacingB;

    int Code(char c)
    {
        if (c < 128) return c;
        Span<byte> one = stackalloc byte[2];
        Span<char> ch = stackalloc char[1] { c };
        return Cp1252.GetBytes(ch, one) > 0 ? one[0] : '?';
    }

    /// <summary>Width of a single line of text in pixels.</summary>
    public float Measure(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (_ttf != null && _tracking >= 0) return text.Sum(c => _ttf.GetCharSize(c, _ttfSize).X + _tracking) - _tracking;
        if (_ttf != null) return _ttf.GetStringSize(text, HorizontalAlignment.Left, -1, _ttfSize).X;
        float x = 0;
        foreach (var c in text)
        {
            int code = Code(c);
            if (code < _advance.Length) x += _advance[code] + _spacingR;
        }
        return x;
    }

    /// <summary>Draws one line with its top-left at <paramref name="at"/>.</summary>
    public void Draw(CanvasItem ci, Vector2 at, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (_ttf != null && _tracking >= 0)
        {
            float cx = at.X;
            foreach (var c in text)
            {
                ci.DrawChar(_ttf, new Vector2(cx, at.Y + Baseline), c.ToString(), _ttfSize, color);
                cx += _ttf.GetCharSize(c, _ttfSize).X + _tracking;
            }
            return;
        }
        if (_ttf != null)
        {
            ci.DrawString(_ttf, at + new Vector2(0, Baseline), text, HorizontalAlignment.Left, -1, _ttfSize, color);
            return;
        }
        float x = at.X;
        foreach (var c in text)
        {
            int code = Code(c);
            if (code >= _advance.Length) continue;
            float adv = _advance[code];
            if (adv > 0 && _src[code].Size.Y > 0)
                ci.DrawTextureRectRegion(_texture, new Rect2(MathF.Round(x), MathF.Round(at.Y), adv, Height), _src[code], color);
            x += adv + _spacingR;
        }
    }

    /// <summary>Breaks text into lines no wider than <paramref name="width"/> (explicit newlines kept).</summary>
    public List<string> Wrap(string text, float width)
    {
        var result = new List<string>();
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            if (width <= 0) { result.Add(para); continue; }
            var line = "";
            foreach (var word in para.Split(' '))
            {
                var attempt = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(attempt) > width)
                {
                    result.Add(line);
                    line = word;
                }
                else line = attempt;
            }
            result.Add(line);
        }
        return result;
    }

    /// <summary>
    /// Draws text in a box with the original's alignment (h: 0 left, 0.5 centre, 1 right; v: 0 top, 0.5 middle,
    /// 1 bottom), wrapping when <paramref name="wrap"/> is set. Returns the height used.
    /// </summary>
    public float DrawBox(CanvasItem ci, Rect2 box, string text, Color color, float alignH = 0, float alignV = 0, bool wrap = false)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var lines = wrap ? Wrap(text, box.Size.X) : new List<string> { text.Replace("\r", "").Split('\n')[0] };
        float total = lines.Count * LineHeight - _spacingB;
        float y = box.Position.Y + (box.Size.Y - total) * Math.Clamp(alignV, 0, 1);
        if (wrap && alignV == 0) y = box.Position.Y;
        foreach (var line in lines)
        {
            float w = Measure(line);
            float x = box.Position.X + (box.Size.X - w) * Math.Clamp(alignH, 0, 1);
            Draw(ci, new Vector2(x, y), line, color);
            y += LineHeight;
        }
        return total;
    }
}
