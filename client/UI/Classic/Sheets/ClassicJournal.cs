using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// The original's journal (J), top-left: jnl_panel (title, tabs) over a page. Quests and Completed quests use
/// jnl_page_quest (sort by Date, Name or Priority, then a tree: a quest per row, its entries under it when
/// unfolded); Notes uses jnl_page_notes (free text, kept for the session). Quests come from the modern journal's
/// data (the server's "journal" notifies). The World page (jnl_page_world: world entries sorted by Date or Name over
/// the selected entry's text) is built too, but like the original (players and DMs, single player and on a server;
/// mp_journal, mp_journaldm) no tab leads to it: it opens only through <see cref="ShowTab"/>(3), with entries a server
/// sends as "journal-world" (none does) or a capture sets (docs/CLASSIC_UI.md "Journal world page").
/// </summary>
public partial class ClassicJournal : ClassicWindow
{
    /// <summary>A world journal entry (jnl_page_world).</summary>
    public sealed record WorldEntry(string Title, string Text, int Day, int Time, long Order);

    int _tab, _sort, _worldSort, _worldSelected = -1;
    GuiPanel? _worldPage;
    GuiList? _worldList, _worldText;
    readonly GuiButton?[] _worldSorts = new GuiButton?[2];
    readonly List<WorldEntry> _world = new();
    readonly GuiButton[] _tabs = new GuiButton[3];
    readonly GuiButton?[] _sorts = new GuiButton?[3];
    GuiPanel _questPage = null!, _notesPage = null!;
    SheetScroll _tree = null!;
    TextEdit _notes = null!;
    readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);
    const float Row = 34;

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromLayout(a, "jnl_panel") ?? GuiPanel.FromModel(a, "pnl_jnl_panel");
        var pagesAt = p.Node("pages") ?? new Vector2(0, 58);

        _questPage = GuiPanel.FromLayout(a, "jnl_page_quest") ?? GuiPanel.FromModel(a, "pnl_jnl_page1");
        _questPage.Position = pagesAt;
        p.AddChild(_questPage);
        string[] sortTags = { "DateRadio", "NameRadio", "PriorityRadio" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            if (_questPage.Get<GuiButton>(sortTags[i]) is not { } b) continue;
            b.ToggleMode = true;
            b.ButtonPressed = i == 0;
            b.Pressed += () => SetSort(index);
            _sorts[i] = b;
        }
        // The tree between the page's view0 / view1 nodes, its scroll bar on the right edge.
        var v0 = _questPage.Node("view0") ?? new Vector2(1, 401);
        var v1 = _questPage.Node("view1") ?? new Vector2(343, 31);
        _tree = SheetScroll.Create(a, new Rect2(v0.X + 2, v1.Y + 2, v1.X - v0.X - 4, v0.Y - v1.Y - 2),
            new Rect2(v1.X, v1.Y, SheetScrollBar.Width, v0.Y - v1.Y + 1), _questPage);
        _tree.Bar.Step = Row;

        _notesPage = GuiPanel.FromLayout(a, "jnl_page_notes") ?? GuiPanel.FromModel(a, "gui_empty");
        _notesPage.Position = pagesAt;
        _notesPage.Visible = false;
        p.AddChild(_notesPage);
        var box = _notesPage.Get<GuiList>("TextBox");
        var area = box != null ? new Rect2(box.Position + new Vector2(6, 4), new Vector2(330, box.Size.Y - 8)) : new Rect2(6, 4, 330, 400);
        _notes = new TextEdit { Position = area.Position, Size = area.Size, WrapMode = TextEdit.LineWrappingMode.Boundary, ContextMenuEnabled = false };
        var clear = new StyleBoxEmpty();
        foreach (var s in new[] { "normal", "focus", "read_only" }) _notes.AddThemeStyleboxOverride(s, clear);
        _notes.AddThemeFontSizeOverride("font_size", 14);
        _notes.AddThemeColorOverride("font_color", Colors.White);
        _notesPage.AddChild(_notes);

        BuildWorldPage(a, pagesAt, p);

        // Tabs: Quests, Completed quests, Notes.
        var group = new ButtonGroup();
        var at = p.Node("tabs") ?? new Vector2(0, 58);
        string[] models = { "ctl_jnl_btn_tab", "ctl_jnl_btn_tab2", "ctl_jnl_btn_tab3" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var t = GuiButton.Create(a, models[i]);
            t.ToggleMode = true;
            // The tab textures' TXI says "clamp 3" and "filter 0": their frames are stretched edge texels, not repeats.
            t.TextureRepeat = TextureRepeatEnum.Disabled;
            t.TextureFilter = TextureFilterEnum.Nearest;
            t.ButtonGroup = group;
            t.ButtonPressed = i == 0;
            t.PlaceOrigin(at);
            t.Pressed += () => ShowTab(index);
            p.AddChild(t);
            _tabs[i] = t;
            at += t.Model?.Node("next") is { } next ? next : new Vector2(120, 0);
        }
        p.Size = p.CustomMinimumSize = new Vector2(360, pagesAt.Y + _questPage.Size.Y);
        return p;
    }

    public override void _Ready()
    {
        base._Ready();
        Ctx.Journal.QuestsChanged += OnQuests;
        Ctx.Session.NotifyReceived += OnWorldNotify;
        TreeExiting += () => { Ctx.Journal.QuestsChanged -= OnQuests; Ctx.Session.NotifyReceived -= OnWorldNotify; };
    }

    /// <summary>The World page from its layout: "Sort by:" Date / Name (ctl_chk_pre), the entries (QuestBox) and the
    /// selected entry's text (TextBox).</summary>
    void BuildWorldPage(ClassicAssets a, Vector2 pagesAt, GuiPanel p)
    {
        _worldPage = GuiPanel.FromLayout(a, "jnl_page_world");
        if (_worldPage == null) return;
        _worldPage.Position = pagesAt;
        _worldPage.Visible = false;
        p.AddChild(_worldPage);
        _worldList = _worldPage.Get<GuiList>("QuestBox");
        // ctl_jnl_lstbx's scrolling row pattern (its scrollpane plane) reaches 31 px below the box, over "Sort by:" and
        // the radios: the list is clipped at its Viewport0 node, the bottom of its view.
        if (_worldList is { Frame: { } frame } && frame.Node("Viewport0") is { } bottom && _worldList.GetParent() is { } parent)
        {
            var clip = new Control { Name = "QuestBoxClip", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Pass,
                Position = _worldList.Position, Size = new Vector2(_worldList.Size.X, bottom.Y) };
            parent.AddChild(clip);
            parent.MoveChild(clip, _worldList.GetIndex());
            _worldList.Reparent(clip, false);
            _worldList.Position = Vector2.Zero;
        }
        _worldText = _worldPage.Get<GuiList>("TextBox");
        string[] tags = { "DateRadio", "NameRadio" };
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            if (_worldPage.Get<GuiButton>(tags[i]) is not { } b) continue;
            b.ToggleMode = true;
            b.ButtonPressed = i == 0;
            // ctl_chk_pre's states never hide its X (gui_pre_chkbox): drawn only while checked.
            b.Skip = (m, state) => m.Texture.Equals("gui_pre_chkbox", StringComparison.OrdinalIgnoreCase) && state.StartsWith("unchecked");
            b.Pressed += () => { _worldSort = index; for (int k = 0; k < 2; k++) _worldSorts[k]?.SetPressedNoSignal(k == index); FillWorld(); };
            _worldSorts[i] = b;
        }
    }

    /// <summary>World entries ("journal-world": {"entries": [{title, text, day, time}]}), newest last.</summary>
    void OnWorldNotify(OpenWinter.Net.NotifyMessage m)
    {
        if (m.Kind != "journal-world") return;
        using var doc = System.Text.Json.JsonDocument.Parse(m.Json);
        var list = new List<WorldEntry>();
        if (doc.RootElement.TryGetProperty("entries", out var entries))
            foreach (var e in entries.EnumerateArray())
            {
                string S(string k) => e.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
                int I(string k) => e.TryGetProperty(k, out var v) && v.TryGetInt32(out var i) ? i : 0;
                list.Add(new WorldEntry(S("title"), S("text"), I("day"), I("time"), list.Count));
            }
        SetWorldEntries(list);
    }

    public void SetWorldEntries(IEnumerable<WorldEntry> entries)
    {
        _world.Clear();
        _world.AddRange(entries);
        _worldSelected = _world.Count > 0 ? 0 : -1;
        if (Visible && _tab == 3) FillWorld();
    }

    void FillWorld()
    {
        if (_worldList == null) return;
        _worldList.Clear();
        var order = (_worldSort == 1
            ? _world.OrderBy(e => NwnText.Strip(e.Title), StringComparer.CurrentCultureIgnoreCase)
            : _world.OrderByDescending(e => e.Order)).ToList();
        foreach (var e in order)
        {
            int index = _world.IndexOf(e);
            var row = _worldList.AddText(e.Title, () => { _worldSelected = index; FillWorld(); });
            row.Selected = index == _worldSelected;
        }
        _worldText?.Clear();
        if (_worldText != null && _worldSelected >= 0 && _worldSelected < _world.Count)
        {
            var e = _world[_worldSelected];
            if (JournalPanel.Stamp(e.Day, e.Time) is { Length: > 0 } when) _worldText.AddText(when, color: SheetKit.Caption);
            _worldText.AddText(e.Text, wrap: true);
        }
    }

    public override void Refresh()
    {
        Ctx.Journal.SetAttention(false);
        Ctx.Session.Request("journal");
        Fill();
    }

    public override void Close()
    {
        base.Close();
        foreach (var q in Ctx.Journal.Quests.Where(q => _open.Contains(q.Tag)).ToList()) Ctx.Journal.MarkRead(q.Tag);
    }

    /// <summary>0 quests, 1 completed, 2 notes, 3 the World page (no tab of its own, as on the original).</summary>
    public void ShowTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, _worldPage != null ? 3 : 2);
        if (_tab < 3) _tabs[_tab].SetPressedNoSignal(true);
        else foreach (var t in _tabs) t.SetPressedNoSignal(false);
        _questPage.Visible = _tab < 2;
        _notesPage.Visible = _tab == 2;
        if (_worldPage != null) _worldPage.Visible = _tab == 3;
        if (_tab == 3) FillWorld();
        else Fill();
    }

    /// <summary>Unfolds the first quest shown (captures).</summary>
    public void UnfoldFirst()
    {
        if (_tree.Content.GetChildren().OfType<TreeRow>().FirstOrDefault() is { } row) row.Clicked?.Invoke();
    }

    void SetSort(int sort)
    {
        _sort = sort;
        for (int i = 0; i < 3; i++) _sorts[i]?.SetPressedNoSignal(i == sort);
        Fill();
    }

    void OnQuests() { if (Visible) Fill(); }

    public void Fill()
    {
        if (_tab >= 2) return;
        var a = Assets;
        _tree.Clear();
        var quests = Ctx.Journal.Quests.Where(q => q.End == (_tab == 1));
        quests = _sort switch
        {
            1 => quests.OrderBy(q => NwnText.Strip(q.Name), StringComparer.CurrentCultureIgnoreCase),
            2 => quests.OrderBy(q => q.Priority).ThenByDescending(q => q.Order),
            _ => quests.OrderByDescending(q => q.Order),
        };
        float y = 0;
        var font = a.Font(SheetKit.Body);
        float textWidth = _tree.Size.X - 50;
        foreach (var q in quests.ToList())
        {
            bool open = _open.Contains(q.Tag);
            var row = new TreeRow { Assets = a, Arrow = open ? 1 : -1, HideIconBox = true, Text = NwnText.Strip(q.Name), Position = new Vector2(0, y), Size = new Vector2(_tree.Size.X, Row) };
            row.Draw += () => { if (Ctx.Journal.IsUnread(q.Tag)) row.DrawCircle(new Vector2(row.Size.X - 12, Row / 2), 3.5f, GuiDraw.Selected); };
            var tag = q.Tag;
            row.Clicked = () => { if (!_open.Remove(tag)) _open.Add(tag); else Ctx.Journal.MarkRead(tag); Fill(); };
            _tree.Content.AddChild(row);
            y += Row + 2;
            if (!open) continue;
            // The current entry, then the earlier ones, each under its date.
            var entries = new List<(string When, string Text)> { (JournalPanel.Stamp(q.Day, q.Time), q.Text) };
            entries.AddRange(q.History.Select(e => (JournalPanel.Stamp(e.Day, e.Time), e.Text)));
            foreach (var (when, text) in entries)
            {
                var body = NwnText.Strip(text).Trim();
                if (when.Length > 0)
                {
                    _tree.Content.AddChild(new SheetLine { Assets = a, Value = when, ValueColor = SheetKit.Caption, Position = new Vector2(41, y), Size = new Vector2(textWidth, font.LineHeight + 2) });
                    y += font.LineHeight + 2;
                }
                int lines = font.Wrap(body, textWidth).Count;
                float h = lines * font.LineHeight + 4;
                _tree.Content.AddChild(new SheetLine { Assets = a, Value = body, Wrap = true, Position = new Vector2(41, y), Size = new Vector2(textWidth, h) });
                y += h + 6;
            }
        }
        _tree.SetContentHeight(y);
    }
}
