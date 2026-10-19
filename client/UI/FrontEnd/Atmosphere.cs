using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// The front end's layered backdrop, drawn over a 3D scene or on its own: drifting smoke lit by a warm
/// glow (when there is nothing behind), a vignette, a darker band on the left for the menu, and embers
/// rising from the bottom. All procedural (no image assets).
/// </summary>
public partial class Atmosphere : Control
{
    /// <summary>1 draws the smoke background, 0 leaves what is behind visible (a 3D scene) and only darkens it.</summary>
    public float Smoke = 1;
    /// <summary>How dark the left side gets (0 - 1).</summary>
    public float LeftShade = 0.78f;
    public float Vignette = 0.8f;
    public float Embers = 1;

    ShaderMaterial _shade = null!, _embers = null!;

    const string ShadeShader = """
        shader_type canvas_item;
        uniform float smoke_amount = 1.0;
        uniform float vignette = 0.8;
        uniform float left_shade = 0.78;
        uniform vec4 tint : source_color = vec4(0.83, 0.62, 0.32, 1.0);
        float hash(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }
        float noise(vec2 p) {
            vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
        }
        float fbm(vec2 p) {
            float v = 0.0; float a = 0.5;
            for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
            return v;
        }
        void fragment() {
            vec2 uv = UV;
            float aspect = (1.0 / SCREEN_PIXEL_SIZE.x) / (1.0 / SCREEN_PIXEL_SIZE.y);
            vec2 p = vec2(uv.x * aspect, uv.y);
            float t = TIME;
            vec3 col = vec3(0.0);
            if (smoke_amount > 0.0) {
                float n = fbm(p * 1.6 + vec2(t * 0.012, -t * 0.02));
                float n2 = fbm(p * 3.2 - vec2(t * 0.018, t * 0.006) + n * 1.5);
                col = mix(vec3(0.016, 0.018, 0.026), vec3(0.11, 0.10, 0.105), smoothstep(0.25, 0.85, n2));
                float glow = exp(-pow(distance(uv * vec2(aspect, 1.0), vec2(0.68 * aspect, 0.55)) * 1.15, 2.0));
                col += tint.rgb * glow * (0.10 + 0.30 * smoothstep(0.3, 0.8, n2));
                // A cold rim of light along the top.
                col += vec3(0.25, 0.32, 0.45) * 0.035 * smoothstep(0.35, 0.0, uv.y) * n;
            }
            float v = smoothstep(1.25, 0.3, length((uv - vec2(0.55, 0.5)) * vec2(1.15, 1.0)));
            float shade = mix(1.0 - left_shade, 1.0, smoothstep(0.02, 0.6, uv.x));
            float bottom = mix(0.55, 1.0, smoothstep(1.0, 0.7, uv.y));
            float keep = mix(1.0 - vignette, 1.0, v) * shade * bottom;
            COLOR = vec4(col * keep, mix(1.0 - keep, 1.0, smoke_amount));
        }
        """;

    const string EmberShader = """
        shader_type canvas_item;
        render_mode blend_add;
        uniform float amount = 1.0;
        float hash(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }
        void fragment() {
            vec2 uv = UV;
            float aspect = (1.0 / SCREEN_PIXEL_SIZE.x) / (1.0 / SCREEN_PIXEL_SIZE.y);
            vec2 p = vec2(uv.x * aspect, uv.y);
            float t = TIME;
            float e = 0.0;
            float halo = 0.0;
            for (int l = 0; l < 3; l++) {
                float fl = float(l);
                float scale = 6.0 + fl * 5.0;
                float speed = 0.035 + fl * 0.02;
                vec2 q = p * scale;
                q.y += t * speed * scale;
                q.x += sin(t * 0.35 + q.y * 0.21 + fl * 2.0) * 0.45;
                vec2 cell = floor(q);
                vec2 f = fract(q) - 0.5;
                float h = hash(cell + fl * 17.0);
                if (h > 0.88) {
                    vec2 off = vec2(hash(cell + 3.1) - 0.5, hash(cell + 7.7) - 0.5) * 0.55;
                    float d = length(f - off);
                    float flick = 0.55 + 0.45 * sin(t * (1.5 + h * 4.0) + h * 40.0);
                    float size = 0.035 + 0.03 * hash(cell + 1.3);
                    float w = (1.0 - fl * 0.28) * flick;
                    e += smoothstep(size, 0.0, d) * w;
                    halo += smoothstep(size * 5.0, 0.0, d) * 0.18 * w;
                }
            }
            float fade = smoothstep(0.05, 0.75, uv.y) * smoothstep(0.0, 0.12, uv.x) * smoothstep(1.0, 0.88, uv.x);
            vec3 c = vec3(1.0, 0.58, 0.22) * (e * 1.5 + halo) * fade * amount;
            COLOR = vec4(c, 1.0);
        }
        """;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _shade = new ShaderMaterial { Shader = new Shader { Code = ShadeShader } };
        _embers = new ShaderMaterial { Shader = new Shader { Code = EmberShader } };
        AddChild(new ColorRect { Material = _shade, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new ColorRect { Material = _embers, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        Refresh();
    }

    /// <summary>Pushes the public fields to the shaders.</summary>
    public void Refresh()
    {
        if (_shade == null) return;
        _shade.SetShaderParameter("smoke_amount", Smoke);
        _shade.SetShaderParameter("left_shade", LeftShade);
        _shade.SetShaderParameter("vignette", Vignette);
        _shade.SetShaderParameter("tint", Palette.Accent);
        _embers.SetShaderParameter("amount", Embers);
    }
}

/// <summary>Large display text with a soft brass glow that breathes slowly (the title on the main menu and loading screens).</summary>
public partial class GlowTitle : Control
{
    public string Text = "";
    public int FontSize = 96;
    public Font Font = Fonts.DisplayBold;
    public Color Color = new("f4dca6");
    public Color GlowColor = Palette.Accent;
    public float Spacing = 0.12f;
    public HorizontalAlignment Align = HorizontalAlignment.Left;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Measure(), FontSize * 1.25f);
    }

    float Measure()
    {
        float w = 0;
        foreach (var ch in Text) w += Font.GetStringSize(ch.ToString(), HorizontalAlignment.Left, -1, FontSize).X + FontSize * Spacing;
        return w - FontSize * Spacing;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        float breathe = 0.75f + 0.25f * MathF.Sin((float)Time.GetTicksMsec() / 1000f * 0.9f);
        float width = Measure();
        float x0 = Align switch { HorizontalAlignment.Center => (Size.X - width) / 2, HorizontalAlignment.Right => Size.X - width, _ => 0 };
        float baseline = (Size.Y + Font.GetAscent(FontSize) - Font.GetDescent(FontSize)) / 2;
        // Glow: wide faint outlines first, then tighter brighter ones.
        foreach (var (outline, alpha) in new[] { (44, 0.035f), (30, 0.05f), (18, 0.07f), (9, 0.09f) })
            DrawLetters(x0, baseline, (pos, s) => DrawStringOutline(Font, pos, s, HorizontalAlignment.Left, -1, FontSize, outline, GlowColor with { A = alpha * breathe }));
        DrawLetters(x0, baseline, (pos, s) => DrawStringOutline(Font, pos, s, HorizontalAlignment.Left, -1, FontSize, 3, new Color(0.08f, 0.05f, 0.02f, 0.6f)));
        DrawLetters(x0, baseline, (pos, s) => DrawString(Font, pos, s, HorizontalAlignment.Left, -1, FontSize, Color));
    }

    void DrawLetters(float x, float baseline, Action<Vector2, string> draw)
    {
        foreach (var ch in Text)
        {
            var s = ch.ToString();
            draw(new Vector2(x, baseline), s);
            x += Font.GetStringSize(s, HorizontalAlignment.Left, -1, FontSize).X + FontSize * Spacing;
        }
    }
}
