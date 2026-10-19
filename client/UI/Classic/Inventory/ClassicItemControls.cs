using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// What the classic item grids and equipment boxes share: picking an item up (a click attaches it to the cursor
/// as in the original; pressing and dragging works too), dropping it on the world, the quickbar or another item
/// window (the drag data is the action bar's "item" dictionary carrying an <see cref="ItemDrag"/>), right-click
/// options, hover and the item's name as hover text (shown at once, <see cref="IClassicHover"/>). Putting an item straight back where it was picked up
/// (a double-click) uses it: <see cref="Activated"/>.
/// </summary>
public abstract partial class ClassicItemControl : Control, IClassicHover
{
    public ClassicAssets Assets = null!;
    /// <summary>Drag source name ("pack", "bag", "equip", "container", "store").</summary>
    public string Source = "pack";
    /// <summary>The holder whose grid this is (0: the backpack; a bag, container or store id).</summary>
    public uint Holder;
    public Func<ItemDrag, bool>? AcceptsFrom;
    /// <summary>Double-click, or an item put straight back: equip, use, open, take off, buy ...</summary>
    public Action<ItemEntry>? Activated;
    /// <summary>A plain click on an item; return true to keep it from being picked up.</summary>
    public Func<ItemEntry, bool>? Clicked;
    /// <summary>Optional Shift-click override (the store's immediate purchase, the backpack's immediate sale while a
    /// store is open). Returns true when it acted; a stack's Shift-click splits only when it didn't.</summary>
    public Func<ItemEntry, bool>? ShiftClicked;
    /// <summary>Shift-click on a stack.</summary>
    public Action<ItemEntry>? SplitRequested;
    public Action<ItemEntry, Vector2>? Context;
    public Action<ItemDrag>? DroppedOnWorld;
    public Action<ItemEntry?>? Hovered;
    /// <summary>Tooltip text for an item (default: its name).</summary>
    public Func<ItemEntry, string>? Tip;
    public bool CanPickUp = true;

    /// <summary>The item picked up from here while it is carried.</summary>
    protected ItemDrag? Carried;
    ItemEntry? _pressed;
    bool _doubleClickDrop;
    bool _cancelled;
    protected ItemEntry? HoverItem;

    protected ClassicItemControl() { MouseFilter = MouseFilterEnum.Stop; }

    /// <summary>The pointer as the last mouse event put it (viewport coordinates).</summary>
    static Vector2 _mouse;

    public override void _Input(InputEvent e)
    {
        ItemDragging.Observe(e);
        if (e is InputEventMouse m) _mouse = m.Position;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
            _doubleClickDrop = Carried != null && GetViewport().GuiIsDragging() && click.DoubleClick;
        // Right-click or Esc while carrying an item picked up here puts it back.
        if (Carried != null && GetViewport().GuiIsDragging()
            && (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } || e is InputEventKey { Keycode: Key.Escape, Pressed: true }))
        {
            _cancelled = true;
            GetViewport().GuiCancelDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// The pointer in this control's coordinates, from the mouse events themselves: drop positions follow the
    /// events that chose the drop target (scripted captures push events without moving the cursor).
    /// </summary>
    protected Vector2 Pointer => GetGlobalTransformWithCanvas().AffineInverse() * _mouse;

    /// <summary>The item under a point (local), or null.</summary>
    protected abstract ItemEntry? ItemAt(Vector2 local);
    /// <summary>Where an item is drawn (local).</summary>
    protected abstract Rect2 ItemRect(ItemEntry item);
    /// <summary>Where an item's middle is on screen (captures).</summary>
    public Vector2 ScreenCentreOf(ItemEntry item) => GetGlobalTransformWithCanvas() * ItemRect(item).GetCenter();
    /// <summary>Cell size for grids (the drag remembers the grabbed cell), zero for single boxes.</summary>
    protected virtual Vector2 GridCell => Vector2.Zero;

    ItemDrag Make(ItemEntry item, Vector2 at, out Control preview)
    {
        _cancelled = false;
        ItemDragging.PlayPickupSound(Assets.Content);
        var rect = ItemRect(item);
        var offset = at - rect.Position;
        int gx = 0, gy = 0;
        if (GridCell.X > 0)
        {
            gx = Math.Clamp((int)(offset.X / GridCell.X), 0, item.W - 1);
            gy = Math.Clamp((int)(offset.Y / GridCell.Y), 0, item.H - 1);
        }
        // The cursor holds the icon where it was grabbed (the icon is drawn centred on the item's cells).
        var size = ClassicItemArt.IconSize(Assets, item);
        float scale = Math.Min(1f, Math.Min(rect.Size.X / size.X, rect.Size.Y / size.Y));
        var iconPos = rect.GetCenter() - size * scale / 2;
        var grab = (at - iconPos).Clamp(Vector2.Zero, size);
        preview = ClassicItemArt.DragPreview(Assets, item, GridCell.X > 0 ? grab : size / 2);
        return new ItemDrag { Item = item, Source = Source, Slot = item.Slot, Holder = Holder, GrabX = gx, GrabY = gy };
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!CanPickUp || ItemAt(atPosition) is not { } item) return default;
        Carried = Make(item, atPosition, out var preview);
        _doubleClickDrop = false;
        _pressed = null;
        SetDragPreview(preview);
        QueueRedraw();
        return ClassicItemArt.DragData(Carried);
    }

    /// <summary>Attaches an item to the cursor (the original's click to pick up).</summary>
    public void PickUp(ItemEntry item, Vector2 at)
    {
        if (!CanPickUp) return;
        Carried = Make(item, at, out var preview);
        _doubleClickDrop = false;
        ForceDrag(ClassicItemArt.DragData(Carried), preview);
        QueueRedraw();
    }

    /// <summary>
    /// A recognized double-click returning to the same item. Ordinary carry/drop
    /// clicks must not become activations merely because they happened quickly.
    /// </summary>
    protected bool DropBack(ItemDrag d, Vector2 at)
    {
        if (d != Carried || !_doubleClickDrop || ItemAt(at)?.Id != d.Item.Id) return false;
        _doubleClickDrop = false;
        Activated?.Invoke(d.Item);
        return true;
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion mm:
                var h = ItemAt(mm.Position);
                if (h?.Id != HoverItem?.Id) { HoverItem = h; Hovered?.Invoke(h); QueueRedraw(); }
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb:
            {
                // A click-to-drop may reach the destination before Godot ends the
                // drag. Its release must not become a fresh loot/use click there.
                if (GetViewport().GuiIsDragging()) { _pressed = null; AcceptEvent(); break; }
                var item = ItemAt(mb.Position);
                _pressed = item;
                if (item == null) break;
                AcceptEvent();
                if (mb.DoubleClick)
                {
                    _pressed = null;
                    if (!(mb.ShiftPressed && ShiftClicked?.Invoke(item) == true)) Activated?.Invoke(item);
                }
                else if (mb.ShiftPressed && item.Stack > 1 && SplitRequested != null)
                {
                    _pressed = null;
                    if (ShiftClicked?.Invoke(item) != true) SplitRequested(item);
                }
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb:
            {
                var item = _pressed;
                _pressed = null;
                if (item == null || ItemAt(mb.Position)?.Id != item.Id || GetViewport().GuiIsDragging()) break;
                AcceptEvent();
                if (mb.ShiftPressed && ShiftClicked?.Invoke(item) == true) break;
                if (Clicked?.Invoke(item) == true) break;
                PickUp(item, mb.Position);
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } mb:
                if (ItemAt(mb.Position) is { } it) { AcceptEvent(); Context?.Invoke(it, GetGlobalTransform() * mb.Position); }
                break;
        }
    }

    public void ClearHover()
    {
        HoverItem = null;
        Hovered?.Invoke(null);
        QueueRedraw();
    }

    /// <summary>The hovered item's name in the original's hover box, at once (measured: invitemring).</summary>
    /// <remarks>
    /// Read from where the pointer is now, not from the last motion's hover (a page change or a refresh clears that
    /// while the pointer stays on an item).
    /// </remarks>
    public string[]? HoverLines() =>
        !GetViewport().GuiIsDragging() && ItemAt(GetGlobalTransformWithCanvas().AffineInverse() * _mouse) is { } item
            && (Tip?.Invoke(item) ?? item.Name) is { Length: > 0 } t ? t.Split('\n') : null;

    public bool HoverInstant => true;

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin || what == NotificationDragEnd) _pressed = null;
        if (what == NotificationMouseExit && HoverItem != null) { HoverItem = null; Hovered?.Invoke(null); QueueRedraw(); }
        if (what == NotificationDragEnd)
        {
            _doubleClickDrop = false;
            if (Carried != null)
            {
                var d = Carried;
                if (!_cancelled && GetViewport().GuiIsDragSuccessful()) ItemDragging.PlayDropSound(Assets.Content, d);
                Carried = null;
                if (!_cancelled && !GetViewport().GuiIsDragSuccessful() && ItemDragging.OverWorld(this)) DroppedOnWorld?.Invoke(d);
            }
            OnDragEnded();
            QueueRedraw();
        }
        else if (what == NotificationDragBegin) { OnDragBegan(); QueueRedraw(); }
    }

    protected virtual void OnDragBegan() { }
    protected virtual void OnDragEnded() { }

    /// <summary>The item being dragged from anywhere, or null.</summary>
    protected ItemDrag? CurrentDrag() =>
        IsInsideTree() && GetViewport().GuiIsDragging() ? ItemDragging.From(GetViewport().GuiGetDragData()) : null;
}

/// <summary>
/// An inventory grid as the original draws it: <see cref="Columns"/> x <see cref="Rows"/> cells a page over the
/// box model's cell art, row 0 at the bottom (the original keeps Repos_PosY counting up from the bottom of a
/// page), each item on its cells' gui_invm background with its icon at texel size and its stack count. While an
/// item is carried over it, the cells it would cover light up (gui_invm_hl) or turn red where it can't go.
/// </summary>
public partial class ClassicItemGrid : ClassicItemControl
{
    public int Columns = 10, Rows = 6;
    public Vector2 Cell = new(33.1f, 33.1f);
    /// <summary>The grid's shape on the server (pages there may differ from the rows shown here).</summary>
    public GridSpec Spec = GridSpec.Backpack;
    /// <summary>A drop at a cell (top-left of the item in the server's grid).</summary>
    public Action<ItemDrag, int, int>? Dropped;
    public Func<ItemEntry, bool>? Dim;
    public uint SelectedId;
    /// <summary>Takes a drop anywhere on it, not at a cell (the store: selling).</summary>
    public bool DropAnywhere;
    public event Action? PageChanged;

    List<ItemEntry> _items = new();
    int _page;
    (int X, int Top, int W, int H, bool Ok)? _target;
    internal (int X, int Top, int W, int H, bool Ok)? CaptureDropTarget => _target;

    public IReadOnlyList<ItemEntry> Items => _items;

    public void SetItems(IEnumerable<ItemEntry> items)
    {
        _items = items.Where(i => i.X >= 0 && i.Y >= 0).ToList();
        if (_page >= PageCount) _page = Math.Max(0, PageCount - 1);
        QueueRedraw();
    }

    public int Page
    {
        get => _page;
        set
        {
            int p = Math.Clamp(value, 0, Math.Max(0, PageCount - 1));
            if (p == _page) return;
            _page = p;
            HoverItem = null;
            QueueRedraw();
            PageChanged?.Invoke();
        }
    }

    /// <summary>Pages: the server's for a paged grid, else as many as the items need.</summary>
    public int PageCount => Spec.Pages > 0
        ? Math.Max(1, Spec.Pages * Spec.PageHeight / Rows)
        : Math.Max(1, (_items.Select(i => i.Y + i.H).DefaultIfEmpty(0).Max() + Rows - 1) / Rows);

    public int CountOnPage(int page) => _items.Count(i => i.Y / Rows == page);

    protected override Vector2 GridCell => Cell;

    int TopRow(ItemEntry item) => Rows - item.Y % Rows - item.H;

    bool OnPage(ItemEntry item) => item.Y / Rows == _page;

    protected override Rect2 ItemRect(ItemEntry item) =>
        new(new Vector2(item.X * Cell.X, TopRow(item) * Cell.Y), new Vector2(item.W * Cell.X, item.H * Cell.Y));

    protected override ItemEntry? ItemAt(Vector2 local) =>
        _items.LastOrDefault(i => OnPage(i) && ItemRect(i).HasPoint(local));

    public override void _Draw()
    {
        var clip = new Rect2(Vector2.Zero, Size);
        foreach (var item in _items)
        {
            if (!OnPage(item)) continue;
            var r = ItemRect(item).Intersection(clip);
            if (r.Size.X <= 0 || r.Size.Y <= 0) continue;
            bool carried = Carried?.Item.Id == item.Id;
            string bg = item.Id == HoverItem?.Id || item.Id == SelectedId && SelectedId != 0
                ? (ClassicItemArt.Unusable(item) ? ClassicItemArt.CellUnusable : ClassicItemArt.CellHover)
                : ClassicItemArt.CellPlain; // unidentified items rest on plain cells too (armouricons)
            if (bg != ClassicItemArt.CellPlain) ClassicItemArt.DrawCells(this, Assets.Texture(bg), r, Cell);
            var mod = carried || Dim?.Invoke(item) == true ? new Color(1, 1, 1, 0.3f) : Colors.White;
            ClassicItemArt.DrawIcon(this, Assets, item, r, mod);
            if (!carried) ClassicItemArt.DrawCount(this, Assets, ClassicItemArt.CountOf(item), r);
        }
        if (_target is { } t)
        {
            var r = new Rect2(t.X * Cell.X, t.Top * Cell.Y, t.W * Cell.X, t.H * Cell.Y).Intersection(clip);
            if (r.Size.X > 0 && r.Size.Y > 0)
                ClassicItemArt.DrawCells(this, Assets.Texture(t.Ok ? ClassicItemArt.CellHover : ClassicItemArt.CellUnusable), r, Cell);
        }
    }

    // ------------------------------------------------------------------ dropping

    /// <summary>Where a carried item would land under the cursor: display column / top row, and the server's cell.</summary>
    (int X, int Top, int DataY) Under(ItemDrag d, Vector2 m)
    {
        int cx = (int)MathF.Floor(m.X / Cell.X) - d.GrabX;
        int top = (int)MathF.Floor(m.Y / Cell.Y) - d.GrabY;
        // Clamp in display coordinates before converting the bottom-up repository Y.
        var shape = new OpenWinter.Game.Inventory.GridShape(Columns, Rows, 1);
        if (OpenWinter.Game.Inventory.InventoryGridPlacement.ClampToPage(shape, d.Item.W, d.Item.H, cx, top, 0) is { } cell)
            (cx, top) = cell;
        return (cx, top, _page * Rows + Rows - top - d.Item.H);
    }

    bool Fits(ItemDrag d, int x, int top, int y)
    {
        var item = d.Item;
        if (top < 0 || top + item.H > Rows || x < 0 || x + item.W > Columns) return false;
        if (!Spec.Contains(x, y, item.W, item.H)) return false;
        bool sameGrid = d.Holder == Holder && d.Source == Source;
        var under = _items.Where(o => !(sameGrid && o.Id == item.Id)
            && x < o.X + o.W && o.X < x + item.W && y < o.Y + o.H && o.Y < y + item.H).ToList();
        if (under.Count == 0) return true;
        var other = under.Count == 1 ? under[0] : null;
        // A matching stack with room.
        if (other != null && other.Name == item.Name && other.BaseItem == item.BaseItem && (other.Info?.MaxStack ?? 1) > 1 && other.Stack < other.Info!.MaxStack) return true;
        // Same size, same grid, exactly on it: swap.
        if (other != null && sameGrid && item.Slot < 0 && other.W == item.W && other.H == item.H && other.X == x && other.Y == y) return true;
        var occupied = OpenWinter.Game.Inventory.InventoryGridPlacement.Occupied(_items.Where(o => !(sameGrid && o.Id == item.Id))
            .Select(o => (o.X, o.Y, o.W, o.H)));
        var shape = new OpenWinter.Game.Inventory.GridShape(Spec.Width, Spec.PageHeight, Spec.Pages);
        return OpenWinter.Game.Inventory.InventoryGridPlacement.FirstFit(shape, item.W, item.H, occupied) != null;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (ItemDragging.From(data) is not { } d || AcceptsFrom?.Invoke(d) != true) { SetTarget(null); return false; }
        if (DropAnywhere) return d != Carried;
        var (x, top, y) = Under(d, Pointer);
        bool ok = Fits(d, x, top, y);
        SetTarget((x, top, d.Item.W, d.Item.H, ok));
        // Accept a click on the item's own cells too (put back / double-click).
        return ok || d == Carried;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        SetTarget(null);
        if (ItemDragging.From(data) is not { } d) return;
        if (DropBack(d, Pointer)) return;
        if (DropAnywhere) { if (d != Carried) Dropped?.Invoke(d, -1, -1); return; }
        var (x, top, y) = Under(d, Pointer);
        if (Fits(d, x, top, y)) Dropped?.Invoke(d, x, y);
    }

    void SetTarget((int, int, int, int, bool)? t)
    {
        if (_target == t) return;
        _target = t;
        QueueRedraw();
    }

    protected override void OnDragEnded() => _target = null;

    public override void _Process(double delta)
    {
        if (_target != null && !new Rect2(Vector2.Zero, Size).HasPoint(Pointer)) SetTarget(null);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w && PageCount > 1)
        {
            Page += w.ButtonIndex == MouseButton.WheelDown ? 1 : -1;
            AcceptEvent();
            return;
        }
        base._GuiInput(e);
    }
}

/// <summary>
/// An equipment box of the inventory (inv_eqp_*): the equipped item's icon at its "icon" node, or the slot's
/// silhouette (inv_slot_*) when empty; lit with gui_inv_hilite while an item that fits it is carried.
/// </summary>
public partial class ClassicEquipSlot : ClassicItemControl
{
    public int Slot;
    public ItemEntry? Item;
    /// <summary>Outside edges of the equipment layout; shared dividers are drawn once by the next slot.</summary>
    public bool RightDivider, BottomDivider;
    /// <summary>The model's icon node (local); the box's centre when null.</summary>
    public Vector2? IconCentre;
    /// <summary>Space reserved at the bottom (the armour box's AC line).</summary>
    public float BottomReserve;
    public Action<ItemDrag>? Accept;
    bool _canTake, _cannotTake, _hot;

    public ClassicEquipSlot() { Source = "equip"; }

    Rect2 Area => new(Vector2.Zero, new Vector2(Size.X, Size.Y - BottomReserve));

    protected override ItemEntry? ItemAt(Vector2 local) => Item != null && new Rect2(Vector2.Zero, Size).HasPoint(local) ? Item : null;
    protected override Rect2 ItemRect(ItemEntry item) => Area;

    public void SetItem(ItemEntry? item)
    {
        Item = item;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // inv_eqp_* stretches the first box in gui_boxes_ingame. Its 2-texel fill
        // is drawn by the model; restore the one-texel edge excluded by GuiDraw's
        // narrow-slice inset. Layout and edge color both come from installed art.
        if (Assets.Texture("gui_boxes_ingame") is { } box)
        {
            var edge = new Rect2(0, 0, 1, 1);
            DrawTextureRectRegion(box, new Rect2(0, 0, Size.X, 1), edge);
            DrawTextureRectRegion(box, new Rect2(0, 0, 1, Size.Y), edge);
            if (RightDivider) DrawTextureRectRegion(box, new Rect2(Size.X - 1, 0, 1, Size.Y), edge);
            if (BottomDivider) DrawTextureRectRegion(box, new Rect2(0, Size.Y - 1, Size.X, 1), edge);
        }
        if (_canTake || _hot) ClassicItemArt.DrawCells(this, Assets.Texture(ClassicItemArt.SlotHilite), new Rect2(Vector2.Zero, Size), Size);
        else if (_cannotTake) ClassicItemArt.DrawCells(this, Assets.Texture(ClassicItemArt.SlotUnhilite), new Rect2(Vector2.Zero, Size), Size);
        if (Item != null)
        {
            bool carried = Carried?.Item.Id == Item.Id;
            if (HoverItem != null) ClassicItemArt.DrawCells(this, Assets.Texture(ClassicItemArt.CellHover), Area, Area.Size);
            ClassicItemArt.DrawIcon(this, Assets, Item, Area.Grow(-1), carried ? new Color(1, 1, 1, 0.3f) : Colors.White, IconCentre);
            if (!carried) ClassicItemArt.DrawCount(this, Assets, Item.Stack, Area);
        }
        else if (Assets.Texture(ClassicItemArt.Silhouette(Slot)) is { } sil)
            ClassicItemArt.DrawFitted(this, sil, Area, Colors.White, IconCentre);
    }

    protected override void OnDragBegan()
    {
        var d = CurrentDrag();
        _canTake = d != null && d != Carried && AcceptsFrom?.Invoke(d) == true;
        _cannotTake = d != null && !_canTake && d != Carried;
    }

    protected override void OnDragEnded() => _canTake = _cannotTake = _hot = false;

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool ok = ItemDragging.From(data) is { } d && (d == Carried || AcceptsFrom?.Invoke(d) == true);
        if (ok != _hot) { _hot = ok; QueueRedraw(); }
        return ok;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _hot = false;
        QueueRedraw();
        if (ItemDragging.From(data) is not { } d) return;
        if (DropBack(d, Pointer) || d == Carried) return;
        Accept?.Invoke(d);
    }

    public override void _Process(double delta)
    {
        if (_hot && !GetGlobalRect().HasPoint(GetGlobalMousePosition())) { _hot = false; QueueRedraw(); }
    }
}
