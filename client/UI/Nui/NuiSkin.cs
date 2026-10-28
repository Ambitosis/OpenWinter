using OpenWinter.Aurora.Resources;
using OpenWinter.Client.UI.Nui.Model;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// NUI look: the game's own NUI skin textures (nui_* in the base data, read at run
/// time) as nine-slice boxes, with flat UiTheme fallbacks when a texture is
/// missing; UI textures for images and buttons, and the font.
/// </summary>
public sealed class NuiSkin
{
    public readonly ClientContent? Content;
    readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, StyleBox> _boxes = new();

    public Font Font { get; }
    public int FontSize = NuiStyle.FontSize;

    public static readonly Color Text = UiTheme.Text;
    public static readonly Color TextDisabled = new(0.55f, 0.53f, 0.5f);
    public static readonly Color Placeholder = new(0.55f, 0.52f, 0.46f);
    public static readonly Color Title = new(0.95f, 0.9f, 0.78f);
    public static readonly Color Bronze = new(0.62f, 0.45f, 0.25f);
    public static readonly Color FieldBg = new(0.03f, 0.03f, 0.03f, 0.95f);
    public static readonly Color Track = new(0.16f, 0.13f, 0.1f);
    public static readonly Color Accent = new(0.85f, 0.68f, 0.38f);
    public static readonly Color Highlight = new(0.2f, 0.32f, 0.47f);
    /// <summary>Window background opacity (the skin's black is drawn translucent, as in game).</summary>
    public const float WindowAlpha = 0.94f;

    public NuiSkin(ClientContent? content)
    {
        Content = content;
        Font = LoadFont() ?? ThemeDB.FallbackFont;
    }

    Font? LoadFont()
    {
        // The face the original draws NUI text in (its log names it; visual scene nuifont); Godot's when it's missing.
        if (Content?.Resources.Read("fnt_maintext", ResType.Ttf) is not { Length: > 0 } ttf) return null;
        try
        {
            var f = new FontFile { Data = ttf, Antialiasing = TextServer.FontAntialiasing.Gray, Hinting = TextServer.Hinting.Light };
            return f;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A UI texture by resref (tga/dds, then png/jpg), top-down, no mipmaps; null when missing.</summary>
    public Texture2D? Texture(string resref)
    {
        if (string.IsNullOrEmpty(resref) || Content == null) return null;
        if (_textures.TryGetValue(resref, out var cached)) return cached;
        return _textures[resref] = LoadTexture(Content, resref);
    }

    /// <summary>Loads an interface texture by resref (tga/dds, then png/jpg), top-down, no mipmaps; null when missing. Not cached.</summary>
    public static Texture2D? LoadTexture(ClientContent content, string resref)
    {
        Texture2D? tex = null;
        try
        {
            if (content.Textures.Source.Get(resref) is { } data && Rendering.TextureFactory.ToImage(data) is { } image)
            {
                if (image.IsCompressed()) image.Decompress();
                image.ClearMipmaps();
                if (data.FlipV) image.FlipY();
                tex = ImageTexture.CreateFromImage(image);
            }
            else if (content.Resources.Read(resref, ResType.Png) is { } png)
            {
                var img = new Image();
                if (img.LoadPngFromBuffer(png) == Error.Ok) tex = ImageTexture.CreateFromImage(img);
            }
            else if (content.Resources.Read(resref, ResType.Jpg) is { } jpg)
            {
                var img = new Image();
                if (img.LoadJpgFromBuffer(jpg) == Error.Ok) tex = ImageTexture.CreateFromImage(img);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ui] texture {resref}: {e.Message}");
        }
        return tex;
    }

    /// <summary>Nine-slice box from a skin texture, or a flat fallback.</summary>
    public StyleBox Box(string resref, int slice, Color fallbackBg, Color? fallbackBorder = null, float alpha = 1, bool center = true)
    {
        string key = $"{resref}|{slice}|{alpha}|{center}";
        if (_boxes.TryGetValue(key, out var box)) return box;
        if (Texture(resref) is { } tex)
        {
            var s = new StyleBoxTexture { Texture = tex, DrawCenter = center, ModulateColor = new Color(1, 1, 1, alpha) };
            s.SetTextureMarginAll(slice);
            s.SetContentMarginAll(slice);
            box = s;
        }
        else
        {
            var s = new StyleBoxFlat { BgColor = new Color(fallbackBg, fallbackBg.A * alpha), BorderColor = fallbackBorder ?? UiTheme.Border, DrawCenter = center };
            s.SetBorderWidthAll(1);
            s.SetCornerRadiusAll(2);
            s.SetContentMarginAll(slice);
            box = s;
        }
        _boxes[key] = box;
        return box;
    }

    public StyleBox WindowBox(bool tall, bool border, bool background)
    {
        if (!border)
        {
            var key = "noborder";
            if (!_boxes.TryGetValue(key, out var b))
                _boxes[key] = b = new StyleBoxFlat { BgColor = new Color(0, 0, 0, WindowAlpha) };
            return b;
        }
        return Box(tall ? "nui_windowv" : "nui_windowh", 3, new Color(0, 0, 0, 1), alpha: background ? WindowAlpha : 1, center: background);
    }

    public StyleBox Header => Box("nui_header", 3, new Color(0.1f, 0.2f, 0.32f));
    public StyleBox Group => Box("nui_windowh", 3, new Color(0, 0, 0, 0.25f), center: false);
    public StyleBox Field => Box("nui_button_d", 3, FieldBg);

    /// <summary>Button faces: normal, hover, active (pressed or selected), active+hover, disabled.</summary>
    public StyleBox Button(bool hover, bool active, bool disabled) =>
        disabled ? Box("nui_button_d", 3, new Color(0.1f, 0.1f, 0.1f))
        : active ? Box(hover ? "nui_button_ah" : "nui_button_a", 3, Highlight)
        : Box(hover ? "nui_button_h" : "nui_button_n", 3, hover ? new Color(0.16f, 0.13f, 0.1f) : new Color(0.06f, 0.05f, 0.04f));

    /// <summary>Draws text in a rectangle with NUI alignment (halign: 0 centre, 1 left, 2 right; valign: 0 middle, 1 top, 2 bottom).</summary>
    public void DrawText(CanvasItem ci, Rect2 rect, string text, int halign, int valign, Color color, int? size = null)
    {
        if (string.IsNullOrEmpty(text) || rect.Size.X <= 0) return;
        int fs = size ?? FontSize;
        float height = Font.GetHeight(fs);
        float y = valign switch
        {
            1 => rect.Position.Y,
            2 => rect.End.Y - height,
            _ => rect.Position.Y + (rect.Size.Y - height) / 2,
        };
        var align = halign switch { 1 => HorizontalAlignment.Left, 2 => HorizontalAlignment.Right, _ => HorizontalAlignment.Center };
        // Text wider than the box starts at the left edge and is clipped, as in game.
        float textWidth = Font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X;
        if (textWidth > rect.Size.X) align = HorizontalAlignment.Left;
        ci.DrawString(Font, new Vector2(rect.Position.X, y + Font.GetAscent(fs)), text, align, rect.Size.X, fs, color);
    }

    public static Color ToColor(NuiColor c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
}
