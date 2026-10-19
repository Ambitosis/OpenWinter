using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// --capture-radial dir: drives the radial menu through real mouse input (right-click on yourself, then a
/// left-click on a wedge) and logs what happened, to check the whole input path end to end.
/// </summary>
public static class RadialCapture
{
    static async Task Click(Node n, MouseButton button, Vector2 at)
    {
        var tree = n.GetTree();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at, GlobalPosition = at, ButtonMask = mask });
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = at, GlobalPosition = at });
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    public static async Task Run(string dir, ClientSession session, Camera3D camera, RadialMenu radial,
        Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(4.0);
        var me = session.Controlled!;
        var at = camera.UnprojectPosition(me.GlobalPosition + Vector3.Up * 1.0f);
        GD.Print($"[radial] right-click on self at {at}");
        await Click(radial, MouseButton.Right, at);
        await wait(0.5);
        GD.Print($"[radial] open={radial.Visible} wedges=[{string.Join(", ", radial.Labels)}]");
        await shot(dir, "r1_open");
        GD.Print($"[radial] rect={radial.GetGlobalRect()} visibleInTree={radial.IsVisibleInTree()} filter={radial.MouseFilter} parent={radial.GetParent()?.GetType().Name} layer={(radial.GetCanvasLayerNode()?.Layer)}");
        foreach (var layer in radial.GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>())
            GD.Print($"[radial] layer {layer.Name} {layer.Layer} visible={layer.Visible}");
        foreach (var label in new[] { "Character Sheet" })
        {
            if (radial.WedgePoint(label) is not { } p) { GD.Print($"[radial] no '{label}' wedge"); continue; }
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
            await radial.ToSignal(radial.GetTree(), SceneTree.SignalName.ProcessFrame);
            var hovered = radial.GetViewport().GuiGetHoveredControl();
            GD.Print($"[radial] left-click '{label}' at {p}; control under the mouse: {hovered?.GetType().Name} '{hovered?.Name}' path {hovered?.GetPath()}");
            await Click(radial, MouseButton.Left, p);
            await wait(1.5);
            GD.Print($"[radial] after '{label}': open={radial.Visible}");
            await shot(dir, "r2_after_" + label.ToLowerInvariant());
        }
        quit();
    }
}
