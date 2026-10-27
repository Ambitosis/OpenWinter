using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-shaders dir: the area from a few camera angles (the ground near the player, the horizon and sky, from
/// above, the player up close) and shaders.txt, the module shader programs this run translated or left (with the
/// reason). Combine with --shader-uniforms to preset the script uniforms a module's shaders read (docs/SHADERS.md),
/// --shader-script name to run a script on the player first (with --script-dir, to set material uniforms) and
/// --shader-wait / --shader-after seconds to start later (8 s in) and to wait longer after the script (4 s);
/// --shader-focus tag frames the creature with that tag instead of the player (and adds two views from close by).
/// </summary>
public partial class Main
{
    async Task CaptureShaders(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(_args.TryGetValue("shader-wait", out var wait) && double.TryParse(wait, System.Globalization.CultureInfo.InvariantCulture, out var seconds) ? seconds : 8.0);
        if (_args.TryGetValue("shader-script", out var script))
        {
            _session.Send(new ChatSendMessage { Text = "/script " + script }.Write());
            GD.Print($"[capture] ran {script}");
            await Wait(_args.TryGetValue("shader-after", out var after) && double.TryParse(after, System.Globalization.CultureInfo.InvariantCulture, out var later) ? later : 4.0);
        }
        if (_args.TryGetValue("shader-focus", out var focus) && _session.Objects.Values.OfType<Views.CreatureView>()
                .FirstOrDefault(c => IsInstanceValid(c) && c.Tag.Equals(focus, StringComparison.OrdinalIgnoreCase)) is { } npc)
            _camera.Target = npc;
        else if (focus != null) GD.Print($"[capture] no creature tagged {focus}: " + string.Join(", ", _session.Objects.Values.OfType<Views.CreatureView>().Select(c => c.Tag)));
        var views = new List<(string Name, float Pitch, float Distance)>
        {
            ("s1_ground", -40f, 10f),
            ("s2_horizon", -6f, 12f),
            ("s3_above", -80f, 18f),
            ("s4_close", -35f, 3.5f),
            ("s5_back", -35f, 3.5f),
        };
        // A focused creature also from close by, front and back.
        if (_camera.Target != _session.Controlled) views.AddRange([("s6_near", -15f, 1.8f), ("s7_near_back", -15f, 1.8f)]);
        foreach (var (name, pitch, distance) in views)
        {
            _camera.Pitch = Mathf.DegToRad(pitch);
            _camera.Distance = distance;
            if (name is "s5_back" or "s7_near_back") _camera.Yaw += Mathf.Pi;
            await Wait(1.5);
            await Shot(dir, name);
        }
        var lines = new List<string>();
        if (_session.Content?.Materials.Shaders is { } shaders)
            foreach (var (program, status) in shaders.Programs.OrderBy(p => p.Key)) lines.Add($"{program}: {status}");
        lines.AddRange(Rendering.Shaders.ShaderGlobals.History.Select(h => "uniform " + h));
        if (_session.Controlled is { } pc && IsInstanceValid(pc))
            lines.Add($"player {pc.GlobalPosition.X:0.00} {-pc.GlobalPosition.Z:0.00} {pc.GlobalPosition.Y:0.00} (area coordinates)");
        System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "shaders.txt"), lines);
        GetTree().Quit();
    }
}
