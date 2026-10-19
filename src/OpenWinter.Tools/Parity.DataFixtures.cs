using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

static partial class Parity
{
    // Runtime clones of installed data plus authored deltas; no installed assets in fixture sources.
    internal static IEnumerable<ErfEntry> DataFixtures(LocalConfig cfg, string dir, string buildDir)
    {
        var resources = new ResMan(new GamePaths(cfg.GameRoot, Path.Combine(buildDir, "resource-user"), cfg.Language));
        resources.AddBaseGame(includeUserOverride: false);
        foreach (string file in Directory.GetFiles(dir, "*.2da.json"))
        {
            string name = Path.GetFileName(file)[..^".2da.json".Length];
            var table = resources.TwoDA(name);
            if (table.RowCount == 0) throw new InvalidDataException($"Missing fixture table {name}");
            var rows = Enumerable.Range(0, table.RowCount)
                .Select(i => table.Columns.Select(c => table.Get(i, c)).ToArray()).ToList();
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var edit in document.RootElement.GetProperty("rows").EnumerateArray())
            {
                int row = edit.GetProperty("row").GetInt32();
                if (row < 0 || row > 65535) throw new InvalidDataException("Invalid fixture row");
                while (rows.Count <= row) rows.Add(new string[table.Columns.Length]);
                if (edit.TryGetProperty("copy", out var copy))
                {
                    int source = copy.GetInt32();
                    if (source < 0 || source >= table.RowCount) throw new InvalidDataException("Invalid source row");
                    rows[row] = table.Columns.Select(c => table.Get(source, c)).ToArray();
                }
                if (edit.TryGetProperty("values", out var values))
                    foreach (var value in values.EnumerateObject())
                    {
                        int column = table.ColumnIndex(value.Name);
                        if (column < 0) throw new InvalidDataException($"Unknown {name} column {value.Name}");
                        rows[row][column] = value.Value.ValueKind == JsonValueKind.Null ? "" : value.Value.ToString();
                    }
            }
            static string Cell(string? value) => string.IsNullOrEmpty(value) ? "****"
                : value.Any(char.IsWhiteSpace) ? "\"" + value.Replace("\"", "") + "\"" : value;
            var text = new StringBuilder("2DA V2.0\n\n").AppendLine(string.Join(" ", table.Columns));
            for (int row = 0; row < rows.Count; row++)
                text.Append(row).Append(' ').AppendLine(string.Join(" ", rows[row].Select(Cell)));
            byte[] bytes = OpenWinter.Aurora.Text.Windows1252.GetBytes(text.ToString().Replace("\r\n", "\n"));
            File.WriteAllBytes(Path.Combine(buildDir, name + ".2da"), bytes);
            yield return new(name, ResType.FromExtension("2da"), bytes);
        }
        foreach (string file in Directory.GetFiles(dir, "*.uti.json"))
        {
            string name = Path.GetFileName(file)[..^".uti.json".Length];
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var spec = document.RootElement;
            string template = spec.GetProperty("template").GetString()!;
            var bytes = resources.Read(template, ResType.FromExtension("uti"))
                ?? throw new InvalidDataException($"Missing fixture item {template}");
            var item = Gff.Read(bytes);
            item.SetResRef("TemplateResRef", name).SetString("Tag", name);
            if (spec.TryGetProperty("baseItem", out var baseItem)) item.SetInt("BaseItem", baseItem.GetInt32());
            // "castSubtype": the iprp_spells row of the blueprint's first Cast Spell property (scripts can't add rows
            // AddItemProperty refuses).
            if (spec.TryGetProperty("castSubtype", out var castSubtype))
                (item.GetList("PropertiesList").FirstOrDefault(p => p.GetInt("PropertyName") == 15)
                    ?? throw new InvalidDataException($"{template} has no Cast Spell property"))
                    .SetWord("Subtype", castSubtype.GetInt32());
            yield return new(name, ResType.FromExtension("uti"), Gff.Write(item, "UTI "));
        }
    }
}
