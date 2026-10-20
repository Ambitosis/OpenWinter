using OpenWinter.Client.Views;
using Godot;
using OpenWinter.Content;

namespace OpenWinter.Client.Rendering;

/// <summary>Rendered occlusion control across perception and effect fading.</summary>
public static class HiliteOcclusionProbe
{
    public static async Task<int> Run(Node root, string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var camera = new Camera3D
        {
            Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 6,
            Near = .1f, Far = 50, Environment = new Godot.Environment
            { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black }
        };
        root.AddChild(camera);
        var actor = new ObjectView { Position = new Vector3(0, 0, -8) };
        root.AddChild(actor);
        actor.SetProcess(false);
        actor.AddChild(new MeshInstance3D
        {
            Name = "Body", Mesh = new BoxMesh { Size = new Vector3(2, 2, 1) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(.2f, .2f, .2f)
            }
        });
        // Hide the left half; the right half is a positive visible-highlight control.
        // Synthetic texture goes through the production world-material path; no resource lookup is needed.
        var textures = new TextureFactory(null!);
        var pixels = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        pixels.Fill(Colors.White);
        textures.Put("probe_wall", pixels, true).Data!.Info.HasInfo = true;
        var material = (StandardMaterial3D)new MaterialFactory(textures).Get(new MeshData { Texture = "probe_wall" }, MaterialKind.World)!;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = new Color(.1f, .1f, .1f);
        var wall = new MeshInstance3D
        {
            Position = new Vector3(-1, 0, -5), Mesh = new BoxMesh { Size = new Vector3(2, 5, .2f) },
            MaterialOverride = material
        };
        root.AddChild(wall);
        root.AddChild(new HiliteGlow { Camera = camera });
        async Task<Image> Capture(string name)
        {
            for (int i = 0; i < 6; i++) await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
            await root.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = root.GetViewport().GetTexture().GetImage();
            image.SavePng(System.IO.Path.Combine(directory, name + ".png"));
            return image;
        }
        var records = new List<object>();
        bool passed = true;
        foreach (var keyholed in new[] { false, true })
            foreach (var stage in new[] { "seen", "heard", "effect", "restored" })
            {
                wall.MaterialOverride = keyholed ? Keyhole.Convert(material) : material;
                string name = (keyholed ? "keyholed_" : "blended_") + stage;
                Hilite.Remove(actor);
                actor.SetPerception(stage == "heard" ? false : true, true);
                actor.SetEffectAlpha(1, stage == "effect" ? .25f : null);
                var plain = await Capture(name + "_plain");
                Hilite.Apply(actor, new Color(0, 1, 0));
                // Exercise updates after the twins already exist as well as creation.
                actor.SetPerception(stage == "heard" ? false : true, true);
                Hilite.Apply(actor, new Color(0, 1, 0));
                var lit = await Capture(name + "_highlight");
                float hidden = 0, visible = 0;
                int w = lit.GetWidth(), h = lit.GetHeight();
                for (int y = h / 3; y < 2 * h / 3; y++)
                    for (int x = w / 2 - h / 6; x < w / 2 - h / 20; x++)
                        hidden = Math.Max(hidden, Math.Abs(lit.GetPixel(x, y).G - plain.GetPixel(x, y).G));
                for (int y = h / 3; y < 2 * h / 3; y++)
                    for (int x = w / 2 + h / 20; x < w / 2 + h / 5; x++)
                        visible = Math.Max(visible, lit.GetPixel(x, y).G - plain.GetPixel(x, y).G);
                bool ok = hidden <= 1f / 255 && visible > .02f;
                passed &= ok;
                records.Add(new { stage, keyholed, alpha = actor.PresentationAlpha, hidden, visible, ok });
            }
        // Genuine translucency must still transmit the model; only solid texels enter the prepass.
        foreach (bool keyholed in new[] { false, true })
        {
            Hilite.Remove(actor);
            var glass = (StandardMaterial3D)material.Duplicate();
            glass.AlbedoColor = new Color(.1f, .1f, .1f, .4f);
            wall.MaterialOverride = keyholed ? Keyhole.Convert(glass) : glass;
            actor.Visible = false;
            var background = await Capture($"glass_{keyholed}_background");
            actor.Visible = true;
            var foreground = await Capture($"glass_{keyholed}_foreground");
            int x = foreground.GetWidth() / 2 - foreground.GetHeight() / 12, y = foreground.GetHeight() / 2;
            float transmitted = foreground.GetPixel(x, y).R - background.GetPixel(x, y).R;
            bool ok = transmitted > .02f;
            passed &= ok;
            records.Add(new { stage = "glass", keyholed, transmitted, ok });
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "result.json"),
            System.Text.Json.JsonSerializer.Serialize(new { passed, records }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"[hilite-occlusion] passed={passed}");
        return passed ? 0 : 1;
    }
}
