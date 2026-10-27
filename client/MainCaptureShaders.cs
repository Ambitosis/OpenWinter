using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-shaders dir: the area from a few camera angles (the ground near the player, the horizon and sky, from
/// above) and shaders.txt, the module shader programs this run translated or left (with the reason). Combine with
/// --shader-uniforms to preset the script uniforms a module's shaders read (docs/SHADERS.md).
/// </summary>
public partial class Main
{
    async Task CaptureShaders(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(8.0);
        var views = new (string Name, float Pitch, float Distance)[]
        {
            ("s1_ground", -40f, 10f),
            ("s2_horizon", -6f, 12f),
            ("s3_above", -80f, 18f),
        };
        foreach (var (name, pitch, distance) in views)
        {
            _camera.Pitch = Mathf.DegToRad(pitch);
            _camera.Distance = distance;
            await Wait(1.5);
            await Shot(dir, name);
        }
        var lines = new List<string>();
        if (_session.Content?.Materials.Shaders is { } shaders)
            foreach (var (program, status) in shaders.Programs.OrderBy(p => p.Key)) lines.Add($"{program}: {status}");
        lines.AddRange(Rendering.Shaders.ShaderGlobals.History.Select(h => "uniform " + h));
        System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "shaders.txt"), lines);
        GetTree().Quit();
    }
}
