using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Barter with another player (modern look; the classic one is ClassicBarterWindow). Their offer on the left, yours
/// on the right: drag backpack items onto yours (or click them in the inventory) and click your items to take them
/// back, set the gold, then Offer to lock your offer and Accept once both are locked. Driven by the server's
/// "barter-prompt", "barter-state" and "barter-close" (docs/BARTER.md).
/// </summary>
public partial class BarterPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    public InventoryPanel Inventory = null!;
    public Action<uint>? Examine;

    const int Columns = 3, SlotSize = 52;
    GridContainer _theirs = null!, _mine = null!;
    Label _theirName = null!, _theirStatus = null!, _myStatus = null!, _theirGold = null!;
    SpinBox _gold = null!;
    Button _offer = null!, _accept = null!;
    bool _serverClosed, _filling, _placed;
    List<ItemEntry> _myItems = new();
    Dialogs? _prompt;

    void DropPrompt()
    {
        if (_prompt != null && IsInstanceValid(_prompt)) _prompt.QueueFree();
        _prompt = null;
    }

    public static BarterPanel Create(ClientSession session, InventoryPanel inventory)
    {
        var p = Create<BarterPanel>("Barter", new Vector2(0, 0));
        p.Session = session;
        p.Inventory = inventory;
        return p;
    }

    public override void _Ready()
    {
        var columns = Ui.HBox(16);
        Body.AddChild(columns);
        (_theirs, _theirName, _theirStatus, _theirGold) = Column(columns, mine: false);
        (_mine, _, _myStatus, _) = Column(columns, mine: true);

        var gold = Ui.HBox(8);
        gold.AddChild(Ui.Text("Your gold offer", Ui.TextStyle.Caption, Palette.TextMuted));
        _gold = new SpinBox { MinValue = 0, MaxValue = 1_000_000_000, Step = 1, CustomMinimumSize = new Vector2(140, 0) };
        _gold.ValueChanged += v => { if (!_filling) Session.Request("barter-gold", new { gold = (int)v }); };
        gold.AddChild(_gold);
        Body.AddChild(gold);

        var foot = Ui.HBox(8);
        foot.AddChild(Ui.Ghost("Cancel", () => Close()));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foot.AddChild(spacer);
        _offer = Ui.Ghost("Offer", () => Session.Request("barter-lock"));
        _offer.TooltipText = "Lock your offer";
        foot.AddChild(_offer);
        _accept = Ui.Primary("Accept", () => Session.Request("barter-accept"));
        _accept.TooltipText = "Accept the trade once both offers are locked";
        foot.AddChild(_accept);
        Body.AddChild(foot);

        Closed += () =>
        {
            Inventory.PutHandler = null;
            if (!_serverClosed) Session.Request("barter-cancel");
            _serverClosed = false;
        };
        VisibilityChanged += () =>
        {
            if (!Visible || _placed) return;
            _placed = true;
            Callable.From(() =>
            {
                var view = GetViewportRect().Size;
                Position = new Vector2(24, Math.Max(16, (view.Y - GetCombinedMinimumSize().Y) / 2 - 40));
            }).CallDeferred();
        };
        Session.NotifyReceived += OnNotify;
        TreeExiting += () => Session.NotifyReceived -= OnNotify;
    }

    (GridContainer Grid, Label Name, Label Status, Label Gold) Column(HBoxContainer parent, bool mine)
    {
        var col = Ui.VBox(6);
        var name = Ui.Text(mine ? "Your offer" : "", Ui.TextStyle.Heading);
        col.AddChild(name);
        var area = new ItemDropArea();
        area.AddThemeStyleboxOverride("panel", Styles.Inset(8));
        if (mine)
        {
            area.CanAccept = d => d.Source is "pack";
            area.Accept = d => Session.Request("barter-offer", new { item = d.Item.Id });
        }
        var grid = new GridContainer { Columns = Columns, CustomMinimumSize = new Vector2(Columns * SlotSize + (Columns - 1) * 6, 4 * SlotSize + 18) };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        area.AddChild(grid);
        col.AddChild(area);
        var gold = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        col.AddChild(gold);
        var status = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        col.AddChild(status);
        parent.AddChild(col);
        return (grid, name, status, gold);
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "barter-prompt":
            {
                if (Classic.ClassicGame.Current != null) break; // the classic window asks
                using var doc = JsonDocument.Parse(m.Json);
                uint from = doc.RootElement.GetProperty("from").GetUInt32();
                string text = doc.RootElement.GetProperty("text").GetString() ?? "Barter";
                DropPrompt();
                _prompt = Dialogs.Confirm(GetParent(), "Barter", NwnText.Strip(text), "Barter",
                    () => Session.Request("barter-answer", new { from, accept = true }), cancel: "Decline",
                    onCancel: () => Session.Request("barter-answer", new { from, accept = false }));
                break;
            }
            case "barter-state":
            {
                DropPrompt();
                using var doc = JsonDocument.Parse(m.Json);
                Fill(doc.RootElement);
                if (Classic.ClassicGame.Current != null) break;
                Visible = true;
                Inventory.PutHandler = item => Session.Request("barter-offer", new { item = item.Id });
                if (!Inventory.Visible) Inventory.Toggle();
                break;
            }
            case "barter-close":
                DropPrompt();
                _serverClosed = true;
                Close();
                _serverClosed = false;
                break;
        }
    }

    void Fill(JsonElement d)
    {
        var mine = d.GetProperty("mine");
        var theirs = d.GetProperty("theirs");
        _myItems = mine.GetProperty("items").EnumerateArray().Select(e => ItemEntry.From(e)).ToList();
        Slots(_theirs, theirs.GetProperty("items").EnumerateArray().Select(e => ItemEntry.From(e)).ToList(), mine: false);
        Slots(_mine, _myItems, mine: true);
        string Status(JsonElement side) => side.GetProperty("accepted").GetBoolean() ? "Accepted"
            : side.GetProperty("offered").GetBoolean() ? "Offer locked" : "Choosing";
        _theirName.Text = NwnText.Strip(theirs.GetProperty("name").GetString() ?? "");
        _theirGold.Text = $"{theirs.GetProperty("gold").GetInt32():N0} gold";
        _theirStatus.Text = Status(theirs);
        _myStatus.Text = Status(mine);
        TitleLabel.Text = "Barter with " + _theirName.Text;
        bool locked = mine.GetProperty("offered").GetBoolean();
        _filling = true;
        _gold.Value = mine.GetProperty("gold").GetInt32();
        _filling = false;
        _gold.Editable = !locked;
        _offer.Disabled = locked;
        _accept.Disabled = !locked || !theirs.GetProperty("offered").GetBoolean() || mine.GetProperty("accepted").GetBoolean();
        Callable.From(() => { if (IsInsideTree()) ResetSize(); }).CallDeferred();
    }

    void Slots(GridContainer grid, List<ItemEntry> items, bool mine)
    {
        foreach (var c in grid.GetChildren()) { grid.RemoveChild(c); c.QueueFree(); }
        for (int i = 0; i < Math.Max(Columns * 4, items.Count); i++)
        {
            var slot = ItemSlot.Create(SlotSize);
            if (i < items.Count)
            {
                var item = items[i];
                slot.SetItem(item, Session.Content);
                slot.ContextRequested = (it, _) => Examine?.Invoke(it.Id);
                if (mine) slot.Clicked = it => Session.Request("barter-withdraw", new { item = it.Id });
            }
            if (mine)
            {
                slot.CanAccept = d => d.Source is "pack";
                slot.Accept = d => Session.Request("barter-offer", new { item = d.Item.Id });
            }
            grid.AddChild(slot);
        }
    }
}
