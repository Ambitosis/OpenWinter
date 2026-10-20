using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// A merchant's window: the merchant's wares and your own pack side by side, with Buy / Sell (and
/// Identify) bars. Classic view: the store's grid by category (Armour, Miscellaneous, Potions,
/// Jewellery, Weapons, as the store lists them) beside your backpack grid; Modern view: priced lists
/// with filters. Double-click or drag an item across to trade; big purchases and sales ask first.
/// Driven by "store-open" (re-sent after every trade) and "store-close"; your side follows the
/// inventory window's data.
/// </summary>
public partial class StorePanel : UiTheme.Window
{
    public ClientSession Session = null!;
    public InventoryPanel Inventory = null!;
    public Action<uint>? Examine;

    /// <summary>Purchases and sales from this many gold pieces up ask for confirmation.</summary>
    public const int ConfirmAbove = 1000;
    const float Cell = 42;

    // Store categories (UTM StoreList ids) in the order the store shows them, with glyphs.
    static readonly (int Panel, string Name, string Icon)[] Categories =
        [(4, "Weapons", "weapon"), (0, "Armour", "armor"), (3, "Jewellery", "ring"), (2, "Potions & scrolls", "potion"), (1, "Miscellaneous", "chest")];

    List<ItemEntry> _stock = new();
    readonly Dictionary<uint, int> _offers = new();
    int _storeGold = -1, _identifyCost = -1, _markUp = 100, _markDown = 100, _category = 4;
    uint _store;
    GridSpec _spec = new(10, 6, 0);
    ItemFilter _buyFilter = ItemFilter.All, _sellFilter = ItemFilter.All;
    uint _selBuy, _selSell;
    bool _serverClosed, _placed;

    Pane _merchant = null!, _mine = null!;
    Label _terms = null!;

    sealed class Pane
    {
        public VBoxContainer List = null!;
        public HBoxContainer Chips = null!, Categories = null!, Gold = null!, Summary = null!;
        public Control ListArea = null!, GridArea = null!;
        public ItemGrid Grid = null!;
        public GridPageTabs? Pages;
        public Label Count = null!;
        public Button Action = null!, Second = null!;
    }

    public static StorePanel Create(ClientSession session, InventoryPanel inventory)
    {
        var p = Create<StorePanel>("Store", new Vector2(0, 0));
        p.Session = session;
        p.Inventory = inventory;
        return p;
    }

    public override void _Ready()
    {
        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(columns);
        _merchant = BuildPane(columns, "Merchant", true);
        _mine = BuildPane(columns, "Your pack", false);
        _terms = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        _terms.HorizontalAlignment = HorizontalAlignment.Center;
        Body.AddChild(_terms);

        Closed += () =>
        {
            Inventory.SellHandler = null;
            Inventory.CanSellHandler = null;
            if (!_serverClosed) Session.Request("store-close");
            _serverClosed = false;
        };
        VisibilityChanged += () =>
        {
            if (!Visible || _placed) return;
            _placed = true;
            Callable.From(() =>
            {
                var view = GetViewportRect().Size;
                var size = GetCombinedMinimumSize();
                Position = new Vector2(Math.Max(16, (view.X - size.X) / 2), Math.Max(16, (view.Y - size.Y) / 2 - 20));
            }).CallDeferred();
        };
        Inventory.Changed += () => { if (Visible) FillMine(); };
        InventorySettings.Changed += OnViewChanged;
        Session.NotifyReceived += OnNotify;
    }

    Pane BuildPane(HBoxContainer parent, string title, bool merchant)
    {
        var pane = new Pane();
        var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var style = Styles.Card(14);
        style.BgColor = Palette.Bg2 with { A = 0.7f };
        card.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(card);
        var v = Ui.VBox(10);
        card.AddChild(v);

        var head = Ui.HBox(8);
        head.AddChild(Ui.Text(title, Ui.TextStyle.Title, Palette.AccentBright, 18));
        pane.Count = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        pane.Count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(pane.Count);
        head.AddChild(Ui.Spacer());
        pane.Gold = Ui.HBox(4);
        head.AddChild(pane.Gold);
        v.AddChild(head);

        // Filters (modern lists; classic dims what doesn't match on your side).
        pane.Chips = Ui.HBox(6);
        foreach (var f in Enum.GetValues<ItemFilter>())
        {
            var filter = f;
            pane.Chips.AddChild(ItemUi.Chip(ItemUi.FilterIcon(f), f == ItemFilter.All ? "All" : "", ItemUi.FilterName(f), f == ItemFilter.All, b =>
            {
                if (merchant) _buyFilter = filter; else _sellFilter = filter;
                foreach (var c in pane.Chips.GetChildren().OfType<Button>()) c.SetPressedNoSignal(c == b);
                if (merchant) FillMerchant(); else FillMine();
            }));
        }
        v.AddChild(pane.Chips);
        if (merchant)
        {
            // The store's own categories (classic view).
            pane.Categories = Ui.HBox(6);
            foreach (var (panel, name, icon) in Categories)
            {
                int p = panel;
                pane.Categories.AddChild(ItemUi.Chip(icon, "", name, p == _category, b =>
                {
                    _category = p;
                    _selBuy = 0;
                    foreach (var c in pane.Categories.GetChildren().OfType<Button>()) c.SetPressedNoSignal(c == b);
                    FillMerchant();
                }));
            }
            v.AddChild(pane.Categories);
        }

        // Modern: priced rows.
        var area = new ItemDropArea { SizeFlagsVertical = SizeFlags.ExpandFill };
        area.AddThemeStyleboxOverride("panel", Styles.Inset(6));
        area.CanAccept = merchant ? d => d.Source is "pack" or "bag" : d => d.Source == "store";
        area.Accept = merchant ? d => TrySell(d.Item) : d => BuyDrop(d);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(10 * Cell + 2, 6 * Cell) };
        area.AddChild(scroll);
        pane.List = Ui.VBox(2);
        pane.List.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(pane.List);
        pane.ListArea = area;
        v.AddChild(area);

        // Classic: grids.
        pane.Grid = new ItemGrid { Cell = Cell, MinRows = 6 };
        var well = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        well.AddThemeStyleboxOverride("panel", Styles.Inset(6));
        well.AddChild(pane.Grid);
        if (merchant)
        {
            pane.Grid.DragSource = "store";
            pane.Grid.AcceptsFrom = d => d.Source is "pack" or "bag";
            pane.Grid.Dropped = (d, _, _) => TrySell(d.Item);
            pane.Grid.Clicked = i => { _selBuy = i.Id; FillMerchant(); };
            pane.Grid.DoubleClicked = TryBuy;
            pane.Grid.ContextRequested = WareMenu;
            pane.Grid.TooltipExtra = BuyTooltip;
            pane.Grid.Badge = i => i.Price > 0 ? i.Price.ToString("N0") : null;
            var gridScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(10 * Cell + 12 + 10, 6 * Cell + 12), SizeFlagsVertical = SizeFlags.ExpandFill };
            gridScroll.AddChild(well);
            pane.GridArea = gridScroll;
        }
        else
        {
            pane.Grid.DragSource = "pack";
            pane.Grid.Holder = 0;
            pane.Grid.AcceptsFrom = d => d.Source is "store" or "pack" or "bag";
            pane.Grid.Dropped = (d, x, y) =>
            {
                if (d.Source == "store") BuyDrop(d, (x, y));
                else Session.Request("item-move", new { item = d.Item.Id, x, y });
            };
            pane.Grid.Clicked = i => { _selSell = i.Id; FillMine(); };
            pane.Grid.DoubleClicked = TrySell;
            pane.Grid.ContextRequested = PackMenu;
            pane.Grid.TooltipExtra = SellTooltip;
            pane.Grid.Highlight = i => _sellFilter == ItemFilter.All || i.Filter == _sellFilter;
            var box = Ui.HBox(8);
            box.AddChild(well);
            pane.Pages = GridPageTabs.Create(pane.Grid);
            pane.Pages.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            box.AddChild(pane.Pages);
            pane.GridArea = box;
        }
        v.AddChild(pane.GridArea);

        var foot = Ui.HBox(8);
        pane.Summary = Ui.HBox(6);
        pane.Summary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foot.AddChild(pane.Summary);
        pane.Second = Ui.Button("Identify", () => { if (Find(_selSell) is { } i) Identify(i); });
        pane.Second.Visible = false;
        foot.AddChild(pane.Second);
        pane.Action = Ui.Primary(merchant ? "Buy" : "Sell", merchant
            ? () => { if (_stock.FirstOrDefault(s => s.Id == _selBuy) is { } i) TryBuy(i); }
        : () => { if (Find(_selSell) is { } i) TrySell(i); });
        pane.Action.CustomMinimumSize = new Vector2(96, 34);
        foot.AddChild(pane.Action);
        v.AddChild(foot);
        return pane;
    }

    ItemEntry? Find(uint id) => Inventory.Carried.FirstOrDefault(i => i.Id == id);

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "store-open":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                TitleLabel.Text = d.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name ? NwnText.Strip(name) : "Store";
                _store = d.TryGetProperty("store", out var sid) ? sid.GetUInt32() : m.Subject;
                _stock = d.GetProperty("items").EnumerateArray().Select(e => ItemEntry.From(e)).ToList();
                _storeGold = d.TryGetProperty("gold", out var g) ? g.GetInt32() : -1;
                _identifyCost = d.TryGetProperty("identifyCost", out var ic) ? ic.GetInt32() : -1;
                _markUp = d.TryGetProperty("markUp", out var mu) ? mu.GetInt32() : 100;
                _markDown = d.TryGetProperty("markDown", out var md) ? md.GetInt32() : 100;
                _spec = GridSpec.From(d, "grid", _spec);
                _offers.Clear();
                if (d.TryGetProperty("offers", out var offers))
                    foreach (var o in offers.EnumerateArray()) _offers[o.GetProperty("id").GetUInt32()] = o.GetProperty("price").GetInt32();
                _terms.Text = $"The merchant sells at {_markUp}% of an item's value and buys at {_markDown}%" +
                    (_identifyCost >= 0 ? $"  ·  identifies for {ItemUi.Gp(_identifyCost)}" : "") + "  ·  double-click or drag to trade";
                if (!Visible)
                {
                    Visible = true;
                    _selBuy = _selSell = 0;
                    // Open on the first category that has wares.
                    foreach (var (panel, _, _) in Categories)
                        if (_stock.Any(s => s.Panel == panel)) { SelectCategory(panel); break; }
                }
                Inventory.SellHandler = item => { TrySell(item); return true; };
                Inventory.CanSellHandler = CanSell;
                if (Inventory.Visible) Inventory.Close();
                Session.Request("inventory");
                FillMerchant();
                FillMine();
                break;
            }
            case "store-close":
                _serverClosed = true;
                Close();
                break;
        }
    }

    void SelectCategory(int panel)
    {
        _category = panel;
        var chips = _merchant.Categories.GetChildren().OfType<Button>().ToList();
        for (int i = 0; i < chips.Count; i++) chips[i].SetPressedNoSignal(Categories[i].Panel == panel);
    }

    // ------------------------------------------------------------------ lists and grids

    void FillMerchant()
    {
        if (_merchant == null) return;
        bool modern = InventorySettings.ModernView;
        _merchant.Chips.Visible = _merchant.ListArea.Visible = modern;
        _merchant.Categories.Visible = _merchant.GridArea.Visible = !modern;
        var content = Session.Content;
        if (!modern)
        {
            _merchant.Grid.Holder = _store;
            _merchant.Grid.Spec = _spec;
            _merchant.Grid.SelectedId = _selBuy;
            _merchant.Grid.SetItems(_stock.Where(s => s.Panel == _category), content);
        }
        else
        {
            Clear(_merchant.List);
            var shown = _stock.Where(s => _buyFilter == ItemFilter.All || s.Filter == _buyFilter)
                .OrderBy(s => s.Filter).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var item in shown)
            {
                var row = ItemRow.Create(item, content);
                bool afford = item.Price <= Inventory.Gold;
                row.SetTrailing(ItemUi.Price(item.Price, afford ? Palette.AccentBright : Palette.Danger));
                row.DragSource = "store";
                row.Selected = item.Id == _selBuy;
                row.Clicked = i => { _selBuy = i.Id; Highlight(_merchant.List, i.Id); UpdateMerchantFooter(); };
                row.DoubleClicked = i => TryBuy(i);
                row.ContextRequested = WareMenu;
                row.CanAccept = d => d.Source is "pack" or "bag";
                row.Accept = d => TrySell(d.Item);
                row.TooltipExtra = BuyTooltip;
                _merchant.List.AddChild(row);
            }
            if (shown.Count == 0) _merchant.List.AddChild(Empty(_stock.Count == 0 ? "The merchant has nothing for sale." : "Nothing of this kind for sale."));
        }
        _merchant.Count.Text = $"{_stock.Count} wares";
        Gold(_merchant.Gold, _storeGold, "The merchant's gold: the most they can pay you");
        UpdateMerchantFooter();
        Shrink();
    }

    /// <summary>Fits the window to its content again (switching views changes what it needs).</summary>
    void Shrink() => Callable.From(() => { if (IsInsideTree()) ResetSize(); }).CallDeferred();

    void FillMine()
    {
        if (_mine == null) return;
        bool modern = InventorySettings.ModernView;
        _mine.ListArea.Visible = modern;
        _mine.GridArea.Visible = !modern;
        var content = Session.Content;
        var all = Inventory.Carried;
        if (!modern)
        {
            _mine.Grid.Spec = Inventory.Grid;
            _mine.Grid.SelectedId = _selSell;
            _mine.Grid.SetItems(all, content);
            _mine.Pages?.Refresh();
        }
        else
        {
            Clear(_mine.List);
            var shown = all.Where(s => _sellFilter == ItemFilter.All || s.Filter == _sellFilter)
                .OrderBy(s => s.Filter).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var item in shown)
            {
                var row = ItemRow.Create(item, content);
                int offer = Offer(item);
                if (offer < 0) { row.SetTrailing(Ui.Text("Won't buy", Ui.TextStyle.Caption, Palette.TextMuted)); row.Dimmed = true; }
                else if (_storeGold >= 0 && offer > _storeGold) row.SetTrailing(ItemUi.Price(offer, Palette.Warning));
                else row.SetTrailing(ItemUi.Price(offer));
                row.DragSource = "pack";
                row.Selected = item.Id == _selSell;
                row.Clicked = i => { _selSell = i.Id; Highlight(_mine.List, i.Id); UpdateMineFooter(); };
                row.DoubleClicked = TrySell;
                row.ContextRequested = PackMenu;
                row.CanAccept = d => d.Source == "store";
                row.Accept = d => BuyDrop(d);
                row.TooltipExtra = SellTooltip;
                _mine.List.AddChild(row);
            }
            if (shown.Count == 0) _mine.List.AddChild(Empty(all.Count == 0 ? "Your pack is empty." : "Nothing of this kind in your pack."));
        }
        _mine.Count.Text = $"{all.Count} items";
        Gold(_mine.Gold, Inventory.Gold, "Your gold");
        UpdateMineFooter();
        // Prices shown red/white depend on your gold.
        UpdateMerchantFooter();
        Shrink();
    }

    ItemTooltip.Extra BuyTooltip(ItemEntry i) => new()
    {
        PriceLabel = "Buy for", Price = i.Price, PriceBad = i.Price > Inventory.Gold,
        Note = i.Price > Inventory.Gold ? "You can't afford this." : null, NoteColor = Palette.Danger,
        Hint = "Double-click or drag to your pack to buy  ·  right-click for its ring",
    };

    ItemTooltip.Extra SellTooltip(ItemEntry i)
    {
        int offer = Offer(i);
        return new ItemTooltip.Extra
        {
            PriceLabel = offer < 0 ? "The merchant won't buy this" : "Sell for", Price = offer,
            Note = offer >= 0 && _storeGold >= 0 && offer > _storeGold ? "The merchant can't afford this." :
                !i.Identified && _identifyCost >= 0 ? $"The merchant can identify it for {ItemUi.Gp(_identifyCost)}." : null,
            NoteColor = offer > _storeGold && _storeGold >= 0 ? Palette.Warning : Palette.Info,
            Hint = "Double-click or drag to the merchant to sell  ·  right-click for its ring",
        };
    }

    int Offer(ItemEntry item) => _offers.TryGetValue(item.Id, out var p) ? p : -1;

    bool CanSell(ItemEntry item) => item.Slot < 0 && Offer(item) is var offer && offer >= 0 && (_storeGold < 0 || offer <= _storeGold);

    /// <summary>
    /// Right-click on a ware: the original's ware ring (measured, `storeradial`), Examine N and Buy E (greyed when you
    /// can't afford it).
    /// </summary>
    void WareMenu(ItemEntry item, Vector2 at)
    {
        if (Inventory.Radial is not { } radial) return;
        radial.OpenItemMenu(item, at, () => Visible && _stock.Any(s => s.Id == item.Id), () => RadialMenu.FixedRing(
            (0, new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(item.Id) }),
            (2, RadialMenu.Icon(new RadialEntry
            {
                Label = "Buy", Glyph = "bag", Disabled = item.Price > Inventory.Gold,
                Act = () => TryBuy(item)
            }, "ir_buy"))));
    }

    /// <summary>Right-click on one of your items beside the wares: its item ring, with Sell at E.</summary>
    void PackMenu(ItemEntry item, Vector2 at) => Inventory.ShowMenu(item, at, () => Visible);

    void UpdateMerchantFooter()
    {
        var sel = _stock.FirstOrDefault(s => s.Id == _selBuy);
        Clear(_merchant.Summary);
        if (sel == null)
        {
            _merchant.Summary.AddChild(Ui.Text("Select an item to buy", Ui.TextStyle.Small, Palette.TextMuted));
            _merchant.Action.Disabled = true;
            return;
        }
        bool afford = sel.Price <= Inventory.Gold;
        var name = Ui.Text(sel.Name, Ui.TextStyle.Small, ItemUi.NameColor(sel));
        name.ClipText = true;
        name.CustomMinimumSize = new Vector2(120, 0);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _merchant.Summary.AddChild(name);
        _merchant.Summary.AddChild(ItemUi.Price(sel.Price, afford ? Palette.AccentBright : Palette.Danger, Palette.FontBody));
        _merchant.Action.Disabled = !afford;
    }

    void UpdateMineFooter()
    {
        var sel = Find(_selSell);
        Clear(_mine.Summary);
        _mine.Second.Visible = sel is { Identified: false } && _identifyCost >= 0;
        if (_mine.Second.Visible)
        {
            _mine.Second.Text = $"Identify ({ItemUi.Gp(_identifyCost)})";
            _mine.Second.Disabled = Inventory.Gold < _identifyCost;
        }
        if (sel == null)
        {
            _mine.Summary.AddChild(Ui.Text("Select an item to sell", Ui.TextStyle.Small, Palette.TextMuted));
            _mine.Action.Disabled = true;
            return;
        }
        int offer = Offer(sel);
        var name = Ui.Text(sel.Name, Ui.TextStyle.Small, ItemUi.NameColor(sel));
        name.ClipText = true;
        name.CustomMinimumSize = new Vector2(120, 0);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _mine.Summary.AddChild(name);
        if (offer < 0) _mine.Summary.AddChild(Ui.Text("won't buy", Ui.TextStyle.Small, Palette.TextMuted));
        else _mine.Summary.AddChild(ItemUi.Price(offer, null, Palette.FontBody));
        _mine.Action.Disabled = offer < 0 || _storeGold >= 0 && offer > _storeGold;
    }

    static void Highlight(VBoxContainer list, uint id)
    {
        foreach (var r in list.GetChildren().OfType<ItemRow>()) r.Selected = r.Item.Id == id;
    }

    static void Gold(HBoxContainer box, int amount, string tooltip)
    {
        Clear(box);
        if (amount < 0) box.AddChild(Ui.Text("Unlimited gold", Ui.TextStyle.Caption, Palette.TextSecondary));
        else box.AddChild(ItemUi.Price(amount, null, Palette.FontBody));
        box.TooltipText = tooltip;
        box.MouseFilter = MouseFilterEnum.Pass;
    }

    static Control Empty(string text)
    {
        var l = Ui.Text(text, Ui.TextStyle.ReadingItalic, Palette.TextMuted, 16);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        return Ui.Pad(l, 0, 24, 0, 0);
    }

    static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }

    // ------------------------------------------------------------------ trades

    void BuyDrop(ItemDrag drag, (int X, int Y)? cell = null)
    {
        drag.ServerPlacementSound = true;
        TryBuy(drag.Item, cell);
    }

    void TryBuy(ItemEntry item) => TryBuy(item, null);

    /// <summary>Buys an item (into a backpack cell when it was dragged onto one).</summary>
    void TryBuy(ItemEntry item, (int X, int Y)? cell)
    {
        if (_stock.All(s => s.Id != item.Id)) return;
        if (item.Price > Inventory.Gold) return;
        void Buy()
        {
            if (cell is { } c) Session.Request("store-buy", new { item = item.Id, x = c.X, y = c.Y });
            else Session.Request("store-buy", new { item = item.Id });
        }
        if (item.Price >= ConfirmAbove || item.Price > Inventory.Gold / 2 && item.Price >= 100)
            ConfirmOverlay.Show(this, "Confirm purchase", $"Buy {item.Name} for {ItemUi.Gp(item.Price)}? You will have {ItemUi.Gp(Inventory.Gold - item.Price)} left.", "Buy", Buy);
        else Buy();
    }

    void TrySell(ItemEntry item)
    {
        if (item.Slot >= 0) return;
        int offer = Offer(item);
        if (offer < 0 || _storeGold >= 0 && offer > _storeGold) return;
        void Sell() { Session.Request("store-sell", new { item = item.Id }); if (_selSell == item.Id) _selSell = 0; }
        if (offer >= ConfirmAbove || ItemUi.IsMagic(item) && offer >= 200)
            ConfirmOverlay.Show(this, "Confirm sale", $"Sell {item.Name} for {ItemUi.Gp(offer)}?", "Sell", Sell);
        else Sell();
    }

    void Identify(ItemEntry item)
    {
        if (_identifyCost < 0 || Inventory.Gold < _identifyCost) return;
        Session.Request("store-identify", new { item = item.Id });
    }

    void OnViewChanged() { if (Visible) { FillMerchant(); FillMine(); } }

    // The view setting outlives this panel (a new game builds a new one): stop listening when it goes.
    public override void _ExitTree() => InventorySettings.Changed -= OnViewChanged;
}
