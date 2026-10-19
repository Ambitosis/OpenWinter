using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>Style boxes for the kit's surfaces, and the frosted-glass material panels draw with.</summary>
public static class Styles
{
    /// <summary>Panels blur what's behind them (Options → Interface can turn it off).</summary>
    public static bool GlassEnabled = true;

    public static StyleBoxFlat Box(Color bg, int radius = Palette.Radius, Color? border = null, int borderWidth = 1, int padX = 12, int padY = -1,
        int shadow = 0, float shadowAlpha = 0.45f)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg, BorderColor = border ?? Colors.Transparent, AntiAliasing = true,
            ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY < 0 ? padX : padY, ContentMarginBottom = padY < 0 ? padX : padY,
        };
        if (border != null) s.SetBorderWidthAll(borderWidth);
        s.SetCornerRadiusAll(radius);
        if (shadow > 0)
        {
            s.ShadowColor = new Color(0, 0, 0, shadowAlpha);
            s.ShadowSize = shadow;
            s.ShadowOffset = new Vector2(0, shadow / 3f);
        }
        return s;
    }

    /// <summary>A window or floating panel: dark glass, hairline border, soft drop shadow.</summary>
    public static StyleBoxFlat Window(int pad = 0) =>
        Box(GlassEnabled ? Palette.Bg1 : Palette.Bg1 with { A = 0.96f }, Palette.RadiusLarge, Palette.LineStrong, 1, pad, pad, shadow: 22, shadowAlpha: 0.5f);

    /// <summary>A HUD element: lighter glass, no heavy shadow.</summary>
    public static StyleBoxFlat Hud(int pad = 8, int radius = Palette.Radius) =>
        Box(GlassEnabled ? Palette.Bg1 : Palette.Bg1 with { A = 0.86f }, radius, Palette.Line, 1, pad, pad, shadow: 8, shadowAlpha: 0.3f);

    /// <summary>A raised card inside a window.</summary>
    public static StyleBoxFlat Card(int pad = 12) => Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, pad);

    /// <summary>A sunken well (lists, text areas).</summary>
    public static StyleBoxFlat Inset(int pad = 10) => Box(new Color(0, 0, 0, 0.28f), Palette.RadiusSmall, Palette.Line, 1, pad);

    /// <summary>Item / ability slot states.</summary>
    public static StyleBoxFlat Slot(bool hover = false, bool active = false)
    {
        var s = Box(hover ? Palette.Bg3 : new Color(0.04f, 0.045f, 0.06f, 0.85f), Palette.RadiusSmall,
            active ? Palette.Accent : hover ? Palette.AccentDim : Palette.LineStrong, active ? 2 : 1, 3);
        return s;
    }

    public static StyleBoxFlat Tooltip() => Box(new Color("0d0f14f5"), 8, Palette.AccentDim with { A = 0.6f }, 1, 12, 10, shadow: 14);

    static ShaderMaterial? _glass;

    /// <summary>
    /// Blurs the screen behind whatever draws with it, then lays its own colour over that at
    /// <c>opacity</c>. Applied to panels so their style box (fill, border, rounded corners) becomes frosted glass.
    /// </summary>
    public static ShaderMaterial? Glass
    {
        get
        {
            if (!GlassEnabled) return null;
            if (_glass != null) return _glass;
            var shader = new Shader
            {
                Code = """
                shader_type canvas_item;
                uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
                uniform float blur = 3.0;
                uniform float opacity = 0.84;
                void fragment() {
                    vec2 o = SCREEN_PIXEL_SIZE * 6.0;
                    vec3 b = textureLod(screen_tex, SCREEN_UV, blur).rgb * 0.28;
                    b += textureLod(screen_tex, SCREEN_UV + vec2(o.x, o.y), blur).rgb * 0.18;
                    b += textureLod(screen_tex, SCREEN_UV + vec2(-o.x, o.y), blur).rgb * 0.18;
                    b += textureLod(screen_tex, SCREEN_UV + vec2(o.x, -o.y), blur).rgb * 0.18;
                    b += textureLod(screen_tex, SCREEN_UV + vec2(-o.x, -o.y), blur).rgb * 0.18;
                    // Darken and desaturate what shows through so text on top stays readable.
                    float l = dot(b, vec3(0.299, 0.587, 0.114));
                    b = mix(vec3(l), b, 0.6) * 0.55;
                    COLOR = vec4(mix(b, COLOR.rgb, opacity), COLOR.a);
                }
                """,
            };
            _glass = new ShaderMaterial { Shader = shader };
            return _glass;
        }
    }

    /// <summary>True for the frosted-glass material (or a copy of it with another opacity).</summary>
    public static bool IsGlass(Material? m) => m is ShaderMaterial sm && _glass != null && sm.Shader == _glass.Shader;

    /// <summary>Makes a control's own background (its style box) frosted glass; children are unaffected.</summary>
    public static void ApplyGlass(CanvasItem item, float opacity = 0.84f)
    {
        if (Glass is not { } baseMat) return;
        if (Math.Abs(opacity - 0.84f) < 0.001f) { item.Material = baseMat; return; }
        var m = (ShaderMaterial)baseMat.Duplicate();
        m.SetShaderParameter("opacity", opacity);
        item.Material = m;
    }
}
