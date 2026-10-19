using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Resources;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// Long text as the original draws it in conversations and message boxes: the game's TrueType faces (from its
/// data: fnt_default or fnt_default_hr for text, fnt_maintext for names) with the font's TXI contrast
/// and faint dark outline, in colour runs
/// (NWN &lt;cRGB&gt; tokens and the conversation's action / check / highlight spans), wrapped to a width.
/// </summary>
public sealed class ClassicText
{
    static readonly ConditionalWeakTable<ClassicAssets, Dictionary<(string, int), ClassicText>> All = new();
    /// <summary>The conversation's text face and the speaker's name face (measured against the original at 1280x720).</summary>
    public const string TextFont = "fnt_default_hr", NameFont = "fnt_maintext";
    public static string DefaultTextFont => Settings.Current.Interface.HighResolutionFont ? TextFont : "fnt_default";

    public readonly Font Font;
    public readonly int Size;
    public readonly float OutlineAlpha, OutlineBrightness;
    public readonly ShaderMaterial Material;
    static Shader? _coverage;
    /// <summary>Distance between lines in pixels.</summary>
    public readonly float LineHeight;
    public readonly float Ascent;
    /// <summary>
    /// The TXI spacingR as the original applies it to a TrueType face: spacingR x 128 px more on every character's
    /// unhinted advance, each advance then rounded to a whole pixel (fnt_default_hr's 0.003 is 0.384 px: partyframes:
    /// the party names and the large-font chat lines at 13 px). Used by <see cref="DrawLineTracked"/>.
    /// </summary>
    public readonly float Tracking;
    /// <summary>pixels of tracking per unit of TXI spacingR (fits 0.38-0.41 px for 0.003 at 13 px; other sizes not measured).</summary>
    public const float TrackingPerSpacingR = 128;
    readonly OpenWinter.Aurora.Formats.TrueTypeMetrics? _metrics;

    ClassicText(Font font, int size, OpenWinter.Content.TextureInfo style, OpenWinter.Aurora.Formats.TrueTypeMetrics? metrics = null)
    {
        _metrics = metrics;
        Tracking = style.FontSpacingR * TrackingPerSpacingR;
        Font = font;
        Size = size;
        OutlineAlpha = style.TtfOutlineOpacity;
        OutlineBrightness = style.TtfOutlineBrightness;
        _coverage ??= new Shader { Code = "shader_type canvas_item; uniform float contrast = 1.0; void fragment() { COLOR.a = clamp(COLOR.a * contrast, 0.0, 1.0); }" };
        Material = new ShaderMaterial { Shader = _coverage };
        Material.SetShaderParameter("contrast", style.TtfContrast);
        Ascent = font.GetAscent(size);
        // Individual controls can override the line pitch measured from the original.
        LineHeight = MathF.Round(size * 1.25f);
    }

    /// <summary>A TrueType face of the game at a pixel size (Godot's own font when the game has none).</summary>
    public static ClassicText For(ClassicAssets assets, string? resref = null, int size = 0)
    {
        resref ??= DefaultTextFont;
        if (size <= 0) size = resref == TextFont ? 13 : 11;
        if (resref == TextFont && !assets.Content.Resources.Contains(resref, ResType.Ttf)) resref = "fnt_default";
        var bySize = All.GetValue(assets, _ => new Dictionary<(string, int), ClassicText>());
        if (bySize.TryGetValue((resref, size), out var t)) return t;
        Font font = ThemeDB.FallbackFont;
        var res = assets.Content.Resources;
        OpenWinter.Aurora.Formats.TrueTypeMetrics? metrics = null;
        if (res.Read(resref, ResType.Ttf) is { Length: > 0 } ttf)
        {
            metrics = OpenWinter.Aurora.Formats.TrueTypeMetrics.Read(ttf);
            try
            {
                font = new FontFile { Data = ttf, Antialiasing = TextServer.FontAntialiasing.Gray, Hinting = TextServer.Hinting.Light, SubpixelPositioning = TextServer.SubpixelPositioning.Disabled, KeepRoundingRemainders = false };
            }
            catch (Exception) { font = ThemeDB.FallbackFont; metrics = null; }
        }
        var style = res.Read(resref, ResType.Txi) is { } txi ? OpenWinter.Aurora.Textures.TextureDecoder.ParseTxi(txi) : new OpenWinter.Content.TextureInfo();
        return bySize[(resref, size)] = new ClassicText(font, size, style, metrics);
    }

    /// <summary>Only glyphs use the font's coverage adjustment; panel art and portraits keep their own alpha.</summary>
    public Control CreateLayer(Control parent, Action<CanvasItem> draw)
    {
        var layer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Material = Material };
        layer.Draw += () => draw(layer);
        parent.AddChild(layer);
        return layer;
    }

    public float Measure(string s) => s.Length == 0 ? 0 : Font.GetStringSize(s, HorizontalAlignment.Left, -1, Size).X;

    /// <summary>One run of text in one colour.</summary>
    public readonly record struct Run(string Text, Color Color);

    /// <summary>A laid-out piece: a run on one line at an x offset.</summary>
    public readonly record struct Piece(float X, int Line, string Text, Color Color);

    // The colours the original gives the conversation's <StartAction>, <StartCheck> and <StartHighlight> spans.
    public static readonly Color Action = new(0.004f, 0.996f, 0.004f), Check = new(0.996f, 0.004f, 0.004f), Highlight = new(0.004f, 0.004f, 0.996f);

    /// <summary>Splits text into colour runs: NWN colour tokens and the server's [[action]] / [[check]] / [[highlight]] ... [[/]] spans.</summary>
    public static List<Run> Runs(string text, Color baseColor)
    {
        var runs = new List<Run>();
        var stack = new Stack<Color>();
        var color = baseColor;
        var sb = new System.Text.StringBuilder();
        void Flush()
        {
            if (sb.Length > 0) runs.Add(new Run(sb.ToString(), color));
            sb.Clear();
        }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<' && i + 5 < text.Length && (text[i + 1] is 'c' or 'C') && text[i + 5] == '>')
            {
                Flush();
                stack.Push(color);
                color = Color.Color8(Byte(text[i + 2]), Byte(text[i + 3]), Byte(text[i + 4]));
                i += 5;
                continue;
            }
            if (c == '<' && (string.CompareOrdinal(text, i, "</c>", 0, 4) == 0 || string.CompareOrdinal(text, i, "</C>", 0, 4) == 0))
            {
                Flush();
                color = stack.Count > 0 ? stack.Pop() : baseColor;
                i += 3;
                continue;
            }
            if (c == '[' && i + 1 < text.Length && text[i + 1] == '[')
            {
                int end = text.IndexOf("]]", i + 2, StringComparison.Ordinal);
                if (end > 0)
                {
                    string tag = text[(i + 2)..end];
                    Color? span = tag switch { "action" => Action, "check" => Check, "highlight" => Highlight, _ => null };
                    if (span != null || tag == "/")
                    {
                        Flush();
                        if (span is { } s) { stack.Push(color); color = s; }
                        else color = stack.Count > 0 ? stack.Pop() : baseColor;
                        i = end + 1;
                        continue;
                    }
                }
            }
            if (c != '\r') sb.Append(c);
        }
        Flush();
        return runs;
    }

    static byte Byte(char c)
    {
        if (c <= 0xFF) return (byte)c;
        var bytes = OpenWinter.Aurora.Text.Windows1252.GetBytes(new[] { c });
        return bytes.Length == 1 ? bytes[0] : (byte)0xFF;
    }

    /// <summary>Lays runs out in lines no wider than <paramref name="width"/>, breaking at spaces (explicit newlines kept). Returns the pieces and the line count.</summary>
    public (List<Piece> Pieces, int Lines) Layout(IEnumerable<Run> runs, float width)
    {
        var pieces = new List<Piece>();
        int line = 0;
        float x = 0;
        float space = Measure(" ");
        bool lineEmpty = true;
        foreach (var run in runs)
        {
            var paras = run.Text.Split('\n');
            for (int p = 0; p < paras.Length; p++)
            {
                if (p > 0) { line++; x = 0; lineEmpty = true; }
                var words = paras[p].Split(' ');
                for (int w = 0; w < words.Length; w++)
                {
                    var word = words[w];
                    // Spaces between words of one run (and at run edges) are kept as advances.
                    if (w > 0) { if (!lineEmpty) x += space; }
                    if (word.Length == 0) continue;
                    float ww = Measure(word);
                    if (!lineEmpty && x + ww > width && x > 0)
                    {
                        line++;
                        x = 0;
                    }
                    pieces.Add(new Piece(x, line, word, run.Color));
                    x += ww;
                    lineEmpty = false;
                }
            }
        }
        return (pieces, pieces.Count == 0 && !runs.Any(r => r.Text.Length > 0) ? 0 : line + 1);
    }

    /// <summary>Draws laid-out pieces with the top of the first line at <paramref name="at"/>; <paramref name="tint"/> multiplies their colours.</summary>
    public void Draw(CanvasItem ci, Vector2 at, IEnumerable<Piece> pieces, Color? tint = null, float? lineHeight = null)
    {
        var outline = new Color(OutlineBrightness, OutlineBrightness, OutlineBrightness, OutlineAlpha);
        foreach (var p in pieces)
        {
            var pos = new Vector2(MathF.Round(at.X + p.X), MathF.Round(at.Y + p.Line * (lineHeight ?? LineHeight) + Ascent));
            var color = tint is { } t ? p.Color * t : p.Color;
            if (OutlineAlpha > 0) ci.DrawStringOutline(Font, pos, p.Text, HorizontalAlignment.Left, -1, Size, 2, outline with { A = outline.A * color.A });
            ci.DrawString(Font, pos, p.Text, HorizontalAlignment.Left, -1, Size, color);
        }
    }

    /// <summary>A character's advance as the original lays the face out: unhinted advance plus <see cref="Tracking"/>, rounded.</summary>
    float TrackedAdvance(string ch) => _metrics is { } m
        ? MathF.Round(m.Advance(char.ConvertToUtf32(ch, 0), Size) + Tracking, MidpointRounding.AwayFromZero)
        : Font.GetStringSize(ch, HorizontalAlignment.Left, -1, Size).X;

    static IEnumerable<string> Characters(string s)
    {
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext()) yield return (string)e.Current;
    }

    /// <summary>The width of a plain line laid out as <see cref="DrawLineTracked"/> draws it (the sum of the rounded advances).</summary>
    public float MeasureTracked(string s) => Characters(s).Sum(TrackedAdvance);

    /// <summary>
    /// Draws one line of plain text character by character at the original's rounded, tracked advances (party member
    /// names: "Hench A" 54 px with the large font, as the original). Faces without TXI spacing draw as <see cref="DrawLine"/>.
    /// </summary>
    public void DrawLineTracked(CanvasItem ci, Vector2 at, string text, Color color)
    {
        if (Tracking == 0 || _metrics == null) { DrawLine(ci, at, text, color); return; }
        var outline = new Color(OutlineBrightness, OutlineBrightness, OutlineBrightness, OutlineAlpha * color.A);
        float x = MathF.Round(at.X), y = MathF.Round(at.Y + Ascent);
        foreach (var ch in Characters(text))
        {
            var pos = new Vector2(x, y);
            if (OutlineAlpha > 0) ci.DrawCharOutline(Font, pos, ch, Size, 2, outline);
            ci.DrawChar(Font, pos, ch, Size, color);
            x += TrackedAdvance(ch);
        }
    }

    /// <summary>Draws one line of plain text (colour tokens stripped) with its top-left at <paramref name="at"/>.</summary>
    public void DrawLine(CanvasItem ci, Vector2 at, string text, Color color)
    {
        var (pieces, _) = Layout(Runs(text, color), float.MaxValue);
        Draw(ci, at, pieces);
    }
}
