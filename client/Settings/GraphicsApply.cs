using Godot;

namespace OpenWinter.Client.Preferences;

/// <summary>
/// Applies <see cref="GraphicsSettings"/> to the window, the root viewport and every area's environment
/// and sun (areas register themselves with <see cref="RegisterEnvironment"/> when they are built).
/// </summary>
public static class GraphicsApply
{
    public static readonly Vector2I DefaultWindow = new(1600, 900);

    static readonly List<(WeakReference<Godot.Environment> Env, WeakReference<DirectionalLight3D>? Sun)> Environments = new();
    static GraphicsSettings _current = new();

    public static void Apply(Window root, GraphicsSettings g, bool window)
    {
        _current = g;
        if (window) ApplyWindow(g);
        DisplayServer.WindowSetVsyncMode(g.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = g.FrameCap;

        root.Scaling3DMode = g.RenderScale >= 0.999f && g.RenderScale <= 1.001f ? Viewport.Scaling3DModeEnum.Bilinear : g.Upscaler switch
        {
            Upscaler.Fsr1 when g.RenderScale < 1 => Viewport.Scaling3DModeEnum.Fsr,
            Upscaler.Fsr2 => Viewport.Scaling3DModeEnum.Fsr2,
            _ => Viewport.Scaling3DModeEnum.Bilinear,
        };
        root.Scaling3DScale = g.RenderScale;
        root.FsrSharpness = g.Sharpness;
        root.Msaa3D = g.Msaa switch { Msaa.X2 => Viewport.Msaa.Msaa2X, Msaa.X4 => Viewport.Msaa.Msaa4X, Msaa.X8 => Viewport.Msaa.Msaa8X, _ => Viewport.Msaa.Disabled };
        root.ScreenSpaceAA = g.PostAntiAlias switch
        {
            PostAntiAlias.Fxaa => Viewport.ScreenSpaceAAEnum.Fxaa,
            PostAntiAlias.Smaa => Viewport.ScreenSpaceAAEnum.Smaa,
            _ => Viewport.ScreenSpaceAAEnum.Disabled,
        };
        // FSR 2 brings its own temporal antialiasing.
        root.UseTaa = g.PostAntiAlias == PostAntiAlias.Taa && root.Scaling3DMode != Viewport.Scaling3DModeEnum.Fsr2;
        root.AnisotropicFilteringLevel = g.TextureFiltering switch
        {
            TextureFiltering.Bilinear => Viewport.AnisotropicFiltering.Disabled,
            TextureFiltering.Aniso2 => Viewport.AnisotropicFiltering.Anisotropy2X,
            TextureFiltering.Aniso4 => Viewport.AnisotropicFiltering.Anisotropy4X,
            TextureFiltering.Aniso8 => Viewport.AnisotropicFiltering.Anisotropy8X,
            _ => Viewport.AnisotropicFiltering.Anisotropy16X,
        };

        var (atlas, soft, positional) = g.Shadows switch
        {
            ShadowQuality.Low => (1024, RenderingServer.ShadowQuality.Hard, 1024),
            ShadowQuality.Medium => (2048, RenderingServer.ShadowQuality.SoftLow, 2048),
            ShadowQuality.High => (4096, RenderingServer.ShadowQuality.SoftMedium, 4096),
            ShadowQuality.Ultra => (8192, RenderingServer.ShadowQuality.SoftHigh, 8192),
            _ => (1024, RenderingServer.ShadowQuality.Hard, 1024),
        };
        RenderingServer.DirectionalShadowAtlasSetSize(atlas, true);
        RenderingServer.DirectionalSoftShadowFilterSetQuality(soft);
        RenderingServer.PositionalSoftShadowFilterSetQuality(soft);
        root.PositionalShadowAtlasSize = positional;

        for (int i = Environments.Count - 1; i >= 0; i--)
        {
            if (!Environments[i].Env.TryGetTarget(out var env)) { Environments.RemoveAt(i); continue; }
            DirectionalLight3D? sun = null;
            Environments[i].Sun?.TryGetTarget(out sun);
            ApplyEnvironment(env, sun, g);
        }
    }

    static void ApplyWindow(GraphicsSettings g)
    {
        var screen = DisplayServer.WindowGetCurrentScreen();
        switch (g.WindowMode)
        {
            case WindowMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
            case WindowMode.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            default:
            {
                if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                var usable = DisplayServer.ScreenGetUsableRect(screen);
                var size = g.Width > 0 && g.Height > 0 ? new Vector2I(g.Width, g.Height) : DefaultWindow;
                size = new Vector2I(Math.Min(size.X, usable.Size.X), Math.Min(size.Y, usable.Size.Y));
                if (DisplayServer.WindowGetSize() != size)
                {
                    DisplayServer.WindowSetSize(size);
                    DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
                }
                break;
            }
        }
    }

    /// <summary>Common window sizes that fit the current screen, largest first.</summary>
    public static List<Vector2I> Resolutions()
    {
        var screen = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());
        var all = new[]
        {
            new Vector2I(1280, 720), new Vector2I(1366, 768), new Vector2I(1600, 900), new Vector2I(1920, 1080), new Vector2I(2560, 1440),
            new Vector2I(3200, 1800), new Vector2I(3840, 2160), new Vector2I(1280, 800), new Vector2I(1440, 900), new Vector2I(1680, 1050),
            new Vector2I(1920, 1200), new Vector2I(2560, 1600), new Vector2I(2560, 1080), new Vector2I(3440, 1440),
        };
        var list = all.Where(r => r.X <= screen.X && r.Y <= screen.Y).Distinct().OrderByDescending(r => r.X * r.Y).ToList();
        if (list.Count == 0) list.Add(screen);
        return list;
    }

    /// <summary>
    /// Called for every area's environment and sun when it is built: applies the current settings now
    /// and again whenever they change.
    /// </summary>
    public static void RegisterEnvironment(Godot.Environment env, DirectionalLight3D? sun)
    {
        Environments.Add((new WeakReference<Godot.Environment>(env), sun == null ? null : new WeakReference<DirectionalLight3D>(sun)));
        ApplyEnvironment(env, sun, _current);
    }

    static void ApplyEnvironment(Godot.Environment env, DirectionalLight3D? sun, GraphicsSettings g)
    {
        env.SsaoEnabled = g.Ssao;
        env.GlowEnabled = g.Glow;
        if (g.Glow)
        {
            env.GlowIntensity = 0.55f;
            env.GlowStrength = 0.9f;
            env.GlowBloom = 0.02f;
            env.GlowHdrThreshold = 0.95f;
            env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight;
        }
        env.TonemapExposure = g.Brightness;
        // Always: the original writes its picture with a plain power curve (its gamma option, 2.2 by default)
        // where Godot writes sRGB, whose straight segment near black makes dark scenes far darker (a graveyard
        // at night came out at 0.6 of the original's brightness; parity/visual, scene lightcalnight).
        env.AdjustmentEnabled = true;
        env.AdjustmentColorCorrection = GammaCurve(g.Gamma);
        // The area decides whether its sun or moon casts shadows (SunShadows / MoonShadows); the setting can only turn them off.
        if (sun != null && GodotObject.IsInstanceValid(sun)) sun.ShadowEnabled = g.Shadows != ShadowQuality.Off && (!sun.HasMeta(AreaShadowsMeta) || sun.GetMeta(AreaShadowsMeta).AsBool());
    }

    /// <summary>Metadata on an area's sun light: whether the area wants its shadows.</summary>
    public const string AreaShadowsMeta = "area_shadows";

    /// <summary>Whether shadows are on in the graphics options.</summary>
    public static bool ShadowsOn => _current.Shadows != ShadowQuality.Off;

    static (float Gamma, Texture2D Tex)? _gammaCache;

    /// <summary>
    /// The colour-correction curve from Godot's sRGB output to the original's: decode sRGB, then encode with the
    /// display gamma (2.2, the original's default; a higher setting brightens the mid-tones as its slider does).
    /// </summary>
    static Texture2D GammaCurve(float gamma)
    {
        if (_gammaCache is { } c && Math.Abs(c.Gamma - gamma) < 0.001f) return c.Tex;
        const int n = 1024;
        var image = Image.CreateEmpty(n, 1, false, Image.Format.Rgbf);
        for (int i = 0; i < n; i++)
        {
            float x = i / (float)(n - 1);
            float linear = x <= 0.04045f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f);
            float y = MathF.Pow(linear, 1f / gamma);
            image.SetPixel(i, 0, new Color(y, y, y));
        }
        var tex = ImageTexture.CreateFromImage(image);
        _gammaCache = (gamma, tex);
        return tex;
    }
}
