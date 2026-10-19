using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// Area weather (SetWeather and the ChanceRain/ChanceSnow rolls): rain or snow falling around
/// the camera, the rain loop, and lightning flashes with thunder in storms. The look comes from
/// the weathertypes.2da models' emitters (fx_rain, fx_snow: texture, size, speed, flipbook);
/// weather fades in and out over the row's FadeTime.
/// Drops live in a box that follows the camera but are anchored in the world (they wrap around
/// inside the box in a shader), so moving the camera doesn't drag the rain along.
/// </summary>
public partial class WeatherFx : Node
{
    public ClientSession Session = null!;

    /// <summary>WEATHER_CLEAR 0, RAIN 1, SNOW 2.</summary>
    public int Current { get; private set; }
    /// <summary>The area's ChanceLightning (percent), or -1 when the server didn't say.</summary>
    public int LightningChance { get; private set; } = -1;
    public int Flashes { get; private set; }

    sealed class Layer
    {
        public required MultiMeshInstance3D Node;
        public required ShaderMaterial Material;
        public AudioStreamPlayer? Loop;
        public float Level, Target;
    }

    readonly List<Layer> _layers = new();
    DirectionalLight3D? _flash;
    float _fade = 6f;
    double _nextStrike;
    static Shader? _shader;

    public void Reset()
    {
        foreach (var l in _layers) Free(l);
        _layers.Clear();
        _flash?.QueueFree();
        _flash = null;
        Current = 0;
        LightningChance = -1;
    }

    static void Free(Layer l)
    {
        if (IsInstanceValid(l.Node)) l.Node.QueueFree();
        if (l.Loop != null && IsInstanceValid(l.Loop)) l.Loop.QueueFree();
    }

    public void Set(int weather, int lightningChance)
    {
        weather = FxMath.NormalizeWeather(weather);
        if (lightningChance >= 0) LightningChance = lightningChance;
        if (weather == Current) return;
        var content = Session.Content;
        if (content == null) return;
        Current = weather;
        var table = content.Resources.TwoDA("weathertypes");
        _fade = Math.Max(0.5f, table.GetFloat(weather == 0 ? 1 : weather, "FadeTime", 6000) / 1000f);
        // The old weather fades out while the new one fades in.
        foreach (var l in _layers) l.Target = 0;
        if (weather == 0) return;
        var layer = Build(content, weather, table.Get(weather, "Model"));
        _layers.Add(layer);
        Session.AddChild(layer.Node);
        if (weather == 1 && content.Audio.Get("al_wt_rainlight1") is { } rain)
        {
            var loop = new AudioStreamPlayer { Stream = rain, VolumeDb = -60, Name = "RainLoop", Bus = OpenWinter.Client.Preferences.AudioBuses.Ambient };
            AddChild(loop);
            loop.Finished += () => loop.Play();
            loop.Play();
            layer.Loop = loop;
        }
        _nextStrike = Time.GetTicksMsec() / 1000.0 + GD.RandRange(4.0, 12.0);
    }

    /// <summary>The first emitter of the weather model (texture, colour, size, speed, flipbook).</summary>
    static EmitterData? Emitter(ClientContent content, string model) =>
        content.Models.Get(model)?.Parts.FirstOrDefault(p => p.Emitter != null)?.Emitter;

    static Layer Build(ClientContent content, int weather, string model)
    {
        var e = Emitter(content, model);
        bool rain = weather == 1;
        var tex = content.Textures.Get(e?.Texture is { Length: > 0 } t ? t : rain ? "fxpa_rain01" : "fxpa_fig8");
        _shader ??= new Shader { Code = ShaderCode };
        var mat = new ShaderMaterial { Shader = _shader };
        mat.SetShaderParameter("tex", tex?.Texture);
        mat.SetShaderParameter("additive", e?.Blend.Equals("Lighten", StringComparison.OrdinalIgnoreCase) ?? !rain);
        if (rain)
        {
            // Motion_Blur emitters stretch each drop along its fall.
            float speed = e?.Velocity is > 0 and var v ? v : 11f;
            mat.SetShaderParameter("speed", speed);
            mat.SetShaderParameter("quad_size", new Vector2(Math.Clamp((e?.SizeStart ?? 0.03f) * 1.8f, 0.045f, 0.09f), speed * 0.08f));
            mat.SetShaderParameter("color", new Color(0.85f, 0.88f, 0.95f, 0.8f));
            mat.SetShaderParameter("stretch", true);
            mat.SetShaderParameter("sway", 0.0f);
            mat.SetShaderParameter("box_size", new Vector3(30, 20, 30));
        }
        else
        {
            float size = Math.Clamp((e?.SizeStart ?? 0.08f) * 2f, 0.08f, 0.3f);
            mat.SetShaderParameter("speed", 1.6f);
            mat.SetShaderParameter("quad_size", new Vector2(size, size));
            mat.SetShaderParameter("color", new Color(1, 1, 1, 0.95f));
            mat.SetShaderParameter("stretch", false);
            mat.SetShaderParameter("sway", 0.35f);
            mat.SetShaderParameter("box_size", new Vector3(30, 16, 30));
            mat.SetShaderParameter("frames", new Vector2(Math.Max(1, e?.GridX ?? 1), Math.Max(1, e?.GridY ?? 1)));
        }
        mat.SetShaderParameter("level", 0f);
        var quad = new QuadMesh { Size = Vector2.One, CenterOffset = new Vector3(0, 0.5f, 0) };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = quad, InstanceCount = rain ? 9000 : 10000 };
        var node = new MultiMeshInstance3D
        {
            Name = rain ? "Rain" : "Snow", Multimesh = mm, MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The shader places every drop itself; never cull the batch.
            CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)),
        };
        return new Layer { Node = node, Material = mat, Target = 1 };
    }

    public override void _Process(double delta)
    {
        var cam = GetViewport().GetCamera3D();
        float step = (float)delta / _fade;
        for (int i = _layers.Count - 1; i >= 0; i--)
        {
            var l = _layers[i];
            l.Level = l.Target > l.Level ? Math.Min(l.Target, l.Level + step) : Math.Max(l.Target, l.Level - step);
            if (l.Level <= 0 && l.Target <= 0)
            {
                Free(l);
                _layers.RemoveAt(i);
                continue;
            }
            l.Material.SetShaderParameter("level", l.Level);
            if (cam != null) l.Material.SetShaderParameter("box_center", cam.GlobalPosition);
            if (l.Loop != null) l.Loop.VolumeDb = Mathf.LinearToDb(Math.Max(0.001f, l.Level * 0.6f));
        }
        Storm(delta);
    }

    /// <summary>Lightning while it rains or snows, if the area has a ChanceLightning (fx_lightning01's flash, plus thunder).</summary>
    void Storm(double delta)
    {
        if (_flash != null && IsInstanceValid(_flash) && _flash.LightEnergy > 0)
            _flash.LightEnergy = Math.Max(0, _flash.LightEnergy - (float)delta * 7f);
        if (Current == 0 || LightningChance <= 0) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextStrike) return;
        // Higher chances mean more frequent strikes: about every 40 s at 10%, every 6 s at 100%.
        _nextStrike = now + GD.RandRange(0.6, 1.4) * (6 + 34 * (1 - LightningChance / 100.0));
        Strike();
    }

    /// <summary>One lightning flash and its thunder.</summary>
    public void Strike()
    {
        if (_flash == null || !IsInstanceValid(_flash))
        {
            _flash = new DirectionalLight3D { Name = "LightningFlash", LightColor = new Color(0.8f, 0.85f, 1f), ShadowEnabled = false, LightEnergy = 0, RotationDegrees = new Vector3(-70, 20, 0) };
            Session.AddChild(_flash);
        }
        _flash.LightEnergy = 3.2f;
        Flashes++;
        var content = Session.Content;
        if (content == null) return;
        bool near = GD.Randf() < 0.5f;
        var sound = content.Audio.Get($"as_wt_thunder{(near ? "cl" : "ds")}{GD.RandRange(1, 4)}");
        if (sound == null) return;
        var thunder = new AudioStreamPlayer { Stream = sound, VolumeDb = near ? -2 : -6, Bus = OpenWinter.Client.Preferences.AudioBuses.Ambient };
        AddChild(thunder);
        thunder.Finished += thunder.QueueFree;
        GetTree().CreateTimer(near ? 0.3 : 1.2).Timeout += () => { if (IsInstanceValid(thunder)) thunder.Play(); };
    }

    const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, skip_vertex_transform, shadows_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform vec4 color : source_color = vec4(1.0);
uniform bool additive = false;
uniform vec3 box_size = vec3(30.0, 20.0, 30.0);
uniform vec3 box_center = vec3(0.0);
uniform float speed = 11.0;
uniform vec3 drift = vec3(0.6, 0.0, 0.35);
uniform vec2 quad_size = vec2(0.05, 0.7);
uniform bool stretch = true;
uniform float sway = 0.0;
uniform float level = 1.0;
uniform vec2 frames = vec2(1.0);
varying float fade;
varying vec2 frame;

float hash(float n) { return fract(sin(n) * 43758.5453123); }

void vertex() {
    float id = float(INSTANCE_ID);
    // Fading weather draws a growing share of the drops.
    if (hash(id * 7.31 + 0.5) > level) {
        VERTEX = vec3(0.0, 0.0, 1e6);
        fade = 0.0;
    } else {
        vec3 seed = vec3(hash(id * 1.13 + 0.1), hash(id * 2.71 + 3.1), hash(id * 5.17 + 1.7));
        vec3 velocity = vec3(drift.x, -speed, drift.z) * (0.85 + 0.3 * hash(id * 3.3));
        vec3 origin = box_center - box_size * 0.5;
        // Anchored in the world, wrapped into the box around the camera.
        vec3 p = origin + mod(seed * box_size * 7.0 + velocity * TIME - origin, box_size);
        if (sway > 0.0) p.xz += vec2(sin(TIME * 1.3 + id), cos(TIME * 1.1 + id * 1.7)) * sway;
        vec3 cam = INV_VIEW_MATRIX[3].xyz;
        vec3 up = stretch ? normalize(-velocity) : INV_VIEW_MATRIX[1].xyz;
        vec3 right = stretch ? normalize(cross(up, normalize(cam - p))) : INV_VIEW_MATRIX[0].xyz;
        vec3 world = p + right * (VERTEX.x * quad_size.x) + up * ((VERTEX.y - (stretch ? 0.0 : 0.5)) * quad_size.y);
        VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
        NORMAL = vec3(0.0, 0.0, 1.0);
        float d = length(p.xz - cam.xz) / (box_size.x * 0.5);
        fade = 1.0 - smoothstep(0.65, 1.0, d);
        float f = floor(hash(id * 9.7) * frames.x * frames.y);
        frame = vec2(mod(f, frames.x), floor(f / frames.x));
    }
}

void fragment() {
    vec4 c = texture(tex, (UV + frame) / frames);
    float a = c.a * color.a * fade;
    if (additive) {
        // Light-on-black flipbooks (snow): brightness is coverage.
        ALBEDO = color.rgb;
        ALPHA = clamp(max(max(c.r, c.g), c.b) * 1.4 * a, 0.0, 1.0);
    } else {
        ALBEDO = c.rgb * color.rgb;
        ALPHA = a;
    }
}
";
}
