using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's quickbar: twelve slots along the bottom centre of the screen (pnl_quick_bar's Slot01..12 nodes,
/// 0.67 units apart), each a qb_but67 slot (the last one qb_but67end, which closes the row), on the brass line
/// the panel model draws under them. It shows and uses the modern action bar's slots (same data, keys, banks,
/// saving): hold Shift for the second bank and Ctrl for the third, scroll over the bar to change the base bank.
/// Drag spells, items, feats and skills onto it from any window; drag a slot off to clear it; right-click a slot for
/// its radial (assign, use, Empty Quickslot; docs/QUICKBAR_RADIAL.md).
/// </summary>
public partial class ClassicQuickbar : Control
{
    public ClassicContext Ctx = null!;
    public ActionBar Bar => Ctx.ActionBar;
    GuiModelView _frame = null!;
    readonly ClassicQuickSlot[] _slots = new ClassicQuickSlot[ActionBar.Slots];
    /// <summary>Set by a slot that took a drop from another slot (the source must not clear itself).</summary>
    internal bool DropHandled;

    public IReadOnlyList<ClassicQuickSlot> Slots => _slots;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var assets = Ctx.Assets;
        _frame = GuiModelView.Create(assets, "pnl_quick_bar");
        AddChild(_frame);
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = new ClassicQuickSlot { Bar = this, Index = i, Name = $"Slot{i + 1:00}" };
            slot.Model = assets.Model(i == _slots.Length - 1 ? "qb_but67end" : "qb_but67") ?? assets.Model("qb_but66");
            AddChild(slot);
            _slots[i] = slot;
        }
        Bar.User.Used += OnUsed;
        TreeExiting += () => Bar.User.Used -= OnUsed;
    }

    void OnUsed(BarSlot s)
    {
        for (int i = 0; i < _slots.Length; i++)
            if (Bar.Get(Bar.Page, i) == s) _slots[i].Flash();
    }

    /// <summary>
    /// Bottom centre: the model's origin (Slot01's bottom-left) sits on the bottom edge, the twelve slots centred
    /// (8 units wide: 240 px from the left at 1280).
    /// </summary>
    public void Place(Vector2 view)
    {
        var model = _frame.Model;
        float width = (model?.Node("Slot12")?.X ?? 7.33f * GuiModel.PixelsPerUnit) + 67;
        var origin = new Vector2(MathF.Floor((view.X - width) / 2), view.Y);
        Position = Vector2.Zero;
        Size = view;
        _frame.PlaceOrigin(origin);
        for (int i = 0; i < _slots.Length; i++)
        {
            var at = model?.Node($"Slot{i + 1:00}") ?? new Vector2(i * 67, 0);
            _slots[i].PlaceOrigin(origin + at);
        }
    }

    public override void _Process(double delta)
    {
        // The bank changes with Shift / Ctrl and the slots' uses with the server: redraw each frame (twelve quads).
        foreach (var s in _slots) s.QueueRedraw();
    }
}

/// <summary>One quickbar slot: the slot model in its state, the icon at the model's icon node, uses at count1.</summary>
public partial class ClassicQuickSlot : Control, IClassicHover
{
    public ClassicQuickbar Bar = null!;
    public int Index;
    GuiModel? _model;
    bool _hover, _pressed, _dragSource;
    double _flash, _sweep;

    public GuiModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            var b = _model?.Bounds ?? new Rect2(0, -33, 67, 33);
            Size = CustomMinimumSize = b.Size;
        }
    }

    Vector2 Origin => _model == null ? new Vector2(0, Size.Y) : -_model.Bounds.Position;
    public void PlaceOrigin(Vector2 point) => Position = point - Origin;

    ActionBar ActionBar => Bar.Bar;
    BarSlot? Slot => ActionBar.Get(ActionBar.Page, Index);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => _hover = true;
        MouseExited += () => { _hover = false; _pressed = false; };
    }

    public void Flash() { _flash = 0.2; _sweep = 1; }

    public override void _Process(double delta)
    {
        if (_flash > 0) _flash -= delta;
        if (_sweep > 0) _sweep -= delta;
    }

    public override void _Draw()
    {
        if (_model == null) return;
        var assets = Bar.Ctx.Assets;
        var content = Bar.Ctx.Session.Content;
        var slot = Slot;
        var look = slot != null ? ActionBar.User.Look(slot) : null;
        string state = _pressed || _flash > 0 ? "down"
            : look?.Toggled == true ? (slot!.Kind == "item" ? "qbEquip" : "qbMode")
            : _hover ? "hilite" : "up";
        var pose = _model.PoseOf(state);
        GuiDraw.Model(this, assets, pose, Origin, Colors.White);
        if (look == null)
        {
            DrawKey(assets, pose);
            return;
        }
        // A text macro: its label, no icon, and the key as on an empty slot (quickbargaps g_assigned).
        if (slot!.Kind == "macro")
        {
            ClassicQuickbarText.DrawMacro(this, assets, pose, Origin, slot);
            DrawKey(assets, pose);
            return;
        }
        var centre = (pose.Node("icon") ?? new Vector2(34, -17)) + Origin;
        var size = look.SecondaryIcons.Length > 0 ? new Vector2(64, 32) : new Vector2(32, 32);
        var box = new Rect2(centre - size / 2, size);
        var icons = ClassicIcons.For(content, look, slot);
        var mod = look.Usable ? (_hover ? GuiDraw.Hilite : Colors.White) : new Color(0.4f, 0.4f, 0.4f);
        ClassicIcons.Draw(this, content, look, icons, box, mod);
        if (look.Toggled)
        {
            // Modes that are on (and equipped items) glow, as the original lights its slot.
            float pulse = 0.55f + 0.25f * MathF.Sin((float)Time.GetTicksMsec() / 300f);
            DrawRect(box.Grow(1), new Color(1f, 0.85f, 0.35f, pulse), false, 1);
        }
        SlotArt.DrawSweep(this, box, (float)Math.Clamp(_sweep, 0, 1), new Color(0, 0, 0, 0.5f));
        if (look.Uses >= 0)
        {
            // Uses left: right-aligned at count1, as the original's "3" beside a potion.
            var font = assets.Font(ClassicChatStyle.FontName);
            var text = look.Uses.ToString();
            var at = (pose.Node("count1") ?? new Vector2(66, -16)) + Origin;
            font.Draw(this, new Vector2(at.X - font.Measure(text), at.Y - font.Height), text, look.Uses == 0 ? new Color(1, 0.3f, 0.3f) : Colors.White);
        }
        // Over the icon, when the player keeps key labels on filled slots (the original drops them there).
        if (Settings.Current.Interface.QuickbarKeyLabels) DrawKey(assets, pose);
    }

    /// <summary>
    /// The slot's key at the model's authored text anchor (qb_but67: 0.05, 0.04), grey (153,153,153 in the original's
    /// empty F12, quickbarradial c_emptied: x 979-998, baseline at y 715 of 720).
    /// </summary>
    void DrawKey(ClassicAssets assets, GuiModel.Pose pose)
    {
        string key = Preferences.InputBindings.KeyText(Preferences.ActionCatalog.Quickbar(Index));
        if (key.Length == 0) return;
        var font = assets.Font(ClassicChatStyle.FontName);
        var at = (pose.Node("text") ?? new Vector2(5, -4)) + Origin;
        font.Draw(this, at - new Vector2(0, font.Baseline), key, new Color(0.6f, 0.6f, 0.6f));
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed) _pressed = true;
            else
            {
                if (_pressed && new Rect2(Vector2.Zero, Size).HasPoint(mb.Position)) ActionBar.UseSlot(ActionBar.Page, Index);
                _pressed = false;
            }
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            // The original's slot radial (quickbarradial), opened on the press so a held button can be let go over a
            // choice; the release that follows comes back here and goes no further.
            if (right.Pressed) ActionBar.OpenSlotMenu(ActionBar.Page, Index, right.GlobalPosition);
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } w)
        {
            // Scrolling over the bar picks the bank shown without Shift / Ctrl.
            int page = ActionBar.Page + (w.ButtonIndex == MouseButton.WheelDown ? 1 : ActionBar.Pages - 1);
            ActionBar.SetBasePage(page % ActionBar.Pages);
            AcceptEvent();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        _pressed = false;
        if (Slot is not { } s) return default;
        var data = s.ToDrag();
        data["fromBar"] = ActionBar.Page;
        data["fromSlot"] = Index;
        _dragSource = true;
        SetDragPreview(DragPreview(ActionBar.User.Look(s), s));
        return data;
    }

    /// <summary>The icon that follows the mouse: the original's icon, centred on the cursor.</summary>
    Control DragPreview(SlotLook look, BarSlot slot)
    {
        var content = Bar.Ctx.Session.Content;
        var icons = ClassicIcons.For(content, look, slot);
        var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        var icon = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(-16, -16), Size = new Vector2(32, 32) };
        icon.Draw += () => ClassicIcons.Draw(icon, content, look, icons, new Rect2(Vector2.Zero, icon.Size), Colors.White);
        holder.AddChild(icon);
        return holder;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => BarSlot.FromDrag(data) != null;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (BarSlot.FromDrag(data) is not { } incoming) return;
        var d = data.VariantType == Variant.Type.Dictionary ? data.AsGodotDictionary() : new Godot.Collections.Dictionary();
        if (d.ContainsKey("fromBar") && d.ContainsKey("fromSlot"))
        {
            // Moving within the bar swaps with whatever was here.
            int fromPage = (int)d["fromBar"].AsInt64(), fromSlot = (int)d["fromSlot"].AsInt64();
            if (fromPage == ActionBar.Page && fromSlot == Index) { Bar.DropHandled = true; return; }
            var here = Slot;
            ActionBar.Set(fromPage, fromSlot, here);
            Bar.DropHandled = true;
        }
        else incoming = ActionBar.CombineEquipmentDrop(Slot, incoming, data);
        ActionBar.Set(ActionBar.Page, Index, incoming);
        ClassicSounds.Play(Bar.Ctx.Assets, "gui_quick_add");
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_dragSource) return;
        _dragSource = false;
        // Dragged off the bar (not onto another slot): clear it.
        if (!GetViewport().GuiIsDragSuccessful() && !Bar.DropHandled)
        {
            ActionBar.Set(ActionBar.Page, Index, null);
            ClassicSounds.Play(Bar.Ctx.Assets, "gui_quick_erase");
        }
        Bar.DropHandled = false;
    }

    public string[]? HoverLines()
    {
        string key = Preferences.InputBindings.KeyText(Preferences.ActionCatalog.Quickbar(Index)) is { Length: > 0 } k ? k : $"F{Index + 1}";
        if (Slot is not { } s) return null;
        if (s.Kind == "macro") return [ClassicQuickbarText.Tooltip(key, s)];
        var look = ActionBar.User.Look(s);
        return [$"{look.Name} ({key})", look.Detail];
    }
}
