using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The loot window for chests, barrels and corpses. Classic view: the container's grid (items at
/// their cells, 6 wide, as many rows as it holds); Modern view: uniform slots. Click an item (or drag
/// it to a backpack cell) to take it, "Take all" for everything; drag backpack items onto it to put
/// them in, onto a cell of your choosing. Closes when the server says so (walked away).
/// </summary>
public partial class ContainerPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    public InventoryPanel Inventory = null!;
    public Action<uint>? Examine;

    const int ModernColumns = 5, SlotSize = 56;
    const float Cell = 52;
    GridContainer _uniform = null!;
    ItemGrid _grid = null!;
    Control _classic = null!, _modern = null!;
    Label _count = null!;
    Button _takeAll = null!;
    List<ItemEntry> _items = new();
    uint _container;
    GridSpec _spec = new(6, 6, 0);
    bool _serverClosed, _placed;

    public static ContainerPanel Create(ClientSession session, InventoryPanel inventory)
    {
        var p = Create<ContainerPanel>("Container", new Vector2(0, 0));
        p.Session = session;
        p.Inventory = inventory;
        return p;
    }

    public override void _Ready()
    {
        // Classic: the container's own grid.
        _grid = new ItemGrid { Cell = Cell, DragSource = "container", MinRows = 6 };
        _grid.AcceptsFrom = d => d.Source is "pack" or "bag" or "container";
        _grid.Dropped = (d, x, y) =>
        {
            if (d.Source == "container") Session.Request("item-move", new { item = d.Item.Id, x, y, target = _container });
            else Session.Request("container-put", new { item = d.Item.Id, x, y });
        };
        _grid.Clicked = Take;
        _grid.ContextRequested = (i, _) => Examine?.Invoke(i.Id);
        _grid.TooltipExtra = _ => new ItemTooltip.Extra { Hint = "Click or drag to your backpack to take  ·  right-click to examine" };
        var classicScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(6 * Cell + 12 + 10, 6 * Cell + 12) };
        var well = new PanelContainer();
        well.AddThemeStyleboxOverride("panel", Styles.Inset(6));
        well.AddChild(_grid);
        classicScroll.AddChild(well);
        _classic = classicScroll;
        Body.AddChild(classicScroll);

        // Modern: uniform slots.
        var area = new ItemDropArea();
        area.AddThemeStyleboxOverride("panel", Styles.Inset(8));
        area.CanAccept = d => d.Source is "pack" or "bag";
        area.Accept = d => Session.Request("container-put", new { item = d.Item.Id });
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(ModernColumns * SlotSize + (ModernColumns - 1) * 6 + 10, 3 * SlotSize + 2 * 6) };
        area.AddChild(scroll);
        _uniform = new GridContainer { Columns = ModernColumns };
        _uniform.AddThemeConstantOverride("h_separation", 6);
        _uniform.AddThemeConstantOverride("v_separation", 6);
        scroll.AddChild(_uniform);
        _modern = area;
        Body.AddChild(area);

        var foot = Ui.HBox(8);
        _count = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        _count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _count.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foot.AddChild(_count);
        _takeAll = Ui.Primary("Take all", () => Session.Request("container-take-all"));
        _takeAll.CustomMinimumSize = new Vector2(110, 34);
        _takeAll.TooltipText = "Move everything into your inventory";
        foot.AddChild(_takeAll);
        Body.AddChild(foot);

        Closed += () =>
        {
            Inventory.PutHandler = null;
            if (!_serverClosed) Session.Request("container-close");
            _serverClosed = false;
        };
        VisibilityChanged += () =>
        {
            if (!Visible || _placed) return;
            _placed = true;
            // At the left, so it sits beside the inventory window (which opens at the right).
            Callable.From(() =>
            {
                var view = GetViewportRect().Size;
                var size = GetCombinedMinimumSize();
                Position = new Vector2(24, Math.Max(16, (view.Y - size.Y) / 2 - 40));
            }).CallDeferred();
        };
        InventorySettings.Changed += OnViewChanged;
        Session.NotifyReceived += OnNotify;
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "container-open":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                TitleLabel.Text = d.GetProperty("name").GetString() is { Length: > 0 } n ? NwnText.Strip(n) : "Container";
                _container = d.TryGetProperty("container", out var c) ? c.GetUInt32() : m.Subject;
                _spec = GridSpec.From(d, "grid", _spec);
                _items = d.GetProperty("items").EnumerateArray().Select(e => ItemEntry.From(e)).ToList();
                Fill();
                Visible = true;
                Inventory.PutHandler = item => Session.Request("container-put", new { item = item.Id });
                if (Inventory.Visible) Session.Request("inventory");
                break;
            }
            case "container-close":
                _serverClosed = true;
                Close();
                break;
        }
    }

    void Take(ItemEntry item) => Session.Request("container-take", new { item = item.Id });

    void Fill()
    {
        var content = Session.Content;
        bool modern = InventorySettings.ModernView;
        _classic.Visible = !modern;
        _modern.Visible = modern;
        if (!modern)
        {
            _grid.Holder = _container;
            _grid.Spec = _spec;
            _grid.SetItems(_items, content);
        }
        else
        {
            foreach (var c in _uniform.GetChildren()) { _uniform.RemoveChild(c); c.QueueFree(); }
            foreach (var item in _items)
            {
                var slot = ItemSlot.Create(SlotSize);
                slot.SetItem(item, content);
                slot.DragSource = "container";
                slot.GridHolder = _container;
                slot.Clicked = Take;
                slot.ContextRequested = (i, _) => Examine?.Invoke(i.Id);
                slot.CanAccept = d => d.Source is "pack" or "bag";
                slot.Accept = d => Session.Request("container-put", new { item = d.Item.Id });
                slot.TooltipExtra = _ => new ItemTooltip.Extra { Hint = "Click or drag to your inventory to take  ·  right-click to examine" };
                _uniform.AddChild(slot);
            }
            int cells = Math.Max(ModernColumns * 3, (_items.Count + ModernColumns - 1) / ModernColumns * ModernColumns);
            for (int i = _items.Count; i < cells; i++)
            {
                var empty = ItemSlot.Create(SlotSize);
                empty.CanAccept = d => d.Source is "pack" or "bag";
                empty.Accept = d => Session.Request("container-put", new { item = d.Item.Id });
                _uniform.AddChild(empty);
            }
        }
        _count.Text = _items.Count == 0 ? "Empty" : _items.Count == 1 ? "1 item" : $"{_items.Count} items";
        _takeAll.Disabled = _items.Count == 0;
        Callable.From(() => { if (IsInsideTree()) ResetSize(); }).CallDeferred();
    }

    void OnViewChanged() { if (Visible) Fill(); }

    // The view setting outlives this panel (a new game builds a new one): stop listening when it goes.
    public override void _ExitTree() => InventorySettings.Changed -= OnViewChanged;
}
