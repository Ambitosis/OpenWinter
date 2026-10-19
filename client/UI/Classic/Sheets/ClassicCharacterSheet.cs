using System.Text.Json;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// The original's character record (C), top-left: pnl_chrsht (title, tabs, resize bar) with a page under its
/// "pages" node. The character page (pnl_chrsht_char) shows the portrait, race / alignment and classes, the six
/// abilities with icons, scores and modifiers, AC and hit points, experience (or the pulsing Level Up button), and
/// a scrolling combat summary; the Skills and Feats pages (pnl_chrsht_skils) show a description over a list.
/// Filled from the server's "character-sheet" notify, as the modern sheet is.
/// </summary>
public partial class ClassicCharacterSheet : ClassicWindow
{
    JsonDocument? _last;
    int _tab;
    readonly GuiButton[] _tabs = new GuiButton[3];
    Control _pages = null!;
    Control _charPage = null!, _listPage = null!;
    PortraitView _charView = null!;
    GuiLabel _title = null!;
    readonly Dictionary<string, GuiLabel> _labels = new();
    GuiButton _levelUp = null!;
    SheetScroll _summary = null!, _list = null!, _desc = null!;
    SheetLine _descText = null!;
    int _selected = -1;

    static readonly string[] Abilities = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
    static readonly string[] AbilityNodes = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
    static readonly uint[] AbilityNames = { 135, 133, 132, 134, 136, 131 };
    static readonly string[] AbilityIcons = { "gui_chrsht_str", "gui_chrsht_dex", "gui_chrsht_con", "gui_chrsht_int", "gui_chrsht_wis", "gui_chrsht_cha" };

    /// <summary>The level-up button was pressed.</summary>
    public Action? LevelUpRequested;

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromModel(a, "pnl_chrsht");
        var frame = p.Frame;
        // Title bar.
        if (SheetKit.NodeBox(frame, "title0", "title1") is { } tb) _title = SheetKit.Text(p, a, tb);
        else _title = SheetKit.Text(p, a, new Rect2(5, 1, 330, 18));
        var x = GuiButton.Create(a, "ctl_btn_x");
        x.PlaceOrigin(p.Node("xbutton") ?? new Vector2(342, 18));
        p.AddChild(x);
        p.Register("XButton", x);

        // Pages hang under the "pages" node down to the resize bar.
        var pagesAt = p.Node("pages") ?? new Vector2(0, 59);
        var resizeAt = p.Node("resizebar") ?? new Vector2(0, 468);
        _pages = new Control { Position = pagesAt, Size = new Vector2(360, resizeAt.Y - pagesAt.Y), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        p.AddChild(_pages);
        BuildCharPage();
        BuildListPage();
        var resize = GuiModelView.Create(a, "ctl_chrsht_resiz");
        resize.PlaceOrigin(resizeAt);
        p.AddChild(resize);

        // Tabs: Character, Skills, Feats, each starting at the previous one's "next" node.
        var group = new ButtonGroup();
        var at = p.Node("tabs") ?? new Vector2(0, 58);
        string[] tabModels = { "ctl_chr_btn_tab1", "ctl_chr_btn_tab2", "ctl_chr_btn_tab3" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var t = GuiButton.Create(a, tabModels[i]);
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
            at += t.Model?.Node("next") is { } next ? next : new Vector2(90, 0);
        }
        p.Size = p.CustomMinimumSize = new Vector2(360, resizeAt.Y);
        return p;
    }

    void BuildCharPage()
    {
        var a = Assets;
        _charPage = new Control { Size = _pages.Size, MouseFilter = MouseFilterEnum.Pass };
        _pages.AddChild(_charPage);
        _charView = new PortraitView { Assets = a, Name = "pnl_chrsht_char" };
        _charView.Model = a.Model("pnl_chrsht_char");
        _charView.Position = Vector2.Zero;
        _charPage.AddChild(_charView);
        var v = _charView;
        GuiLabel Box(string key, string n0, string n1, float alignH = 0, string font = SheetKit.Label)
        {
            var box = SheetKit.NodeBox(v, n0, n1) ?? new Rect2();
            return _labels[key] = SheetKit.Text(_charPage, a, box, "", font, null, alignH);
        }
        for (int i = 0; i < 6; i++)
        {
            Box("l" + Abilities[i], "l" + AbilityNodes[i] + "0", "l" + AbilityNodes[i] + "1").Text = a.Tlk(AbilityNames[i]);
            Box(Abilities[i] + "Stat", Abilities[i] + "Stat0", Abilities[i] + "Stat1", 0.5f);
            Box(Abilities[i] + "Mod", Abilities[i] + "Mod0", Abilities[i] + "Mod1", 0.5f);
            var iconNode = Abilities[i].ToLowerInvariant() + "Icon";
            if (v.Node(iconNode) is { } c && a.Texture(AbilityIcons[i]) is { } tex)
                _charPage.AddChild(new TextureRect { Texture = tex, Position = (c - new Vector2(8, 8)).Floor(), Size = new Vector2(16, 16), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore });
        }
        Box("lAC", "lAC0", "lAC1", 0.5f).Text = a.Tlk(321);
        Box("lHP", "lHP0", "lHP1", 0.5f).Text = a.Tlk(320);
        Box("AC", "ArmorClass0", "ArmorClass1", 0.5f);
        Box("HP", "HitPoints0", "HitPoints1", 0.5f);
        Box("XP", "Experience0", "Experience1");
        Box("Next", "NextLevel0", "NextLevel1");
        // Race and alignment, then a line per class, centred together in the identity box.
        var identity = SheetKit.NodeBox(v, "RaceAlignClass0", "RaceAlignClass1") ?? new Rect2(136, 2, 219, 72);
        _labels["Identity"] = SheetKit.Text(_charPage, a, identity, "", SheetKit.Label, null, 0, 0.5f);
        _labels["Identity"].Wrap = true;

        // The level-up button sits over the experience box when a level is waiting, captioned "Click To Level Up"
        // (TLK 8346, levelup scene).
        _levelUp = GuiButton.Create(a, "ctl_chrsht_lvlup", a.Tlk(8346), SheetKit.Label);
        _levelUp.PlaceOrigin(v.Node("LevelUpButton") ?? new Vector2(130, 255));
        _levelUp.Visible = false;
        _levelUp.Pressed += () => LevelUpRequested?.Invoke();
        _charPage.AddChild(_levelUp);

        // The combat summary between the List node and the resize bar, its scroll bar on the trough at the right.
        float top = v.Node("List")?.Y ?? 258;
        float bottom = _pages.Size.Y;
        float barX = v.Node("scrollmin")?.X ?? 342;
        _summary = SheetScroll.Create(a, new Rect2(0, top - 3, 340, bottom - top + 3), new Rect2(barX + 2 - SheetScrollBar.Width / 2, top + 2, SheetScrollBar.Width, bottom - top - 6), _charPage);
    }

    void BuildListPage()
    {
        var a = Assets;
        _listPage = new Control { Size = _pages.Size, MouseFilter = MouseFilterEnum.Pass, Visible = false };
        _pages.AddChild(_listPage);
        var view = GuiModelView.Create(a, "pnl_chrsht_skils");
        _listPage.AddChild(view);
        // Description of the selected entry at the top (155 px), the list under it.
        float split = view.Node("List")?.Y ?? 155;
        _desc = SheetScroll.Create(a, new Rect2(6, 4, 334, split - 8), new Rect2(344, 4, SheetScrollBar.Width, split - 8), _listPage);
        _descText = new SheetLine { Assets = a, Wrap = true, Position = Vector2.Zero, Size = new Vector2(334, 0) };
        _desc.Content.AddChild(_descText);
        float bottom = _pages.Size.Y;
        _list = SheetScroll.Create(a, new Rect2(0, split + 2, 340, bottom - split - 2), new Rect2(344, split + 4, SheetScrollBar.Width, bottom - split - 8), _listPage);
        _list.Bar.Step = 20;
    }

    public override void _Ready()
    {
        base._Ready();
        CharacterSheetRefresh.Attach(this, Ctx.Session);
        Ctx.Session.NotifyReceived += OnNotify;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
    }

    public override void Refresh() => Ctx.Session.Request("character");

    public void ShowTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, 2);
        _tabs[_tab].SetPressedNoSignal(true);
        _charPage.Visible = _tab == 0;
        _listPage.Visible = _tab != 0;
        _selected = -1;
        if (_last != null) Fill(_last.RootElement);
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind == "xp-changed" && m.Subject == Ctx.Session.ControlledId)
        {
            if (Visible) Ctx.Session.Request("character");
            return;
        }
        if (m.Kind != "character-sheet" || m.Subject != Ctx.Session.ControlledId) return;
        if (_last?.RootElement.GetRawText() == m.Json) return;
        _last?.Dispose();
        _last = JsonDocument.Parse(m.Json);
        Fill(_last.RootElement);
    }

    static int I(JsonElement e, string name, int fallback = 0) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    static string S(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? NwnText.Strip(v.GetString() ?? "") : "";

    void Fill(JsonElement d)
    {
        var a = Assets;
        _title.Text = $"{a.Tlk(61826)}, {S(d, "name")}";
        if (_tab == 0) FillCharacter(d);
        else FillList(d, _tab == 1 ? "skills" : "feats");
    }

    void FillCharacter(JsonElement d)
    {
        var a = Assets;
        _charView.Portrait = ChargenModel.Portrait(Ctx.Session.Content, S(d, "portrait"), "l");
        var lines = new List<string> { $"{S(d, "race")}, {S(d, "alignment")}" };
        foreach (var c in d.GetProperty("classes").EnumerateArray()) lines.Add($"{S(c, "name")} ({I(c, "level")})");
        _labels["Identity"].Text = string.Join("\n", lines);
        int i = 0;
        foreach (var ab in d.GetProperty("abilities").EnumerateArray())
        {
            if (i >= 6) break;
            _labels[Abilities[i] + "Stat"].Text = I(ab, "score").ToString();
            var color = I(ab, "score") > I(ab, "base") ? Colors.Green : I(ab, "score") < I(ab, "base") ? Colors.Red : Colors.White;
            _labels[Abilities[i] + "Stat"].Modulate = color;
            _labels[Abilities[i] + "Mod"].Modulate = color;
            _labels[Abilities[i] + "Mod"].Text = I(ab, "mod").ToString();
            i++;
        }
        _labels["AC"].Text = I(d, "ac").ToString();
        // Current hit points with temporary ones, like GetCurrentHitPoints: a polymorphed Rakshasa with its 30 bonus hit
        // points reads 35/6 on the original's sheet (visual scene polymorphpc). Other temporary hit points: not measured.
        _labels["HP"].Text = $"{I(d, "hp") + I(d, "tempHp")}/{I(d, "maxHp")}";
        int level = I(d, "level"), xp = I(d, "xp"), next = I(d, "xpNext");
        _labels["XP"].Text = $"{a.Tlk(314)}: {xp}";
        _labels["Next"].Text = level >= 40 ? "" : $"{a.Tlk(315)}: {next}";
        bool canLevel = xp >= next && level < 40;
        _levelUp.Visible = canLevel;
        _levelUp.ForcedState = canLevel ? "encouraged" : null;
        _labels["XP"].Visible = _labels["Next"].Visible = !canLevel;

        // The combat summary.
        float offset = _summary.Bar.Offset;
        _summary.Clear();
        var rows = new List<(string Caption, string Value)>();
        // Damage property lines under a weapon's damage (row index to the damage type's colour), drawn indented.
        var styled = new Dictionary<int, Color>();
        var weapons = d.TryGetProperty("weapons", out var w) ? w : default;
        var main = weapons.ValueKind == JsonValueKind.Object && weapons.TryGetProperty("main", out var m) ? m : default;
        var off = weapons.ValueKind == JsonValueKind.Object && weapons.TryGetProperty("off", out var o) ? o : default;
        void Weapon(JsonElement wpn, uint caption, bool offHand)
        {
            if (wpn.ValueKind != JsonValueKind.Object)
            {
                rows.Add((a.Tlk(caption), offHand ? a.Tlk(58329) : a.Tlk(58291)));
                return;
            }
            var name = S(wpn, "name");
            rows.Add((a.Tlk(caption), name.Length > 0 ? name : a.Tlk(58291)));
            rows.Add((a.Tlk(58292), SheetKit.Signed(I(wpn, "attack"))));
            int threat = I(wpn, "threat", 20);
            string crit = threat >= 20 ? "20" : $"{threat}-20";
            // The original: the weapon's dice, then what is added to them, then a line per damage property in the damage
            // type's colour ("1-6 + 3 (Critical: 20 / x2)", "+1d10 Fire Damage"; visual scene rulesfollowpc).
            string range = wpn.TryGetProperty("diceMin", out _)
                ? $"{I(wpn, "diceMin")}-{I(wpn, "diceMax")}" + (I(wpn, "flat") switch { > 0 and var f => $" + {f}", < 0 and var f => $" - {-f}", _ => "" })
                : $"{I(wpn, "min")}-{I(wpn, "max")}";
            rows.Add((a.Tlk(58313), $"{range} ({a.Tlk(58312)} {crit} / x{I(wpn, "mult", 2)})"));
            if (wpn.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array)
                foreach (var b in bonuses.EnumerateArray())
                {
                    styled[rows.Count] = Color.FromString("#" + S(b, "color"), SheetKit.White);
                    rows.Add(($"+{S(b, "amount")} ", S(b, "type")));
                }
        }
        Weapon(main, 58290, false);
        Weapon(off, 58328, true);
        var saves = d.GetProperty("saves");
        rows.Add((a.Tlk(317) + ": ", I(saves, "fortitude").ToString()));
        rows.Add((a.Tlk(318) + ": ", I(saves, "reflex").ToString()));
        rows.Add((a.Tlk(319) + ": ", I(saves, "will").ToString()));
        rows.Add((a.Tlk(58349), I(d, "spellResistance").ToString()));
        rows.Add((a.Tlk(58350), I(d, "arcaneFailure") + "%"));
        rows.Add((a.Tlk(58351), I(d, "armourCheckPenalty").ToString()));
        float y = 0;
        for (int r = 0; r < rows.Count; r++)
        {
            var (caption, value) = rows[r];
            var line = new SheetLine { Assets = a, Caption = caption, Value = value, Position = new Vector2(22, y), Size = new Vector2(316, 20) };
            if (styled.TryGetValue(r, out var color))
            {
                line.Position = new Vector2(38, y);
                line.CaptionColor = SheetKit.White;
                line.ValueColor = color;
            }
            _summary.Content.AddChild(line);
            y += 20;
        }
        _summary.SetContentHeight(y + 3);
        _summary.ScrollTo(offset);
    }

    /// <summary>Skills or feats: a row per entry (icon, name, and for skills the ranks and total); clicking one shows its description above.</summary>
    void FillList(JsonElement d, string key)
    {
        var a = Assets;
        float offset = _list.Bar.Offset;
        _list.Clear();
        var entries = d.TryGetProperty(key, out var arr) ? arr.EnumerateArray().ToList() : new();
        float y = 2;
        int index = 0;
        foreach (var e in entries)
        {
            int i = index++;
            var row = new SheetRow
            {
                Assets = a, Icon = a.Texture(S(e, "icon")), Name_ = S(e, "name"), Position = new Vector2(4, y), Size = new Vector2(334, 36),
                Right = key == "skills" ? $"{I(e, "ranks")}" : "", Right2 = key == "skills" ? (I(e, "total", -1) < 0 ? "-" : I(e, "total").ToString()) : "",
                Selected = i == _selected, Dim = key == "skills" && I(e, "ranks") == 0,
            };
            var desc = S(e, "description");
            var name = S(e, "name");
            row.Clicked = () => { _selected = i; foreach (var r in _list.Content.GetChildren().OfType<SheetRow>()) { r.Selected = r == row; r.QueueRedraw(); } ShowDescription(name, desc); };
            _list.Content.AddChild(row);
            y += 36;
        }
        _list.SetContentHeight(y + 2);
        _list.ScrollTo(offset);
        if (_selected < 0 && entries.Count > 0) ShowDescription(S(entries[0], "name"), S(entries[0], "description"));
        else if (entries.Count == 0) ShowDescription("", "");
    }

    void ShowDescription(string name, string text)
    {
        var f = Assets.Font(SheetKit.Body);
        var body = (name.Length > 0 ? name + "\n\n" : "") + text;
        _descText.Value = body;
        int lines = f.Wrap(NwnText.Strip(body), _descText.Size.X).Count;
        _descText.Size = new Vector2(_descText.Size.X, lines * f.LineHeight + 4);
        _descText.QueueRedraw();
        _desc.SetContentHeight(_descText.Size.Y);
        _desc.ScrollTo(0);
    }

    /// <summary>The character page's model with the portrait drawn where the model has its placeholder (gui_replace01).</summary>
    partial class PortraitView : GuiModelView
    {
        Texture2D? _portrait;
        public Texture2D? Portrait { get => _portrait; set { _portrait = value; QueueRedraw(); } }

        public override void _Draw()
        {
            if (Model == null) return;
            var pose = Model.Rest;
            GuiDraw.Model(this, Assets, pose, Origin, Colors.White, skip: m => m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase));
            if (_portrait == null) return;
            foreach (var m in pose.Meshes)
            {
                if (!m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase)) continue;
                var r = new Rect2(m.Points[0], Vector2.Zero);
                foreach (var pt in m.Points) r = r.Expand(pt);
                DrawTextureRect(_portrait, new Rect2(r.Position + Origin, r.Size), false);
            }
        }
    }
}

/// <summary>A list row of the character record's Skills and Feats pages: icon, name, and right-hand numbers.</summary>
public partial class SheetRow : Control
{
    public ClassicAssets Assets = null!;
    public Texture2D? Icon;
    public string Name_ = "", Right = "", Right2 = "";
    public bool Selected, Dim;
    public Action? Clicked;

    public SheetRow() { MouseFilter = MouseFilterEnum.Stop; }

    public override void _Ready()
    {
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
    }

    public override void _Draw()
    {
        bool hover = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        SheetKit.RowBox(this, new Rect2(0, 0, 34, 34), Colors.Black);
        if (Icon != null) DrawTextureRect(Icon, new Rect2(1, 1, 32, 32), false, Dim ? new Color(0.55f, 0.55f, 0.55f) : Colors.White);
        var box = new Rect2(34, 0, Size.X - 34, 34);
        SheetKit.RowBox(this, box, Selected ? new Color(0.35f, 0.3f, 0.15f, 0.45f) : hover ? new Color(1, 1, 1, 0.06f) : null);
        var f = Assets.Font(SheetKit.Label);
        var color = Selected || hover ? GuiDraw.Selected : Dim ? SheetKit.Caption : SheetKit.White;
        f.DrawBox(this, new Rect2(42, 0, Size.X - 140, 34), Name_, color, 0, 0.5f);
        if (Right.Length > 0) f.DrawBox(this, new Rect2(Size.X - 90, 0, 40, 34), Right, SheetKit.Caption, 1, 0.5f);
        if (Right2.Length > 0) f.DrawBox(this, new Rect2(Size.X - 46, 0, 38, 34), Right2, color, 1, 0.5f);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            if (Clicked != null) ClassicSounds.Play(Assets, "gui_select");
            Clicked?.Invoke();
            AcceptEvent();
        }
    }
}
