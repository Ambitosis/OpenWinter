using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// What is being dragged between item windows: the item and where it came from
/// ("pack", "equip", "store", "container"), plus the equipment slot for "equip".
/// </summary>
public partial class ItemDrag : RefCounted
{
    public ItemEntry Item = null!;
    public string Source = "";
    public int Slot = -1;
    /// <summary>The grid it was picked up from (0: the player's backpack; a bag, container or store id).</summary>
    public uint Holder;
    /// <summary>Which of its cells the cursor holds it by (so it lands where the ghost shows).</summary>
    public int GrabX, GrabY;
    /// <summary>The acquisition acknowledgement supplies the placement sound after a loot transfer.</summary>
    public bool ServerPlacementSound;
}

/// <summary>
/// Drag-and-drop plumbing shared by <see cref="ItemSlot"/>, <see cref="ItemRow"/> and <see cref="ItemDropArea"/>:
/// the floating icon under the cursor, highlighting valid targets while a drag is on, and telling
/// a drop on the 3D world (nothing under the cursor) from a drop on a window.
/// </summary>
public static class ItemDragging
{
    static Vector2? _pointer;
    public static void Observe(InputEvent e) { if (e is InputEventMouse mouse) _pointer = mouse.Position; }
    public static Vector2 Pointer(Control control) => _pointer ?? control.GetViewport().GetMousePosition();
    public static void PlayPickupSound(ClientContent? content)
    {
        if (content != null) Classic.ClassicSounds.Play(Classic.ClassicAssets.For(content), "it_pickup");
    }

    public static void PlaySound(ClientContent? content, ItemEntry item)
    {
        if (content == null) return;
        string? sound = item.Info?.InventorySound;
        if (sound == null)
        {
            int type = content.Resources.TwoDA("baseitems").GetInt(item.BaseItem, "InvSoundType", -1);
            sound = content.Resources.TwoDA("inventorysnds").Get(type, "InventorySound");
        }
        Classic.ClassicSounds.Play(Classic.ClassicAssets.For(content), sound);
    }

    public static void PlayDropSound(ClientContent? content, ItemDrag drag)
    {
        if (!drag.ServerPlacementSound) PlaySound(content, drag.Item);
    }

    public static ItemDrag? Current(Control c) =>
        c.IsInsideTree() && c.GetViewport().GuiIsDragging() ? From(c.GetViewport().GuiGetDragData()) : null;

    public static ItemDrag? From(Variant data) => data.VariantType switch
    {
        Variant.Type.Object => data.AsGodotObject() as ItemDrag,
        // The classic interface drags the action bar's "item" dictionary with the ItemDrag under "drag".
        Variant.Type.Dictionary => data.AsGodotDictionary().TryGetValue("drag", out var v) && v.VariantType == Variant.Type.Object ? v.AsGodotObject() as ItemDrag : null,
        _ => null,
    };

    /// <summary>
    /// Starts a drag from <paramref name="from"/>: a translucent ghost of the item follows the cursor,
    /// the size of its footprint when it comes from a grid (<paramref name="cell"/> &gt; 0), held by the grabbed cell.
    /// </summary>
    public static ItemDrag Begin(Control from, ItemEntry item, ClientContent? content, string source, int slot = -1, float cell = 0, Vector2 grabbedAt = default, uint gridHolder = 0)
    {
        PlayPickupSound(content);
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        int grabX = 0, grabY = 0;
        Vector2 size = new(60, 60), offset = new(-30, -30);
        if (cell > 0)
        {
            size = new Vector2(item.W * cell, item.H * cell);
            grabX = Math.Clamp((int)(grabbedAt.X / cell), 0, item.W - 1);
            grabY = Math.Clamp((int)(grabbedAt.Y / cell), 0, item.H - 1);
            offset = -new Vector2((grabX + 0.5f) * cell, (grabY + 0.5f) * cell);
        }
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Size = size, Position = offset };
        // From a grid: a light ghost, so the green / red footprint under it stays readable.
        var s = cell > 0 ? Styles.Box(Palette.Bg3 with { A = 0.35f }, Palette.RadiusSmall, Palette.LineStrong, 1, 3) : Styles.Slot(hover: true, active: true);
        s.ShadowColor = new Color(0, 0, 0, 0.45f);
        s.ShadowSize = 12;
        frame.AddThemeStyleboxOverride("panel", s);
        if (content != null) frame.AddChild(UiTheme.Icon(content, item.Icon, item.Stack, (int)Math.Min(size.X, size.Y) - 8));
        frame.Modulate = new Color(1, 1, 1, cell > 0 ? 0.7f : 0.88f);
        holder.AddChild(frame);
        from.SetDragPreview(holder);
        return new ItemDrag { Item = item, Source = source, Slot = slot, Holder = gridHolder, GrabX = grabX, GrabY = grabY };
    }

    /// <summary>True when the cursor is over no window or HUD control: the drop landed on the world.</summary>
    public static bool OverWorld(Control c)
    {
        var vp = c.GetViewport();
        var mouse = Pointer(c);
        foreach (var w in Classic.ClassicWindow.OpenWindows)
            if (GodotObject.IsInstanceValid(w) && w.IsVisibleInTree() && w.GetGlobalRect().HasPoint(mouse)) return false;
        foreach (var w in WindowStack.Windows)
            if (GodotObject.IsInstanceValid(w) && w.Visible && w.GetGlobalRect().HasPoint(mouse)) return false;
        return vp.GuiGetHoveredControl() == null;
    }
}

/// <summary>
/// An item slot: a square with the item's icon (or a faint silhouette when empty), rarity-tinted
/// border, rich tooltip, drag and drop, double-click and right-click. Used by the paper doll,
/// the backpack grid and the loot window.
/// </summary>
public partial class ItemSlot : Button
{
    public ItemEntry? Item { get; private set; }
    public ClientContent? Content;
    /// <summary>Silhouette shown while the slot is empty (Icons name: helm, ring...).</summary>
    public string? Placeholder;
    /// <summary>Tooltip of an empty slot ("Head").</summary>
    public string EmptyTooltip = "";
    /// <summary>Drag source name; empty = the item can't be dragged from here.</summary>
    public string DragSource = "";
    /// <summary>Equipment slot index (paper doll) or -1.</summary>
    public int EquipSlot = -1;
    public Func<ItemEntry, ItemTooltip.Extra?>? TooltipExtra;
    public Func<ItemDrag, bool>? CanAccept;
    public Action<ItemDrag>? Accept;
    /// <summary>The item was dragged out and released over the 3D world.</summary>
    public Action<ItemDrag>? DroppedOnWorld;
    public Action<ItemEntry>? Clicked, DoubleClicked;
    /// <summary>Right-click: the item and the screen position for a context menu.</summary>
    public Action<ItemEntry, Vector2>? ContextRequested;

    int _size;
    bool _target, _dropHover, _dragging, _unusable;
    Control? _icon;
    ItemDrag? _drag;

    /// <summary>Grid tiles: the cell size (the drag ghost covers the footprint) and the grid's holder id.</summary>
    public float CellSize;
    public uint GridHolder;
    /// <summary>False for grid tiles: the grid draws the drop footprint instead of the tile lighting up.</summary>
    public bool ShowDropHighlight = true;

    public static ItemSlot Create(int size = 56, string? placeholder = null, string emptyTooltip = "") =>
        Create(new Vector2(size, size), placeholder, emptyTooltip);

    /// <summary>A slot of any size (a grid tile spans the item's cells).</summary>
    public static ItemSlot Create(Vector2 size, string? placeholder = null, string emptyTooltip = "")
    {
        var s = new ItemSlot
        {
            _size = (int)Math.Min(size.X, size.Y), Placeholder = placeholder, EmptyTooltip = emptyTooltip,
            CustomMinimumSize = size, Size = size, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        s.SetItem(null, null);
        return s;
    }

    public void SetItem(ItemEntry? item, ClientContent? content)
    {
        Item = item;
        Content = content;
        _icon?.QueueFree();
        _icon = null;
        _unusable = item?.Info?.Requirements.Any(r => !r.Met) == true;
        TooltipText = item?.Name ?? EmptyTooltip;
        if (item != null && content != null)
            _icon = UiTheme.Icon(content, item.Icon, item.Stack, _size - 8);
        else if (item == null && Placeholder != null)
            _icon = Icons.Rect(Placeholder, (int)(_size * 0.52f), new Color(1, 1, 1, 0.11f));
        if (_icon != null)
        {
            _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _icon.OffsetLeft = _icon.OffsetTop = 4;
            _icon.OffsetRight = _icon.OffsetBottom = -4;
            _icon.MouseFilter = MouseFilterEnum.Ignore;
            if (_unusable) _icon.Modulate = new Color(1f, 0.66f, 0.62f);
            AddChild(_icon);
        }
        MouseDefaultCursorShape = item != null ? CursorShape.PointingHand : CursorShape.Arrow;
        RefreshStyle();
    }

    void RefreshStyle()
    {
        var tint = ItemUi.RarityTint(Item);
        StyleBoxFlat Make(bool hover, bool pressed)
        {
            var s = Styles.Slot(hover, pressed);
            if (tint is { } t && !pressed) s.BorderColor = t with { A = hover ? 0.95f : 0.55f };
            if (_target)
            {
                s.BorderColor = _dropHover ? Palette.AccentBright : Palette.AccentDim;
                s.SetBorderWidthAll(_dropHover ? 2 : 1);
                if (_dropHover) s.BgColor = Palette.AccentGlow;
            }
            if (_unusable && !_target) s.BgColor = new Color(0.32f, 0.07f, 0.06f, 0.42f);
            return s;
        }
        AddThemeStyleboxOverride("normal", Make(false, false));
        AddThemeStyleboxOverride("hover", Make(true, false));
        AddThemeStyleboxOverride("pressed", Make(true, true));
        AddThemeStyleboxOverride("hover_pressed", Make(true, true));
        AddThemeStyleboxOverride("disabled", Make(false, false));
        QueueRedraw();
    }

    public override void _Draw()
    {
        // A soft glow rising from the bottom for magic and plot items.
        if (ItemUi.RarityTint(Item) is not { } tint) return;
        var r = new Rect2(Vector2.Zero, Size).Grow(-2);
        const int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)steps;
            float h = r.Size.Y * 0.55f / steps;
            DrawRect(new Rect2(r.Position.X, r.End.Y - (i + 1) * h, r.Size.X, h), tint with { A = 0.16f * (1 - t) });
        }
    }

    public override GodotObject? _MakeCustomTooltip(string forText)
    {
        if (Item == null) return null;
        return ItemTooltip.Build(Item, Content, TooltipExtra?.Invoke(Item));
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb || Item == null) return;
        if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick) { DoubleClicked?.Invoke(Item); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Right) { ContextRequested?.Invoke(Item, GetGlobalTransform() * mb.Position); AcceptEvent(); }
    }

    public override void _Ready()
    {
        Pressed += () => { if (Item != null) Clicked?.Invoke(Item); };
        MouseExited += () => { if (_dropHover) { _dropHover = false; RefreshStyle(); } };
    }

    bool _dragCancelled;
    public override void _Input(InputEvent e)
    {
        ItemDragging.Observe(e);
        if (_dragging && GetViewport().GuiIsDragging() &&
            (e is InputEventKey { Keycode: Key.Escape, Pressed: true } || e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }))
        {
            _dragCancelled = true;
            GetViewport().GuiCancelDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Item == null || DragSource.Length == 0) return default;
        _drag = ItemDragging.Begin(this, Item, Content, DragSource, EquipSlot, CellSize, atPosition, GridHolder);
        _dragging = true;
        _dragCancelled = false;
        if (_icon != null) _icon.Modulate = _icon.Modulate with { A = 0.3f };
        return _drag;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool ok = ItemDragging.From(data) is { } d && (d != _drag || !ShowDropHighlight) && CanAccept?.Invoke(d) == true;
        if (ShowDropHighlight && ok != _dropHover) { _dropHover = ok; RefreshStyle(); }
        return ok;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _dropHover = false;
        if (ItemDragging.From(data) is { } d) Accept?.Invoke(d);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin)
        {
            bool target = ShowDropHighlight && ItemDragging.Current(this) is { } d && d != _drag && d.Item != Item && CanAccept?.Invoke(d) == true;
            if (target != _target) { _target = target; RefreshStyle(); }
        }
        else if (what == NotificationDragEnd)
        {
            if (_dragging && _drag != null)
            {
                _dragging = false;
                if (_icon != null) _icon.Modulate = _icon.Modulate with { A = 1f };
                if (!_dragCancelled && GetViewport().GuiIsDragSuccessful()) ItemDragging.PlayDropSound(Content, _drag);
                if (!_dragCancelled && !GetViewport().GuiIsDragSuccessful() && ItemDragging.OverWorld(this)) DroppedOnWorld?.Invoke(_drag);
                _drag = null;
            }
            if (_target || _dropHover) { _target = _dropHover = false; RefreshStyle(); }
        }
    }
}

/// <summary>A container that accepts dropped items anywhere inside it (the backpack area, a loot window).</summary>
public partial class ItemDropArea : PanelContainer
{
    public Func<ItemDrag, bool>? CanAccept;
    public Action<ItemDrag>? Accept;
    bool _target, _hover;
    StyleBox? _base;

    public override void _Ready()
    {
        _base = GetThemeStylebox("panel");
        MouseExited += () => { if (_hover) { _hover = false; Refresh(); } };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool ok = ItemDragging.From(data) is { } d && CanAccept?.Invoke(d) == true;
        if (ok != _hover) { _hover = ok; Refresh(); }
        return ok;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _hover = false;
        Refresh();
        if (ItemDragging.From(data) is { } d) Accept?.Invoke(d);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin)
        {
            _target = ItemDragging.Current(this) is { } d && CanAccept?.Invoke(d) == true;
            Refresh();
        }
        else if (what == NotificationDragEnd && (_target || _hover)) { _target = _hover = false; Refresh(); }
    }

    void Refresh()
    {
        if (_base is not StyleBoxFlat b) return;
        if (!_target && !_hover) { AddThemeStyleboxOverride("panel", b); return; }
        var s = (StyleBoxFlat)b.Duplicate();
        s.BorderColor = _hover ? Palette.Accent : Palette.AccentDim with { A = 0.7f };
        s.SetBorderWidthAll(_hover ? 2 : 1);
        if (_hover) s.BgColor = s.BgColor.Blend(Palette.AccentGlow with { A = 0.08f });
        AddThemeStyleboxOverride("panel", s);
    }
}
