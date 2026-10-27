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
        _registered = true;
        Presets();
        foreach (var g in EngineNames.Globals)
        {
            var (type, value) = g.Type switch
            {
                "int" => (RenderingServer.GlobalShaderParameterType.Int, (Variant)0),
                "vec2" => (RenderingServer.GlobalShaderParameterType.Vec2, (Variant)Vector2.Zero),
                "vec3" => (RenderingServer.GlobalShaderParameterType.Vec3, (Variant)Vector3.Zero),
                "vec4" => (RenderingServer.GlobalShaderParameterType.Vec4, (Variant)Vector4.Zero),
                "ivec2" => (RenderingServer.GlobalShaderParameterType.Ivec2, (Variant)Vector2I.Zero),
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
            var eye = camera.GlobalPosition;
            Set("cameraPosition", Aurora(eye));
            Set("clipParams", new Vector3(camera.Near, camera.Far, 1f - camera.Near / camera.Far));
            Set("cameraViewAngle", camera.Fov);
            if (camera is OrbitCamera orbit)
            {
                Set("cameraPitch", Mathf.RadToDeg(orbit.Pitch));
                Set("cameraYaw", Mathf.RadToDeg(orbit.Yaw));
                if (orbit.Target is { } target && GodotObject.IsInstanceValid(target))
                {
                    Set("cameraFocus", Aurora(target.GlobalPosition));
                    Set("playerCameraDist", eye.DistanceTo(target.GlobalPosition));
                }
            }
        }
        if (session.Area is not { } area || !GodotObject.IsInstanceValid(area)) return;
        var info = area.Info;
        Set("areaFlags", (info.IsInterior ? 1 : 0) | (info.IsUnderground ? 2 : 0) | (info.IsNatural ? 4 : 0));
        if (area.Environment is { } env)
        {
            var fog = env.FogLightColor;
            Set("fogColor", new Vector4(fog.R, fog.G, fog.B, 1f));
            float begin = Math.Max(0f, env.FogDepthBegin), end = Math.Max(begin + 0.01f, env.FogDepthEnd);
            Set("fogParams", new Vector4(env.FogEnabled ? 1f : 0f, begin, end, 1f / (end - begin)));
            var ambient = env.AmbientLightColor;
            Set("lightAreaAmbient", new Vector3(ambient.R, ambient.G, ambient.B));
        }
        if (area.SunLight is { } sun && GodotObject.IsInstanceValid(sun))
        {
            var c = sun.LightColor;
            Set("lightAreaDiffuse", new Vector3(c.R, c.G, c.B));
            // The direction the light travels, in Aurora coordinates.
            Set("lightAreaDiffuseDirection", Aurora(-sun.GlobalTransform.Basis.Z));
        }
        double hours = area.ClockMinutes / 60.0;
        Set("moduleHour", (int)hours % 24);
    }
}
