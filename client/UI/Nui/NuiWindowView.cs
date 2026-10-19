using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// One NUI window: frame, title bar (collapse, title, close), the element tree,
/// and the window properties of NuiWindow (geometry, resizable, collapsed,
/// closable, transparent, border, accepts_input, size and edge constraints).
/// Moving or resizing writes the actual rectangle back to a bound geometry;
/// collapsing writes a bound collapsed flag.
/// </summary>
public partial class NuiWindowView : Control
{
    public NuiManager Manager = null!;
    public int Token;
    public string WindowId = "";
    public JsonObject Json = new();
    public readonly NuiBinds Binds = new();
    public NuiContext Ctx = null!;

    NuiNode _root = new();
    NuiElement? _rootView;
    readonly Control _content = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    NuiRect _rect;
    bool _collapsed;
    bool _dragging;
    Vector2 _dragStart, _rectStart;
    bool _hoverClose, _hoverCollapse;
    HashSet<string> _windowBinds = new();

    public bool AcceptsInput => Binds.Bool(Json["accepts_input"], -1, true);
    public bool Closable => Json["closable"] == null || Binds.Bool(Json["closable"], -1, true);
    public bool Resizable => (Json["resizable"] is null || Binds.Resolve(Json["resizable"]) is null || Binds.Bool(Json["resizable"], -1, true)) && AcceptsInput;
    bool CanCollapse => Json["collapsed"] is null || NuiBinds.IsBind(Json["collapsed"], out _) || Binds.Resolve(Json["collapsed"]) is null || Binds.Bool(Json["collapsed"], -1, false);
    bool Transparent => Binds.Bool(Json["transparent"], -1, false);
    bool HasBorder => Json["border"] == null || Binds.Bool(Json["border"], -1, true);
    NuiRect? SizeConstraint => Binds.Rect(Json["size_constraint"]);
    NuiRect? EdgeConstraint => Binds.Rect(Json["edge_constraint"]);

    /// <summary>Title bar hidden: title false, collapsing disabled and not closable.</summary>
    public bool HasTitleBar => !(Binds.Resolve(Json["title"]) is JsonValue t && t.TryGetValue<bool>(out var b) && !b && !CanCollapse && !Closable);

    float TitleH => HasTitleBar ? NuiStyle.TitleHeight : 0;
    public NuiRect Rect => _rect;
    public NuiElement? RootView => _rootView;

    public void Init(NuiManager manager, int token, string id, JsonObject json)
    {
        Manager = manager;
        Token = token;
        WindowId = id;
        Json = json;
        Name = $"nui_{token}_{(id.Length > 0 ? id : "window")}";
        Binds.StrRef = s => manager.Session.Content?.Resources.Talk.Get(s) ?? "";
        Ctx = new NuiContext
        {
            Token = token,
            Binds = Binds,
            Skin = manager.Skin,
            Window = this,
            Event = (type, element, index, payload) => manager.SendEvent(token, type, element, index, payload),
            BindToServer = (bind, value) => manager.SendBind(token, bind, value),
        };
        _windowBinds = NuiBinds.BindNames(Json, "root").ToHashSet(StringComparer.Ordinal);
        Binds.Changed += OnBindChanged;
    }

    public override void _Ready()
    {
        MouseFilter = AcceptsInput ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        AddChild(_content);
        _root = NuiNode.Parse(Json["root"]);
        BuildRoot();
        _collapsed = Binds.Bool(Json["collapsed"], -1, false);
        Place();
    }

    void BuildRoot()
    {
        _rootView?.QueueFree();
        _rootView = NuiElement.Create(_root, Ctx);
        _content.AddChild(_rootView);
        if (!AcceptsInput) IgnoreInput(this);
    }

    static void IgnoreInput(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Control ctl) ctl.MouseFilter = MouseFilterEnum.Ignore;
            IgnoreInput(c);
        }
    }

    void OnBindChanged(string name)
    {
        Ctx.Refresh(name);
        if (!_windowBinds.Contains(name)) return;
        if (NuiBinds.IsBind(Json["geometry"], out var g) && g == name && !_dragging) Place();
        if (NuiBinds.IsBind(Json["collapsed"], out var c) && c == name) SetCollapsed(Binds.Bool(Json["collapsed"], -1, false), false);
        if (NuiBinds.IsBind(Json["accepts_input"], out var a) && a == name)
        {
            MouseFilter = AcceptsInput ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            if (!AcceptsInput) IgnoreInput(this);
        }
        QueueRedraw();
    }

    /// <summary>Applies the geometry property (centering rules and constraints), writing the result back to a bound geometry.</summary>
    void Place()
    {
        var requested = Binds.Rect(Json["geometry"]) ?? new NuiRect(-1, -1, 400, 300);
        var screen = GetViewportRect().Size;
        var mouse = GetViewport().GetMousePosition();
        _rect = NuiGeometry.Place(requested, new NuiVec(screen.X, screen.Y), new NuiVec(mouse.X, mouse.Y), SizeConstraint, EdgeConstraint);
        ApplyRect();
        if (Binds.Rect(Json["geometry"]) is { } bound && bound != _rect) WriteGeometry();
    }

    void WriteGeometry()
    {
        if (!NuiBinds.IsBind(Json["geometry"], out _)) return;
        Ctx.Write(Json["geometry"], -1, _rect.ToJson());
    }

    void ApplyRect()
    {
        Position = new Vector2(_rect.X, _rect.Y);
        float h = _collapsed ? TitleH : _rect.H;
        Size = new Vector2(_rect.W, h);
        _content.Visible = !_collapsed;
        Relayout();
        QueueRedraw();
    }

    /// <summary>Content area inside the frame, in window coordinates.</summary>
    Rect2 ContentRect()
    {
        float p = NuiStyle.WindowPadding;
        return new Rect2(p, TitleH + p, Math.Max(0, _rect.W - 2 * p), Math.Max(0, _rect.H - TitleH - 2 * p));
    }

    void Relayout()
    {
        if (_rootView == null) return;
        var cr = ContentRect();
        _content.Position = cr.Position;
        _content.Size = cr.Size;
        NuiLayout.Arrange(_root, new NuiRect(0, 0, cr.Size.X, cr.Size.Y));
        _rootView.ApplyLayout();
    }

    /// <summary>NuiSetGroupLayout: "_window_" swaps the root, otherwise the group with that id.</summary>
    public void SetGroupLayout(string element, JsonNode? layout)
    {
        var node = NuiNode.Parse(layout);
        if (element == "_window_")
        {
            _root = node;
            BuildRoot();
        }
        else if (FindElement(element) is NuiGroupView group)
        {
            group.SetLayout(node);
            if (!AcceptsInput) IgnoreInput(group);
        }
        else return;
        Relayout();
    }

    public NuiElement? FindElement(string id, int row = -1)
    {
        if (_rootView == null) return null;
        foreach (var e in All(_rootView))
        {
            if (e.Node.Id == id && (row < 0 || e.Row == row)) return e;
            if (row >= 0 && e is NuiListView list && list.RowElement(row, id) is { } re) return re;
        }
        return null;
    }

    static IEnumerable<NuiElement> All(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is NuiElement e) yield return e;
            foreach (var d in All(c)) yield return d;
        }
    }

    void SetCollapsed(bool collapsed, bool user)
    {
        if (_collapsed == collapsed) return;
        _collapsed = collapsed;
        ApplyRect();
        if (user && NuiBinds.IsBind(Json["collapsed"], out _)) Ctx.Write(Json["collapsed"], -1, JsonValue.Create(collapsed));
    }

    public void ToggleCollapsed()
    {
        if (CanCollapse) SetCollapsed(!_collapsed, true);
    }

    public void Activate() => Manager.Focus(this);

    // ----- chrome -----

    Rect2 CloseRect => new(_rect.W - 26, (NuiStyle.TitleHeight - 16) / 2, 16, 16);
    Rect2 CollapseRect => new(8, (NuiStyle.TitleHeight - 16) / 2, 16, 16);
    public Rect2 GripRect => new(GlobalPosition + new Vector2(_rect.W - 14, _rect.H - 14), new Vector2(14, 14));

    public override void _Draw()
    {
        var skin = Ctx.Skin;
        var full = new Rect2(Vector2.Zero, Size);
        bool tall = _rect.H > _rect.W;
        if (!Transparent || HasBorder) DrawStyleBox(skin.WindowBox(tall, HasBorder, !Transparent), full);
        if (HasTitleBar)
        {
            DrawStyleBox(skin.Header, new Rect2(0, 0, Size.X, NuiStyle.TitleHeight));
            float left = 8, right = 8;
            if (CanCollapse)
            {
                var tex = skin.Texture(_collapsed ? "nui_maximize" : "nui_minimize");
                var mod = _hoverCollapse ? Colors.White : new Color(0.85f, 0.85f, 0.85f);
                if (tex != null) DrawTextureRect(tex, CollapseRect, false, mod);
                else skin.DrawText(this, CollapseRect, _collapsed ? "+" : "-", 0, 0, mod);
                left = CollapseRect.End.X + 6;
            }
            if (Closable)
            {
                var mod = _hoverClose ? Colors.White : new Color(0.85f, 0.85f, 0.85f);
                if (skin.Texture("nui_close") is { } tex) DrawTextureRect(tex, CloseRect, false, mod);
                else skin.DrawText(this, CloseRect, "x", 0, 0, mod);
                right = Size.X - CloseRect.Position.X + 6;
            }
            float side = Math.Max(left, right);
            var titleColor = Binds.Color(Json["foreground_color"]) is { } fc ? NuiSkin.ToColor(fc) : NuiSkin.Title;
            skin.DrawText(this, new Rect2(side, 0, Math.Max(0, Size.X - 2 * side), NuiStyle.TitleHeight), Binds.Text(Json["title"]), 0, 0, titleColor, skin.FontSize + 1);
        }
        if (Resizable && !_collapsed)
        {
            var c = new Color(NuiSkin.Accent, 0.7f);
            for (int i = 0; i < 3; i++)
            {
                float o = 4 + i * 4;
                DrawLine(new Vector2(Size.X - o, Size.Y - 3), new Vector2(Size.X - 3, Size.Y - o), c, 1);
            }
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (!AcceptsInput) return;
        switch (e)
        {
            case InputEventMouseMotion mm:
            {
                bool hc = Closable && CloseRect.Grow(3).HasPoint(mm.Position), hl = CanCollapse && HasTitleBar && CollapseRect.Grow(3).HasPoint(mm.Position);
                if (hc != _hoverClose || hl != _hoverCollapse) { _hoverClose = hc; _hoverCollapse = hl; QueueRedraw(); }
                if (_dragging)
                {
                    var delta = mm.GlobalPosition - _dragStart;
                    var r = new NuiRect(_rectStart.X + delta.X, _rectStart.Y + delta.Y, _rect.W, _rect.H);
                    _rect = ConstrainEdges(r);
                    Position = new Vector2(_rect.X, _rect.Y);
                }
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                Activate();
                if (mb.Pressed && HasTitleBar && mb.Position.Y < NuiStyle.TitleHeight)
                {
                    if (Closable && CloseRect.Grow(3).HasPoint(mb.Position)) { Manager.CloseByUser(this); AcceptEvent(); return; }
                    if (CanCollapse && CollapseRect.Grow(3).HasPoint(mb.Position)) { SetCollapsed(!_collapsed, true); AcceptEvent(); return; }
                    _dragging = true;
                    _dragStart = mb.GlobalPosition;
                    _rectStart = new Vector2(_rect.X, _rect.Y);
                }
                else if (!mb.Pressed && _dragging)
                {
                    _dragging = false;
                    WriteGeometry();
                }
                AcceptEvent();
                break;
            case InputEventMouseButton:
                AcceptEvent();
                break;
        }
    }

    NuiRect ConstrainEdges(NuiRect r)
    {
        if (EdgeConstraint is not { } ec) return r;
        var screen = GetViewportRect().Size;
        float x = r.X, y = r.Y;
        if (ec.W > 0) x = Math.Min(x, screen.X - ec.W - r.W);
        if (ec.H > 0) y = Math.Min(y, screen.Y - ec.H - r.H);
        if (ec.X > 0) x = Math.Max(x, ec.X);
        if (ec.Y > 0) y = Math.Max(y, ec.Y);
        return r with { X = x, Y = y };
    }

    Vector2 _resizeGrab;

    /// <summary>Starts an interactive resize from the bottom-right grip (driven by the manager).</summary>
    public void BeginResize(Vector2 globalMouse) => _resizeGrab = new Vector2(_rect.Right, _rect.Bottom) - globalMouse;

    public void ResizeTo(Vector2 globalMouse)
    {
        var corner = globalMouse + _resizeGrab;
        float w = Math.Max(80, corner.X - _rect.X), h = Math.Max(TitleH + 30, corner.Y - _rect.Y);
        if (SizeConstraint is { } sc)
        {
            if (sc.X > 0) w = Math.Max(w, sc.X);
            if (sc.Y > 0) h = Math.Max(h, sc.Y);
            if (sc.W > 0) w = Math.Min(w, sc.W);
            if (sc.H > 0) h = Math.Min(h, sc.H);
        }
        if (!NuiGeometry.Resizable(SizeConstraint, true)) w = _rect.W;
        if (!NuiGeometry.Resizable(SizeConstraint, false)) h = _rect.H;
        _rect = _rect with { W = MathF.Round(w), H = MathF.Round(h) };
        ApplyRect();
    }

    public void EndResize() => WriteGeometry();

    public bool CanResizeAt(Vector2 globalMouse) => Resizable && !_collapsed && Visible && GripRect.HasPoint(globalMouse);
}
