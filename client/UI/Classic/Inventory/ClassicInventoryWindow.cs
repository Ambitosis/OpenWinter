using System.Text.Json;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's inventory (inv_panel): the equipment (inv_equip) or an open container or bag (inv_container)
/// in the top half, the backpack grid (inv_box_10x6 at the panel's "repository" node) with its six page tabs
/// (ctl_inv_btn_tab down the "tabs" node), and the examine and gold buttons, weight and gold along the bottom.
/// Top-left of the screen, 360 px wide; beside the store while one is open.
/// </summary>
public partial class ClassicInventoryWindow : ClassicWindow
{
    public ClassicItems Items = null!;

    GuiPanel _equip = null!, _cont = null!;
    ClassicItemGrid _grid = null!, _contGrid = null!;
    readonly List<InvTab> _tabs = new();
    readonly Dictionary<int, ClassicEquipSlot> _slots = new();
    GuiLabel? _title, _weight, _maxWeight, _gold, _ac, _contTitle;
    GuiButton? _contLabel, _inspect;
    GuiModelView? _contBox;
    int _armorClass = -1;
    bool _examineMode;

    // The open container or bag.
    uint _container;
    ItemEntry? _bag;
    string _contName = "";
    List<ItemEntry> _contItems = new();
    GridSpec _contSpec = new(7, 5, 0);
    bool _serverClosed, _openedForContainer;

    /// <summary>Equipment boxes of inv_equip and the slots (INVENTORY_SLOT_*) they hold.</summary>
    public static readonly (string Tag, int Slot)[] Boxes =
    [
        ("HelmetBox", 0), ("ArmorBox", 1), ("BootBox", 2), ("GauntletBox", 3), ("RightWeaponBox", 4), ("LeftWeaponBox", 5),
        ("CloakBox", 6), ("RingBox1", 7), ("RingBox2", 8), ("AmuletBox", 9), ("BeltBox", 10),
        ("AmmoBox3", 11), ("AmmoBox1", 12), ("AmmoBox2", 13),
    ];

    public ClassicItemGrid Grid => _grid;
    public ClassicItemGrid ContainerGrid => _contGrid;
    public IReadOnlyDictionary<int, ClassicEquipSlot> Slots => _slots;
    public bool ContainerOpen => _cont.Visible;
    public bool ExamineMode => _examineMode;

    protected override GuiPanel Build()
    {
        var p = GuiPanel.FromLayout(Assets, "inv_panel") ?? GuiPanel.FromModel(Assets, "pnl_inv");
        _weight = p.Get<GuiLabel>("WeightLabel");
        _maxWeight = p.Get<GuiLabel>("MaxWeightLabel");
        _gold = p.Get<GuiLabel>("GPLabel");
        _inspect = p.Get<GuiButton>("InspectButton");
        if (_inspect != null) _inspect.SetMeta(ClassicTooltip.Meta, "Examine an item");
        if (p.Get<GuiButton>("GoldButton") is { } goldButton) goldButton.SetMeta(ClassicTooltip.Meta, "Drop gold");
        p.ButtonPressed += tag =>
        {
            if (tag == "InspectButton") SetExamineMode(!_examineMode);
            if (tag == "GoldButton") Items.Split.ShowGold(Items.Data.Gold);
        };

        // The backpack grid and its page tabs.
        var box = GuiModelView.Create(Assets, "inv_box_10x6");
        box.PlaceOrigin(p.Node("repository") ?? new Vector2(3, 418));
        p.AddChild(box);
        _grid = new ClassicItemGrid { Assets = Assets, Columns = 10, Rows = 6, Position = box.Position, Size = box.Size, Name = "Backpack" };
        _grid.Cell = new Vector2(box.Size.X / 10, box.Size.Y / 6);
        WirePack(_grid);
        p.AddChild(_grid);
        var tabModel = Assets.Model("ctl_inv_btn_tab");
        var step = tabModel?.Node("next") ?? new Vector2(0, 33);
        var at = p.Node("tabs") ?? new Vector2(334, 219);
        for (int i = 0; i < 6; i++)
        {
            var tab = new InvTab { Assets = Assets, Model = tabModel, Page = i, Turn = page => _grid.Page = page, Name = $"Tab{i + 1}" };
            tab.PlaceOrigin(at + step * i);
            _tabs.Add(tab);
            p.AddChild(tab);
        }
        _grid.PageChanged += RefreshTabs;

        // Equipment.
        _equip = GuiPanel.FromLayout(Assets, "inv_equip") ?? GuiPanel.FromModel(Assets, "pnl_equip");
        _equip.Position = (p.Node("Equip") ?? new Vector2(0, 217)) - _equip.OriginOf(_equip.Frame);
        // Its box reaches over the grid's top row: only its controls take the mouse.
        _equip.MouseFilter = MouseFilterEnum.Ignore;
        p.AddChild(_equip);
        _title = _equip.Get<GuiLabel>("TGuiLabel");
        _ac = _equip.Get<GuiLabel>("ArmorClassLabel");
        foreach (var (tag, slot) in Boxes)
        {
            if (_equip.Get<GuiModelView>(tag) is not { } v) continue;
            var s = new ClassicEquipSlot { Assets = Assets, Slot = slot, Position = v.Position, Size = v.Size, IconCentre = v.Node("icon"), Name = "Slot" + slot };
            if (slot == 1) s.BottomReserve = 24;
            WireEquip(s);
            _equip.AddChild(s);
            _slots[slot] = s;
        }
        if (_slots.Count > 0)
        {
            float right = _slots.Values.Max(s => s.Position.X + s.Size.X);
            float bottom = _slots.Values.Max(s => s.Position.Y + s.Size.Y);
            foreach (var s in _slots.Values)
            {
                s.RightDivider = Math.Abs(s.Position.X + s.Size.X - right) < .1f;
                s.BottomDivider = Math.Abs(s.Position.Y + s.Size.Y - bottom) < .1f;
            }
        }
        if (_ac != null) _equip.MoveChild(_ac, -1);

        // Containers and bags take the equipment's place.
        _cont = GuiPanel.FromLayout(Assets, "inv_container") ?? GuiPanel.FromModel(Assets, "pnl_container");
        _cont.Position = (p.Node("Container") ?? new Vector2(0, 217)) - _cont.OriginOf(_cont.Frame);
        _cont.Visible = false;
        _cont.MouseFilter = MouseFilterEnum.Ignore;
        p.AddChild(_cont);
        _contTitle = _cont.Get<GuiLabel>("Title");
        _contLabel = _cont.Get<GuiButton>("Label");
        if (_contLabel != null) _contLabel.MouseFilter = MouseFilterEnum.Ignore;
        _contBox = _cont.Get<GuiModelView>("ItemGrid");
        _contGrid = new ClassicItemGrid { Assets = Assets, Columns = 7, Rows = 5, Name = "ContainerGrid" };
        if (_contBox != null)
        {
            _contGrid.Position = _contBox.Position;
            _contGrid.Size = _contBox.Size;
            _contGrid.Cell = new Vector2(_contBox.Size.X / 7, _contBox.Size.Y / 5);
        }
        _contGrid.PageChanged += RefreshContainerLabel;
        _cont.AddChild(_contGrid);
        _cont.ButtonPressed += tag =>
        {
            switch (tag)
            {
                case "LeftButton": _contGrid.Page--; break;
                case "RightButton": _contGrid.Page++; break;
                case "CloseButton": CloseContainer(); break;
                case "XButton": Close(); break;
            }
        };

        Items.Data.Changed += () => { if (IsInstanceValid(this)) Fill(); };
        CharacterSheetRefresh.Attach(this, Ctx.Session);
        Ctx.Session.NotifyReceived += OnNotify;
        Ctx.Session.ControlledChanged += ControlChanged;
        TreeExiting += () => Ctx.Session.ControlledChanged -= ControlChanged;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
        return p;
    }

    // ------------------------------------------------------------------ wiring

    void WirePack(ClassicItemGrid g)
    {
        g.Source = "pack";
        g.Holder = 0;
        g.AcceptsFrom = d => d.Source is "pack" or "equip" or "container" or "bag" or "store" or "ground" or "barter";
        g.Dropped = (d, x, y) =>
        {
            d.ServerPlacementSound = d.Source is "container" or "ground" or "store";
            switch (d.Source)
            {
                case "ground": Ctx.Session.Request("pickup", new { item = d.Item.Id, x, y }); break;
                case "container": Ctx.Session.Request("container-take", new { item = d.Item.Id, x, y }); break;
                case "store": Items.Store.Buy(d.Item, (x, y), confirm: false, playPickup: false); break;
                case "barter": Ctx.Session.Request("barter-withdraw", new { item = d.Item.Id }); break;
                default: Ctx.Session.Request("item-move", new { item = d.Item.Id, x, y }); break;
            }
        };
        g.Activated = Items.Primary;
        g.Context = Items.ShowMenu;
        g.DroppedOnWorld = d => Items.DropOnWorld(d.Item);
        g.SplitRequested = i => Items.Split.Show(i);
        g.Clicked = ExamineClick;
        g.ShiftClicked = QuickSell;
        g.Hovered = i => Items.Store.ShowInfo(i, selling: true);
        g.Tip = PackTip;
    }

    void WireEquip(ClassicEquipSlot s)
    {
        int slot = s.Slot;
        s.AcceptsFrom = d => d.Source is "pack" or "bag" or "ground" && ClassicItems.Fits(d.Item, slot);
        s.Accept = d => Ctx.Session.Request(d.Source == "ground" ? "equip-ground" : "equip", new { item = d.Item.Id, slot });
        s.Activated = i => Ctx.Session.Request("unequip", new { item = i.Id });
        s.Context = Items.ShowMenu;
        s.DroppedOnWorld = d => Items.DropOnWorld(d.Item);
        s.Clicked = ExamineClick;
        s.Tip = i => i.Name;
    }

    string PackTip(ItemEntry item) => item.Name;

    /// <summary>With the examine button down, a click on an item examines it.</summary>
    bool ExamineClick(ItemEntry item)
    {
        // A spell waiting for its target takes the clicked item (crafting feats cast at blank items).
        if (Ctx.Interaction.TargetItem(item.Id)) return true;
        if (!_examineMode) return false;
        SetExamineMode(false);
        Items.ExamineItem(item.Id);
        return true;
    }

    void SetExamineMode(bool on)
    {
        _examineMode = on;
        if (_inspect != null) _inspect.ForcedState = on ? "down" : null;
        HudParts.Cursors.InterfaceCursor = on ? 17 : null;
    }

    // ------------------------------------------------------------------ data

    void ControlChanged(uint _) => Close();

    public override void Close()
    {
        if (!Visible) return;
        SetExamineMode(false);
        if (_cont.Visible) CloseContainer(closeWindow: false);
        base.Close();
    }

    public override void Refresh() => Fill();

    /// <summary>Width of the party bar along the right edge (the inventory sits against it while a store is open).</summary>
    const float PartyBarWidth = 80;

    /// <summary>Inventory participates in the same native panel slots as the store and character sheets.</summary>
    public void Reposition()
    {
        ArrangeWithPanels = true;
        Place();
    }

    public override void Open()
    {
        ClassicSheets.Memorize?.Close();
        Reposition();
        base.Open();
        Ctx.Session.Request("inventory");
        Ctx.Session.Request("character");
    }

    /// <summary>Turns the backpack's page ([ and ] while open).</summary>
    public void Cycle(int step)
    {
        if (!Visible) return;
        int pages = _grid.PageCount;
        _grid.Page = ((_grid.Page + step) % pages + pages) % pages;
    }

    void Fill()
    {
        if (_grid == null) return;
        var data = Items.Data;
        string name = Ctx.Session.Controlled?.DisplayName is { Length: > 0 } n ? NwnText.Strip(n) : "";
        if (_title != null) _title.Text = name.Length > 0 ? $"Inventory: {name}" : "Inventory";
        _grid.Spec = data.Grid;
        _grid.SetItems(data.Carried);
        foreach (var (slot, s) in _slots) s.SetItem(data.Equipped.GetValueOrDefault(slot));
        if (_weight != null) _weight.Text = ((data.Weight + 5) / 10).ToString();
        if (_maxWeight != null) _maxWeight.Text = (data.NormalLoad / 10).ToString();
        if (_gold != null) _gold.Text = $"GP: {data.Gold}";
        if (_ac != null) _ac.Text = _armorClass >= 0 ? $"AC: {_armorClass}" : "AC:";
        RefreshTabs();
        if (_bag != null)
        {
            if (data.Carried.All(i => i.Id != _bag.Id)) CloseContainer(closeWindow: false);
            else ShowContainerItems(data.BagContents.GetValueOrDefault(_bag.Id) ?? new());
        }
    }

    void RefreshTabs()
    {
        for (int i = 0; i < _tabs.Count; i++) _tabs[i].Checked = i == _grid.Page;
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "inventory-denied" when m.Subject == Ctx.Session.ControlledId: Close(); break;
            case "character-sheet" when m.Subject == Ctx.Session.ControlledId:
            {
                using var doc = JsonDocument.Parse(m.Json);
                if (doc.RootElement.TryGetProperty("ac", out var ac) && ac.TryGetInt32(out int v)) _armorClass = v;
                if (_ac != null) _ac.Text = $"AC: {_armorClass}";
                break;
            }
            case "creature-equipment" when m.Subject == Ctx.Session.ControlledId && Visible:
                Ctx.Session.Request("inventory");
                Ctx.Session.Request("character");
                break;
            case "container-open":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                _bag = null;
                _container = d.TryGetProperty("container", out var c) ? c.GetUInt32() : m.Subject;
                _contName = d.TryGetProperty("name", out var nm) && nm.GetString() is { Length: > 0 } s ? NwnText.Strip(s) : "Container";
                _contSpec = GridSpec.From(d, "grid", new GridSpec(7, 5, 0));
                var items = d.GetProperty("items").EnumerateArray().Select(e => ItemEntry.From(e)).ToList();
                if (!_cont.Visible) _contGrid.Page = 0;
                WireContainer();
                ShowContainerItems(items);
                if (!Visible) { _openedForContainer = true; Open(); }
                break;
            }
            case "container-close":
                if (_bag == null && _cont.Visible)
                {
                    _serverClosed = true;
                    CloseContainer(closeWindow: _openedForContainer);
                }
                break;
        }
    }

    // ------------------------------------------------------------------ containers and bags

    void WireContainer()
    {
        var g = _contGrid;
        g.Source = "container";
        g.Holder = _container;
        g.Spec = _contSpec;
        g.AcceptsFrom = d => d.Source is "pack" or "bag" or "container";
        g.Dropped = (d, x, y) =>
        {
            if (d.Source == "container") Ctx.Session.Request("item-move", new { item = d.Item.Id, x, y, target = _container });
            else Ctx.Session.Request("container-put", new { item = d.Item.Id, x, y });
        };
        g.Activated = i => Ctx.Session.Request("container-take", new { item = i.Id });
        g.Context = (i, at) => Items.ShowForeignMenu(i, at, () => IsOpen && _cont.Visible && _bag == null,
            (0, "Examine", "", () => Items.ExamineItem(i.Id)),
            (4, "Take", "ir_pickup", () => Ctx.Session.Request("container-take", new { item = i.Id })),
            (3, "Take All", "", () => Ctx.Session.Request("container-take-all")));
        g.DroppedOnWorld = null;
        g.SplitRequested = null;
        g.ShiftClicked = null;
        g.Clicked = item =>
        {
            if (!ExamineClick(item)) Ctx.Session.Request("container-take", new { item = item.Id });
            return true; // A click loots automatically; a held drag still selects an explicit destination.
        };
        g.Hovered = null;
        g.Tip = null;
    }

    void WireBag(ItemEntry bag)
    {
        var g = _contGrid;
        g.Source = "bag";
        g.Holder = bag.Id;
        g.Spec = Items.Data.BagGrid;
        g.AcceptsFrom = d => d.Source is "pack" or "bag" or "equip" && !d.Item.IsBag;
        g.Dropped = (d, x, y) => Ctx.Session.Request("item-move", new { item = d.Item.Id, x, y, target = bag.Id });
        g.Activated = Items.Primary;
        g.Context = Items.ShowMenu;
        g.DroppedOnWorld = d => Items.DropOnWorld(d.Item);
        g.SplitRequested = i => Items.Split.Show(i);
        g.Clicked = ExamineClick;
        g.ShiftClicked = QuickSell;
        g.Hovered = i => Items.Store.ShowInfo(i, selling: true);
        g.Tip = PackTip;
    }

    /// <summary>Shift-click in the backpack or a bag while a store is open: sells the item (the whole stack) at once,
    /// without asking, as Shift-click on a ware buys it.</summary>
    bool QuickSell(ItemEntry item)
    {
        if (!Items.Store.CanSell(item)) return false;
        Items.Store.Sell(item, confirm: false);
        return true;
    }

    /// <summary>Opens a bag from the backpack in the top half (in place of the equipment).</summary>
    public void OpenBag(ItemEntry bag)
    {
        if (_cont.Visible && _bag == null) CloseContainer(closeWindow: false);
        _bag = bag;
        _contName = bag.Name;
        _contGrid.Page = 0;
        WireBag(bag);
        if (!Visible) Open();
        ShowContainerItems(Items.Data.BagContents.GetValueOrDefault(bag.Id) ?? new());
    }

    void ShowContainerItems(List<ItemEntry> items)
    {
        _contItems = items;
        _contGrid.SetItems(items);
        _equip.Visible = false;
        _cont.Visible = true;
        // The original titles the window with the container's name and numbers its pages between the arrows.
        if (_contTitle != null) _contTitle.Text = _contName;
        RefreshContainerLabel();
    }

    void RefreshContainerLabel()
    {
        if (_contLabel == null) return;
        int pages = _contGrid.PageCount;
        _contLabel.Caption = $"{_contGrid.Page + 1:00}/{pages:00}";
    }

    /// <summary>Puts an item from the backpack into the open container or bag.</summary>
    public void PutInContainer(ItemEntry item)
    {
        if (_bag != null) Ctx.Session.Request("item-move", new { item = item.Id, target = _bag.Id });
        else Ctx.Session.Request("container-put", new { item = item.Id });
    }

    /// <summary>Back to the equipment; a container is closed on the server too (unless it closed it).</summary>
    public void CloseContainer(bool closeWindow = true)
    {
        if (!_cont.Visible) return;
        bool wasContainer = _bag == null;
        _cont.Visible = false;
        _equip.Visible = true;
        _bag = null;
        if (wasContainer && !_serverClosed) Ctx.Session.Request("container-close");
        _serverClosed = false;
        bool close = closeWindow && _openedForContainer && wasContainer;
        _openedForContainer = false;
        if (close) Close();
    }

    public uint OpenContainerId => _cont.Visible && _bag == null ? _container : 0;

    /// <summary>A page tab of the backpack (ctl_inv_btn_tab): checked shows the arrow (gui_inv_icons' lower half), else a dot. Carrying an item over one turns to it.</summary>
    public partial class InvTab : Control
    {
        public ClassicAssets Assets = null!;
        public GuiModel? Model;
        public int Page;
        public Action<int> Turn = null!;
        bool _checked;

        public bool Checked { get => _checked; set { if (_checked == value) return; _checked = value; QueueRedraw(); } }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += QueueRedraw;
            MouseExited += QueueRedraw;
            SetMeta(ClassicTooltip.Meta, $"Page {Page + 1}");
        }

        Vector2 Origin => Model == null ? Vector2.Zero : -Model.Bounds.Position;

        public void PlaceOrigin(Vector2 point)
        {
            var size = Model?.Bounds.Size ?? new Vector2(16, 33);
            Size = CustomMinimumSize = size;
            Position = point - Origin;
        }

        public override void _Draw()
        {
            if (Model == null) return;
            var pose = Model.PoseOf(_checked ? "checkedup" : "uncheckedup");
            bool hover = GetGlobalRect().HasPoint(GetGlobalMousePosition());
            GuiModel.PosedMesh? icon = pose.Meshes.FirstOrDefault(m => m.Texture.Equals("gui_inv_icons", StringComparison.OrdinalIgnoreCase));
            GuiDraw.Model(this, Assets, pose, Origin, hover ? GuiDraw.Hilite : Colors.White, skip: m => m == icon);
            if (icon == null || Assets.Texture("gui_inv_icons") is not { } tex) return;
            var r = new Rect2(icon.Points[0], Vector2.Zero);
            foreach (var p in icon.Points) r = r.Expand(p);
            var c = r.GetCenter() + Origin;
            float h = tex.GetHeight() / 2f, w = tex.GetWidth();
            // Upper half: the dot; lower half: the arrow of the page shown.
            var src = new Rect2(0, _checked ? h : 0, w, h);
            DrawTextureRectRegion(tex, new Rect2((c - new Vector2(w, h) / 2).Round(), new Vector2(w, h)), src);
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                Turn(Page);
                AcceptEvent();
            }
        }

        double _dragHover;
        bool _dragWasOver;
        public override void _Process(double delta)
        {
            if (!GetViewport().GuiIsDragging() || !_dragWasOver || !GetGlobalRect().HasPoint(ItemDragging.Pointer(this)) || _checked)
            { _dragHover = 0; return; }
            _dragHover += delta;
            if (_dragHover >= 0.5) { Turn(Page); _dragHover = 0; }
        }
        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (new Rect2(Vector2.Zero, Size).HasPoint(atPosition) && ItemDragging.From(data) != null) _dragWasOver = true;
            return false;
        }
    }
}
