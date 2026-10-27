using OpenWinter.Aurora.Shaders;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.Rendering.Shaders;

/// <summary>
/// The engine values module shaders read as global parameters (<see cref="EngineNames.Globals"/>): the
/// SetShaderUniform* banks scripts set for this player, and the world state (time, player, camera, area, fog,
/// light) kept up to date every frame. Positions are in Aurora coordinates.
/// </summary>
public static class ShaderGlobals
{
    static bool _registered;
    static readonly DateTime Start = DateTime.UtcNow;
    static readonly Dictionary<string, Variant> Script = new();
    /// <summary>Set from the command line: scripts don't change them.</summary>
    static readonly HashSet<string> Pinned = new();
    /// <summary>Every script uniform set this run, in order (capture reports).</summary>
    public static readonly List<string> History = new();

    /// <summary>
    /// Declares every global once, before the first module shader is compiled (Godot needs them to exist); a client
    /// with no module shaders never does. Script uniforms set before then are kept and applied here.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;
        // Presets first: they are kept and applied as the globals are declared.
        Presets();
        _registered = true;
        foreach (var g in EngineNames.Globals)
        {
            var (type, value) = g.Type switch
            {
                "int" => (RenderingServer.GlobalShaderParameterType.Int, (Variant)0),
                "vec2" => (RenderingServer.GlobalShaderParameterType.Vec2, (Variant)Vector2.Zero),
                "vec3" => (RenderingServer.GlobalShaderParameterType.Vec3, (Variant)Vector3.Zero),
                "vec4" => (RenderingServer.GlobalShaderParameterType.Vec4, (Variant)Vector4.Zero),
                "ivec2" => (RenderingServer.GlobalShaderParameterType.Ivec2, (Variant)Vector2I.Zero),
                "mat4" => (RenderingServer.GlobalShaderParameterType.Mat4, (Variant)Projection.Identity),
                _ => (RenderingServer.GlobalShaderParameterType.Float, (Variant)0f),
            };
            RenderingServer.GlobalShaderParameterAdd(g.Name, type, Script.TryGetValue(g.Name, out var set) ? set : value);
        }
    }

    static bool _presets;

    static void Presets()
    {
        if (_presets) return;
        _presets = true;
        // --shader-uniforms "int9=65280;float1=0.5;vec2=1,0,0,1": script uniforms held at these values (scripts
        // can't change them), for previewing how a module's shaders react.
        var args = OS.GetCmdlineUserArgs();
        int at = Array.IndexOf(args, "--shader-uniforms");
        if (at >= 0 && at + 1 < args.Length)
            foreach (var item in args[at + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var m = System.Text.RegularExpressions.Regex.Match(item, @"^(float|int|vec)(\d+)=(.+)$");
                if (!m.Success) continue;
                var parts = m.Groups[3].Value.Split(',');
                var values = parts.Select(p => float.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray();
                long? exact = long.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l) ? l : null;
                SetScriptUniform(m.Groups[1].Value == "vec" ? "vec4" : m.Groups[1].Value, int.Parse(m.Groups[2].Value) - 1, values, exact, true);
            }
    }

    static void Set(string name, Variant value)
    {
        if (_registered) RenderingServer.GlobalShaderParameterSet(name, value);
    }

    /// <summary>
    /// SetShaderUniformFloat / Int / Vec for this player: bank "float", "int" or "vec4", index 0-15. Ints are
    /// exact (scripts pack bit fields into them).
    /// </summary>
    public static bool SetScriptUniform(string type, int index, float[] value, long? exact = null) => SetScriptUniform(type, index, value, exact, false);

    static bool SetScriptUniform(string type, int index, float[] value, long? exact, bool pin)
    {
        if (EngineNames.ScriptUniform(type, index) is not { } name) return false;
        Presets();
        if (pin) Pinned.Add(name);
        else if (Pinned.Contains(name))
        {
            History.Add($"{name} kept (script sent {(exact?.ToString() ?? string.Join(",", value))})");
            return true;
        }
        Variant v = type switch
        {
            "int" => (int)(exact ?? (long)value.ElementAtOrDefault(0)),
            "float" => value.ElementAtOrDefault(0),
            _ => new Vector4(value.ElementAtOrDefault(0), value.ElementAtOrDefault(1), value.ElementAtOrDefault(2), value.ElementAtOrDefault(3)),
        };
        Script[name] = v;
        History.Add($"{name} = {v}");
        Set(name, v);
        return true;
    }

    /// <summary>The value a script uniform was last given (capture checks), or null.</summary>
    public static Variant? ScriptUniform(string name) => Script.TryGetValue(name, out var v) ? v : (Variant?)null;

    /// <summary>Aurora (Z-up) coordinates of a point in Godot's world.</summary>
    static Vector3 Aurora(Vector3 g) => new(g.X, -g.Z, g.Y);

    /// <summary>Aurora world space to Godot's (x, y, z) to (x, z, -y).</summary>
    static readonly Transform3D AuroraToGodot = new(new Basis(new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0)), Vector3.Zero);

    /// <summary>
    /// The area's wind: SetAreaWind's direction times its magnitude, as given (a direction of length 3 triples it);
    /// the area's own wind power blows diagonally at its magnitude (medium (0.707, 0.707, 0), strong twice that).
    /// Measured: parity/visual, scene postfx.
    /// </summary>
    public static void SetWind(Vector3 direction, float magnitude, bool scripted)
    {
        var d = scripted || direction.LengthSquared() < 1e-8f ? direction : direction.Normalized();
        _wind = d * magnitude;
    }

    static Vector3 _wind;

    /// <summary>The spell targeting shape of a script's targeting mode (SetEnterTargetingModeData).</summary>
    public sealed record TargetingShape(int Shape, float SizeX, float SizeY, int Flags, float Range, int Spell, int Feat);

    static TargetingShape? _target;
    static Func<bool>? _targetActive;
    static Func<System.Numerics.Vector3?>? _targetPoint;
    static bool _targetShown;

    /// <summary>A targeting mode starts (shape null: none); it lasts while <paramref name="active"/> says so.</summary>
    public static void SetTargeting(TargetingShape? shape, Func<bool> active, Func<System.Numerics.Vector3?> point)
    {
        _target = shape is { Shape: > 0 } or { Range: > 0 } ? shape : null;
        _targetActive = active;
        _targetPoint = point;
    }

    /// <summary>The weather the area shows (WEATHER_* 0 clear, 1 rain, 2 snow) and how far it has faded in (0 to 1).</summary>
    public static Func<(int Type, float Density)>? Weather;
    static int _lastWeather;

    static void UpdateTargeting()
    {
        var shape = _target;
        if (shape != null && _targetActive?.Invoke() != true) { shape = _target = null; _targetActive = null; _targetPoint = null; }
        if (shape == null)
        {
            if (!_targetShown) return;
            _targetShown = false;
            Set("targetShape", 0);
            Set("targetShapeSpellRange", 0f);
            Set("targetShapeSizeX", 0f);
            Set("targetShapeSizeY", 0f);
            return;
        }
        _targetShown = true;
        Set("targetShape", shape.Shape);
        Set("targetShapeSizeX", shape.SizeX);
        Set("targetShapeSizeY", shape.SizeY);
        Set("targetShapeSpellRange", shape.Range);
        Set("targetShapeSpellId", shape.Spell);
        Set("targetShapeFeatId", shape.Feat);
        Set("targetShapeHarmsEnemies", (shape.Flags & 1) != 0 ? 1 : 0);
        Set("targetShapeHarmsAllies", (shape.Flags & 2) != 0 ? 1 : 0);
        Set("targetShapeHelpsAllies", (shape.Flags & 4) != 0 ? 1 : 0);
        Set("targetShapeIgnoresSelf", (shape.Flags & 8) != 0 ? 1 : 0);
        Set("targetShapeOriginOnSelf", (shape.Flags & 16) != 0 ? 1 : 0);
        if (_targetPoint?.Invoke() is { } p) Set("targetWorldCoords", new Vector3(p.X, p.Y, p.Z));
    }

    /// <summary>Updates the world state for this frame.</summary>
    public static void Update(ClientSession session, Camera3D? camera)
    {
        if (!_registered) return;
        Set("worldtimerTimeOfDay", (int)((DateTime.UtcNow - Start).TotalMilliseconds % 86_400_000));
        Set("sceneCurrentFrame", (int)Engine.GetFramesDrawn());
        if (session.Controlled is { } pc && GodotObject.IsInstanceValid(pc) && pc.IsInsideTree())
            Set("playerPosition", Aurora(pc.GlobalPosition));
        if (camera != null && camera.IsInsideTree())
        {
            // The post-process pass's view and GL projection (Aurora coordinates).
            var view = new Projection(camera.GlobalTransform.AffineInverse() * AuroraToGodot);
            var proj = camera.GetCameraProjection();
            Set(EngineNames.PostView, view);
            Set(EngineNames.PostViewInv, view.Inverse());
            Set(EngineNames.PostProj, proj);
            Set(EngineNames.PostProjInv, proj.Inverse());
            // Pixels, as FRAGCOORD counts them (the interface's scale factor doesn't apply).
            var viewport = camera.GetViewport();
            Set(EngineNames.PostSize, viewport is Window window ? (Vector2)window.Size : viewport.GetVisibleRect().Size);
            var eye = camera.GlobalPosition;
            Set("cameraPosition", Aurora(eye));
            Set("clipParams", new Vector3(camera.Near, camera.Far, 1f - camera.Near / camera.Far));
            Set("cameraViewAngle", camera.Fov);
            if (camera is OrbitCamera orbit)
            {
                // Degrees from straight down, as SetCameraFacing's pitch (scene postfx).
                Set("cameraPitch", 90f + Mathf.RadToDeg(orbit.Pitch));
                Set("cameraYaw", Mathf.RadToDeg(orbit.Yaw));
                if (orbit.Target is { } target && GodotObject.IsInstanceValid(target))
                {
                    Set("cameraFocus", Aurora(target.GlobalPosition));
                    Set("playerCameraDist", eye.DistanceTo(target.GlobalPosition));
                }
            }
        }
        Set("areaGlobalWind", _wind);
        if (Weather?.Invoke() is { } weather)
        {
            // Density runs from 0 to 100 as the weather fades in; the type is kept while it fades out (scene postfx).
            if (weather.Type != 0) _lastWeather = weather.Type;
            Set("areaWeatherType", weather.Type != 0 || weather.Density > 0 ? _lastWeather : 0);
            Set("areaWeatherDensity", weather.Density * 100f);
        }
        UpdateTargeting();
        if (session.Area is not { } area || !GodotObject.IsInstanceValid(area)) return;
        var info = area.Info;
        Set("areaFlags", (info.IsInterior ? 1 : 0) | (info.IsUnderground ? 2 : 0) | (info.IsNatural ? 4 : 0));
        if (area.Environment is { } env)
        {
            var fog = env.FogLightColor;
            Set("fogColor", new Vector4(fog.R, fog.G, fog.B, 1f));
            float begin = Math.Max(0f, env.FogDepthBegin), end = Math.Max(begin + 0.01f, env.FogDepthEnd);
            Set("fogParams", new Vector4(env.FogEnabled ? 1f : 0f, begin, end, 1f / (end - begin)));
            // Area colours arrive linear, as the original gives them (scene shaderplt: 0xC0 grey reads 0.53).
            var ambient = env.AmbientLightColor.SrgbToLinear();
            Set("lightAreaAmbient", new Vector3(ambient.R, ambient.G, ambient.B));
        }
        if (area.SunLight is { } sun && GodotObject.IsInstanceValid(sun))
        {
            var c = sun.LightColor.SrgbToLinear();
            Set("lightAreaDiffuse", new Vector3(c.R, c.G, c.B));
            // Towards the light in view space, the frame the original gives it in (scene shaderplt); OpenWinter's sun
            // stands elsewhere in the sky than the original's, so the direction itself differs.
            if (camera != null && camera.IsInsideTree())
                Set("lightAreaDiffuseDirection", (camera.GlobalTransform.Basis.Inverse() * sun.GlobalTransform.Basis.Z).Normalized());
        }
        double hours = area.ClockMinutes / 60.0;
        Set("moduleHour", (int)hours % 24);
        Set("moduleMinutesPerHour", area.MinutesPerHour);
        Set("moduleDawnHour", area.DawnHour);
        Set("moduleDuskHour", area.DuskHour);
    }
}
