using System.Numerics;

namespace OpenWinter.Content;

/// <summary>
/// A classic interface layout: a panel (an interface model) and the controls placed on it. Positions are
/// in interface units (100 px at the original's 1x scale), y up, relative to the model node named by
/// <see cref="GuiNode.ParentHook"/> (the panel's own origin when empty or not found).
/// </summary>
public sealed class GuiLayout
{
    public string Name = "";
    public GuiNode Root = new();

    /// <summary>Every node, depth first (the root first).</summary>
    public IEnumerable<GuiNode> All()
    {
        var stack = new Stack<GuiNode>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            for (int i = n.Children.Count - 1; i >= 0; i--) stack.Push(n.Children[i]);
        }
    }

    public GuiNode? Find(string tag) => All().FirstOrDefault(n => string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Kinds of control, as the layout files number them.</summary>
public enum GuiKind
{
    /// <summary>A control the game fills itself (portraits, item grids, model viewers).</summary>
    Custom = 0,
    /// <summary>A button without caption (close, icon buttons).</summary>
    Button = 1,
    CheckBox = 2,
    Panel = 3,
    Label = 5,
    Slider = 6,
    /// <summary>List boxes, text areas and edit boxes.</summary>
    ListBox = 8,
    /// <summary>A button with a caption.</summary>
    TextButton = 9,
}

public sealed class GuiNode
{
    public string Tag = "";
    public GuiKind Kind;
    /// <summary>The interface model drawn for this control (empty for labels).</summary>
    public string ResRef = "";
    public string Parent = "";
    public string ParentHook = "";
    public int Layer;
    public bool Locked;
    public Vector3 Position;
    public bool HasPosition;
    /// <summary>Label rectangle (bottom-left corner and size), for controls that carry text.</summary>
    public Vector3 LabelPosition;
    public Vector2 LabelSize;
    public bool HasLabel;
    public GuiCaption? Caption;
    public bool? Checked;
    public int? Length;
    public readonly List<GuiNode> Children = new();
}

public sealed class GuiCaption
{
    /// <summary>Talk-table string, or uint.MaxValue for none.</summary>
    public uint StrRef = uint.MaxValue;
    public string Text = "";
    public string Font = "";
    public Vector4 Color = Vector4.One;
    /// <summary>0 left/bottom, 0.5 centre, 1 right/top.</summary>
    public float AlignH, AlignV;
}
