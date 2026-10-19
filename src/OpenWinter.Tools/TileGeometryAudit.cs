using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;

static class TileGeometryAudit
{
    public static void Run(GamePaths paths, string output)
    {
        using var resources = new ResMan(paths);
        resources.AddBaseGame(includeUserOverride: false);
        var models = new ModelCache(resources);
        var rows = new List<object>();
        var problems = new List<string>();
        var missingModels = new List<string>();
        var checkedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modes = new Dictionary<int, int>();
        int totalParts = 0, totalTriangles = 0, mirrored = 0, blackParts = 0;
        foreach (var key in resources.All(ResType.Set).OrderBy(k => k.ResRef))
        {
            var set = Ini.Parse(resources.Read(key.ResRef, ResType.Set)!);
            int count = set.GetInt("TILES", "Count");
            int unique = 0;
            for (int i = 0; i < count; i++)
            {
                string name = set.Get($"TILE{i}", "Model");
                if (!checkedModels.Add(name)) continue;
                unique++;
                var model = models.Get(name);
                if (model == null) { missingModels.Add($"{key.ResRef}/{i}: {name}"); continue; }
                var globals = model.RestGlobals();
                for (int j = 0; j < model.Parts.Count; j++)
                {
                    var part = model.Parts[j];
                    if (part.Mesh is not { Render: true, IsWalkmesh: false } mesh) continue;
                    totalParts++; totalTriangles += mesh.Indices.Length / 3;
                    modes[part.TileFadeMode] = modes.GetValueOrDefault(part.TileFadeMode) + 1;
                    if (mesh.Texture.Contains("black", StringComparison.OrdinalIgnoreCase)) blackParts++;
                    if (globals[j].GetDeterminant() < 0) mirrored++;
                    if (mesh.Indices.Length % 3 != 0 || mesh.Indices.Any(v => v < 0 || v >= mesh.Vertices.Length))
                        problems.Add($"{name}/{part.Name}: invalid triangle indices");
                    if (mesh.Vertices.Any(v => !float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z)))
                        problems.Add($"{name}/{part.Name}: nonfinite vertex");
                    if (Math.Abs(globals[j].GetDeterminant()) < 1e-8f)
                        problems.Add($"{name}/{part.Name}: singular transform");
                }
            }
            rows.Add(new { tileset = key.ResRef, interior = set.GetInt("GENERAL", "Interior"), tiles = count, uniqueModels = unique });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { models = checkedModels.Count, totalParts, totalTriangles,
            mirrored, blackParts, modes, tilesets = rows, missingModels, problems }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Tile audit: {checkedModels.Count} models, {totalParts} render parts, {totalTriangles} triangles, {blackParts} black parts, {mirrored} mirrored transforms, {missingModels.Count} missing models, {problems.Count} geometry problems");
        if (problems.Count > 0) throw new InvalidOperationException(string.Join("; ", problems.Take(10)));
    }
}
