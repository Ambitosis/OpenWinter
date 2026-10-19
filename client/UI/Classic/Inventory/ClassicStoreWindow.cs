using System.Text.Json;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's store (ingame_store): category tabs (ctl_inv_sto_tab1..5 along the panel's "tabs" node), a 10 x 10 grid
/// of the category's wares (inv_box_10x10) paged with the arrows, the item under the cursor named and priced in
/// the info line, and the examine button. The inventory opens beside it; carry wares into the backpack to buy
/// and items onto the store to sell immediately. Clicking a ware asks first with buysell_confirm.
/// Driven by "store-open" (re-sent after every trade) and "store-close".
/// </summary>
public partial class ClassicStoreWindow : ClassicWindow
{
    public ClassicItems Items = null!;

    // Store categories (UTM StoreList ids) in the original's tab order (ctl_inv_sto_tab1..5 show armour, a sword, potions,
    // jewellery and gauntlets), with the name its page label shows (measured, `storeradial`).
    static readonly (int Panel, string Name)[] Categories =
        [(0, "Clothing and Armor"), (4, "Weapons"), (2, "Scrolls and Potions"), (3, "Magic Items"), (1, "Miscellaneous")];

    ClassicItemGrid _grid = null!;
    readonly List<GuiButton> _tabs = new();
    GuiButton? _pageLabel;
    GuiLabel? _title, _info;
    List<ItemEntry> _stock = new();
    readonly Dictionary<uint, int> _offers = new();
    int[] _categoryPages = [1, 1, 1, 1, 1];
    int _storeGold = -1, _identifyCost = -1, _category;
    uint _store, _selected;
    bool _serverClosed;

    public uint StoreId => _store;
    public ClassicItemGrid Grid => _grid;

    protected override GuiPanel Build()
    {
        var p = GuiPanel.FromLayout(Assets, "ingame_store") ?? GuiPanel.FromModel(Assets, "pnl_store");
        _title = p.Get<GuiLabel>("Title");
        _info = p.Get<GuiLabel>("ItemInfo");
        if (_info != null) _info.Wrap = true;
        _pageLabel = p.Get<GuiButton>("ScrollPageLabel");
        if (_pageLabel != null) _pageLabel.MouseFilter = MouseFilterEnum.Ignore;
        if (p.Get<GuiButton>("ExamineButton") is { } eye) eye.SetMeta(ClassicTooltip.Meta, "Examine");

        var box = p.Get<GuiModelView>("InvBox");
        _grid = new ClassicItemGrid { Assets = Assets, Columns = 10, Rows = 10, Name = "StoreGrid", Source = "store", DropAnywhere = true };
        if (box != null)
        {
            _grid.Position = box.Position;
            _grid.Size = box.Size;
            _grid.Cell = new Vector2(box.Size.X / 10, box.Size.Y / 10);
        }
        _grid.AcceptsFrom = d => d.Source is "pack" or "bag";
        _grid.Dropped = (d, _, _) => Sell(d.Item, confirm: false);
        _grid.Activated = i => Buy(i, null);
        _grid.Clicked = i => ClickWare(i, confirm: true);
        _grid.ShiftClicked = i => ClickWare(i, confirm: false);
        // The original's ware ring (measured, `storeradial`): Examine N and Buy E, nothing else.
        _grid.Context = (i, at) => Items.ShowForeignMenu(i, at, () => IsOpen,
            (0, "Examine", "", () => Items.ExamineItem(i.Id)),
            (2, "Buy", "ir_buy", i.Price <= Items.Data.Gold ? () => Buy(i, null) : null));
        _grid.Hovered = i => ShowInfo(i, selling: false);
        _grid.Tip = i => i.Name;
        _grid.PageChanged += RefreshPageLabel;
        p.AddChild(_grid);

        var step = Assets.Model("ctl_inv_sto_tab1")?.Node("next") ?? new Vector2(66, 0);
        var at = p.Node("tabs") ?? new Vector2(14, 57);
        for (int i = 0; i < Categories.Length; i++)
        {
            int panel = Categories[i].Panel;
            var b = GuiButton.Create(Assets, $"ctl_inv_sto_tab{i + 1}");
            b.SetMeta(ClassicTooltip.Meta, Categories[i].Name);
            b.ToggleMode = true;
            b.TextureRepeat = TextureRepeatEnum.Disabled;
            b.TextureFilter = TextureFilterEnum.Nearest;
            b.Name = "Tab" + panel;
            b.PlaceOrigin(at + step * i);
            b.Pressed += () => SelectCategory(panel);
            _tabs.Add(b);
            p.AddChild(b);
        }

        p.ButtonPressed += tag =>
        {
            switch (tag)
            {
                case "ScrollUp": _grid.Page--; break;
                case "ScrollDown": _grid.Page++; break;
                case "ExamineButton":
                    if (_selected != 0) Items.ExamineItem(_selected);
                    break;
            }
        };
        Ctx.Session.NotifyReceived += OnNotify;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
        return p;
    }

    bool ClickWare(ItemEntry item, bool confirm)
    {
        ShowInfo(item, selling: false);
        if (Items.Inventory.ExamineMode) return Examine(item);
        Buy(item, null, confirm);
        return true;
    }

    bool Examine(ItemEntry i)
    {
        Items.ExamineItem(i.Id);
        return true;
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "store-open":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                // The original keeps the layout's "Store" title (not the store's name).
                _store = d.TryGetProperty("store", out var sid) ? sid.GetUInt32() : m.Subject;
                _stock = d.GetProperty("items").EnumerateArray().Select(e => ItemEntry.From(e)).ToList();
                _storeGold = d.TryGetProperty("gold", out var g) ? g.GetInt32() : -1;
                _identifyCost = d.TryGetProperty("identifyCost", out var ic) ? ic.GetInt32() : -1;
                _grid.Spec = GridSpec.From(d, "grid", new GridSpec(10, 10, 0));
                _categoryPages = d.TryGetProperty("categoryPages", out var pages)
                    ? pages.EnumerateArray().Select(p => p.GetInt32()).ToArray() : [1, 1, 1, 1, 1];
                _offers.Clear();
                if (d.TryGetProperty("offers", out var offers))
                    foreach (var o in offers.EnumerateArray()) _offers[o.GetProperty("id").GetUInt32()] = o.GetProperty("price").GetInt32();
                _grid.Holder = _store;
                if (!Visible)
                {
                    _selected = 0;
                    _category = Categories.Select(c => c.Panel).FirstOrDefault(c => _stock.Any(s => s.Panel == c), 0);
                    Items.Inventory.Close();
                    CloseLeftPanel();
                    Open();
                    Items.Inventory.Open();
                }
                Ctx.Session.Request("inventory");
                Fill();
                break;
            }
            case "store-close":
                _serverClosed = true;
                Close();
                break;
        }
    }

    public void SelectCategory(int panel)
    {
        _category = panel;
        _selected = 0;
        _grid.SelectedId = 0;
        _grid.Page = 0;
        Fill();
    }

    void Fill()
    {
        for (int i = 0; i < _tabs.Count; i++) _tabs[i].SetPressedNoSignal(Categories[i].Panel == _category);
        _grid.Spec = _grid.Spec with { Pages = _categoryPages.ElementAtOrDefault(_category) };
        _grid.SetItems(_stock.Where(s => s.Panel == _category));
        RefreshPageLabel();
        ShowInfo(_stock.FirstOrDefault(s => s.Id == _selected), selling: false);
    }

    void RefreshPageLabel()
    {
        if (_pageLabel == null) return;
        string name = Categories.FirstOrDefault(c => c.Panel == _category).Name ?? "";
        _pageLabel.Caption = $"{name} {_grid.Page + 1}/{_grid.PageCount}";
    }

    /// <summary>Names and prices the item under the cursor (a ware, or one of yours with what the store pays).</summary>
    public void ShowInfo(ItemEntry? item, bool selling)
    {
        if (!Visible || _info == null) return;
        if (item == null) { _info.Text = ""; return; }
        if (selling)
        {
            int offer = Offer(item);
            _info.Text = offer < 0 ? "The store will not buy this." : TradeText(item, selling: true);
        }
        else _info.Text = TradeText(item, selling: false);
        // The original prices a ware it can't afford in the same white as any other (no red warning).
        _info.Color = Colors.White;
    }

    public override void Close()
    {
        if (!Visible) return;
        base.Close();
        Items.Confirm.Close();
        if (!_serverClosed) Ctx.Session.Request("store-close");
        _serverClosed = false;
        if (Items.Inventory.IsOpen) Items.Inventory.Reposition();
    }

    // ------------------------------------------------------------------ trades (as the modern store)

    string TradeText(ItemEntry item, bool selling) => Assets.Tlk(selling ? 61619u : 61620u)
        .Replace("<CUSTOM0>", item.Name).Replace("<CUSTOM1>", (selling ? Offer(item) : item.Price).ToString());

    public int Offer(ItemEntry item) => _offers.TryGetValue(item.Id, out var p) ? p : -1;

    public bool CanSell(ItemEntry item) => Visible && item.Slot < 0 && Offer(item) is var o && o >= 0 && (_storeGold < 0 || o <= _storeGold);

    /// <summary>Buys a ware (into a backpack cell when it was carried onto one).</summary>
    public void Buy(ItemEntry item, (int X, int Y)? cell, bool confirm = true, bool playPickup = true)
    {
        if (!Visible || _stock.All(s => s.Id != item.Id) || item.Price > Items.Data.Gold) return;
        void Go()
        {
            if (cell is { } c) Ctx.Session.Request("store-buy", new { item = item.Id, x = c.X, y = c.Y });
            else Ctx.Session.Request("store-buy", new { item = item.Id });
        }
        if (playPickup) ItemDragging.PlayPickupSound(Assets.Content);
        if (confirm)
        {
            Items.Confirm.Ask(TradeText(item, selling: false), Go);
            _grid.ClearHover();
        }
        else Go();
    }

    public void Sell(ItemEntry item, bool confirm = true)
    {
        if (!CanSell(item)) return;
        int offer = Offer(item);
        void Go()
        {
            Ctx.Session.Request("store-sell", new { item = item.Id });
            if (_selected == item.Id) _selected = 0;
        }
        if (confirm) Items.Confirm.Ask(TradeText(item, selling: true), Go);
        else Go();
    }

    public bool CanIdentify(uint id) =>
        Visible && _identifyCost >= 0 && Items.Data.Gold >= _identifyCost
        && (Items.Data.Carried.FirstOrDefault(i => i.Id == id) ?? Items.Data.BagContents.Values.SelectMany(v => v).FirstOrDefault(i => i.Id == id)) is { Identified: false };

    public void Identify(uint id)
    {
        if (CanIdentify(id)) Ctx.Session.Request("store-identify", new { item = id });
    }
}
