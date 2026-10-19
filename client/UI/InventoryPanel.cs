using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The player's inventory: a paper doll (live 3D portrait between the equipment slots, weapons and
/// ammunition below) beside the backpack, with gold and carried weight at the bottom.
///
/// <para>The backpack is the original's grid (10 x 6 cells a page, six pages): items cover their
/// footprint at the cells the server keeps, drag to any cell (green / red outline shows whether
/// they fit), swap with an item of the same size, stack onto a matching stack, go onto the paper
/// doll, into open bags and loot windows, or onto the world to drop them. The Modern view
/// (<see cref="InventorySettings.ModernView"/>) shows the same items as uniform slots with sorting;
/// "Arrange" repacks the grid on the server in either view. Double-click equips, uses or takes off;
/// right-click opens the item menu. Toggled with I; refreshed from the server's "inventory-open".</para>
/// </summary>
public partial class InventoryPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    public Func<Vector2, System.Numerics.Vector3?> PickGround = null!;
    /// <summary>Set while a store is open: the item ring offers to sell (E, as the original's).</summary>
    public Func<ItemEntry, bool>? SellHandler;
    /// <summary>Set while a store is open: whether it would buy an item (greys the ring's Sell when not).</summary>
    public Func<ItemEntry, bool>? CanSellHandler;
    /// <summary>Set while a container is open: the item menu (and dragging onto it) puts items in.</summary>
    public Action<ItemEntry>? PutHandler;
    public Action<uint>? Examine;
    public RadialMenu? Radial;

    /// <summary>Items directly in the backpack (not in bags, not equipped).</summary>
    public List<ItemEntry> Carried { get; private set; } = new();
    /// <summary>Bag contents by bag id.</summary>
    public Dictionary<uint, List<ItemEntry>> BagContents { get; private set; } = new();
    public Dictionary<int, ItemEntry> Equipped { get; private set; } = new();
    public int Gold { get; private set; }
    /// <summary>Carried weight and the light / heavy load limits, in tenths of a pound.</summary>
    public int Weight { get; private set; }
    public int NormalLoad { get; private set; }
    public int HeavyLoad { get; private set; }
    public GridSpec Grid { get; private set; } = GridSpec.Backpack;
    public GridSpec BagGrid { get; private set; } = new(10, 6, 0);

    /// <summary>Raised after every refresh (the store's "your pack" side and bag windows listen).</summary>
    public event Action? Changed;

    const int SlotSize = 58, ModernColumns = 9, ModernSlot = 56, MinCells = 45;
    const float CellSize = 54;

    readonly Dictionary<int, ItemSlot> _slots = new();
    ItemGrid _grid = null!;
    GridPageTabs _pages = null!;
    Control _classic = null!, _modern = null!;
    GridContainer _uniform = null!;
    HBoxContainer _filters = null!;
    OptionButton _sort = null!;
    IconButton _viewGrid = null!, _viewList = null!;
    Label _count = null!, _gold = null!, _weightText = null!, _name = null!, _loadNote = null!;
    Bar _weightBar = null!;
    PopupMenu _menu = null!;
    ItemEntry? _menuItem;
    ItemFilter _filter = ItemFilter.All;
    BagWindow? _bag;
    bool _placed;

    // NWN inventory slots (INVENTORY_SLOT_*): placeholder glyph and name.
    static readonly (int Slot, string Icon, string Name)[] LeftColumn =
        [(0, "helm", "Head"), (9, "amulet", "Neck"), (6, "cloak", "Cloak"), (1, "armor", "Armour"), (10, "belt", "Belt")];
    static readonly (int Slot, string Icon, string Name)[] RightColumn =
        [(3, "gloves", "Arms"), (8, "ring", "Right ring"), (7, "ring", "Left ring"), (2, "boots", "Boots")];
    static readonly (int Slot, string Icon, string Name)[] WeaponRow =
        [(4, "weapon", "Main hand"), (5, "offhand", "Off hand")];
    static readonly (int Slot, string Icon, string Name)[] AmmoRow =
        [(11, "arrows", "Arrows"), (13, "bolts", "Bolts"), (12, "bullets", "Bullets")];

    public static InventoryPanel Create(ClientSession session)
    {
        var p = Create<InventoryPanel>("Inventory", new Vector2(900, 0));
        p.Session = session;
        return p;
    }

    public override void _Ready()
    {
        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(columns);
        columns.AddChild(BuildDoll());
        columns.AddChild(BuildBackpack());
        Body.AddChild(BuildFooter());

        // Popups are their own windows: give them the kit's theme explicitly.
        _menu = new PopupMenu { Theme = GameTheme.Theme };
        _menu.IdPressed += OnMenu;
        AddChild(_menu);
        Session.NotifyReceived += OnNotify;
        Session.ControlledChanged += ControlChanged;
        TreeExiting += () => Session.ControlledChanged -= ControlChanged;
        InventorySettings.Changed += ApplyView;
        VisibilityChanged += () =>
        {
            if (!Visible) { _bag?.Close(); return; }
            if (_placed) return;
            _placed = true;
            // First time: against the right edge, vertically centred.
            Callable.From(() =>
            {
                var view = GetViewportRect().Size;
                var size = GetCombinedMinimumSize();
                Position = new Vector2(Math.Max(16, view.X - size.X - 32), Math.Max(16, (view.Y - size.Y) / 2 - 24));
            }).CallDeferred();
        };
        ApplyView();
    }

    // ------------------------------------------------------------------ layout

    Control BuildDoll()
    {
        var card = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var style = Styles.Card(14);
        style.BgColor = Palette.Bg2 with { A = 0.7f };
        card.AddThemeStyleboxOverride("panel", style);
        var v = Ui.VBox(10);
        card.AddChild(v);

        _name = Ui.Text("", Ui.TextStyle.Title, Palette.AccentBright, 18);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.ClipText = true;
        v.AddChild(_name);

        var middle = Ui.HBox(10);
        middle.SizeFlagsVertical = SizeFlags.ExpandFill;
        v.AddChild(middle);
        middle.AddChild(SlotColumn(LeftColumn));
        var preview = new PaperDollPreview { Session = Session, CustomMinimumSize = new Vector2(196, 330), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        middle.AddChild(preview);
        middle.AddChild(SlotColumn(RightColumn));

        var bottom = Ui.HBox(8);
        bottom.Alignment = BoxContainer.AlignmentMode.Center;
        foreach (var (slot, icon, name) in WeaponRow) bottom.AddChild(MakeEquipSlot(slot, icon, name, SlotSize));
        bottom.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = MouseFilterEnum.Ignore });
        var ammo = Ui.HBox(6);
        ammo.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        foreach (var (slot, icon, name) in AmmoRow) ammo.AddChild(MakeEquipSlot(slot, icon, name, 46));
        bottom.AddChild(ammo);
        v.AddChild(bottom);
        return card;
    }

    Control SlotColumn((int Slot, string Icon, string Name)[] slots)
    {
        // Slots spread over the full height so both columns line up top and bottom.
        var col = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < slots.Length; i++)
        {
            if (i > 0) col.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            var (slot, icon, name) = slots[i];
            col.AddChild(MakeEquipSlot(slot, icon, name, SlotSize));
        }
        return col;
    }

    ItemSlot MakeEquipSlot(int slot, string icon, string name, int size)
    {
        var s = ItemSlot.Create(size, icon, name);
        s.EquipSlot = slot;
        s.DragSource = "equip";
        s.CanAccept = d => d.Source is "pack" or "bag" or "ground" && Fits(d.Item, slot);
        s.Accept = d => Session.Request(d.Source == "ground" ? "equip-ground" : "equip", new { item = d.Item.Id, slot });
        s.DoubleClicked = item => Session.Request("unequip", new { item = item.Id });
        s.ContextRequested = ShowMenu;
        s.DroppedOnWorld = d => DropOnWorld(d.Item);
        s.TooltipExtra = _ => new ItemTooltip.Extra { Hint = $"{name}  ·  double-click to take off  ·  right-click for options" };
        _slots[slot] = s;
        return s;
    }

    static bool Fits(ItemEntry item, int slot) => item.Info is { } i && (i.Slots & (1 << slot)) != 0;

    Control BuildBackpack()
    {
        var v = Ui.VBox(10);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var head = Ui.HBox(6);
        head.AddChild(Ui.Text("Backpack", Ui.TextStyle.Title, Palette.AccentBright, 18));
        _count = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        _count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(_count);
        head.AddChild(Ui.Spacer());
        _sort = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Sort (modern view)" };
        foreach (var s in new[] { "Grid order", "Type", "Name", "Value", "Weight" }) _sort.AddItem(s);
        _sort.ItemSelected += _ => Rebuild();
        _sort.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        head.AddChild(_sort);
        var arrange = IconButton.Create("arrange", "Arrange: repack the backpack, largest items first", 32, 16, () => Session.Request("inventory-arrange"));
        head.AddChild(arrange);
        var sep = new VSeparator();
        head.AddChild(sep);
        _viewGrid = IconButton.Create("grid", "Classic view: the original grid", 32, 16, () => InventorySettings.ModernView = false);
        _viewList = IconButton.Create("list", "Modern view: uniform slots, sorting", 32, 16, () => InventorySettings.ModernView = true);
        _viewGrid.ToggleMode = _viewList.ToggleMode = true;
        head.AddChild(_viewGrid);
        head.AddChild(_viewList);
        v.AddChild(head);

        _filters = Ui.HBox(6);
        foreach (var f in Enum.GetValues<ItemFilter>()) _filters.AddChild(FilterChip(f));
        v.AddChild(_filters);

        // Classic: the grid page and its page tabs.
        var classic = Ui.HBox(8);
        classic.SizeFlagsVertical = SizeFlags.ExpandFill;
        _grid = new ItemGrid { Spec = Grid, Cell = CellSize, Holder = 0, DragSource = "pack" };
        _grid.AcceptsFrom = d => d.Source is "pack" or "equip" or "container" or "bag" or "ground";
        _grid.Dropped = DropOnGrid;
        _grid.DoubleClicked = Primary;
        _grid.ContextRequested = ShowMenu;
        _grid.DroppedOnWorld = d => DropOnWorld(d.Item);
        _grid.TooltipExtra = PackTooltip;
        _grid.Highlight = i => _filter == ItemFilter.All || i.Filter == _filter;
        var well = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        well.AddThemeStyleboxOverride("panel", Styles.Inset(6));
        well.AddChild(_grid);
        classic.AddChild(well);
        _pages = GridPageTabs.Create(_grid);
        _pages.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        classic.AddChild(_pages);
        _classic = classic;
        var hint = Ui.Text("Drag to move or swap  ·  onto the doll to equip  ·  outside the window to drop", Ui.TextStyle.Caption, Palette.TextMuted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(10 * CellSize, 0);
        var classicBox = Ui.VBox(8);
        classicBox.AddChild(classic);
        classicBox.AddChild(hint);
        _classic = classicBox;
        v.AddChild(classicBox);

        // Modern: uniform slots in sort order.
        var area = new ItemDropArea { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        area.AddThemeStyleboxOverride("panel", Styles.Inset(8));
        area.CanAccept = AcceptsIntoPack;
        area.Accept = IntoPack;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(10 * CellSize + 12 + 8 + 34 - 16, 6 * CellSize + 12 - 16) };
        area.AddChild(scroll);
        _uniform = new GridContainer { Columns = ModernColumns };
        _uniform.AddThemeConstantOverride("h_separation", 6);
        _uniform.AddThemeConstantOverride("v_separation", 6);
        scroll.AddChild(_uniform);
        _modern = area;
        v.AddChild(area);
        return v;
    }

    /// <summary>Shows one filter tab of the backpack.</summary>
    public void ShowFilter(ItemFilter f)
    {
        _filter = f;
        var chips = _filters.GetChildren().OfType<Button>().ToList();
        for (int i = 0; i < chips.Count; i++) chips[i].SetPressedNoSignal(i == (int)f);
        Rebuild();
    }

    /// <summary>Turns to a page of the backpack grid.</summary>
    public void ShowPage(int page)
    {
        _grid.Page = page;
        _pages.Refresh();
    }

    /// <summary>
    /// The original's previous / next inventory tab keys ([ and ]) while the window is open: turn the grid's page,
    /// or in the modern view step through the filter tabs.
    /// </summary>
    public void Cycle(int step)
    {
        if (!Visible) return;
        if (InventorySettings.ModernView)
        {
            var filters = Enum.GetValues<ItemFilter>();
            ShowFilter(filters[((int)_filter + step % filters.Length + filters.Length) % filters.Length]);
        }
        else
        {
            int pages = _grid.Spec.Pages;
            if (pages <= 1) return;
            ShowPage(((_grid.Page + step) % pages + pages) % pages);
        }
    }

    Button FilterChip(ItemFilter f) => ItemUi.Chip(ItemUi.FilterIcon(f), f == ItemFilter.All ? "All" : "", ItemUi.FilterName(f), f == _filter, b =>
    {
        _filter = f;
        foreach (var c in _filters.GetChildren().OfType<Button>()) c.SetPressedNoSignal(c == b);
        Rebuild();
    });

    // The view setting outlives this panel (a new game builds a new one): stop listening when it goes.
    public override void _ExitTree() => InventorySettings.Changed -= ApplyView;

    void ApplyView()
    {
        if (_classic == null) return;
        bool modern = InventorySettings.ModernView;
        _classic.Visible = !modern;
        _modern.Visible = modern;
        _sort.Visible = modern;
        _viewGrid.SetPressedNoSignal(!modern);
        _viewList.SetPressedNoSignal(modern);
        Rebuild();
        Callable.From(() => { if (IsInsideTree()) ResetSize(); }).CallDeferred();
    }

    Control BuildFooter()
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.25f), Palette.Radius, Palette.Line, 1, 14, 9));
        var h = Ui.HBox(10);
        bar.AddChild(h);
        var coin = IconButton.Create("gold", "Drop gold", 28, 20, DropGold);
        coin.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(coin);
        _gold = Ui.Text("0", Ui.TextStyle.Number, Palette.AccentBright);
        _gold.TooltipText = "Gold pieces";
        _gold.MouseFilter = MouseFilterEnum.Pass;
        h.AddChild(_gold);
        h.AddChild(Ui.Spacer());
        _loadNote = Ui.Text("", Ui.TextStyle.Small, Palette.Warning);
        _loadNote.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(_loadNote);
        var weight = Icons.Rect("weight", 15, Palette.TextSecondary);
        weight.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(weight);
        _weightText = Ui.Text("", Ui.TextStyle.Small, Palette.Text);
        _weightText.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(_weightText);
        _weightBar = new Bar { CustomMinimumSize = new Vector2(150, 8), ShowText = false, SizeFlagsVertical = SizeFlags.ShrinkCenter, Radius = 3 };
        h.AddChild(_weightBar);
        return bar;
    }

    // ------------------------------------------------------------------ data

    void ControlChanged(uint _) { Close(); _bag?.Close(); _menu.Hide(); }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        Visible = true;
        Session.Request("inventory");
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "inventory-denied" when m.Subject == Session.ControlledId: ControlChanged(m.Subject); break;
            case "ground-item-carry": ReceiveGroundItem(m); break;
            case "inventory-open":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                uint me = d.GetProperty("creature").GetUInt32();
                if (me != Session.ControlledId) return;
                Gold = d.GetProperty("gold").GetInt32();
                Weight = d.TryGetProperty("weight", out var w) ? w.GetInt32() : 0;
                NormalLoad = d.TryGetProperty("normalLoad", out var nl) ? nl.GetInt32() : 0;
                HeavyLoad = d.TryGetProperty("heavyLoad", out var hl) ? hl.GetInt32() : 0;
                Grid = GridSpec.From(d, "grid", GridSpec.Backpack);
                BagGrid = GridSpec.From(d, "bagGrid", BagGrid);
                Equipped = d.GetProperty("equipped").EnumerateArray().Select(e => ItemEntry.From(e, e.GetProperty("slot").GetInt32())).ToDictionary(e => e.Slot);
                var all = d.GetProperty("items").EnumerateArray().Select(e =>
                {
                    uint holder = e.TryGetProperty("container", out var c) ? c.GetUInt32() : me;
                    return ItemEntry.From(e) with { Bag = holder == me ? 0 : holder };
                }).ToList();
                Carried = all.Where(i => i.Bag == 0).ToList();
                BagContents = all.Where(i => i.Bag != 0).GroupBy(i => i.Bag).ToDictionary(g => g.Key, g => g.ToList());
                Rebuild();
                Changed?.Invoke();
                break;
            }
            case "gold-changed" when m.Subject == Session.ControlledId:
            {
                using var doc = JsonDocument.Parse(m.Json);
                Gold = doc.RootElement.GetProperty("gold").GetInt32();
                UpdateFooter();
                Changed?.Invoke();
                break;
            }
            case "creature-equipment" when m.Subject == Session.ControlledId && Visible:
                Session.Request("inventory");
                break;
        }
    }

    IEnumerable<ItemEntry> Sorted(IEnumerable<ItemEntry> items) => _sort?.Selected switch
    {
        1 => items.OrderBy(i => i.Filter).ThenBy(i => i.Info?.Type).ThenBy(i => i.Name),
        2 => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
        3 => items.OrderByDescending(i => i.Info?.Value ?? 0).ThenBy(i => i.Name),
        4 => items.OrderByDescending(i => i.Info?.Weight ?? 0).ThenBy(i => i.Name),
        _ => items.OrderBy(i => i.Y < 0 ? int.MaxValue : i.Y).ThenBy(i => i.X),
    };

    void Rebuild()
    {
        if (_grid == null) return;
        var content = Session.Content;
        _name.Text = Session.Controlled?.DisplayName is { Length: > 0 } n ? NwnText.Strip(n) : "";
        foreach (var (slot, s) in _slots) s.SetItem(Equipped.GetValueOrDefault(slot), content);

        int shown = Carried.Count(i => _filter == ItemFilter.All || i.Filter == _filter);
        if (!InventorySettings.ModernView)
        {
            _grid.Spec = Grid;
            _grid.SetItems(Carried, content);
            _pages.Refresh();
        }
        else
        {
            foreach (var c in _uniform.GetChildren()) { _uniform.RemoveChild(c); c.QueueFree(); }
            var list = Sorted(Carried.Where(i => _filter == ItemFilter.All || i.Filter == _filter)).ToList();
            foreach (var item in list)
            {
                var s = ItemSlot.Create(ModernSlot);
                s.SetItem(item, content);
                s.DragSource = "pack";
                s.CanAccept = AcceptsIntoPack;
                s.Accept = IntoPack;
                s.DoubleClicked = Primary;
                s.ContextRequested = ShowMenu;
                s.DroppedOnWorld = d => DropOnWorld(d.Item);
                s.TooltipExtra = PackTooltip;
                _uniform.AddChild(s);
            }
            int cells = Math.Max(MinCells, (list.Count + ModernColumns) / ModernColumns * ModernColumns);
            for (int i = list.Count; i < cells; i++)
            {
                var empty = ItemSlot.Create(ModernSlot);
                empty.CanAccept = AcceptsIntoPack;
                empty.Accept = IntoPack;
                _uniform.AddChild(empty);
            }
        }
        int usedCells = Carried.Sum(i => i.W * i.H), totalCells = Grid.Width * Grid.PageHeight * Math.Max(1, Grid.Pages);
        _count.Text = (_filter == ItemFilter.All ? $"{Carried.Count} items" : $"{shown} of {Carried.Count}") + $"  ·  {usedCells}/{totalCells} cells";
        UpdateFooter();
        _bag?.Refresh();
    }

    void UpdateFooter()
    {
        if (_gold == null) return;
        _gold.Text = Gold.ToString("N0");
        int cap = NormalLoad > 0 ? NormalLoad : Math.Max(Weight, 1);
        _weightText.Text = $"{ItemText.Weight(Weight).Replace(" lb", "")} / {ItemText.Weight(cap)}";
        _weightBar.Set(Weight, cap);
        bool heavy = HeavyLoad > 0 && Weight > HeavyLoad, enc = NormalLoad > 0 && Weight > NormalLoad;
        var color = heavy ? Palette.Danger : enc ? Palette.Warning : Palette.Accent;
        _weightBar.Fill = color;
        _weightBar.FillTop = color.Lightened(0.3f);
        _weightText.TooltipText = $"Carried weight. Encumbered above {ItemText.Weight(NormalLoad)}, overloaded above {ItemText.Weight(HeavyLoad)}.";
        _weightText.MouseFilter = MouseFilterEnum.Pass;
        _loadNote.Text = heavy ? "Overloaded" : enc ? "Encumbered" : "";
        _loadNote.AddThemeColorOverride("font_color", heavy ? Palette.Danger : Palette.Warning);
    }

    ItemTooltip.Extra PackTooltip(ItemEntry item) =>
        new() { Hint = $"Double-click to {PrimaryVerb(item)}  ·  drag to move, equip or drop  ·  right-click for options" };

    static string PrimaryVerb(ItemEntry item) =>
        item.IsBag ? "open" : item.Info?.Usable == true && item.Info.Slots == 0 ? "use" : item.Info?.Slots > 0 ? "equip" : "examine";

    // ------------------------------------------------------------------ actions

    bool AcceptsIntoPack(ItemDrag d) => d.Source is "equip" or "container" or "store" or "bag" or "ground";

    /// <summary>Modern view drops (no particular cell): the server picks the first free place.</summary>
    void IntoPack(ItemDrag d)
    {
        d.ServerPlacementSound = d.Source is "container" or "ground" or "store";
        switch (d.Source)
        {
            case "ground": Session.Request("pickup", new { item = d.Item.Id }); break;
            case "equip": Session.Request("unequip", new { item = d.Item.Id }); break;
            case "container": Session.Request("container-take", new { item = d.Item.Id }); break;
            case "store": Session.Request("store-buy", new { item = d.Item.Id }); break;
            case "bag": Session.Request("item-move", new { item = d.Item.Id }); break;
        }
    }

    /// <summary>A drop on a cell of the backpack grid.</summary>
    void DropOnGrid(ItemDrag d, int x, int y)
    {
        d.ServerPlacementSound = d.Source is "container" or "ground" or "store";
        switch (d.Source)
        {
            case "ground": Session.Request("pickup", new { item = d.Item.Id, x, y }); break;
            case "container": Session.Request("container-take", new { item = d.Item.Id, x, y }); break;
            default: Session.Request("item-move", new { item = d.Item.Id, x, y }); break;
        }
    }

    /// <summary>Double-click on a backpack item: open bags, use consumables, equip equipment, otherwise examine.</summary>
    void Primary(ItemEntry item)
    {
        switch (PrimaryVerb(item))
        {
            case "open": OpenBag(item); break;
            case "use": Session.Request("item-use", new { item = item.Id }); break;
            case "equip": Session.Request("equip", new { item = item.Id }); break;
            default: Examine?.Invoke(item.Id); break;
        }
    }

    void Drop(ItemEntry item) => Drop(item, new { item = item.Id });

    void Drop(ItemEntry item, object request)
    {
        if (item.Info?.Plot == true) return;
        if (item.Info?.Value >= 1000)
            ConfirmOverlay.Show(this, "Drop item?", $"Drop {item.Name} on the ground?", "Drop", () => Session.Request("drop", request));
        else Session.Request("drop", request);
    }

    /// <summary>
    /// Right-click on one of the player's items: its radial menu (with Sell while a store is open), or the drop-down
    /// for bags and with a container open. <paramref name="visible"/> keeps the ring open (default: this window shown);
    /// the store's own pack pane passes its window.
    /// </summary>
    public void ShowMenu(ItemEntry item, Vector2 at) => ShowMenu(item, at, null);

    public void ShowMenu(ItemEntry item, Vector2 at, Func<bool>? visible)
    {
        if (!item.IsBag && PutHandler == null && Radial != null)
        {
            var sell = SellHandler;
            Radial.OpenInventory(item, at, this, visible ?? (() => IsVisibleInTree()), Drop, ShowSplit, null,
                sell != null ? i => sell(i) : null, sell != null ? CanSellHandler : null);
            return;
        }
        _menuItem = item;
        _menu.Clear();
        _menu.AddSeparator(item.Name);
        if (item.IsBag) _menu.AddItem("Open", 9);
        if (item.Slot >= 0) _menu.AddItem("Take off", 1);
        else if (item.Info?.Slots > 0) _menu.AddItem("Equip", 2);
        if (item.Info?.Usable == true) _menu.AddItem("Use", 7);
        _menu.AddItem("Examine", 5);
        if (item.Stack > 1 && item.Slot < 0) _menu.AddItem("Split stack…", 8);
        if (SellHandler != null && item.Slot < 0) _menu.AddItem("Sell", 4);
        if (PutHandler != null && item.Slot < 0) _menu.AddItem("Put in container", 6);
        _menu.AddSeparator();
        _menu.AddItem("Drop", 3);
        if (item.Info?.Plot == true) _menu.SetItemDisabled(_menu.GetItemIndex(3), true);
        _menu.ResetSize();
        _menu.Position = (Vector2I)at;
        _menu.Popup();
    }

    void OnMenu(long id)
    {
        if (_menuItem is not { } item) return;
        switch (id)
        {
            case 1: Session.Request("unequip", new { item = item.Id }); break;
            case 2: Session.Request("equip", new { item = item.Id }); break;
            case 3: Drop(item); break;
            case 4: SellHandler?.Invoke(item); break;
            case 5: Examine?.Invoke(item.Id); break;
            case 6: PutHandler?.Invoke(item); break;
            case 7: Session.Request("item-use", new { item = item.Id }); break;
            case 8: SplitStack(item); break;
            case 9: OpenBag(item); break;
        }
    }

    /// <summary>Asks how many to split off a stack.</summary>
    public void ShowSplit(ItemEntry item) => SplitStack(item);

    void DropGold()
    {
        void ClickSound()
        {
            if (Session.Content is { } content)
                Classic.ClassicSounds.Play(Classic.ClassicAssets.For(content), Classic.ClassicSounds.Button);
        }
        ClickSound();
        if (Gold <= 0) return;
        var count = new SpinBox { MinValue = 1, MaxValue = Gold, Value = Gold, Step = 1 };
        var dialog = ConfirmOverlay.Show(this, "Drop gold", "How many gold pieces do you want to put on the ground?", "Drop",
            () => GoldCarry.Start(this, Session, (int)count.Value, PickGround), count);
        foreach (var button in dialog.FindChildren("*", "Button", true, false).OfType<Button>())
            button.Pressed += ClickSound;
    }

    void SplitStack(ItemEntry item)
    {
        var box = Ui.VBox(6);
        var value = Ui.Text("", Ui.TextStyle.Number, Palette.AccentBright);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        var slider = new HSlider { MinValue = 1, MaxValue = item.Stack - 1, Value = item.Stack / 2, Step = 1, CustomMinimumSize = new Vector2(280, 24), FocusMode = FocusModeEnum.None };
        void Show() => value.Text = $"{(int)slider.Value}   |   {item.Stack - (int)slider.Value}";
        slider.ValueChanged += _ => Show();
        Show();
        box.AddChild(value);
        box.AddChild(slider);
        ConfirmOverlay.Show(this, "Split stack", $"How many {item.Name} go into the new stack?", "Split",
            () => Session.Request("item-split", new { item = item.Id, count = (int)slider.Value }), box);
    }

    /// <summary>Opens a bag (container item) in the backpack in its own small grid window.</summary>
    public void OpenBag(ItemEntry bag)
    {
        if (_bag == null)
        {
            _bag = BagWindow.Create(this);
            GetParent().AddChild(_bag);
        }
        _bag.Show(bag);
        // Beside the inventory, to its left.
        var size = _bag.GetCombinedMinimumSize();
        _bag.Position = new Vector2(Math.Max(16, Position.X - size.X - 12), Position.Y + 40);
    }

    /// <summary>A bag's contents: the bag grid (10 wide, grows), dragging to and from the backpack.</summary>
    public partial class BagWindow : UiTheme.Window
    {
        InventoryPanel _inv = null!;
        ItemGrid _grid = null!;
        ItemEntry? _bag;

        public static BagWindow Create(InventoryPanel inv)
        {
            var w = Create<BagWindow>("Bag", new Vector2(0, 0), "chest");
            w._inv = inv;
            return w;
        }

        public override void _Ready()
        {
            _grid = new ItemGrid { Cell = 44, DragSource = "bag", MinRows = 4 };
            _grid.AcceptsFrom = d => d.Source is "pack" or "bag" or "equip" && !d.Item.IsBag;
            _grid.Dropped = (d, x, y) => { if (_bag != null) _inv.Session.Request("item-move", new { item = d.Item.Id, x, y, target = _bag.Id }); };
            _grid.DoubleClicked = i => _inv.Primary(i);
            _grid.ContextRequested = _inv.ShowMenu;
            _grid.DroppedOnWorld = d => _inv.Drop(d.Item);
            _grid.TooltipExtra = _inv.PackTooltip;
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(10 * 44 + 16, 4 * 44 + 6) };
            var well = new PanelContainer();
            well.AddThemeStyleboxOverride("panel", Styles.Inset(6));
            well.AddChild(_grid);
            scroll.AddChild(well);
            Body.AddChild(scroll);
            Body.AddChild(Ui.Text("Drag items between the bag and your backpack.", Ui.TextStyle.Caption, Palette.TextMuted));
        }

        public void Show(ItemEntry bag)
        {
            _bag = bag;
            TitleLabel.Text = bag.Name;
            Visible = true;
            WindowStack.Activate(this);
            Refresh();
        }

        public void Refresh()
        {
            if (_bag == null || _grid == null) return;
            if (!_inv.Carried.Any(i => i.Id == _bag.Id)) { Close(); return; }
            _grid.Holder = _bag.Id;
            _grid.Spec = _inv.BagGrid;
            _grid.SetItems(_inv.BagContents.GetValueOrDefault(_bag.Id) ?? new(), _inv.Session.Content);
        }
    }
}
