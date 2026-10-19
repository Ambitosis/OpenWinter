using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// Base for every rendered NUI element. Handles the properties all elements share
/// (visible, enabled, tooltip, disabled_tooltip, encouraged, draw lists), mouse
/// events for tagged elements (mousedown / mouseup / mousescroll), and bind
/// refresh. <see cref="Row"/> is the list row this element renders (-1 outside lists).
/// </summary>
public partial class NuiElement : Control
{
    public NuiNode Node = null!;
    public NuiContext Ctx = null!;
    public int Row = -1;
    /// <summary>Child elements laid out from their nodes' frames.</summary>
    protected readonly List<NuiElement> Kids = new();
    NuiDrawLayer? _before, _after;
    protected bool Hover;
    double _time;

    static readonly HashSet<string> Unknown = new();

    public static NuiElement Create(NuiNode node, NuiContext ctx, int row = -1)
    {
        NuiElement e = node.Type switch
        {
            "row" or "col" => new NuiLine(),
            "spacer" => new NuiElement(),
            "group" => new NuiGroupView(),
            "label" => new NuiLabelView(),
            "text" => new NuiTextView(),
            "button" or "button_image" or "button_select" => new NuiButtonView(),
            "check" => new NuiCheckView(),
            "image" => new NuiImageView(),
            "combo" => new NuiComboView(),
            "options" => new NuiOptionsView(),
            "tabbar" => new NuiTogglesView(),
            "slider" or "sliderf" => new NuiSliderView(),
            "progress" => new NuiProgressView(),
            "textedit" => new NuiTextEditView(),
            "list" => new NuiListView(),
            "color_picker" => new NuiColorPickerView(),
            "chart" => new NuiChartView(),
            _ => new NuiElement(),
        };
        if (node.Type.Length > 0 && e.GetType() == typeof(NuiElement) && node.Type != "spacer" && Unknown.Add(node.Type))
            GD.PrintErr($"[nui] unknown element type '{node.Type}' (rendered as an empty element)");
        e.Node = node;
        e.Ctx = ctx;
        e.Row = row;
        e.Name = node.Id.Length > 0 ? node.Id.Replace('/', '_').Replace(':', '_').Replace('@', '_') : node.Type;
        return e;
    }

    /// <summary>Containers let clicks through to the window; widgets take them.</summary>
    protected virtual bool TakesMouse => Node.Type != "spacer" || Node.Id.Length > 0;

    public override void _Ready()
    {
        ClipContents = false;
        MouseFilter = TakesMouse ? MouseFilterEnum.Stop : MouseFilterEnum.Pass;
        if (Node["draw_list"] is JsonArray list && list.Count > 0)
        {
            bool scissor = Ctx.Binds.Bool(Node["draw_list_scissor"], Row, false);
            _before = new NuiDrawLayer { Element = this, Items = list, Order = -1, Scissor = scissor, ShowBehindParent = true };
            AddChild(_before);
        }
        Build();
        foreach (var k in Kids)
            if (k.GetParent() == null) AddChild(k);
        if (_before != null) _after = new NuiDrawLayer { Element = this, Items = (JsonArray)Node["draw_list"]!, Order = 1, Scissor = _before.Scissor };
        if (_after != null) AddChild(_after);
        MouseEntered += () => { Hover = true; HoverChanged(); };
        MouseExited += () => { Hover = false; HoverChanged(); };
        Ctx.Watch(this, OwnBinds());
        SetProcess(Node["encouraged"] != null);
        Refresh();
    }

    public override void _ExitTree() => Ctx.Unwatch(this);

    /// <summary>Binds this element reads (child elements register their own).</summary>
    protected virtual IEnumerable<string> OwnBinds() => NuiBinds.BindNames(Node.Json, "children", "row_template");

    /// <summary>Creates sub-controls and child elements (add children to <see cref="Kids"/>).</summary>
    protected virtual void Build() { }

    /// <summary>Re-reads bound values.</summary>
    public virtual void Refresh()
    {
        Visible = Bool("visible", true);
        bool enabled = Bool("enabled", true);
        Modulate = enabled ? Colors.White : new Color(0.62f, 0.62f, 0.62f);
        string tip = Text("tooltip");
        if (!enabled && Node["disabled_tooltip"] != null) tip = Text("disabled_tooltip");
        TooltipText = tip;
        if (_before != null) { _before.Scissor = _after!.Scissor = Ctx.Binds.Bool(Node["draw_list_scissor"], Row, false); _before.Refresh(); _after.Refresh(); }
        QueueRedraw();
    }

    /// <summary>Places this element at its node's frame, then its children.</summary>
    public virtual void ApplyLayout()
    {
        Position = new Vector2(Node.Frame.X, Node.Frame.Y);
        Size = new Vector2(Node.Frame.W, Node.Frame.H);
        foreach (var k in Kids) k.ApplyLayout();
        _before?.Fit();
        _after?.Fit();
        QueueRedraw();
    }

    protected virtual void HoverChanged()
    {
        QueueRedraw();
        _before?.QueueRedraw();
        _after?.QueueRedraw();
    }

    // ----- property helpers (row-aware) -----

    protected JsonNode? Prop(string key) => Node[key];
    protected string Text(string key, string fallback = "") => Ctx.Binds.Text(Node[key], Row, fallback);
    protected bool Bool(string key, bool fallback) => Node[key] == null ? fallback : Ctx.Binds.Bool(Node[key], Row, fallback);
    protected int Int(string key, int fallback = 0) => Node[key] == null ? fallback : Ctx.Binds.Int(Node[key], Row, fallback);
    protected double Float(string key, double fallback = 0) => Node[key] == null ? fallback : Ctx.Binds.Float(Node[key], Row, fallback);

    /// <summary>NuiStyleForegroundColor, or the given default.</summary>
    protected Color Foreground(Color fallback) => Ctx.Binds.Color(Node["foreground_color"], Row) is { } c ? NuiSkin.ToColor(c) : fallback;

    /// <summary>Whether this element and all its ancestors are enabled and the window takes input.</summary>
    public bool Interactive
    {
        get
        {
            if (!Ctx.Window.AcceptsInput) return false;
            for (Node? n = this; n != null && n is not NuiWindowView; n = n.GetParent())
                if (n is NuiElement e && !e.Bool("enabled", true)) return false;
            return true;
        }
    }

    /// <summary>The user changed this element's value property.</summary>
    protected void WriteValue(string key, JsonNode? value) => Ctx.Write(Node[key], Row, value);

    protected void SendEvent(string type, JsonNode? payload = null) => Ctx.Send(type, Node.Id, Row, payload);

    // ----- input -----

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton mb) return;
        if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
        {
            if (!mb.Pressed) return;
            float dy = mb.ButtonIndex == MouseButton.WheelUp ? 1 : mb.ButtonIndex == MouseButton.WheelDown ? -1 : 0;
            float dx = mb.ButtonIndex == MouseButton.WheelLeft ? -1 : mb.ButtonIndex == MouseButton.WheelRight ? 1 : 0;
            if (Interactive) SendEvent("mousescroll", new JsonObject { ["mouse_scroll"] = new NuiVec(dx, dy).ToJson() });
            if (!OnWheel(dx, dy)) ScrollAncestor(dx, dy);
            AcceptEvent();
            return;
        }
        int btn = mb.ButtonIndex switch { MouseButton.Left => 0, MouseButton.Middle => 1, MouseButton.Right => 2, _ => -1 };
        if (btn < 0) return;
        Ctx.Window.Activate();
        if (Interactive)
        {
            SendEvent(mb.Pressed ? "mousedown" : "mouseup", new JsonObject
            {
                ["mouse_btn"] = btn,
                ["mouse_pos"] = new NuiVec(mb.Position.X, mb.Position.Y).ToJson(),
            });
            OnMouse(mb, btn);
        }
        if (TakesMouse) AcceptEvent();
    }

    /// <summary>Widget-specific mouse handling (only called when interactive).</summary>
    protected virtual void OnMouse(InputEventMouseButton mb, int button) { }

    /// <summary>Widget-specific wheel handling; return true when consumed.</summary>
    protected virtual bool OnWheel(float dx, float dy) => false;

    void ScrollAncestor(float dx, float dy)
    {
        for (var n = GetParent(); n != null && n is not NuiWindowView; n = n.GetParent())
            if (n is NuiScrollArea s && s.ScrollBy(dx, dy)) return;
    }

    public override void _Process(double delta)
    {
        if (!Bool("encouraged", false)) return;
        _time += delta;
        QueueRedraw();
    }

    /// <summary>The breathing glow of NuiEncouraged elements.</summary>
    protected void DrawEncouraged()
    {
        if (!Bool("encouraged", false)) return;
        float a = 0.18f + 0.18f * MathF.Sin((float)_time * 3.5f);
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r.Grow(-2), new Color(1f, 0.85f, 0.45f, a));
        DrawRect(r.Grow(-1), new Color(1f, 0.85f, 0.45f, a + 0.2f), false, 2);
    }
}

/// <summary>NuiRow / NuiCol: lays out children (done by the model), draws nothing itself.</summary>
public partial class NuiLine : NuiElement
{
    protected override bool TakesMouse => false;

    protected override void Build()
    {
        foreach (var c in Node.Children) Kids.Add(Create(c, Ctx, Row));
    }
}

/// <summary>NuiGroup: optional border, scrollable content holding one child.</summary>
public partial class NuiGroupView : NuiElement
{
    NuiScrollArea _scroll = null!;
    protected override bool TakesMouse => false;

    protected override void Build()
    {
        _scroll = new NuiScrollArea { Skin = Ctx.Skin };
        AddChild(_scroll);
        RebuildChild();
    }

    void RebuildChild()
    {
        foreach (var k in Kids) k.QueueFree();
        Kids.Clear();
        foreach (var c in Node.Children)
        {
            var e = Create(c, Ctx, Row);
            Kids.Add(e);
            _scroll.Content.AddChild(e);
        }
    }

    /// <summary>NuiSetGroupLayout: swaps the group's content.</summary>
    public void SetLayout(NuiNode child)
    {
        Node.ReplaceChild(child);
        RebuildChild();
    }

    public override void ApplyLayout()
    {
        Position = new Vector2(Node.Frame.X, Node.Frame.Y);
        Size = new Vector2(Node.Frame.W, Node.Frame.H);
        float pad = Node.Padding;
        _scroll.Position = new Vector2(pad, pad);
        _scroll.Size = new Vector2(Math.Max(0, Node.Frame.W - 2 * pad), Math.Max(0, Node.Frame.H - 2 * pad));
        _scroll.SetContent(new Vector2(Node.ContentSize.X, Node.ContentSize.Y), Node.ShowScrollX, Node.ShowScrollY);
        foreach (var k in Kids) k.ApplyLayout();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Node.Border) DrawStyleBox(Ctx.Skin.Group, new Rect2(Vector2.Zero, Size));
    }
}
