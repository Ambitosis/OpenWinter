using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// A list row for an item (store panes): icon, coloured name, type line and a right-hand price or
/// note. Hover and selected states, rich tooltip, drag and drop, click / double-click / right-click.
/// </summary>
public partial class ItemRow : PanelContainer
{
    public ItemEntry Item { get; private set; } = null!;
    public ClientContent? Content;
    public string DragSource = "";
    public Func<ItemEntry, ItemTooltip.Extra?>? TooltipExtra;
    public Func<ItemDrag, bool>? CanAccept;
    public Action<ItemDrag>? Accept;
    public Action<ItemEntry>? Clicked, DoubleClicked;
    public Action<ItemEntry, Vector2>? ContextRequested;
    /// <summary>The trailing column: a price or a short note. Replace with <see cref="SetTrailing"/>.</summary>
    public HBoxContainer Trailing = null!;

    bool _hover, _selected, _dim;
    ItemDrag? _drag;
    Control _iconHost = null!;

    public bool Selected
    {
        get => _selected;
        set { _selected = value; Refresh(); }
    }

    /// <summary>Greys the row (can't afford, merchant won't buy).</summary>
    public bool Dimmed
    {
        get => _dim;
        set { _dim = value; Modulate = value ? new Color(1, 1, 1, 0.55f) : Colors.White; }
    }

    public static ItemRow Create(ItemEntry item, ClientContent? content, string? subtitle = null)
    {
        var row = new ItemRow { Item = item, Content = content, MouseFilter = MouseFilterEnum.Stop, TooltipText = item.Name, MouseDefaultCursorShape = CursorShape.PointingHand };
        var h = Ui.HBox(10);
        h.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(h);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), MouseFilter = MouseFilterEnum.Ignore };
        var fs = Styles.Slot();
        fs.SetContentMarginAll(2);
        if (ItemUi.RarityTint(item) is { } tint) fs.BorderColor = tint with { A = 0.6f };
        frame.AddThemeStyleboxOverride("panel", fs);
        if (content != null) frame.AddChild(UiTheme.Icon(content, item.Icon, item.Stack, 40));
        row._iconHost = frame;
        h.AddChild(frame);
        var text = Ui.VBox(1);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.Alignment = BoxContainer.AlignmentMode.Center;
        text.MouseFilter = MouseFilterEnum.Ignore;
        var name = Ui.Text(item.Name, Ui.TextStyle.Body, ItemUi.NameColor(item), Palette.FontBody - 1);
        name.AddThemeFontOverride("font", Fonts.UiMedium);
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        text.AddChild(name);
        string sub = subtitle ?? item.Info?.Type ?? "";
        if (!item.Identified) sub = sub.Length > 0 ? sub + "  ·  unidentified" : "Unidentified";
        if (sub.Length > 0)
        {
            var s = Ui.Text(sub, Ui.TextStyle.Caption, Palette.TextSecondary);
            s.ClipText = true;
            s.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            text.AddChild(s);
        }
        h.AddChild(text);
        row.Trailing = Ui.HBox(6);
        row.Trailing.MouseFilter = MouseFilterEnum.Ignore;
        row.Trailing.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(row.Trailing);
        row.Refresh();
        return row;
    }

    public void SetTrailing(Control c)
    {
        foreach (var child in Trailing.GetChildren()) child.QueueFree();
        Trailing.AddChild(c);
    }

    void Refresh()
    {
        var bg = _selected ? Palette.Accent with { A = 0.13f } : _hover ? new Color(1, 1, 1, 0.05f) : Colors.Transparent;
        var border = _selected ? Palette.AccentDim : _hover ? Palette.LineStrong : Colors.Transparent;
        var s = Styles.Box(bg, Palette.RadiusSmall, border, 1, 8, 5);
        if (_selected) { s.BorderWidthLeft = 3; s.BorderColor = Palette.Accent; }
        AddThemeStyleboxOverride("panel", s);
    }

    public override void _Ready()
    {
        MouseEntered += () => { _hover = true; Refresh(); };
        MouseExited += () => { _hover = false; Refresh(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.DoubleClick) DoubleClicked?.Invoke(Item);
            else Clicked?.Invoke(Item);
            AcceptEvent();
        }
        else if (mb.ButtonIndex == MouseButton.Right)
        {
            Clicked?.Invoke(Item);
            ContextRequested?.Invoke(Item, GetGlobalMousePosition());
            AcceptEvent();
        }
    }

    public override GodotObject? _MakeCustomTooltip(string forText) => ItemTooltip.Build(Item, Content, TooltipExtra?.Invoke(Item));

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (DragSource.Length == 0) return default;
        _drag = ItemDragging.Begin(this, Item, Content, DragSource);
        _iconHost.Modulate = new Color(1, 1, 1, 0.3f);
        return _drag;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        ItemDragging.From(data) is { } d && d != _drag && CanAccept?.Invoke(d) == true;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (ItemDragging.From(data) is { } d) Accept?.Invoke(d);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _drag != null)
        {
            if (GetViewport().GuiIsDragSuccessful()) ItemDragging.PlayDropSound(Content, _drag);
            _drag = null;
            _iconHost.Modulate = Colors.White;
        }
    }
}
