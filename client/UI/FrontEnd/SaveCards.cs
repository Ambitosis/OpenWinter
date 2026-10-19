using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>A save game as the Save / Load screens show it.</summary>
public sealed record SaveEntry(string Folder, string Name, string Kind, DateTime SavedAt, string Area, string Player, double Played, string Module, bool HasScreen, string ModulePath = "");

/// <summary>
/// A save slot card: screenshot thumbnail (with a Quick / Auto badge), name, character and area, play
/// time and when it was saved. Click selects, double-click activates.
/// </summary>
public partial class SaveCard : PanelContainer
{
    public SaveEntry Entry = null!;
    public Action<SaveCard>? Selected, Activated;
    TextureRect _thumb = null!;
    bool _selected, _hover;
    public const int Width = 252;

    public static SaveCard Create(SaveEntry e)
    {
        var c = new SaveCard { Entry = e, CustomMinimumSize = new Vector2(Width, 0), MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        var col = Ui.VBox(Palette.Space1 + 2);
        col.MouseFilter = MouseFilterEnum.Ignore;
        c.AddChild(col);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(Width - 20, (Width - 20) * 10 / 16), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, 6, Palette.Line, 1, 0));
        var glyph = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        glyph.AddChild(Icons.Rect("save", 30, Palette.TextMuted with { A = 0.4f }));
        frame.AddChild(glyph);
        c._thumb = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        frame.AddChild(c._thumb);
        if (e.Kind is "quick" or "auto")
        {
            var badgeBox = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            badgeBox.AddThemeConstantOverride("margin_left", 6);
            badgeBox.AddThemeConstantOverride("margin_top", 6);
            var badge = FrontEndUi.Chip(e.Kind == "quick" ? "QUICK SAVE" : "AUTOSAVE", e.Kind == "quick" ? Palette.Info : Palette.Success);
            badge.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            badge.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            ((PanelContainer)badge).AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.7f), 10, (e.Kind == "quick" ? Palette.Info : Palette.Success) with { A = 0.6f }, 1, 8, 2));
            badgeBox.AddChild(badge);
            frame.AddChild(badgeBox);
        }
        col.AddChild(frame);
        var name = Ui.Text(e.Name.Length > 0 ? e.Name : "Saved game", Ui.TextStyle.Strong, Palette.Text, Palette.FontBody + 1);
        name.ClipText = true;
        col.AddChild(name);
        var where = Ui.Text(string.Join(" · ", new[] { e.Player, e.Area }.Where(s => s.Length > 0)), Ui.TextStyle.Small);
        where.ClipText = true;
        col.AddChild(where);
        var meta = Ui.HBox(6);
        meta.MouseFilter = MouseFilterEnum.Ignore;
        if (e.Played > 0)
        {
            meta.AddChild(Icons.Rect("hourglass", 12, Palette.TextMuted));
            meta.AddChild(Ui.Text(FrontEndContent.FormatPlayTime(e.Played), Ui.TextStyle.Caption, Palette.TextMuted));
            meta.AddChild(Ui.Text("·", Ui.TextStyle.Caption, Palette.TextMuted));
        }
        var when = Ui.Text(e.SavedAt == default ? "" : FrontEndContent.FormatAgo(e.SavedAt), Ui.TextStyle.Caption, Palette.TextMuted);
        when.TooltipText = e.SavedAt == default ? "" : e.SavedAt.ToString("f");
        meta.AddChild(when);
        col.AddChild(meta);
        c.TooltipText = $"{e.Name}\n{e.Module}\nSaved {e.SavedAt:f}";
        c.Restyle();
        c.MouseEntered += () => { c._hover = true; c.Restyle(); };
        c.MouseExited += () => { c._hover = false; c.Restyle(); };
        c.FocusEntered += () => c.Selected?.Invoke(c);
        c.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
            {
                c.Selected?.Invoke(c);
                if (mb.DoubleClick) c.Activated?.Invoke(c);
            }
            else if (ev.IsActionPressed("ui_accept")) c.Activated?.Invoke(c);
        };
        return c;
    }

    public bool IsSelected
    {
        get => _selected;
        set { _selected = value; Restyle(); }
    }

    public void SetThumbnail(Texture2D? tex)
    {
        if (tex == null) return;
        _thumb.Texture = tex;
        FrontEndUi.FadeIn(_thumb, 0.25);
    }

    void Restyle()
    {
        var (normal, hover, selected) = FrontEndUi.CardStyles(10);
        AddThemeStyleboxOverride("panel", _selected ? selected : _hover ? hover : normal);
    }
}

/// <summary>Save cards flowing in rows, with an empty state.</summary>
public partial class SaveGrid : ScrollContainer
{
    public Action<SaveEntry?>? SelectionChanged;
    public Action<SaveEntry>? Activated;
    HFlowContainer _flow = null!;
    Control _empty = null!;
    readonly List<SaveCard> _cards = new();
    public SaveEntry? Selected { get; private set; }
    public string EmptyText = "No saved games yet.";

    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Disabled;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var col = Ui.VBox(0);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(col);
        _flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _flow.AddThemeConstantOverride("h_separation", Palette.Space3);
        _flow.AddThemeConstantOverride("v_separation", Palette.Space3);
        col.AddChild(Ui.Pad(_flow, 2, 2, 12, 12));
        _empty = new CenterContainer { CustomMinimumSize = new Vector2(0, 260), Visible = false };
        var e = Ui.VBox(Palette.Space2);
        e.Alignment = BoxContainer.AlignmentMode.Center;
        var icon = Icons.Rect("save", 48, Palette.TextMuted with { A = 0.5f });
        e.AddChild(icon);
        var label = Ui.Text(EmptyText, Ui.TextStyle.Secondary, Palette.TextMuted);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Name = "Label";
        e.AddChild(label);
        _empty.AddChild(e);
        col.AddChild(_empty);
    }

    public void Set(IEnumerable<SaveEntry> saves, Control? leading = null)
    {
        FrontEndUi.Clear(_flow);
        _cards.Clear();
        if (leading != null) _flow.AddChild(leading);
        foreach (var s in saves)
        {
            var card = SaveCard.Create(s);
            card.Selected = Select;
            card.Activated = c => Activated?.Invoke(c.Entry);
            _flow.AddChild(card);
            _cards.Add(card);
        }
        _empty.Visible = _cards.Count == 0 && leading == null;
        ((Label)_empty.FindChild("Label", true, false)).Text = EmptyText;
        var keep = Selected == null ? null : _cards.FirstOrDefault(c => c.Entry.Folder == Selected.Folder);
        Select(keep);
    }

    public void Select(SaveCard? card)
    {
        foreach (var c in _cards) c.IsSelected = c == card;
        Selected = card?.Entry;
        SelectionChanged?.Invoke(Selected);
    }

    public void SelectFirst() => Select(_cards.FirstOrDefault());

    public void SetThumbnail(string folder, Texture2D? tex)
    {
        foreach (var c in _cards) if (c.Entry.Folder == folder) c.SetThumbnail(tex);
    }

    public IReadOnlyList<SaveCard> Cards => _cards;
}
