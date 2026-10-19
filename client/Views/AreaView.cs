using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Rendering;
using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>Draws an area's tiles, lighting and atmosphere from a LoadArea message.</summary>
public partial class AreaView : Node3D
{
    public LoadAreaMessage Info = null!;
    /// <summary>Walkable tile faces in area (Z-up) space; walls must not intercept ground picks.</summary>
    public readonly List<Vector3> WalkTriangles = new();

    public static AreaView Build(LoadAreaMessage msg, ClientContent content, out Godot.Environment env, out DirectionalLight3D sun)
    {
        NextAreaShapes();
        var view = new AreaView { Name = "Area_" + msg.ResRef, Info = msg };
        var set = Ini.Parse(content.Resources.Read(msg.Tileset, ResType.Set) ?? Array.Empty<byte>());
        float step = set.GetFloat("GENERAL", "Transition", 5f);
        var tiles = new Node3D { Name = "Tiles" };
        view.AddChild(tiles);
        view.BuildTiles(content, set, step, tiles);
        view.InitExploration();

        // The original lights in linear space with no tone mapping: the area's ambient colour everywhere plus its
        // sun or moon diffuse colour times N.L, measured with parity/visual (scene lightcal). Its output curve is a
        // plain 2.2 power where ours is sRGB's, which makes strong light come out brighter here: the sun at 0.8
        // matches the original's ground within a few percent from moonlight to full sun.
        Color ambient = Rendering.Convert.Bgr(msg.IsNight ? msg.MoonAmbient : msg.SunAmbient);
        Color diffuse = Rendering.Convert.Bgr(msg.IsNight ? msg.MoonDiffuse : msg.SunDiffuse);
        Color fog = Rendering.Convert.Bgr(msg.IsNight ? msg.MoonFogColor : msg.SunFogColor);
        env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = fog,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = ambient,
            AmbientLightEnergy = AmbientEnergy,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            SsaoEnabled = true,
        };
        ApplyFog(env, msg.IsNight ? msg.MoonFogAmount : msg.SunFogAmount, fog, msg.FogClipDistance);
        sun = new DirectionalLight3D
        {
            LightColor = diffuse,
            LightEnergy = SunEnergy,
            ShadowOpacity = msg.ShadowOpacity / 100f,
            DirectionalShadowMaxDistance = 80f,
            RotationDegrees = new Vector3(-55, 35, 0),
        };
        sun.SetMeta(Preferences.GraphicsApply.AreaShadowsMeta, msg.IsNight ? msg.MoonShadows : msg.SunShadows);
        // Options, Graphics (SSAO, glow, brightness, gamma, shadows) now and whenever they change.
        Preferences.GraphicsApply.RegisterEnvironment(env, sun);
        return view;
    }

    /// <summary>Energy of the sun and moon light (see <see cref="Build"/>).</summary>
    const float SunEnergy = 0.72f, AmbientEnergy = 0.85f;

    Godot.Environment? _env;
    DirectionalLight3D? _sun;
    ClientContent? _content;
    TimeMessage _time = new();
    double _minuteClock;
    Node3D? _sky;
    string _skyModel = "";

    /// <summary>
    /// Where the distance fog counts from when no player is followed (the main menu's circling camera looks at the
    /// middle of the area from tens of metres up, which would otherwise put the whole area in the fog).
    /// </summary>
    public Vector3? FogCentre;

    public void Bind(Godot.Environment env, DirectionalLight3D sun, ClientContent content)
    {
        _env = env;
        _sun = sun;
        _content = content;
    }

    /// <summary>Game time of day in minutes since midnight (advances between server updates).</summary>
    public double ClockMinutes => _minuteClock;
    public bool IsDaytime => Daylight() >= 0.5f;

    public void SetTime(TimeMessage t)
    {
        _time = t;
        // An hour has MinutesPerHour minutes of 60 real seconds; the clock here counts 60 per hour.
        _minuteClock = t.Hour * 60 + (t.Minute * 60 + t.Second) / (double)Math.Max(1, t.MinutesPerHour);
    }

    /// <summary>0 = full night, 1 = full day; dawn and dusk hours blend.</summary>
    float Daylight()
    {
        if (!Info.DayNightCycle) return Info.IsNight ? 0 : 1;
        double h = _minuteClock / 60.0 % 24;
        double dawn = _time.DawnHour, dusk = _time.DuskHour;
        if (h >= dawn + 1 && h < dusk) return 1;
        if (h >= dawn && h < dawn + 1) return (float)(h - dawn);
        if (h >= dusk && h < dusk + 1) return (float)(1 - (h - dusk));
        return 0;
    }

    public override void _Process(double delta)
    {
        if (_env == null || _sun == null) return;
        // Advance the clock locally between server updates: a game minute lasts MinutesPerHour real seconds.
        _minuteClock = (_minuteClock + delta / Math.Max(1, _time.MinutesPerHour)) % (24 * 60);
        float f = ScriptDaylight(Daylight(), delta);
        Color Mix(uint night, uint day) => Rendering.Convert.Bgr(night).Lerp(Rendering.Convert.Bgr(day), f);
        var fog = Mix(Info.MoonFogColor, Info.SunFogColor);
        _env.AmbientLightColor = Mix(Info.MoonAmbient, Info.SunAmbient);
        _env.BackgroundColor = fog;
        var camera = GetViewport()?.GetCamera3D();
        float cameraDistance = camera is Gameplay.OrbitCamera { Target: { } t } cam && IsInstanceValid(t)
            ? cam.GlobalPosition.DistanceTo(t.GlobalPosition + Vector3.Up * cam.HeightOffset)
            : camera != null && FogCentre is { } centre ? camera.GlobalPosition.DistanceTo(centre) : 0f;
        ApplyFog(_env, f > 0.5f ? Info.SunFogAmount : Info.MoonFogAmount, fog, Info.FogClipDistance, cameraDistance);
        _sun.LightColor = Mix(Info.MoonDiffuse, Info.SunDiffuse);
        bool shadows = f > 0.5f ? Info.SunShadows : Info.MoonShadows;
        if (!_sun.HasMeta(Preferences.GraphicsApply.AreaShadowsMeta) || _sun.GetMeta(Preferences.GraphicsApply.AreaShadowsMeta).AsBool() != shadows)
        {
            _sun.SetMeta(Preferences.GraphicsApply.AreaShadowsMeta, shadows);
            _sun.ShadowEnabled = shadows && Preferences.GraphicsApply.ShadowsOn;
        }
        // The sun arcs east to west through the day; the moon light stays high.
        double h = _minuteClock / 60.0 % 24;
        float arc = f > 0 ? (float)((h - _time.DawnHour) / Math.Max(1, _time.DuskHour + 1 - _time.DawnHour)) : 0.5f;
        if (!ScriptLightDirection(f, delta)) _sun.RotationDegrees = new Vector3(-25 - 50 * MathF.Sin(arc * MathF.PI), 90 - 180 * arc, 0);
        UpdateSky(f);
    }

    /// <summary>
    /// The area's fog as the original draws it, measured with parity/visual. Every area fades into its fog colour
    /// with distance from the player whatever its fog amount: nothing to about 45% of FogClipDist, then evenly
    /// thicker up to FogClipDist, where it is solid (scene fogcal, the "low" view: tools/visual/clipfog.py). That is the beige
    /// distance in the Chapter 1 woods at night and the haze round every exterior. SunFogAmount / MoonFogAmount
    /// (0-15) add nothing up to 8 and then a haze that is there close up (amount 15: about 14% at 6 m).
    /// </summary>
    static void ApplyFog(Godot.Environment env, int amount, Color color, float clipDistance, float cameraDistance = 0f)
    {
        var (density, begin, end, curve) = FogCurve(amount, clipDistance);
        // The distance fog counts from the player, not the camera (from straight above, the ground round the
        // player stays clear however high the camera): Godot's fog counts from the camera, so push it out by
        // the camera's distance from the player. The amount haze does count from the camera.
        if (amount <= 8) { begin += cameraDistance; end += cameraDistance; }
        env.FogEnabled = density > 0;
        env.FogMode = Godot.Environment.FogModeEnum.Depth;
        env.FogLightColor = color;
        env.FogLightEnergy = 1f;
        env.FogDensity = density;
        env.FogDepthBegin = begin;
        env.FogDepthEnd = end;
        env.FogDepthCurve = curve;
        env.FogSkyAffect = 0f;
    }

    /// <summary>
    /// Godot depth fog (maximum, begin and end in metres, curve) for a fog amount and clip distance. Up to amount 8:
    /// the original's distance fog (fitted at FogClipDist 45, scaled with it). Above: its amount haze, fitted at 6 to
    /// 36 m (scene fogcal), which there outweighs the distance fog.
    /// </summary>
    public static (float Density, float Begin, float End, float Curve) FogCurve(int amount, float clipDistance = 45f)
    {
        float clip = clipDistance > 1f ? clipDistance : 45f;
        // From the player: clear to about a seventh of the clip distance... solid at three quarters of it
        // (measured from a camera 10 m behind the player: clear to 20 m, solid at 43 m).
        if (amount <= 8) return (1f, -6.5f * clip / 45f, 35f * clip / 45f, 2.9f);
        return (Math.Clamp((amount - 8) / 7f, 0f, 1f), -145f, 88f, 6.5f);
    }

    void UpdateSky(float daylight)
    {
        if (_content == null || !int.TryParse(Info.SkyBox, out int row) || row <= 0) return;
        var skies = _content.Resources.TwoDA("skyboxes");
        double h = _minuteClock / 60.0 % 24;
        string column = h >= _time.DawnHour && h < _time.DawnHour + 1 ? "DAWN" : h >= _time.DuskHour && h < _time.DuskHour + 1 ? "DUSK" : daylight > 0.5f ? "DAY" : "NIGHT";
        var model = skies.Get(row, column);
        if (model.Length == 0) model = skies.Get(row, "DAY");
        if (!string.Equals(model, _skyModel, StringComparison.OrdinalIgnoreCase))
        {
            _skyModel = model;
            _sky?.QueueFree();
            _sky = null;
            if (_content.Models.Get(model) is { } data)
            {
                var built = Rendering.ModelBuilder.BuildStatic(data, _content.Materials, lights: false, emitters: false);
                _sky = built.Root;
                foreach (var mi in _sky.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                {
                    mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    if (mi.Mesh is ArrayMesh am)
                        for (int s = 0; s < am.GetSurfaceCount(); s++)
                            if (am.SurfaceGetMaterial(s) is StandardMaterial3D sm)
                            {
                                var sky = (StandardMaterial3D)sm.Duplicate();
                                sky.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                                sky.DisableFog = true;
                                sky.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
                                sky.RenderPriority = -100;
                                sky.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                                am.SurfaceSetMaterial(s, sky);
                            }
                }
                AddChild(_sky);
            }
        }
        // The sky follows the camera so it is always at "infinity".
        if (_sky != null && GetViewport().GetCamera3D() is { } cam)
            _sky.GlobalPosition = cam.GlobalPosition;
    }

    /// <summary>Physics layer used only for camera collision.</summary>
    public const uint CameraLayer = 1 << 19;

    /// <summary>
    /// Gives a static model's meshes trimesh collision on the camera layer. With the model it was built from, the
    /// shapes are made once per model and shared by every copy (an area repeats its tile models many times, and
    /// making the shapes was most of an area's build time).
    /// </summary>
    public static void AddCameraCollision(Node root, ModelData? model = null, IReadOnlyDictionary<string, Vector3[]>? faces = null)
    {
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Name.ToString().StartsWith("Fade") || mi.Mesh == null) continue;
            var shape = TrimeshShape(model, mi, faces);
            if (shape == null) continue;
            var body = new StaticBody3D { CollisionLayer = CameraLayer, CollisionMask = 0 };
            body.AddChild(new CollisionShape3D { Shape = shape });
            mi.AddChild(body);
        }
    }

    sealed class SharedShapes
    {
        public readonly Dictionary<string, Shape3D?> Shapes = new();
        public int Used;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ModelData, SharedShapes> Trimeshes = new();

    /// <summary>Lets go of shared shapes no recent area used, with the shared meshes (ModelBuilder.NextArea).</summary>
    static void NextAreaShapes()
    {
        ModelBuilder.NextArea();
        foreach (var (model, shapes) in Trimeshes.ToList())
            if (shapes.Used < ModelBuilder.AreaGeneration - 2) Trimeshes.Remove(model);
    }

    /// <summary>
    /// A mesh's trimesh shape, shared by every mesh of that name built from the same model (ModelBuilder.BuildStatic
    /// builds the same geometry for a model each time; the shape is in the mesh's own space). Made from the builder's
    /// own triangles when it has them: Mesh.CreateTrimeshShape reads the mesh back from the GPU, which was slow.
    /// </summary>
    public static Shape3D? TrimeshShape(ModelData? model, MeshInstance3D mi, IReadOnlyDictionary<string, Vector3[]>? faces = null)
    {
        if (mi.Mesh == null) return null;
        string key = mi.Name;
        Shape3D? Make() => faces != null && faces.TryGetValue(key, out var f)
            ? (f.Length > 0 ? new ConcavePolygonShape3D { Data = f } : null)
            : mi.Mesh.CreateTrimeshShape();
        if (model == null) return Make();
        var shared = Trimeshes.GetOrCreateValue(model);
        shared.Used = ModelBuilder.AreaGeneration;
        if (!shared.Shapes.TryGetValue(key, out var shape)) shared.Shapes[key] = shape = Make();
        return shape;
    }
}
