using OpenWinter.Aurora.Resources;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// Development capture of the classic interface pieces (--capture-classic dir [--classic-only a,b]): every layout
/// file of the base game, and any extra models given with --classic-models, each drawn alone on grey at the
/// original's 1x scale and saved as PNG, for comparison with the original game.
/// </summary>
public static class ClassicCapture
{
    public static async Task Run(Node host, string dir, string? only, string? models, Func<Task> frame, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        var content = await FrontEnd.FrontEndContent.BaseAsync;
        if (content == null) { GD.PrintErr("[classic] no game data"); quit(); return; }
        var assets = ClassicAssets.For(content);
        var layer = new CanvasLayer { Layer = 50, Name = "ClassicCapture" };
        host.AddChild(layer);
        var bg = new ColorRect { Color = new Color(0.25f, 0.25f, 0.27f), AnchorRight = 1, AnchorBottom = 1 };
        layer.AddChild(bg);
        var wanted = only?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = content.Resources.All(ResType.Gui).Select(k => k.ResRef).Distinct().OrderBy(n => n).Where(n => wanted == null || wanted.Contains(n)).ToList();
        foreach (var name in names)
        {
            var panel = GuiPanel.FromLayout(assets, name);
            if (panel == null) continue;
            await Show(panel, name);
        }
        foreach (var model in (models ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            await Show(GuiPanel.FromModel(assets, model), "model_" + model);
        quit();

        async Task Show(GuiPanel panel, string name)
        {
            layer.AddChild(panel);
            var view = host.GetViewport().GetVisibleRect().Size;
            panel.Position = ((view - panel.Size) / 2).Floor();
            for (int i = 0; i < 3; i++) await frame();
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = host.GetViewport().GetTexture().GetImage();
            // Crop to the panel plus a margin.
            var r = new Rect2I((Vector2I)panel.Position - new Vector2I(8, 8), (Vector2I)panel.Size + new Vector2I(16, 16)).Intersection(new Rect2I(Vector2I.Zero, img.GetSize()));
            if (r.Size.X > 0 && r.Size.Y > 0) img = img.GetRegion(r);
            img.SavePng(System.IO.Path.Combine(dir, name + ".png"));
            GD.Print($"[classic] {name} {panel.Size.X}x{panel.Size.Y}");
            layer.RemoveChild(panel);
            panel.QueueFree();
        }
    }
}
