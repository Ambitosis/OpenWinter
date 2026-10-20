using System.Text.Json;
using OpenWinter.Client.Rendering;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>Actual depth, backface and alpha-cutout coverage when discovery changes.</summary>
public static class TileOcclusionProbe
{
    public static async Task<int> Run(Node owner, string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        try
        {
            var camera = new Camera3D
            {
                Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 6,
                Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Blue }
            };
            owner.AddChild(camera);
            var red = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.Red };
            var target = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(5, 5, .1f), Material = red }, Position = new Vector3(0, 0, -6) };
            owner.AddChild(target);
            var root = new Node3D(); owner.AddChild(root);
            var green = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.Green };
            var wall = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(4, 4), Material = green }, Position = new Vector3(0, 0, -4) };
            root.AddChild(wall);
            var details = new Node3D(); root.AddChild(details);
            var light = new OmniLight3D { Visible = false }; details.AddChild(light);
            Keyhole.Apply(root);
            var original = wall.GetSurfaceOverrideMaterial(0);
            var exploration = new TileExploration(root, details);
            async Task<Image> Shot(string name)
            {
                for (int i = 0; i < 6; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var image = owner.GetViewport().GetTexture().GetImage();
                image.SavePng(Path.Combine(directory, name + ".png"));
                return image;
            }
            Color Centre(Image image) => image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2);
            bool Black(Color c) => c.R < .02f && c.G < .02f && c.B < .02f;
            bool Red(Color c) => c.R > .9f && c.G < .02f && c.B < .02f;
            exploration.SetExplored(true);
            Check(Centre(await Shot("known")).G > .4f, "discovered surface drawn");
            exploration.SetExplored(false);
            Check(Black(Centre(await Shot("unknown"))), "unknown surface blocks red scenery with black depth");
            Check(!details.Visible && wall.Visible, "effects hidden while geometry remains");
            wall.Visible = false;
            exploration.SetExplored(true); exploration.SetExplored(false);
            Check(!wall.Visible && Red(Centre(await Shot("faded"))), "discovery preserves independent wall fade");
            wall.Visible = true;
            exploration.SetExplored(true);
            Check(wall.GetSurfaceOverrideMaterial(0) == original, "exact original keyhole material restored");
            Check(details.Visible && !light.Visible, "reveal preserves disabled light state");
            Check(Centre(await Shot("restored")).G > .4f, "revealed surface recovers colour");
            exploration.SetExplored(false);
            camera.Position = new Vector3(0, 0, -10); camera.LookAt(Vector3.Zero);
            target.Position = new Vector3(0, 0, -1);
            Check(Red(Centre(await Shot("backface"))), "unknown surface retains one-sided culling");
            camera.Position = Vector3.Zero; camera.Rotation = Vector3.Zero; target.Position = new Vector3(0, 0, -6);
            // An authored alpha hole must remain a hole; its solid half still blocks scenery.
            var pixels = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            pixels.Fill(Colors.White);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 8; x++) pixels.SetPixel(x, y, Colors.Transparent);
            var cutout = (StandardMaterial3D)green.Duplicate();
            cutout.AlbedoTexture = ImageTexture.CreateFromImage(pixels);
            cutout.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            cutout.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            wall.Mesh = new QuadMesh { Size = new Vector2(4, 4), Material = cutout };
            wall.SetSurfaceOverrideMaterial(0, null); Keyhole.Apply(root);
            var replacement = new TileExploration(root, details); replacement.SetExplored(false);
            var alpha = await Shot("cutout");
            int cx = alpha.GetWidth() / 2, cy = alpha.GetHeight() / 2, dx = alpha.GetHeight() / 6;
            Check(Red(alpha.GetPixel(cx - dx, cy)) && Black(alpha.GetPixel(cx + dx, cy)), "transparent texels pass scenery; solid texels occlude");
            root.Free(); target.Free(); camera.Free();
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[tile-occlusion] {checks.Count} checks passed");
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = false, checks, error = e.ToString() }));
            GD.PushError(e.ToString()); return 1;
        }
    }
}
