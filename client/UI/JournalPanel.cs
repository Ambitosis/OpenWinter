using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The quest journal (J): quests on the left (In progress / Completed, most recently updated
/// first, with a marker on ones updated since you last read them), the selected quest on the
/// right in the reading face: its current entry, then the earlier entries. Entries arrive as
/// "journal" notifies whenever a script updates them; opening the window asks for the full list.
/// </summary>
public partial class JournalPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    /// <summary>Raised for the journal line ("Your journal has been updated. Quest") in the chat log.</summary>
    public Action<string>? Updated;
    /// <summary>
    /// An update arrived since the journal was last opened: the original's journal button pulses (its "encouraged"
    /// animation, a 1 s glow, seen 0.3 to 4.5 s after an update in scene journalpc) until then.
    /// </summary>
    public bool Attention { get; private set; }
    public event Action? AttentionChanged;
    internal void SetAttention(bool on)
    {
        if (Attention == on) return;
        Attention = on;
        AttentionChanged?.Invoke();
    }

    internal sealed record Entry(int State, string Text, int Day, int Time);
    internal sealed record Quest(string Tag, string Name, string Text, bool End, int Priority, int Xp, int Day, int Time, long Order, IReadOnlyList<Entry> History);

    readonly Dictionary<string, Quest> _quests = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _unread = new(StringComparer.OrdinalIgnoreCase);
    long _order;
    bool _resetting, _completedTab;
    Button _activeTab = null!, _doneTab = null!;
    VBoxContainer _list = null!, _detail = null!;
    ScrollContainer _detailScroll = null!;
    Control _emptyState = null!;
    HBoxContainer _split = null!;
    string? _selected, _reading;

    public static JournalPanel Create(ClientSession session)
    {
        var p = Create<JournalPanel>("Journal", new Vector2(940, 620));
        p.Session = session;
        p.AnchorLeft = p.AnchorRight = 0.5f;
        p.AnchorTop = p.AnchorBottom = 0.5f;
        p.OffsetLeft = -470; p.OffsetRight = 470; p.OffsetTop = -310; p.OffsetBottom = 310;
        return p;
    }

    public override void _Ready()
    {
        // Tabs live in the title bar as a segmented control.
        var seg = new PanelContainer();
        seg.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.3f), Palette.RadiusSmall + 2, Palette.Line, 1, 3, 3));
        var segRow = Ui.HBox(2);
        seg.AddChild(segRow);
        _activeTab = SegmentButton("In progress", () => SetTab(false));
        _doneTab = SegmentButton("Completed", () => SetTab(true));
        segRow.AddChild(_activeTab);
        segRow.AddChild(_doneTab);
        HeaderExtras.AddChild(seg);
        HeaderExtras.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });

        _split = Ui.HBox(Palette.Space4);
        _split.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(_split);

        // Quest list.
        var listFrame = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) };
        listFrame.AddThemeStyleboxOverride("panel", Styles.Inset(6));
        var listScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = Ui.VBox(2);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        listScroll.AddChild(_list);
        listFrame.AddChild(listScroll);
        _split.AddChild(listFrame);

        // Detail.
        _detailScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _detail = Ui.VBox(0);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _detailScroll.AddChild(Ui.Pad(_detail, 8, 4, 18, 12));
        _detailScroll.GetChild<Control>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _split.AddChild(_detailScroll);

        _emptyState = EmptyState();
        _emptyState.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(_emptyState);

        Session.NotifyReceived += OnNotify;
        Closed += () =>
        {
            if (_reading != null) _unread.Remove(_reading);
            _reading = null;
            Session.Request("journal-closed");
        };
        Fill();
    }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        Visible = true;
        SetAttention(false);
        Session.Request("journal");
        // Open on the quest that changed most recently (unread first).
        var latest = _quests.Values.OrderByDescending(q => _unread.Contains(q.Tag)).ThenByDescending(q => q.Order).FirstOrDefault();
        if (latest != null) { _completedTab = latest.End; _selected = latest.Tag; }
        Fill();
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Subject != Session.ControlledId) return;
        if (m.Kind == "journal-reset") { _resetting = true; _seen.Clear(); return; }
        if (m.Kind != "journal") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        string tag = d.GetProperty("tag").GetString() ?? "";
        // A script or conversation changed it (not the resend when the window opens): the original prints
        // "Your journal has been updated. <quest>" (dialog.tlk 6395) in yellow and flashes the journal button, for a
        // new, next, unknown or lowered (override) entry and for a removal alike (scene journalpc).
        bool update = d.TryGetProperty("update", out var u) && u.GetBoolean();
        if (update)
        {
            string name = d.TryGetProperty("name", out var nm) ? NwnText.Strip(nm.GetString() ?? tag) : tag;
            string line = Session.Content?.Resources.TlkString(6395) is { Length: > 0 } t ? t : "Your journal has been updated.";
            Updated?.Invoke($"{line} {name}");
            SetAttention(true);
        }
        if (d.GetProperty("removed").GetBoolean()) { _quests.Remove(tag); _unread.Remove(tag); }
        else
        {
            var history = new List<Entry>();
            if (d.TryGetProperty("history", out var h))
                foreach (var e in h.EnumerateArray())
                    history.Add(new Entry(e.GetProperty("state").GetInt32(), e.GetProperty("text").GetString() ?? "", e.GetProperty("day").GetInt32(), e.GetProperty("time").GetInt32()));
            int day = d.GetProperty("day").GetInt32(), time = d.GetProperty("time").GetInt32();
            _quests.TryGetValue(tag, out var before);
            bool isNew = before == null || before.Text != (d.GetProperty("text").GetString() ?? "");
            // A full resend (the window opened) keeps each quest's place; a script update moves it to the top.
            long order = !_resetting || before == null ? ++_order : before.Order;
            var q = new Quest(tag, d.GetProperty("name").GetString() ?? tag, d.GetProperty("text").GetString() ?? "", d.GetProperty("end").GetBoolean(),
                d.GetProperty("priority").GetInt32(), d.GetProperty("xp").GetInt32(), day, time, order, history);
            _quests[tag] = q;
            if (_resetting) _seen.Add(tag);
            else if (isNew || update) _unread.Add(tag);
            _selected ??= tag;
        }
        QueueFill();
    }

    readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    bool _fillQueued;

    void QueueFill()
    {
        if (_fillQueued) return;
        _fillQueued = true;
        Callable.From(() => { _fillQueued = false; Fill(); QuestsChanged?.Invoke(); }).CallDeferred();
    }

    /// <summary>The quests as the server last sent them (the classic journal reads these).</summary>
    internal IEnumerable<Quest> Quests => _quests.Values;
    /// <summary>Raised after the quests changed.</summary>
    internal event Action? QuestsChanged;
    internal bool IsUnread(string tag) => _unread.Contains(tag);
    internal void MarkRead(string tag) => _unread.Remove(tag);

    public override void _Process(double delta)
    {
        if (!_resetting) return;
        // The full resend is over: drop quests it no longer had.
        _resetting = false;
        foreach (var gone in _quests.Keys.Where(k => !_seen.Contains(k)).ToList()) { _quests.Remove(gone); _unread.Remove(gone); }
        QueueFill();
    }

    /// <summary>Shows the In progress (false) or Completed (true) quests.</summary>
    public void SetTab(bool completed)
    {
        _completedTab = completed;
        _selected = null;
        Fill();
    }

    void Fill()
    {
        if (_list == null) return;
        int active = _quests.Values.Count(q => !q.End), done = _quests.Count - active;
        _activeTab.Text = active > 0 ? $"In progress  {active}" : "In progress";
        _doneTab.Text = done > 0 ? $"Completed  {done}" : "Completed";
        _activeTab.SetPressedNoSignal(!_completedTab);
        _doneTab.SetPressedNoSignal(_completedTab);
        _split.Visible = _quests.Count > 0;
        _emptyState.Visible = _quests.Count == 0;

        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        var shown = _quests.Values.Where(q => q.End == _completedTab).OrderByDescending(q => q.Order).ToList();
        if (_selected == null || !shown.Any(q => q.Tag.Equals(_selected, StringComparison.OrdinalIgnoreCase)))
            _selected = shown.FirstOrDefault()?.Tag;
        foreach (var q in shown)
        {
            var row = new QuestRow(q, q.Tag.Equals(_selected, StringComparison.OrdinalIgnoreCase), _unread.Contains(q.Tag));
            row.Picked = () => { _selected = q.Tag; Fill(); };
            _list.AddChild(row);
        }
        if (shown.Count == 0)
        {
            var none = Ui.Text(_completedTab ? "No completed quests yet." : "Nothing in progress.", Ui.TextStyle.Small, Palette.TextMuted);
            none.HorizontalAlignment = HorizontalAlignment.Center;
            _list.AddChild(Ui.Pad(none, 8, 18, 8, 8));
        }
        ShowQuest(_selected);
    }

    void ShowQuest(string? tag)
    {
        foreach (var c in _detail.GetChildren()) { _detail.RemoveChild(c); c.QueueFree(); }
        if (tag == null || !_quests.TryGetValue(tag, out var q)) return;
        if (_reading != null && !_reading.Equals(tag, StringComparison.OrdinalIgnoreCase)) { _unread.Remove(_reading); _reading = null; }
        bool unread = _unread.Contains(q.Tag);
        // Read once you move on to another quest or close the window.
        if (_reading != null && !_reading.Equals(q.Tag, StringComparison.OrdinalIgnoreCase)) _unread.Remove(_reading);
        if (Visible) _reading = q.Tag;

        var title = Ui.Text(NwnText.Strip(q.Name), Ui.TextStyle.Heading, Palette.AccentBright, 26);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detail.AddChild(title);

        var chips = Ui.HBox(Palette.Space2);
        chips.AddChild(q.End ? Chip("Completed", Palette.Success) : Chip("In progress", Palette.Info));
        if (unread) chips.AddChild(Chip(q.End ? "Just completed" : "Updated", Palette.AccentBright, filled: true));
        if (PriorityName(q.Priority) is { } pr) chips.AddChild(Chip(pr + " priority", q.Priority <= 1 ? Palette.Warning : Palette.TextSecondary));
        if (q.Xp > 0) chips.AddChild(Chip($"{q.Xp:N0} XP", Palette.Xp));
        _detail.AddChild(Ui.Pad(chips, 0, 8, 0, 14));
        _detail.AddChild(new Divider { Fade = Divider.FadeMode.End, Color = Palette.AccentDim, CustomMinimumSize = new Vector2(0, 1) });

        var when = Ui.Text(Stamp(q.Day, q.Time).ToUpperInvariant(), Ui.TextStyle.Caption, Palette.AccentDim.Lightened(0.2f));
        when.AddThemeFontOverride("font", Fonts.UiBold);
        _detail.AddChild(Ui.Pad(when, 0, 16, 0, 6));
        _detail.AddChild(Prose(q.Text, 20, Palette.Text));

        if (q.History.Count > 0)
        {
            _detail.AddChild(Ui.Pad(Ui.Section("Earlier entries"), 0, 26, 0, 10));
            foreach (var e in q.History) _detail.AddChild(HistoryEntry(e));
        }
        _detailScroll.ScrollVertical = 0;
    }

    static Control HistoryEntry(Entry e)
    {
        var row = Ui.HBox(Palette.Space3);
        var rail = new Control { CustomMinimumSize = new Vector2(12, 0), MouseFilter = MouseFilterEnum.Ignore };
        rail.Draw += () =>
        {
            rail.DrawLine(new Vector2(6, 14), new Vector2(6, rail.Size.Y + 6), Palette.LineStrong, 1);
            rail.DrawCircle(new Vector2(6, 9), 3.5f, Palette.Bg1);
            rail.DrawArc(new Vector2(6, 9), 3.5f, 0, MathF.Tau, 16, Palette.AccentDim, 1.2f, true);
        };
        row.AddChild(rail);
        var col = Ui.VBox(2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var when = Ui.Text(Stamp(e.Day, e.Time), Ui.TextStyle.Caption);
        col.AddChild(when);
        col.AddChild(Prose(e.Text, 17, Palette.TextSecondary));
        var pad = Ui.Pad(col, 0, 0, 0, 14);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(pad);
        return row;
    }

    static RichTextLabel Prose(string text, int size, Color color)
    {
        var l = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SelectionEnabled = true,
        };
        l.AddThemeFontOverride("normal_font", Fonts.Reading);
        l.AddThemeFontOverride("italics_font", Fonts.ReadingItalic);
        l.AddThemeFontOverride("bold_font", Fonts.ReadingBold);
        foreach (var f in new[] { "normal_font_size", "italics_font_size", "bold_font_size" }) l.AddThemeFontSizeOverride(f, size);
        l.AddThemeColorOverride("default_color", color);
        l.AddThemeConstantOverride("line_separation", 4);
        l.Text = NwnText.ToBbcode(text.Trim().Replace("\r\n", "\n"));
        return l;
    }

    /// <summary>NWN's journal priorities: 0 highest … 4 lowest.</summary>
    internal static string? PriorityName(int p) => p switch { 0 => "Highest", 1 => "High", 2 => "Medium", 3 => "Low", 4 => "Lowest", _ => null };

    /// <summary>Game date of an entry: the server stamps day = (year·12 + month−1)·28 + day−1 and time in ms.</summary>
    internal static string Stamp(int day, int time)
    {
        if (day <= 0 && time <= 0) return "";
        int year = day / (12 * 28), month = day / 28 % 12 + 1, d = day % 28 + 1;
        int minutes = time / 60000;
        return $"Day {d}, month {month}, {year}  ·  {minutes / 60:00}:{minutes % 60:00}";
    }

    static Control Chip(string text, Color color, bool filled = false)
    {
        var p = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", Styles.Box(filled ? color with { A = 0.9f } : color with { A = 0.12f }, 10, color with { A = filled ? 1 : 0.45f }, 1, 9, 2));
        var l = Ui.Text(text.ToUpperInvariant(), Ui.TextStyle.Caption, filled ? Palette.TextOnAccent : color.Lightened(0.15f), 11);
        l.AddThemeFontOverride("font", Fonts.UiBold);
        p.AddChild(l);
        return p;
    }

    static Button SegmentButton(string text, Action pressed)
    {
        var b = new Button { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(118, 0) };
        b.AddThemeFontOverride("font", Fonts.UiBold);
        b.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        b.AddThemeStyleboxOverride("normal", Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 12, 5));
        b.AddThemeStyleboxOverride("hover", Styles.Box(new Color(1, 1, 1, 0.06f), Palette.RadiusSmall, null, 0, 12, 5));
        var on = Styles.Box(Palette.Accent with { A = 0.18f }, Palette.RadiusSmall, Palette.Accent with { A = 0.6f }, 1, 12, 5);
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.AddThemeColorOverride("font_color", Palette.TextSecondary);
        b.AddThemeColorOverride("font_hover_color", Palette.Text);
        b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("font_hover_pressed_color", Palette.AccentBright);
        b.Pressed += pressed;
        return b;
    }

    static Control EmptyState()
    {
        var box = Ui.VBox(Palette.Space3);
        box.Alignment = BoxContainer.AlignmentMode.Center;
        var icon = Icons.Rect("journal", 72, Palette.AccentDim with { A = 0.5f });
        icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        box.AddChild(icon);
        var title = Ui.Text("Your journal is empty", Ui.TextStyle.Title);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        var sub = Ui.Text("The quests you take on will be recorded here.", Ui.TextStyle.ReadingItalic);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(sub);
        return box;
    }

    /// <summary>A quest in the list: name, when it was last updated, and an unread marker.</summary>
    partial class QuestRow : PanelContainer
    {
        public Action? Picked;
        readonly Quest _q;
        readonly bool _selected;
        readonly bool _unread;
        bool _hover;

        public QuestRow(Quest q, bool selected, bool unread) { _q = q; _selected = selected; _unread = unread; }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.PointingHand;
            var col = Ui.VBox(2);
            col.MouseFilter = MouseFilterEnum.Ignore;
            var name = Ui.Text(NwnText.Strip(_q.Name), Ui.TextStyle.Body, _selected ? Palette.AccentBright : Palette.Text, Palette.FontBody);
            name.AddThemeFontOverride("font", _selected ? Fonts.UiBold : Fonts.UiMedium);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            col.AddChild(name);
            var meta = new List<string>();
            if (_q.End) meta.Add("Completed");
            else if (PriorityName(_q.Priority) is { } p && _q.Priority <= 1) meta.Add(p + " priority");
            var stamp = Stamp(_q.Day, _q.Time);
            if (stamp.Length > 0) meta.Add(stamp.Split("  ·  ")[0]);
            if (meta.Count > 0) col.AddChild(Ui.Text(string.Join("  ·  ", meta), Ui.TextStyle.Caption));
            AddChild(Ui.Pad(col, 14, 9, 24, 9));
            Restyle();
            MouseEntered += () => { _hover = true; Restyle(); };
            MouseExited += () => { _hover = false; Restyle(); };
        }

        void Restyle()
        {
            var bg = _selected ? Palette.Bg3 : _hover ? Palette.Bg2 : Colors.Transparent;
            AddThemeStyleboxOverride("panel", Styles.Box(bg, Palette.RadiusSmall, _selected ? Palette.LineStrong : null, 1, 0, 0));
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_selected) DrawRect(new Rect2(0, 6, 3, Size.Y - 12), Palette.Accent);
            if (_unread)
            {
                var at = new Vector2(Size.X - 13, 17);
                DrawCircle(at, 7, Palette.AccentGlow);
                DrawCircle(at, 3.5f, Palette.AccentBright);
            }
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { Picked?.Invoke(); AcceptEvent(); }
        }
    }
}
