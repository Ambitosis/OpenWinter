using OpenWinter.Client.Views;
using OpenWinter.Game.Inventory;
using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// An inventory grid as the original lays it out: items cover their InvSlotWidth x InvSlotHeight cells
/// at the positions the server keeps. Shows one page of a paged grid (the backpack) or every row of a
/// growing one (containers, stores). While an item is dragged over it, the footprint it would land on
/// is outlined green (fits, swaps or stacks) or red (doesn't).
/// </summary>
public partial class ItemGrid : Control
{
    public GridSpec Spec = GridSpec.Backpack;
    public float Cell = 48;
    /// <summary>The holder whose grid this is (0: the player's backpack; a bag, container or store id).</summary>
    public uint Holder;
    /// <summary>Drag source name given to tiles ("pack", "bag", "container", "store").</summary>
    public string DragSource = "pack";
    /// <summary>Rows always shown for growing grids.</summary>
    public int MinRows = 6;
    public ClientContent? Content;

    // Behaviour, wired by the window.
    public Func<ItemDrag, bool>? AcceptsFrom;
    /// <summary>A drop at a cell (top-left of where the item would sit).</summary>
    public Action<ItemDrag, int, int>? Dropped;
    public Action<ItemEntry>? Clicked, DoubleClicked;
    public Action<ItemEntry, Vector2>? ContextRequested;
    public Action<ItemDrag>? DroppedOnWorld;
    public Func<ItemEntry, ItemTooltip.Extra?>? TooltipExtra;
    /// <summary>Greys tiles that don't match (filter chips in the classic view).</summary>
    public Func<ItemEntry, bool>? Highlight;
    /// <summary>The item shown selected (store windows), 0 for none.</summary>
    public uint SelectedId;
    /// <summary>A right-hand caption on tiles (store prices), or null.</summary>
    public Func<ItemEntry, string?>? Badge;

    int _page;
    List<ItemEntry> _items = new();
    readonly List<ItemSlot> _tiles = new();
    Overlay _overlay = null!;
    (int X, int Y, int W, int H, bool Ok)? _hover;
    Vector2? _pointer;

    // Godot can recheck a drag between events using the OS pointer. Keep the viewport
    // event position, as classic grids do, so both the preview and drop use the same cell.
    public override void _Input(InputEvent e) { if (e is InputEventMouse mouse) _pointer = mouse.Position; }
    Vector2 Pointer => GetGlobalTransformWithCanvas().AffineInverse() * (_pointer ?? GetViewport().GetMousePosition());

    public int Page
    {
        get => _page;
        set { _page = Math.Clamp(value, 0, Math.Max(0, Spec.Pages - 1)); Layout(); }
    }

    public IReadOnlyList<ItemEntry> Items => _items;
    internal (int X, int Y, int W, int H, bool Ok)? CaptureDropTarget => _hover;
    public event Action? PageChanged;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        _overlay = new Overlay { Grid = this, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_overlay);
        Layout();
    }

    public void SetItems(IEnumerable<ItemEntry> items, ClientContent? content)
    {
        Content = content;
        _items = items.ToList();
        Layout();
    }

    int FirstRow => Spec.Pages > 0 ? _page * Spec.PageHeight : 0;

    int VisibleRows
    {
        get
        {
            if (Spec.Pages > 0) return Spec.PageHeight;
            int used = _items.Where(i => i.Y >= 0).Select(i => i.Y + i.H).DefaultIfEmpty(0).Max();
            int rows = Math.Max(MinRows, used + 1);
            // Every hovered row belongs to a complete page, including a growing grid's spare rows.
            return (rows + Spec.PageHeight - 1) / Spec.PageHeight * Spec.PageHeight;
        }
    }

    /// <summary>Items on a page (for the page tabs' fill marks).</summary>
    public int CountOnPage(int page) => _items.Count(i => i.Y >= 0 && i.Y / Spec.PageHeight == page);

    void Layout()
    {
        if (!IsInsideTree()) return;
        foreach (var t in _tiles) { RemoveChild(t); t.QueueFree(); }
        _tiles.Clear();
        int rows = VisibleRows;
        CustomMinimumSize = new Vector2(Spec.Width * Cell, rows * Cell);
        foreach (var item in _items)
        {
            if (item.X < 0 || item.Y < FirstRow || item.Y >= FirstRow + rows) continue;
            var tile = ItemSlot.Create(new Vector2(item.W * Cell - 2, item.H * Cell - 2));
            tile.Position = new Vector2(item.X * Cell + 1, (item.Y - FirstRow) * Cell + 1);
            tile.CellSize = Cell;
            tile.GridHolder = Holder;
            tile.ShowDropHighlight = false;
            tile.SetItem(item, Content);
            tile.DragSource = DragSource;
            tile.CanAccept = CanDropHere;
            tile.Accept = DropHere;
            tile.Clicked = i => Clicked?.Invoke(i);
            tile.DoubleClicked = i => DoubleClicked?.Invoke(i);
            tile.ContextRequested = (i, at) => ContextRequested?.Invoke(i, at);
            tile.DroppedOnWorld = d => DroppedOnWorld?.Invoke(d);
            tile.TooltipExtra = i => TooltipExtra?.Invoke(i);
            if (Highlight != null && !Highlight(item)) tile.Modulate = new Color(1, 1, 1, 0.22f);
            if (item.Id == SelectedId && SelectedId != 0) { tile.ToggleMode = true; tile.SetPressedNoSignal(true); }
            if (Badge?.Invoke(item) is { Length: > 0 } badge)
            {
                var b = Ui.Overlay(badge, 11, Palette.AccentBright);
                b.SetAnchorsPreset(LayoutPreset.TopRight);
                b.OffsetLeft = -60; b.OffsetRight = -3; b.OffsetTop = 1;
                b.HorizontalAlignment = HorizontalAlignment.Right;
                tile.AddChild(b);
            }
            AddChild(tile);
            _tiles.Add(tile);
        }
        MoveChild(_overlay, -1);
        _overlay.Size = CustomMinimumSize;
        QueueRedraw();
        _overlay.QueueRedraw();
    }

    public override void _Draw()
    {
        // Empty cells: faint wells with a hairline, every page row a touch darker at its top.
        int rows = VisibleRows;
        var fill = new Color(0.04f, 0.045f, 0.06f, 0.75f);
        var line = Palette.Line;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < Spec.Width; x++)
            {
                var r = new Rect2(x * Cell + 1, y * Cell + 1, Cell - 2, Cell - 2);
                DrawRect(r, fill);
                DrawRect(r, line, false, 1);
            }
        if (Spec.Pages <= 0)
            for (int y = Spec.PageHeight; y < rows; y += Spec.PageHeight)
                DrawLine(new Vector2(0, y * Cell), new Vector2(Spec.Width * Cell, y * Cell), Palette.LineStrong, 1);
    }

    // ------------------------------------------------------------------ dropping

    (int X, int Y) CellUnderMouse(ItemDrag d)
    {
        var m = Pointer;
        int pointerRow = (int)MathF.Floor(m.Y / Cell) + FirstRow;
        int x = (int)MathF.Floor(m.X / Cell) - d.GrabX, y = pointerRow - d.GrabY;
        var shape = new GridShape(Spec.Width, Spec.PageHeight, Spec.Pages);
        return InventoryGridPlacement.ClampToPage(shape, d.Item.W, d.Item.H, x, y, pointerRow / Spec.PageHeight) ?? (x, y);
    }

    /// <summary>Whether a drop can use the requested footprint, stack, swap or first free position.</summary>
    public bool CanDropHere(ItemDrag d)
    {
        if (AcceptsFrom?.Invoke(d) != true) { SetHover(null); return false; }
        var (x, y) = CellUnderMouse(d);
        bool ok = Fits(d, x, y);
        SetHover((x, y - FirstRow, d.Item.W, d.Item.H, ok));
        return ok;
    }

    public void DropHere(ItemDrag d)
    {
        var (x, y) = CellUnderMouse(d);
        SetHover(null);
        if (Fits(d, x, y)) Dropped?.Invoke(d, x, y);
    }

    bool Fits(ItemDrag d, int x, int y)
    {
        var item = d.Item;
        if (!Spec.Contains(x, y, item.W, item.H)) return false;
        bool sameGrid = d.Holder == Holder && d.Source == DragSource;
        var under = _items.Where(o => o.X >= 0 && !(sameGrid && o.Id == item.Id)
            && x < o.X + o.W && o.X < x + item.W && y < o.Y + o.H && o.Y < y + item.H).ToList();
        if (under.Count == 0) return true;
        var other = under.Count == 1 ? under[0] : null;
        // A matching stack with room.
        if (other != null && other.Name == item.Name && other.BaseItem == item.BaseItem && (other.Info?.MaxStack ?? 1) > 1 && other.Stack < other.Info!.MaxStack) return true;
        // Same size, same grid, exactly on it: swap.
        if (other != null && sameGrid && item.Slot < 0 && other.W == item.W && other.H == item.H && other.X == x && other.Y == y) return true;
        var occupied = InventoryGridPlacement.Occupied(_items.Where(o => !(sameGrid && o.Id == item.Id))
            .Select(o => (o.X, o.Y, o.W, o.H)));
        return InventoryGridPlacement.FirstFit(new GridShape(Spec.Width, Spec.PageHeight, Spec.Pages), item.W, item.H, occupied) != null;
    }

    void SetHover((int, int, int, int, bool)? h)
    {
        if (_hover == h) return;
        _hover = h;
        _overlay.QueueRedraw();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => ItemDragging.From(data) is { } d && CanDropHere(d);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (ItemDragging.From(data) is { } d) DropHere(d);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd || what == NotificationMouseExit) SetHover(null);
    }

    public override void _Process(double delta)
    {
        // The pointer can leave over a child tile without a mouse-exit; drop the outline then.
        if (_hover != null && !new Rect2(Vector2.Zero, Size).HasPoint(Pointer)) SetHover(null);
    }

    partial class Overlay : Control
    {
        public ItemGrid Grid = null!;

        public override void _Draw()
        {
            if (Grid._hover is not { } h) return;
            float c = Grid.Cell;
            var color = h.Ok ? Palette.Success : Palette.Danger;
            var r = new Rect2(h.X * c, h.Y * c, h.W * c, h.H * c).Intersection(new Rect2(Vector2.Zero, Size));
            if (r.Size.X <= 0 || r.Size.Y <= 0) return;
            // Fill each covered cell, then a bold outline around the whole footprint.
            DrawRect(r.Grow(-1), color with { A = 0.22f });
            DrawRect(r.Grow(-1.5f), color, false, 3);
        }
    }
}

/// <summary>Page tabs for a paged grid (I to VI down its side), each marked when it holds items. Dragging over one turns to it.</summary>
public partial class GridPageTabs : VBoxContainer
{
    public ItemGrid Grid = null!;
    readonly List<Button> _tabs = new();

    static readonly string[] Numerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

    public static GridPageTabs Create(ItemGrid grid)
    {
        var t = new GridPageTabs { Grid = grid };
        t.AddThemeConstantOverride("separation", 4);
        for (int i = 0; i < grid.Spec.Pages; i++)
        {
            int page = i;
            var b = new PageTab
            {
                Text = i < Numerals.Length ? Numerals[i] : (i + 1).ToString(), ToggleMode = true, FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(34, Math.Max(28, grid.Cell * grid.Spec.PageHeight / grid.Spec.Pages - 4)), TooltipText = $"Page {i + 1}",
                Turn = () => { if (grid.Page != page) { grid.Page = page; t.Refresh(); } },
            };
            b.AddThemeFontOverride("font", Fonts.Display);
            b.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
            b.AddThemeStyleboxOverride("normal", Styles.Box(new Color(1, 1, 1, 0.03f), Palette.RadiusSmall, Palette.Line, 1, 4, 2));
            b.AddThemeStyleboxOverride("hover", Styles.Box(new Color(1, 1, 1, 0.07f), Palette.RadiusSmall, Palette.LineStrong, 1, 4, 2));
            var on = Styles.Box(Palette.Accent with { A = 0.16f }, Palette.RadiusSmall, Palette.Accent, 1, 4, 2);
            b.AddThemeStyleboxOverride("pressed", on);
            b.AddThemeStyleboxOverride("hover_pressed", on);
            b.AddThemeColorOverride("font_color", Palette.TextSecondary);
            b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
            b.AddThemeColorOverride("font_hover_pressed_color", Palette.AccentBright);
            b.Pressed += b.Turn;
            t.AddChild(b);
            t._tabs.Add(b);
        }
        t.Refresh();
        return t;
    }

    public void Refresh()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].SetPressedNoSignal(i == Grid.Page);
            ((PageTab)_tabs[i]).Filled = Grid.CountOnPage(i) > 0;
            _tabs[i].TooltipText = $"Page {i + 1}: {Grid.CountOnPage(i)} items";
        }
    }

    partial class PageTab : Button
    {
        public Action Turn = null!;
        bool _filled;
        public bool Filled { get => _filled; set { _filled = value; QueueRedraw(); } }

        public override void _Draw()
        {
            if (_filled) DrawCircle(new Vector2(Size.X - 6, 6), 2.2f, Palette.Accent);
        }

        double _dragHover;
        bool _dragWasOver;
        // Crossing a tab on the way to the world must not change the page.
        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (ItemDragging.From(data) != null && new Rect2(Vector2.Zero, Size).HasPoint(atPosition)) _dragWasOver = true;
            return false;
        }
        public override void _Process(double delta)
        {
            if (!GetViewport().GuiIsDragging() || !_dragWasOver || !GetGlobalRect().HasPoint(ItemDragging.Pointer(this)) || ButtonPressed) { _dragHover = 0; return; }
            _dragHover += delta;
            if (_dragHover >= 0.5) { _dragHover = 0; Turn(); }
        }
    }
}
