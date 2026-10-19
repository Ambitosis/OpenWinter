using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// Where an item or gold attached to the pointer from outside a window lives (a ground item picked up with the
/// inventory open, gold counted out of the amount dialog). Godot draws a drag preview in the canvas layer of the
/// control that started the drag, so a carry started from the root window sat under every window on a canvas layer
/// (the inventory, the dialogs, the classic interface) and the carried icon was hidden behind them. This layer is
/// above all of them and below only the pointer itself (HudParts.Cursors, layer 200).
/// </summary>
public static class PointerCarry
{
    public const int Layer = 150;
    const string LayerName = "PointerCarry";

    /// <summary>Adds the carry control to the pointer-carry layer, creating the layer on first use.</summary>
    public static void Add(Node anyNode, Control carry)
    {
        var root = anyNode.GetTree().Root;
        if (root.GetNodeOrNull<CanvasLayer>(LayerName) is not { } layer)
        {
            layer = new CanvasLayer { Name = LayerName, Layer = Layer };
            root.AddChild(layer);
        }
        layer.AddChild(carry);
    }
}
