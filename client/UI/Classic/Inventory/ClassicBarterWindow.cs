using System.Text.Json;
using OpenWinter.Game.Inventory;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's barter window (ingame_barter): the other player's portrait, offer (BarterItems, inv_box_3x10) and
/// Offer / Accept lamps on the left; the player's own portrait, offer (PlayerItems) and Cancel / Offer / Accept
/// buttons on the right, the gold and examine buttons, and the inventory open beside it. Carry backpack items onto
/// your offer, items of your offer back to the backpack (or click them) to take them out. Offer locks your offer,
/// Accept trades once both are locked. Driven by "barter-prompt", "barter-state" and "barter-close"; the rules are
/// the server's (docs/BARTER.md).
/// </summary>
public partial class ClassicBarterWindow : ClassicWindow
{
    public ClassicItems Items = null!;

    ClassicItemGrid _theirs = null!, _mine = null!;
    TextureRect _theirPortrait = null!, _myPortrait = null!;
    GuiLabel? _title;
    GuiLabel _theirGold = null!, _myGold = null!;
    GuiButton? _offer, _accept, _theirOffer, _theirAccept;
    int _cols = 3, _rows = 10, _myGoldOffer;
    bool _serverClosed;
    uint _selected;
    ClassicPopup? _prompt;

    /// <summary>The question goes once the barter opens or ends (or another question comes).</summary>
    void DropPrompt()
    {
        if (_prompt != null && IsInstanceValid(_prompt)) _prompt.QueueFree();
        _prompt = null;
    }

    protected override GuiPanel Build()
    {
        var p = GuiPanel.FromLayout(Assets, "ingame_barter") ?? GuiPanel.FromModel(Assets, "pnl_barter");
        _title = p.Get<GuiLabel>("TitleText");
        _offer = p.Get<GuiButton>("LeftDeclineBut");
        _accept = p.Get<GuiButton>("LeftAcceptBut");
        _theirOffer = p.Get<GuiButton>("RightDeclineBut");
        _theirAccept = p.Get<GuiButton>("RightAcceptBut");
        // The other player's buttons only show what she has done.
        foreach (var lamp in new[] { _theirOffer, _theirAccept }) if (lamp != null) lamp.MouseFilter = MouseFilterEnum.Ignore;
        if (p.Get<GuiButton>("ExamineButton") is { } eye) eye.SetMeta(ClassicTooltip.Meta, "Examine");
        if (p.Get<GuiButton>("GoldButton") is { } gold) gold.SetMeta(ClassicTooltip.Meta, "Offer gold");

        _theirs = Grid(p, "BarterItems", "barter-theirs");
        _mine = Grid(p, "PlayerItems", "barter");
        _mine.AcceptsFrom = d => d.Source is "pack";
        _mine.Dropped = (d, _, _) => Ctx.Session.Request("barter-offer", new { item = d.Item.Id });
        _mine.Clicked = i => { Ctx.Session.Request("barter-withdraw", new { item = i.Id }); return true; };
        _theirs.AcceptsFrom = _ => false;
        _theirs.Clicked = i => { _selected = i.Id; return true; };
        foreach (var g in new[] { _theirs, _mine })
        {
            g.Context = (i, at) => Items.ShowForeignMenu(i, at, () => IsOpen, (0, "Examine", "", () => Items.ExamineItem(i.Id)));
            g.Tip = i => i.Name;
        }

        _theirPortrait = Portrait(p, "BarratorPortrait");
        _myPortrait = Portrait(p, "PlayerPortrait");
        _theirGold = GoldLabel(p, _theirs);
        _myGold = GoldLabel(p, _mine);

        p.ButtonPressed += tag =>
        {
            switch (tag)
            {
                case "XButton":
                case "LeftCancelBut": Close(); break;
                case "LeftDeclineBut": Ctx.Session.Request("barter-lock"); break;
                case "LeftAcceptBut": Ctx.Session.Request("barter-accept"); break;
                case "GoldButton":
                    Items.Split.ShowGold(Items.Data.Gold + _myGoldOffer, count => Ctx.Session.Request("barter-gold", new { gold = count }));
                    break;
                case "ExamineButton":
                    if (_selected != 0) Items.ExamineItem(_selected);
                    break;
            }
        };
        Ctx.Session.NotifyReceived += OnNotify;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
        return p;
    }

    ClassicItemGrid Grid(GuiPanel p, string tag, string source)
    {
        var box = p.Get<GuiModelView>(tag);
        var g = new ClassicItemGrid { Assets = Assets, Name = tag + "Grid", Source = source, DropAnywhere = true };
        if (box != null)
        {
            // inv_box_3x10: as many columns as its shape says (taller than wide: 3 across, 10 down).
            (_cols, _rows) = box.Size.X > box.Size.Y ? (10, 3) : (3, 10);
            g.Position = box.Position;
            g.Size = box.Size;
            g.Cell = new Vector2(box.Size.X / _cols, box.Size.Y / _rows);
        }
        g.Columns = _cols;
        g.Rows = _rows;
        g.Spec = new GridSpec(_cols, _rows, 1);
        p.AddChild(g);
        return g;
    }

    static TextureRect Portrait(GuiPanel p, string tag)
    {
        var frame = p.Get<GuiModelView>(tag);
        // ctl_portr_64x128 frames a medium portrait: the picture fills its top 100 rows.
        var r = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        if (frame != null)
        {
            r.Position = frame.Position;
            r.Size = new Vector2(frame.Size.X, frame.Size.X * 100f / 64f);
        }
        p.AddChild(r);
        return r;
    }

    GuiLabel GoldLabel(GuiPanel p, ClassicItemGrid under)
    {
        var l = GuiLabel.Create(Assets, "", "fnt_dialog16x16", alignH: 0.5f);
        l.Position = under.Position + new Vector2(0, under.Size.Y + 2);
        l.Size = new Vector2(under.Size.X, 18);
        p.AddChild(l);
        return l;
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "barter-prompt":
            {
                using var doc = JsonDocument.Parse(m.Json);
                uint from = doc.RootElement.GetProperty("from").GetUInt32();
                string text = doc.RootElement.GetProperty("text").GetString() ?? "";
                DropPrompt();
                _prompt = ClassicPopup.OkCancel(GetParent(), Assets, text,
                    () => { _prompt = null; Ctx.Session.Request("barter-answer", new { from, accept = true }); },
                    () => { _prompt = null; Ctx.Session.Request("barter-answer", new { from, accept = false }); });
                break;
            }
            case "barter-state":
            {
                DropPrompt();
                using var doc = JsonDocument.Parse(m.Json);
                Fill(doc.RootElement);
                if (!Visible)
                {
                    // Like a store: the inventory opens beside it.
                    Items.Inventory.Close();
                    CloseLeftPanel();
                    Open();
                    Items.Inventory.Open();
                }
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
        _theirs.SetItems(Place(theirs.GetProperty("items")));
        _mine.SetItems(Place(mine.GetProperty("items")));
        _myGoldOffer = mine.GetProperty("gold").GetInt32();
        _theirGold.Text = theirs.GetProperty("gold").GetInt32() is var tg and > 0 ? $"{tg} GP" : "";
        _myGold.Text = _myGoldOffer > 0 ? $"{_myGoldOffer} GP" : "";
        if (_title != null) _title.Text = Assets.Tlk(6836).Replace("<FullName>", theirs.GetProperty("name").GetString() ?? "");
        _theirPortrait.Texture = PortraitTexture(theirs.GetProperty("portrait").GetString());
        _myPortrait.Texture = PortraitTexture(mine.GetProperty("portrait").GetString());
        bool locked = mine.GetProperty("offered").GetBoolean(), theyLocked = theirs.GetProperty("offered").GetBoolean();
        if (_offer != null) { _offer.Disabled = locked; _offer.ForcedState = locked ? "down" : null; }
        if (_accept != null)
        {
            bool accepted = mine.GetProperty("accepted").GetBoolean();
            _accept.Disabled = !locked || !theyLocked || accepted;
            _accept.ForcedState = accepted ? "down" : null;
        }
        // Her lamps are lit once she has offered / accepted (inferred: one original client can't show this).
        if (_theirOffer != null) _theirOffer.Disabled = !theyLocked;
        if (_theirAccept != null) _theirAccept.Disabled = !theirs.GetProperty("accepted").GetBoolean();
        foreach (var b in new[] { _offer, _accept, _theirOffer, _theirAccept }) b?.QueueRedraw();
    }

    /// <summary>The offer laid out first-fit from the bottom row, as other grids place new items.</summary>
    List<ItemEntry> Place(JsonElement items)
    {
        var shape = new GridShape(_cols, _rows, 1);
        var used = new HashSet<(int X, int Y)>();
        var placed = new List<ItemEntry>();
        foreach (var e in items.EnumerateArray())
        {
            var item = ItemEntry.From(e);
            if (InventoryGridPlacement.FirstFit(shape, item.W, item.H, used) is not { } cell) continue;
            for (int x = 0; x < item.W; x++)
                for (int y = 0; y < item.H; y++) used.Add((cell.X + x, cell.Y + y));
            placed.Add(item with { X = cell.X, Y = cell.Y });
        }
        return placed;
    }

    Texture2D? PortraitTexture(string? resref)
    {
        if (string.IsNullOrEmpty(resref) || Ctx.Session.Content is not { } content) return null;
        foreach (var size in new[] { "m", "l", "h", "s" })
        {
            if (content.Textures.Get(resref + size) is not { } tex) continue;
            var s = tex.Texture.GetSize();
            float h = s.Y * 100f / 128f;
            return new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, tex.FlipV ? s.Y - h : 0, s.X, h) };
        }
        return null;
    }

    public override void Close()
    {
        bool wasOpen = Visible;
        base.Close();
        if (wasOpen && !_serverClosed) Ctx.Session.Request("barter-cancel");
    }
}
