using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Spells;

/// <summary>Exhaustive installed-data inventory; resource coverage is not visual parity.</summary>
static class SpellPresentationSurvey
{
    public static void Run(ResMan resources, string? output)
    {
        var spells = resources.TwoDA("spells");
        var visuals = resources.TwoDA("visualeffects");
        var programs = resources.TwoDA("progfx");
        bool Exists(string name, ushort type) => resources.Sources.Any(s => s.Contains(ResKey.Of(name, type)));
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        string Resource(int row, string column, ushort type)
        {
            string name = spells.Get(row, column);
            if (name.Length > 0 && !Exists(name, type)) missing.Add($"spells[{row}].{column}: {name}");
            return name;
        }
        var rows = Enumerable.Range(0, spells.RowCount).Where(i => spells.Get(i, "Label").Length > 0).Select(i => new
        {
            id = i, label = spells.Get(i, "Label"), userType = spells.GetInt(i, "UserType"),
            impactScript = Resource(i, "ImpactScript", ResType.Ncs),
            conjure = spells.Get(i, "ConjAnim"), conjureMilliseconds = spells.GetInt(i, "ConjTime"),
            cast = spells.Get(i, "CastAnim"), castMilliseconds = spells.GetInt(i, "CastTime"),
            castAnimationMapped = spells.Get(i, "CastAnim").Length == 0 || SpellTable.CastAnimation(spells.Get(i, "CastAnim")) >= 0,
            models = new[] { "ConjHeadVisual", "ConjHandVisual", "ConjGrndVisual", "CastHeadVisual", "CastHandVisual", "CastGrndVisual", "ProjModel" }
                .ToDictionary(c => c, c => Resource(i, c, ResType.Mdl)),
            sounds = new[] { "ConjSoundVFX", "ConjSoundMale", "ConjSoundFemale", "CastSound", "ProjSound" }
                .ToDictionary(c => c, c => Resource(i, c, ResType.Wav)),
            projectile = spells.GetInt(i, "Proj"), path = spells.Get(i, "ProjType"),
            spawn = spells.Get(i, "ProjSpwnPoint"), orientation = spells.Get(i, "ProjOrientation"),
            visualParity = "unverified-per-spell",
        }).ToArray();
        var vfx = Enumerable.Range(0, visuals.RowCount).Where(i => visuals.Get(i, "Label").Length > 0).Select(i => new
        {
            id = i, label = visuals.Get(i, "Label"),
            programs = new[] { "ProgFX_Impact", "ProgFX_Duration", "ProgFX_Cessation" }.Select(c =>
            {
                int p = visuals.GetInt(i, c, -1);
                int type = programs.GetInt(p, "Type", -1);
                return new { phase = c, row = p, type, label = programs.Get(p, "Label"),
                    route = p < 0 ? "none" : type switch
                    {
                        1 => "skin", 4 => "light-partial", 5 => "alpha-tint", 7 => "EffectBeam-only",
                        10 => "script-projectile", _ => "unimplemented",
                    } };
            }).Where(p => p.row >= 0).ToArray(),
        }).ToArray();
        var report = new { note = "Resource inventory only. Native per-spell visual/rule parity is not implied.",
            spellRows = spells.RowCount, spells = rows, visualEffects = vfx, missingResources = missing };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (output != null) File.WriteAllText(output, json + "\n", new System.Text.UTF8Encoding(false));
        Console.WriteLine($"{rows.Length}/{spells.RowCount} named spells; {vfx.Length} named visual effects; {missing.Count} unresolved spell resources.");
        Console.WriteLine($"Unmapped cast animations: {string.Join(", ", rows.Where(r => !r.castAnimationMapped).Select(r => $"{r.id}:{r.cast}"))}");
        foreach (var group in vfx.SelectMany(v => v.programs).GroupBy(p => (p.type, p.route)).OrderBy(g => g.Key.type))
            Console.WriteLine($"ProgFX type {group.Key.type}: {group.Count()} references ({group.Key.route})");
        foreach (var value in missing) Console.WriteLine("Missing: " + value);
        if (output != null) Console.WriteLine("Wrote " + output);
    }
}
