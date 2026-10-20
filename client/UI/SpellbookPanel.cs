using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The spellbook (B). Class tabs; spells as icon tiles grouped by level with uses left and metamagic
/// marks; a detail pane (school, range, components, description). Click a spell to cast it, drag it
/// to the action bar (drag data {"kind":"spell","spell":id,"class":cls}) or right-click to add it to
/// the quickbar. Classes that prepare spells have a Prepare view: their slots per level and the
/// spells they can put in them ("spell-memorize"); prepared spells are ready after resting.
/// </summary>
public partial class SpellbookPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    public SpellCasting Casting = null!;
    public Action<int, int>? AddToQuickbar;

    sealed record SpellMeta(int Spell, string Name, string Icon, string Range, string School, string Components, int Innate, int Metamagic, int TargetType, bool Hostile);

    readonly Dictionary<int, SpellMeta> _info = new();
    readonly Dictionary<int, string> _descriptions = new();
    readonly Dictionary<string, Texture2D?> _icons = new();
    JsonDocument? _doc;
    string _lastJson = "";
    int _classIndex, _levelFilter = -1, _meta;
    bool _prepare;
    List<int> _levelIds = new();
    bool _modeChosen;
    (int Spell, int Class, int Meta) _selected = (-1, -1, 0);

    PillTabs _classTabs = null!, _levelTabs = null!, _modeTabs = null!;
    HBoxContainer _classInfo = null!, _metaRow = null!;
    VBoxContainer _list = null!, _detail = null!;
    ScrollContainer _scroll = null!;
    Label _hint = null!;

    static readonly (int Bit, string Name, string Short)[] MetaNames =
    {
        (1, "Empowered", "E"), (2, "Extended", "X"), (4, "Maximized", "M"), (8, "Quickened", "Q"), (16, "Silent", "Si"), (32, "Still", "St"),
    };

    public static SpellbookPanel Create(ClientSession session, SpellCasting casting)
    {
        var p = Create<SpellbookPanel>("Spellbook", new Vector2(960, 650));
        p.Session = session;
        p.Casting = casting;
        p.OffsetLeft = 40; p.OffsetTop = 70;
        return p;
    }

    public override void _Ready()
    {
        // The HUD is a CanvasLayer, which stops the root window's theme reaching its windows.
        Theme = GameTheme.Theme;
        // Class tabs (several casting classes only).
        _classTabs = new PillTabs { Visible = false };
        _classTabs.Changed += i => { _classIndex = i; _levelFilter = -1; Render(); };
        Body.AddChild(_classTabs);

        var main = Ui.HBox(16);
        main.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(main);

        var left = Ui.VBox(10);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        main.AddChild(left);
        // Class line, with the cast / prepare switch at its right.
        var classRow = Ui.HBox(10);
        left.AddChild(classRow);
        _classInfo = Ui.HBox(10);
        _classInfo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        classRow.AddChild(_classInfo);
        _modeTabs = new PillTabs();
        _modeTabs.Add("Cast", "Your spells for today: click to cast, drag to the action bar");
        _modeTabs.Add("Prepare", "Choose the spells to prepare; they are ready after you rest");
        _modeTabs.Changed += i => { _prepare = i == 1; _modeChosen = true; Render(); };
        classRow.AddChild(_modeTabs);
        _levelTabs = new PillTabs();
        _levelTabs.Changed += i => { _levelFilter = i <= 0 || i > _levelIds.Count ? -1 : _levelIds[i - 1]; Render(); };
        left.AddChild(_levelTabs);
        _metaRow = Ui.HBox(6);
        left.AddChild(_metaRow);
        var well = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        well.AddThemeStyleboxOverride("panel", Styles.Inset(12));
        left.AddChild(well);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        well.AddChild(_scroll);
        _list = Ui.VBox(14);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(_list);
        _hint = Ui.Text("", Ui.TextStyle.Caption);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.CustomMinimumSize = new Vector2(400, 0);
        left.AddChild(_hint);

        var pane = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) };
        pane.AddThemeStyleboxOverride("panel", Styles.Card(16));
        main.AddChild(pane);
        _detail = Ui.VBox(10);
        pane.AddChild(_detail);

        Session.NotifyReceived += OnNotify;
        RenderDetail();
    }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        Visible = true;
        Session.Request("spellbook");
    }

    /// <summary>Capture runs: switches to the Prepare view.</summary>
    public void CaptureOpenPicker()
    {
        _modeTabs.Select(1);
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind == "chargen-describe")
        {
            using var dd = JsonDocument.Parse(m.Json);
            var r = dd.RootElement;
            if (r.GetProperty("kind").GetString() != "spell") return;
            _descriptions[r.GetProperty("id").GetInt32()] = NwnText.Strip(r.GetProperty("text").GetString() ?? "");
            if (_selected.Spell == r.GetProperty("id").GetInt32()) RenderDetail();
            return;
        }
        if (m.Kind == "spellbook-error" && m.Subject == Session.ControlledId)
        {
            using var e = JsonDocument.Parse(m.Json);
            _hint.Text = e.RootElement.GetProperty("message").GetString();
            _hint.AddThemeColorOverride("font_color", Palette.Danger);
            return;
        }
        if (m.Kind != "spellbook" || m.Subject != Session.ControlledId) return;
        if (m.Json == _lastJson) return;
        _lastJson = m.Json;
        _doc?.Dispose();
        _doc = JsonDocument.Parse(m.Json);
        var d = _doc.RootElement;
        foreach (var i in d.GetProperty("info").EnumerateArray())
        {
            int id = i.GetProperty("spell").GetInt32();
            var meta = new SpellMeta(id, NwnText.Strip(S(i, "name")), S(i, "icon"), S(i, "range"), S(i, "school"), S(i, "components"),
                I(i, "innate", -1), I(i, "metamagic"), I(i, "targetType"), i.TryGetProperty("hostile", out var h) && h.ValueKind == JsonValueKind.True);
            _info[id] = meta;
            Casting.Info[id] = new SpellInfo(id, meta.Name, meta.Icon, meta.TargetType, meta.Hostile);
        }
        // Class tabs (kept when the same classes come back).
        var classes = d.GetProperty("classes").EnumerateArray().ToList();
        if (_classTabs.Count != classes.Count)
        {
            _classTabs.Clear();
            foreach (var c in classes) _classTabs.Add(NwnText.Strip(S(c, "name")));
            _classIndex = Math.Clamp(_classIndex, 0, Math.Max(0, classes.Count - 1));
            if (classes.Count > 0) _classTabs.Select(_classIndex, notify: false);
        }
        _classTabs.Visible = classes.Count > 1;
        Render();
        // Keep the window its designed size (content scrolls inside it).
        Size = CustomMinimumSize;
    }

    static string S(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static int I(JsonElement e, string name, int fallback = 0) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;

    Texture2D? GameIcon(string resref)
    {
        if (resref.Length == 0) return null;
        if (_icons.TryGetValue(resref, out var t)) return t;
        return _icons[resref] = ChargenModel.Icon(Session.Content, resref);
    }

    static string LevelName(int level) => level == 0 ? "Cantrips" : $"Level {level}";

    // ------------------------------------------------------------------ rendering

    void Render()
    {
        if (!IsInsideTree()) return;
        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        foreach (var c in _classInfo.GetChildren()) { _classInfo.RemoveChild(c); c.QueueFree(); }
        foreach (var c in _metaRow.GetChildren()) { _metaRow.RemoveChild(c); c.QueueFree(); }
        _hint.RemoveThemeColorOverride("font_color");
        var classes = _doc?.RootElement.GetProperty("classes").EnumerateArray().ToList() ?? new();
        if (classes.Count == 0)
        {
            _levelTabs.Visible = _modeTabs.Visible = false;
            var empty = Ui.VBox(10);
            empty.AddChild(Icons.Rect("spellbook", 56, Palette.TextMuted with { A = 0.5f }));
            var t = Ui.Text(_doc == null ? "Opening your spellbook…" : "This character casts no spells.", Ui.TextStyle.Secondary);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            empty.AddChild(t);
            _list.AddChild(Ui.Pad(empty, 0, 60, 0, 0));
            _hint.Text = "";
            return;
        }
        var cls = classes[Math.Clamp(_classIndex, 0, classes.Count - 1)];
        int classId = I(cls, "class");
        bool memorizes = cls.TryGetProperty("memorizes", out var mz) && mz.ValueKind == JsonValueKind.True;
        if (!memorizes && _prepare) _prepare = false;
        // A prepared caster with nothing prepared starts in the Prepare view.
        if (memorizes && !_modeChosen && cls.GetProperty("levels").EnumerateArray().All(l => l.GetProperty("spells").GetArrayLength() == 0)) _prepare = true;
        _modeTabs.Visible = memorizes;
        _modeTabs.Select(_prepare ? 1 : 0, notify: false);

        // Class line: icon, name and level, caster level, casting ability.
        if (GameIcon(S(cls, "icon")) is { } ci)
            _classInfo.AddChild(new TextureRect { Texture = ci, CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        _classInfo.AddChild(Ui.Text($"{NwnText.Strip(S(cls, "name"))} {I(cls, "level")}", Ui.TextStyle.Title));
        _classInfo.AddChild(Chip.Create($"Caster level {I(cls, "casterLevel")}", Palette.Magic));
        if (S(cls, "ability") is { Length: > 0 } ab) _classInfo.AddChild(Chip.Create(ab, Palette.TextSecondary, tooltip: $"Spells of this class depend on {ab}"));
        _classInfo.AddChild(Chip.Create(memorizes ? "Prepares spells" : "Spontaneous", Palette.TextSecondary,
            tooltip: memorizes ? "Prepares spells in advance; each slot casts its spell once per rest." : "Casts any known spell using a slot of its level."));

        var levels = cls.GetProperty("levels").EnumerateArray().ToList();
        _levelTabs.Clear();
        _levelTabs.Add("All");
        foreach (var l in levels) _levelTabs.Add(I(l, "level") == 0 ? "Cantrips" : Ordinal(I(l, "level")));
        _levelTabs.Visible = levels.Count > 1;
        int tabIndex = _levelFilter < 0 ? 0 : levels.FindIndex(l => I(l, "level") == _levelFilter) + 1;
        if (tabIndex <= 0) { _levelFilter = -1; tabIndex = 0; }
        _levelTabs.Select(tabIndex, notify: false);
        // Level tab indexes map to the levels list order.
        _levelIds = levels.Select(l => I(l, "level")).ToList();

        if (_prepare) RenderPrepare(cls, classId, levels);
        else RenderCast(classId, memorizes, levels);
    }

    static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    bool LevelShown(JsonElement l) => _levelFilter < 0 || I(l, "level") == _levelFilter;

    void RenderCast(int classId, bool memorizes, List<JsonElement> levels)
    {
        _hint.Text = "Click a spell to cast it  ·  drag it to the action bar  ·  right-click to add it to the quickbar";
        bool any = false;
        foreach (var l in levels.Where(LevelShown))
        {
            int level = I(l, "level"), max = I(l, "max"), left = I(l, "left");
            var spells = l.GetProperty("spells").EnumerateArray().ToList();
            if (spells.Count == 0) continue;
            any = true;
            _list.AddChild(LevelHeader(level, left, max, memorizes ? "prepared ready" : "slots left"));
            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 8);
            flow.AddThemeConstantOverride("v_separation", 8);
            _list.AddChild(flow);
            foreach (var s in spells.OrderBy(s => _info.GetValueOrDefault(I(s, "spell"))?.Name))
            {
                int spell = I(s, "spell"), ready = I(s, "ready"), total = I(s, "total"), meta = I(s, "meta");
                string badge = memorizes ? (total > 1 || ready != total ? $"{ready}/{total}" : "") : "";
                var tile = SpellTile(spell, classId, meta, ready > 0, badge, castable: true);
                flow.AddChild(tile);
            }
        }
        if (!any)
        {
            var t = Ui.Text(memorizes ? "No spells prepared. Open Prepare to choose spells, then rest." : "No spells known at this level.", Ui.TextStyle.Secondary);
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            t.CustomMinimumSize = new Vector2(360, 0);
            _list.AddChild(t);
        }
    }

    Control LevelHeader(int level, int left, int max, string what)
    {
        var row = Ui.HBox(10);
        var title = Ui.Text(LevelName(level).ToUpperInvariant(), Ui.TextStyle.Caption, Palette.AccentDim.Lightened(0.3f));
        title.AddThemeFontOverride("font", Fonts.UiBold);
        row.AddChild(title);
        row.AddChild(new Divider { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, Fade = Divider.FadeMode.End, Color = Palette.LineStrong });
        if (max > 0)
        {
            // Pips: one per slot, lit while unused.
            var pips = Ui.HBox(3);
            pips.TooltipText = $"{left} of {max} {what}";
            pips.MouseFilter = MouseFilterEnum.Stop;
            for (int i = 0; i < Math.Min(max, 12); i++)
            {
                var pip = new Panel { CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
                pip.AddThemeStyleboxOverride("panel", Styles.Box(i < left ? Palette.Magic : new Color(1, 1, 1, 0.08f), 4, i < left ? null : Palette.LineStrong, 1, 0));
                pips.AddChild(pip);
            }
            row.AddChild(pips);
            row.AddChild(Ui.Text($"{left}/{max}", Ui.TextStyle.Caption, left > 0 ? Palette.Magic : Palette.TextMuted));
        }
        return row;
    }

    /// <summary>A spell as an icon tile with its name; badges for uses and metamagic. Hover shows it in the detail pane.</summary>
    Control SpellTile(int spell, int classId, int meta, bool ready, string badge, bool castable, Action? onClick = null, string? tooltip = null)
    {
        var info = _info.GetValueOrDefault(spell);
        var tile = new SpellTileControl { Name = "SpellTile", CustomMinimumSize = new Vector2(84, 98), MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        tile.Drag = castable ? new Godot.Collections.Dictionary { ["kind"] = "spell", ["spell"] = spell, ["class"] = classId, ["meta"] = meta } : null;
        tile.DragIcon = GameIcon(info?.Icon ?? "");
        bool selected = _selected.Spell == spell && _selected.Meta == meta;
        var normal = Styles.Box(selected ? new Color(0.2f, 0.17f, 0.11f) : Colors.Transparent, Palette.RadiusSmall, selected ? Palette.AccentDim : null, 1, 4, 5);
        var hover = Styles.Box(Palette.Bg3, Palette.RadiusSmall, Palette.AccentDim, 1, 4, 5);
        tile.AddThemeStyleboxOverride("panel", normal);
        tile.MouseEntered += () => { tile.AddThemeStyleboxOverride("panel", hover); Select(spell, classId, meta); };
        tile.MouseExited += () => tile.AddThemeStyleboxOverride("panel", normal);
        var v = Ui.VBox(4);
        v.MouseFilter = MouseFilterEnum.Ignore;
        tile.AddChild(v);
        var iconBox = new Control { CustomMinimumSize = new Vector2(52, 52), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        var frame = new Panel { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.5f), 6, meta != 0 ? Palette.Magic : ready ? Palette.LineStrong : Palette.Line, meta != 0 ? 2 : 1, 0));
        iconBox.AddChild(frame);
        var icon = new TextureRect
        {
            Texture = GameIcon(info?.Icon ?? ""), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 3, OffsetTop = 3, OffsetRight = -3, OffsetBottom = -3, MouseFilter = MouseFilterEnum.Ignore,
            Modulate = ready ? Colors.White : new Color(0.45f, 0.45f, 0.5f, 0.8f),
        };
        iconBox.AddChild(icon);
        if (badge.Length > 0)
        {
            var b = Chip.Create(badge, ready ? Palette.Magic : Palette.TextMuted, solid: ready, fontSize: 10);
            b.AnchorLeft = b.AnchorRight = 1; b.AnchorTop = b.AnchorBottom = 1;
            b.GrowHorizontal = GrowDirection.Begin; b.GrowVertical = GrowDirection.Begin;
            b.OffsetRight = 4; b.OffsetBottom = 4;
            iconBox.AddChild(b);
        }
        if (meta != 0)
        {
            var m = Chip.Create(string.Join("", MetaNames.Where(x => (meta & x.Bit) != 0).Select(x => x.Short)), Palette.Magic, solid: true, fontSize: 9);
            m.OffsetLeft = -4; m.OffsetTop = -4;
            iconBox.AddChild(m);
        }
        v.AddChild(iconBox);
        var name = Ui.Text(info?.Name ?? $"Spell {spell}", Ui.TextStyle.Small, ready ? Palette.Text : Palette.TextMuted, 12);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.CustomMinimumSize = new Vector2(76, 0);
        name.MaxLinesVisible = 2;
        v.AddChild(name);
        var metaText = meta != 0 ? $" ({string.Join(", ", MetaNames.Where(x => (meta & x.Bit) != 0).Select(x => x.Name))})" : "";
        string footer = tooltip ?? (castable ? (ready ? "Click to cast  ·  drag to the action bar  ·  right-click: quickbar" : "No uses left until you rest.") : "");
        string sub = info == null ? "" : (info.Innate == 0 ? "Cantrip" : info.Innate > 0 ? $"Level {info.Innate}" : "") + (info.School.Length > 0 ? $" {info.School.ToLowerInvariant()}" : "") + metaText;
        tile.Tooltip = () => Tip.Card(info?.Name ?? $"Spell {spell}", sub, footer: footer, icon: GameIcon(info?.Icon ?? ""), width: 280);
        tile.Clicked = onClick ?? (() => { if (castable && ready) Cast(spell, classId, meta); });
        if (castable) tile.RightClicked = () => AddToQuickbar?.Invoke(spell, classId);
        return tile;
    }

    void Cast(int spell, int classId, int meta)
    {
        Casting.Cast(spell, classId, meta);
        // Uses left change once the spell goes off.
        GetTree().CreateTimer(2.5).Timeout += () => { if (Visible) Session.Request("spellbook"); };
    }

    void Select(int spell, int classId, int meta)
    {
        if (_selected == (spell, classId, meta)) return;
        _selected = (spell, classId, meta);
        if (!_descriptions.ContainsKey(spell)) Session.Request("chargen-describe", new { kind = "spell", id = spell });
        RenderDetail();
    }

    // ------------------------------------------------------------------ prepare view

    void RenderPrepare(JsonElement cls, int classId, List<JsonElement> levels)
    {
        var known = _doc!.RootElement.TryGetProperty("metamagic", out var mm) ? mm.EnumerateArray().ToList() : new();
        if (known.Count > 0)
        {
            _metaRow.AddChild(Ui.Text("Metamagic", Ui.TextStyle.Caption));
            var none = MetaToggle("None", 0, "Prepare spells normally");
            _metaRow.AddChild(none);
            foreach (var m in known)
                _metaRow.AddChild(MetaToggle(S(m, "name"), I(m, "meta"), $"Prepare with {S(m, "name")}: uses a slot {I(m, "levels")} level{(I(m, "levels") == 1 ? "" : "s")} higher", GameIcon(S(m, "icon"))));
        }
        int cost = _meta == 0 ? 0 : known.Where(m => I(m, "meta") == _meta).Select(m => I(m, "levels")).FirstOrDefault();
        _hint.Text = "Click a spell to prepare it in the first free slot of its level  ·  click a prepared slot to empty it  ·  prepared spells are ready after resting";
        bool spellbook = cls.TryGetProperty("spellbook", out var sb) && sb.ValueKind == JsonValueKind.True;
        foreach (var l in levels.Where(LevelShown))
        {
            int level = I(l, "level");
            var slots = l.GetProperty("slots").EnumerateArray().ToList();
            if (slots.Count == 0) continue;
            int filled = slots.Count(s => I(s, "spell") >= 0);
            _list.AddChild(LevelHeader(level, filled, slots.Count, "slots filled"));
            var slotRow = new HFlowContainer();
            slotRow.AddThemeConstantOverride("h_separation", 6);
            slotRow.AddThemeConstantOverride("v_separation", 6);
            _list.AddChild(slotRow);
            foreach (var s in slots)
            {
                int index = I(s, "index"), spell = I(s, "spell"), meta = I(s, "meta");
                bool ready = s.TryGetProperty("ready", out var r) && r.ValueKind == JsonValueKind.True;
                bool domain = s.TryGetProperty("domain", out var dm) && dm.ValueKind == JsonValueKind.True;
                slotRow.AddChild(SlotTile(classId, level, index, spell, meta, ready, domain));
            }
            // What can go in this level's slots (with the chosen metamagic, spells from lower levels).
            int baseLevel = level - cost;
            var source = levels.FirstOrDefault(x => I(x, "level") == baseLevel);
            var available = source.ValueKind == JsonValueKind.Object ? source.GetProperty("available").EnumerateArray().Select(x => x.GetInt32()).ToList() : new List<int>();
            if (_meta != 0) available = available.Where(sp => (_info.GetValueOrDefault(sp)?.Metamagic & _meta) != 0).ToList();
            var cap = Ui.Text(available.Count == 0 ? (baseLevel < 0 ? "Too low a level for this metamagic." : spellbook ? "No spells of this level in your spellbook." : "Nothing to prepare at this level.")
                : $"{(spellbook ? "From your spellbook" : "Available")}{(_meta != 0 ? $" · level {baseLevel} spells, {MetaNames.First(x => x.Bit == _meta).Name.ToLowerInvariant()}" : "")}", Ui.TextStyle.Caption);
            _list.AddChild(cap);
            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 8);
            flow.AddThemeConstantOverride("v_separation", 8);
            _list.AddChild(flow);
            int free = slots.Where(s => I(s, "spell") < 0 && !(s.TryGetProperty("domain", out var dd) && dd.ValueKind == JsonValueKind.True)).Select(s => I(s, "index")).DefaultIfEmpty(-1).First();
            foreach (var sp in available.OrderBy(sp => _info.GetValueOrDefault(sp)?.Name))
            {
                int spell = sp, lvl = level, slot = free;
                flow.AddChild(SpellTile(sp, classId, _meta, ready: free >= 0, badge: "", castable: false,
                    onClick: () => { if (slot >= 0) Memorize(classId, lvl, slot, spell, _meta); },
                    tooltip: free >= 0 ? "Click to prepare it in the next free slot." : "Every slot of this level is full: click a prepared slot to empty it."));
            }
        }
    }

    Control MetaToggle(string name, int meta, string tooltip, Texture2D? icon = null)
    {
        var b = new Button { Text = name, ToggleMode = true, ButtonPressed = _meta == meta, FocusMode = FocusModeEnum.None, TooltipText = tooltip, Icon = icon, ExpandIcon = true };
        b.AddThemeConstantOverride("icon_max_width", 18);
        b.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        var on = Styles.Box(Palette.Magic with { A = 0.18f }, 12, Palette.Magic, 1, 10, 4);
        b.AddThemeStyleboxOverride("normal", Styles.Box(new Color(0, 0, 0, 0.25f), 12, Palette.LineStrong, 1, 10, 4));
        b.AddThemeStyleboxOverride("hover", Styles.Box(Palette.Bg3, 12, Palette.AccentDim, 1, 10, 4));
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.Pressed += () => { _meta = meta; Render(); };
        return b;
    }

    Control SlotTile(int classId, int level, int index, int spell, int meta, bool ready, bool domain)
    {
        var info = spell >= 0 ? _info.GetValueOrDefault(spell) : null;
        var b = new TipButton { CustomMinimumSize = new Vector2(52, 52), FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        var border = domain ? Palette.Plot : meta != 0 ? Palette.Magic : spell >= 0 ? Palette.LineStrong : Palette.Line;
        var normal = Styles.Box(spell >= 0 ? new Color(0, 0, 0, 0.5f) : new Color(1, 1, 1, 0.03f), 6, border, 1, 3);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", Styles.Box(Palette.Bg3, 6, Palette.Accent, 1, 3));
        b.AddThemeStyleboxOverride("pressed", Styles.Box(Palette.Bg3, 6, Palette.Accent, 1, 3));
        if (info != null)
        {
            b.AddChild(new TextureRect
            {
                Texture = GameIcon(info.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 4, OffsetTop = 4, OffsetRight = -4, OffsetBottom = -4, MouseFilter = MouseFilterEnum.Ignore,
                Modulate = ready ? Colors.White : new Color(0.6f, 0.6f, 0.65f, 0.75f),
            });
            if (!ready)
            {
                var wait = Icons.Rect("hourglass", 14, Palette.AccentBright);
                wait.AnchorLeft = wait.AnchorRight = 1; wait.AnchorTop = wait.AnchorBottom = 1;
                wait.OffsetLeft = -17; wait.OffsetTop = -17; wait.OffsetRight = -2; wait.OffsetBottom = -2;
                b.AddChild(wait);
            }
            b.Pressed += () => Memorize(classId, level, index, -1, 0);
            b.MouseEntered += () => Select(spell, classId, meta);
            var metaText = meta != 0 ? " · " + string.Join(", ", MetaNames.Where(x => (meta & x.Bit) != 0).Select(x => x.Name)) : "";
            b.Tooltip = () => Tip.Card(info.Name, (domain ? "Domain slot" : $"{LevelName(level)} slot") + metaText,
                footer: (ready ? "Ready to cast." : "Prepared: ready after you rest.") + "  Click to empty this slot.", icon: GameIcon(info.Icon), width: 260);
        }
        else
        {
            var plus = Ui.Text("+", Ui.TextStyle.Title, Palette.TextMuted, 22);
            plus.HorizontalAlignment = HorizontalAlignment.Center;
            plus.VerticalAlignment = VerticalAlignment.Center;
            plus.AnchorRight = plus.AnchorBottom = 1;
            b.AddChild(plus);
            b.TooltipText = domain ? "Empty domain slot: prepare one of your domains' spells" : $"Empty {LevelName(level).ToLowerInvariant()} slot: click a spell below to prepare it";
        }
        return b;
    }

    void Memorize(int classId, int level, int slot, int spell, int meta)
    {
        Session.Request("spell-memorize", new { @class = classId, level, slot, spell, meta });
    }

    // ------------------------------------------------------------------ detail pane

    void RenderDetail()
    {
        if (_detail == null) return;
        foreach (var c in _detail.GetChildren()) { _detail.RemoveChild(c); c.QueueFree(); }
        var info = _selected.Spell >= 0 ? _info.GetValueOrDefault(_selected.Spell) : null;
        if (info == null)
        {
            _detail.AddChild(Ui.Spacer());
            var icon = Icons.Rect("spellbook", 64, Palette.AccentDim with { A = 0.6f });
            icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _detail.AddChild(icon);
            var t = Ui.Text("Point at a spell to read about it.", Ui.TextStyle.ReadingItalic);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            t.CustomMinimumSize = new Vector2(260, 0);
            _detail.AddChild(t);
            return;
        }
        var head = Ui.HBox(12);
        _detail.AddChild(head);
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.5f), 8, Palette.AccentDim, 1, 3));
        frame.AddChild(new TextureRect { Texture = GameIcon(info.Icon), CustomMinimumSize = new Vector2(56, 56), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        head.AddChild(frame);
        var names = Ui.VBox(4);
        names.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        names.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var title = Ui.Text(info.Name, Ui.TextStyle.Title);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.CustomMinimumSize = new Vector2(190, 0);
        names.AddChild(title);
        var chips = Ui.HBox(6);
        if (info.School.Length > 0) chips.AddChild(Chip.Create(info.School, Palette.Magic));
        if (info.Innate >= 0) chips.AddChild(Chip.Create(info.Innate == 0 ? "Cantrip" : $"Level {info.Innate}", Palette.TextSecondary));
        if (info.Hostile) chips.AddChild(Chip.Create("Hostile", Palette.Hostile));
        names.AddChild(chips);
        head.AddChild(names);
        _detail.AddChild(new Divider { Color = Palette.AccentDim, CustomMinimumSize = new Vector2(0, 1) });

        var facts = Ui.VBox(3);
        _detail.AddChild(facts);
        facts.AddChild(Ui.Stat("Range", info.Range.ToUpperInvariant() switch
        {
            "P" => "Personal",
            "T" => "Touch",
            "S" => "Short (8 m)",
            "M" => "Medium (20 m)",
            "L" => "Long (40 m)",
            "I" => "Unlimited",
            _ => info.Range,
        }));
        facts.AddChild(Ui.Stat("Components", info.Components.Length == 0 ? "None" : string.Join(", ", info.Components.Select(ch => ch == 'V' ? "Verbal" : ch == 'S' ? "Somatic" : ch.ToString()))));
        facts.AddChild(Ui.Stat("Target", Targets(info.TargetType)));
        var metas = MetaNames.Where(x => (info.Metamagic & x.Bit) != 0).Select(x => x.Name).ToList();
        if (metas.Count > 0) facts.AddChild(Ui.Stat("Metamagic", string.Join(", ", metas)));
        if (_selected.Meta != 0) facts.AddChild(Ui.Stat("Prepared as", string.Join(", ", MetaNames.Where(x => (_selected.Meta & x.Bit) != 0).Select(x => x.Name)), Palette.Magic));

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _detail.AddChild(scroll);
        var text = _descriptions.TryGetValue(info.Spell, out var desc) ? desc : "…";
        var body = Ui.Text(text, Ui.TextStyle.Reading, null, 17);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.CustomMinimumSize = new Vector2(250, 0);
        scroll.AddChild(body);

        if (!_prepare)
        {
            var actions = Ui.HBox(8);
            _detail.AddChild(actions);
            var sel = _selected;
            var cast = Ui.Button("Cast", () => Cast(sel.Spell, sel.Class, sel.Meta), "PrimaryButton", "magic");
            cast.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cast.TooltipText = "Cast this spell (pick a target if it needs one)";
            actions.AddChild(cast);
            var quick = IconButton.Create("menu", "Add to the quickbar", 36, 18, () => AddToQuickbar?.Invoke(sel.Spell, sel.Class));
            actions.AddChild(quick);
        }
    }

    static string Targets(int t)
    {
        if (t is 0 or 1) return "Yourself";
        var parts = new List<string>();
        if ((t & 1) != 0) parts.Add("self");
        if ((t & 2) != 0) parts.Add("creature");
        if ((t & 4) != 0) parts.Add("area");
        if ((t & 8) != 0) parts.Add("item");
        if ((t & 16) != 0) parts.Add("door");
        if ((t & 32) != 0) parts.Add("placeable");
        if ((t & 64) != 0) parts.Add("trigger");
        var s = string.Join(", ", parts);
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : "—";
    }

    /// <summary>A spell tile: click (on release, when not dragged), right-click, and drag with a spell payload.</summary>
    partial class SpellTileControl : TipPanel
    {
        public Godot.Collections.Dictionary? Drag;
        public Texture2D? DragIcon;
        public Action? Clicked, RightClicked;
        bool _down;

        public override void _GuiInput(InputEvent e)
        {
            if (e is not InputEventMouseButton mb) return;
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) _down = true;
                else if (_down) { _down = false; if (!GetViewport().GuiIsDragging()) Clicked?.Invoke(); }
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed) { RightClicked?.Invoke(); AcceptEvent(); }
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            _down = false;
            if (Drag == null) return default;
            var preview = new TextureRect { Texture = DragIcon, CustomMinimumSize = new Vector2(44, 44), Size = new Vector2(44, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = new Color(1, 1, 1, 0.85f), Position = new Vector2(-22, -22) };
            var holder = new Control();
            holder.AddChild(preview);
            SetDragPreview(holder);
            return Drag;
        }
    }
}
