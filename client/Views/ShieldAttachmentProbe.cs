using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>Headless model-assembly regression using entirely authored resources.</summary>
public static class ShieldAttachmentProbe
{
    sealed class Source : IResourceSource
    {
        public string Description => "authored shield attachment probe";
        public readonly Dictionary<ResKey, byte[]> Data = new();
        public IEnumerable<ResKey> Keys => Data.Keys;
        public bool Contains(ResKey key) => Data.ContainsKey(key);
        public byte[]? Read(ResKey key) => Data.GetValueOrDefault(key);
        public void Add(string name, ushort type, string text) => Data[ResKey.Of(name, type)] = Encoding.UTF8.GetBytes(text);
    }

    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        try
        {
            var source = new Source();
            source.Add("baseitems", ResType.TwoDA, "2DA V2.0\n\nBaseAC ItemClass ModelType\n14 0 probe 0\n56 **** probe 0\n57 -1 probe 0\n200 3 probe 0\n201 0 probe 0\n");
            source.Add("appearance", ResType.TwoDA, "2DA V2.0\n\nRACE\n0 h\n");
            const string skeleton = "newmodel pmh0\nbeginmodelgeom pmh0\nnode dummy root\nparent NULL\nendnode\nnode dummy rhand\nparent root\nendnode\nnode dummy lhand\nparent root\nendnode\nnode dummy lforearm\nparent root\nendnode\nendmodelgeom pmh0\ndonemodel pmh0\n";
            source.Add("pmh0", ResType.Mdl, skeleton);
            source.Add("probe_001", ResType.Mdl, "newmodel probe_001\nbeginmodelgeom probe_001\nnode trimesh marker\nparent NULL\nverts 3\n0 0 0\n1 0 0\n0 1 0\nfaces 1\n0 1 2 1 0 1 2 0\nendnode\nendmodelgeom probe_001\ndonemodel probe_001\n");
            var resources = new ResMan(new GamePaths(directory, directory));
            resources.Add(source);
            var content = new ClientContent(resources);
            var original = content.Models.Get("pmh0") ?? throw new InvalidOperationException("authored skeleton did not load");
            Check(content.Models.Get("probe_001")?.Parts.Any(p => p.Mesh != null) == true, "authored held mesh loads");
            foreach (var (row, slot, expected) in new[]
            {
                (200, 0x20, "lforearm"), (201, 0x20, "lhand"),
                (14, 0x20, "lhand"), (56, 0x20, "lhand"), (57, 0x20, "lhand"),
                (200, 0x10, "rhand")
            })
            {
                var appearance = new Appearance { Equipment = new() { new ItemVisual { BaseItem = row, Slot = slot } } };
                foreach (bool partBased in new[] { false, true })
                {
                    var assembled = partBased ? PartBasedBuilder.Build(appearance, content)! : PartBasedBuilder.WithHeldItems(original, appearance, content);
                    var mesh = assembled.Parts.Single(p => p.Name == "marker");
                    Check(assembled.Parts[mesh.Parent].Name == expected, $"{(partBased ? "part" : "whole")}: row {row}, slot {slot} attaches to {expected}");
                }
            }
            Check(original.Parts.All(p => p.Name != "marker"), "cached skeleton remains unchanged");
            var hidden = new Appearance { Equipment = new() { new ItemVisual { BaseItem = 200, Slot = 0x20 } } };
            Check(PartBasedBuilder.WithHeldItems(original, hidden, content, 0x20).Parts.All(p => p.Name != "marker"), "hidden equipment remains hidden");
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[shield-attachment] {checks.Count} checks passed");
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString());
            GD.PrintErr(e);
            return 1;
        }
    }
}
