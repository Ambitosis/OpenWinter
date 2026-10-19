using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// A panel of the original interface: its model drawn as the frame, and either the controls its layout file
/// (.gui) places on it, or none (screens the original lays out in code: add controls at the model's nodes with
/// <see cref="Node"/> / <see cref="At"/>). Controls are reachable by their layout tag with <see cref="Get{T}"/>.
/// Layout positions are relative to a node of the containing control's model (the "parent hook"), y up, in
/// interface units; each control's model origin is its bottom-left.
/// </summary>
public partial class GuiPanel : Control
{
    public ClassicAssets Assets = null!;
    public GuiLayout? Layout;
    public GuiModelView Frame = null!;
    readonly Dictionary<string, Control> _byTag = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Each control's model origin, in this panel's coordinates.</summary>
    readonly Dictionary<Control, Vector2> _origins = new();

    /// <summary>Called when a button from the layout is pressed, with its tag.</summary>
    public event Action<string>? ButtonPressed;

    /// <summary>A panel built from a layout file; null when the game has no such layout.</summary>
    public static GuiPanel? FromLayout(ClassicAssets assets, string layout)
    {
        if (assets.Layout(layout) is not { } l) return null;
        var p = new GuiPanel { Assets = assets, Layout = l, Name = layout };
        p.Build();
        return p;
    }

    /// <summary>A bare panel: just the model as its frame.</summary>
    public static GuiPanel FromModel(ClassicAssets assets, string model)
    {
        var p = new GuiPanel { Assets = assets, Name = model };
        p.Frame = GuiModelView.Create(assets, model);
        p.AddChild(p.Frame);
        p.Frame.Position = Vector2.Zero;
        p.Size = p.CustomMinimumSize = p.Frame.Size;
        p.MouseFilter = MouseFilterEnum.Stop;
        return p;
    }

    /// <summary>The frame model's origin in panel coordinates.</summary>
    public Vector2 ModelOrigin => Frame.Position + Frame.Origin;

    /// <summary>A node of the frame model in panel coordinates (null when missing).</summary>
    public Vector2? Node(string name) => Frame.Node(name) is { } n ? Frame.Position + n : null;

    /// <summary>A point given in interface units from a node of the frame (or its origin), y up, in panel coordinates.</summary>
    public Vector2 At(string? node, float x, float y) =>
        (node != null && Node(node) is { } n ? n : ModelOrigin) + new Vector2(x, -y) * GuiModel.PixelsPerUnit;

    public T? Get<T>(string tag) where T : Control => _byTag.TryGetValue(tag, out var c) ? c as T : null;
    public Control? Get(string tag) => _byTag.GetValueOrDefault(tag);
    public IEnumerable<string> Tags => _byTag.Keys;

    /// <summary>Registers a control built in code under a tag.</summary>
    public void Register(string tag, Control c) => _byTag[tag] = c;

    void Build()
    {
        var root = Layout!.Root;
        Frame = GuiModelView.Create(Assets, root.ResRef);
        Frame.Name = "Frame";
        AddChild(Frame);
        Frame.Position = Vector2.Zero;
        Size = CustomMinimumSize = Frame.Size;
        MouseFilter = MouseFilterEnum.Stop;
        _byTag[root.Tag] = Frame;
        _origins[Frame] = Frame.Origin;
        // Lower layers first so higher ones draw (and take clicks) on top.
        foreach (var child in root.Children.OrderBy(c => c.Layer))
            Add(child, Frame.Model, Frame.Origin);
        // Grow to include controls outside the frame (the original's panels hang some off their edge).
        var bounds = new Rect2(Vector2.Zero, Size);
        foreach (var c in GetChildren().OfType<Control>()) bounds = bounds.Merge(new Rect2(c.Position, c.Size));
        if (bounds.Position != Vector2.Zero)
        {
            foreach (var c in GetChildren().OfType<Control>()) c.Position -= bounds.Position;
            foreach (var c in _origins.Keys.ToArray()) _origins[c] -= bounds.Position;
        }
        Size = CustomMinimumSize = bounds.Size;
    }

    /// <summary>Instantiates a layout node; <paramref name="parentModel"/> holds the hook node, drawn with its origin at <paramref name="parentOrigin"/>.</summary>
    void Add(GuiNode n, GuiModel? parentModel, Vector2 parentOrigin)
    {
        var hook = parentModel?.Node(n.ParentHook) ?? Vector2.Zero;
        var origin = parentOrigin + hook + new Vector2(n.Position.X, -n.Position.Y) * GuiModel.PixelsPerUnit;
        Control? control = null;
        GuiModel? model = null;
        string caption = Assets.Text(n.Caption);
        switch (n.Kind)
        {
            case GuiKind.Label:
            {
                var font = Assets.Font(n.Caption?.Font ?? "");
                // The label box: (x, y) is its top-left corner, y up; its caption's vertical alignment runs from the
                // top (0) down (measured: inventory weights, the death panel's message, the options title).
                var tl = parentOrigin + hook + new Vector2(n.LabelPosition.X, -n.LabelPosition.Y) * GuiModel.PixelsPerUnit;
                var size = new Vector2(n.LabelSize.X, n.LabelSize.Y) * GuiModel.PixelsPerUnit;
                var l = new GuiLabel
                {
                    Assets = Assets, Font = font, Text = caption, Name = n.Tag,
                    Color = Color(n.Caption), AlignH = n.Caption?.AlignH ?? 0, AlignV = n.Caption?.AlignV ?? 0.5f,
                    Wrap = size.Y > font.LineHeight * 1.8f,
                };
                l.Position = tl;
                l.Size = size;
                control = l;
                break;
            }
            case GuiKind.Button or GuiKind.TextButton or GuiKind.CheckBox:
            {
                var b = GuiButton.Create(Assets, n.ResRef, caption, n.Caption?.Font ?? "");
                b.Name = n.Tag;
                b.CaptionColor = Color(n.Caption);
                if (n.Caption != null) { b.AlignH = n.Caption.AlignH; b.AlignV = 1 - n.Caption.AlignV; }
                if (n.Kind == GuiKind.CheckBox) { b.ToggleMode = true; b.ButtonPressed = n.Checked == true; }
                b.PlaceOrigin(origin);
                string tag = n.Tag;
                b.Pressed += () => ButtonPressed?.Invoke(tag);
                model = b.Model;
                control = b;
                break;
            }
            case GuiKind.Slider:
            {
                var s = GuiSlider.Create(Assets, n.ResRef);
                s.Name = n.Tag;
                if (n.Length is > 0 and var len) s.MaxValue = len;
                s.PlaceOrigin(origin);
                model = s.Model;
                control = s;
                break;
            }
            case GuiKind.ListBox:
            {
                var list = GuiList.Create(Assets, n.ResRef);
                list.Name = n.Tag;
                list.PlaceOrigin(origin);
                model = list.Frame?.Model;
                control = list;
                break;
            }
            default:
            {
                // Panels and game-filled controls: the model; the screen fills it.
                var v = GuiModelView.Create(Assets, n.ResRef);
                v.Name = n.Tag;
                v.PlaceOrigin(origin);
                model = v.Model;
                control = v;
                break;
            }
        }
        AddChild(control);
        if (n.Tag.Length > 0) _byTag.TryAdd(n.Tag, control);
        _origins[control] = origin;
        foreach (var child in n.Children.OrderBy(c => c.Layer))
            Add(child, model, origin);
    }

    /// <summary>A control's model origin in panel coordinates.</summary>
    public Vector2 OriginOf(Control c) => _origins.TryGetValue(c, out var o) ? o : c.Position;

    static Color Color(GuiCaption? c) => c == null ? Colors.White : new Color(c.Color.X, c.Color.Y, c.Color.Z, c.Color.W <= 0 ? 1 : c.Color.W);
}
