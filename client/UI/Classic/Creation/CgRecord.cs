using System.Text.Json;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Classic.Sheets;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// The character record beside the creation steps (and of a familiar or companion on their screen): pnl_chrsht2
/// hung from pnl_chr_genm's CharacterSheet node, its four tabs (ctl_chr_btn_tab1..4: Character, Skills, Feats,
/// Spells) and pages pnl_chrsht_char2 (portrait, race and alignment, classes, abilities, AC and hit
/// points, the combat summary) and pnl_chrsht_skil2 (a description over a list). Filled from the server's preview of
/// the build ("chargen-plan" sheet), as the in-game record is from "character-sheet". Measured on c00-c47.
/// </summary>
public partial class CgRecord : Control
{
    public ClassicAssets Assets = null!;
    readonly CgButton[] _tabs = new CgButton[4];
    Control _pages = null!, _charPage = null!, _listPage = null!;
    PortraitView _charView = null!;
    GuiLabel _title = null!, _identity = null!;
    readonly Dictionary<string, GuiLabel> _labels = new();
    SheetScroll _summary = null!, _list = null!, _desc = null!;
    SheetLine _descText = null!;
    int _tab;
    JsonElement? _last;
    int _selected = -1;

    static readonly string[] Abilities = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
    static readonly string[] AbilityNodes = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
    static readonly uint[] AbilityNames = { 135, 133, 132, 134, 136, 131 };
    static readonly string[] AbilityIcons = { "gui_chrsht_str", "gui_chrsht_dex", "gui_chrsht_con", "gui_chrsht_int", "gui_chrsht_wis", "gui_chrsht_cha" };
    static readonly uint[] TabNames = { 0, 327, 326, 2295 };
    /// <summary>
    /// The tabs' hover text (level-up's familiar step): "Character Sheet", "Skills", "Feats" at the pointer in
    /// the hover text's place; none over the spells tab.
    /// </summary>
    static readonly uint[] TabHovers = { 1059, 327, 326, 0 };

    /// <summary>What the record shows of a build in progress: the original fills it as steps are taken.</summary>
    public bool ShowAbilities = true, ShowCombat = true;
    /// <summary>
    /// The saves without the class's base saves: the original's record of a character being created shows only the
    /// ability modifiers (an elf wizard with Wisdom 12: Will 1, not 3; c22, c38).
    /// </summary>
    public bool CreationSaves;
    /// <summary>The identity lines (race and alignment, then a line per class), composed by the screen.</summary>
    public string[] Identity = Array.Empty<string>();
    public string Portrait = "";

    /// <summary>Builds the record with pnl_chrsht2's origin at <paramref name="at"/>, its pages clipped at <paramref name="bottom"/>.</summary>
    public static CgRecord Create(ClassicAssets assets, Vector2 at, float bottom)
    {
        var r = new CgRecord { Assets = assets, Position = at, MouseFilter = MouseFilterEnum.Pass, Name = "CgRecord" };
        r.Build(bottom - at.Y);
        return r;
    }

    void Build(float height)
    {
        var a = Assets;
        var frame = GuiModelView.Create(a, "pnl_chrsht2");
        AddChild(frame);
        var origin = frame.Origin;
        frame.Position = Vector2.Zero - origin + origin; // the model's origin is its top-left
        Size = new Vector2(360, height);
        var titleBox = SheetKit.NodeBox(frame, "title0", "title1") ?? new Rect2(5, 1, 333, 18);
        _title = SheetKit.Text(this, a, titleBox);
        var pagesAt = frame.Node("pages") ?? new Vector2(0, 59);
        _pages = new Control { Position = pagesAt, Size = new Vector2(360, height - pagesAt.Y), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_pages);
        BuildCharPage();
        BuildListPage();
        var tabAt = frame.Node("tabs") ?? new Vector2(0, 58);
        // The in-game record's tab models and a fourth for spells (ctl_chrtab_*'s gui_chrsht_tabs texture isn't
        // installed; the original's four tabs are these: c00, c39, c41).
        string[] models = { "ctl_chr_btn_tab1", "ctl_chr_btn_tab2", "ctl_chr_btn_tab3", "ctl_chr_btn_tab4" };
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var t = CgButton.Make(a, models[i]);
            t.ToggleMode = true;
            t.TextureRepeat = TextureRepeatEnum.Disabled;
            t.TextureFilter = TextureFilterEnum.Nearest;
            t.ButtonPressed = i == 0;
            t.PlaceOrigin(tabAt);
            t.Pressed += () => ShowTab(index);
            if (TabHovers[i] != 0) t.SetMeta(ClassicTooltip.Meta, a.Tlk(TabHovers[i]));
            AddChild(t);
            _tabs[i] = t;
            var next = t.Model?.Rest.Nodes.FirstOrDefault(n => n.Key.StartsWith("next", StringComparison.OrdinalIgnoreCase)).Value;
            tabAt += next is { } n && n != Vector2.Zero ? n : new Vector2(90, 0);
        }
    }

    void BuildCharPage()
    {
        var a = Assets;
        _charPage = new Control { Size = _pages.Size, MouseFilter = MouseFilterEnum.Pass };
        _pages.AddChild(_charPage);
        _charView = new PortraitView { Assets = a, Name = "pnl_chrsht_char2" };
        _charView.Model = a.Model("pnl_chrsht_char2");
        _charPage.AddChild(_charView);
        var v = _charView;
        GuiLabel Box(string key, string n0, string n1, float alignH = 0)
        {
            var box = SheetKit.NodeBox(v, n0, n1) ?? new Rect2();
            return _labels[key] = SheetKit.Text(_charPage, a, box, "", SheetKit.Label, null, alignH);
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
        // Race and alignment, then the classes, left in the box and centred down it (c15: "Elf" / "Wizard (1)").
        var identity = SheetKit.NodeBox(v, "RaceAlignClass0", "RaceAlignClass1") ?? new Rect2(136, 2, 219, 72);
        _identity = SheetKit.Text(_charPage, a, identity, "", SheetKit.Label, null, 0, 0.5f);
        _identity.Wrap = true;
        float top = v.Node("List")?.Y ?? 258;
        float bottom = _pages.Size.Y;
        float barX = v.Node("scrollmin")?.X ?? 340;
        _summary = SheetScroll.Create(a, new Rect2(0, top - 3, 340, bottom - top + 3), new Rect2(barX + 2 - SheetScrollBar.Width / 2, top + 2, SheetScrollBar.Width, bottom - top - 6), _charPage);
    }

    void BuildListPage()
    {
        var a = Assets;
        _listPage = new Control { Size = _pages.Size, MouseFilter = MouseFilterEnum.Pass, Visible = false };
        _pages.AddChild(_listPage);
        var view = GuiModelView.Create(a, "pnl_chrsht_skil2");
        _listPage.AddChild(view);
        float split = view.Node("List")?.Y ?? 155;
        _desc = SheetScroll.Create(a, new Rect2(6, 4, 334, split - 8), new Rect2(344, 4, SheetScrollBar.Width, split - 8), _listPage);
        _descText = new SheetLine { Assets = a, Wrap = true, Position = Vector2.Zero, Size = new Vector2(334, 0) };
        _desc.Content.AddChild(_descText);
        float bottom = _pages.Size.Y;
        _list = SheetScroll.Create(a, new Rect2(0, split + 2, 340, bottom - split - 2), new Rect2(344, split + 4, SheetScrollBar.Width, bottom - split - 8), _listPage);
        _list.Bar.Step = 36;
    }

    public void ShowTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, 3);
        for (int i = 0; i < 4; i++) _tabs[i].SetPressedNoSignal(i == _tab);
        _charPage.Visible = _tab == 0;
        _listPage.Visible = _tab != 0;
        _selected = -1;
        Refill();
    }

    static int I(JsonElement e, string name, int fallback = 0) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    static string S(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? NwnText.Strip(v.GetString() ?? "") : "";

    /// <summary>Shows a record: { sheet: character-sheet payload, spells: [...] } (null: an empty record).</summary>
    public void Fill(JsonElement? record, string name)
    {
        _last = record?.Clone();
        _name = name;
        Refill();
    }
    string _name = "";

    void Refill()
    {
        var a = Assets;
        _title.Text = $"{a.Tlk(61826)}, {_name}";
        var sheet = _last is { } r && r.ValueKind == JsonValueKind.Object && r.TryGetProperty("sheet", out var s) ? s : default;
        if (_tab == 0) FillCharacter(sheet);
        else FillList(sheet, _tab);
    }

    void FillCharacter(JsonElement d)
    {
        var a = Assets;
        string portrait = Portrait.Length > 0 ? Portrait : S(d, "portrait");
        _charView.Portrait = ChargenModel.Portrait(a.Content, portrait.Length > 0 ? portrait : "gui_po_nwnlogo_", "l");
        _identity.Text = string.Join("\n", Identity);
        var abilities = d.ValueKind == JsonValueKind.Object && d.TryGetProperty("abilities", out var ab) ? ab.EnumerateArray().ToList() : new();
        for (int i = 0; i < 6; i++)
        {
            bool show = ShowAbilities && i < abilities.Count;
            _labels[Abilities[i] + "Stat"].Text = show ? I(abilities[i], "score").ToString() : "";
            _labels[Abilities[i] + "Mod"].Text = show ? I(abilities[i], "mod").ToString() : "";
        }
        _labels["AC"].Text = ShowAbilities && d.ValueKind == JsonValueKind.Object ? I(d, "ac").ToString() : "";
        _labels["HP"].Text = ShowAbilities && d.ValueKind == JsonValueKind.Object ? $"{I(d, "hp")}/{I(d, "maxHp")}" : "";

        _summary.Clear();
        if (!ShowCombat || d.ValueKind != JsonValueKind.Object) { _summary.SetContentHeight(0); return; }
        var rows = new List<(string Caption, string Value)>();
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
            // The dice, then the bonus each roll gets: "1-3 - 1" (c00: unarmed, Strength 8), "1-3" without one (c15).
            int bonus = I(wpn, "flat");
            string dice = wpn.TryGetProperty("diceMin", out _) ? $"{I(wpn, "diceMin")}-{I(wpn, "diceMax")}" + (bonus > 0 ? $" + {bonus}" : bonus < 0 ? $" - {-bonus}" : "") : $"{I(wpn, "min")}-{I(wpn, "max")}";
            rows.Add((a.Tlk(58313), $"{dice} ({a.Tlk(58312)} {crit} / x{I(wpn, "mult", 2)})"));
        }
        Weapon(main, 58290, false);
        Weapon(off, 58328, true);
        var saves = d.GetProperty("saves");
        var bases = CreationSaves && d.TryGetProperty("baseSaves", out var bs) ? bs : default;
        int Save(string n) => I(saves, n) - (bases.ValueKind == JsonValueKind.Object ? I(bases, n) : 0);
        rows.Add((a.Tlk(317) + ": ", Save("fortitude").ToString()));
        rows.Add((a.Tlk(318) + ": ", Save("reflex").ToString()));
        rows.Add((a.Tlk(319) + ": ", Save("will").ToString()));
        // "Base Attack: 1" on the original's record (c00, c15): the character's base attack bonus.
        rows.Add((a.Tlk(40424), I(d, "bab").ToString()));
        rows.Add((a.Tlk(58349), I(d, "spellResistance").ToString()));
        rows.Add((a.Tlk(58350), I(d, "arcaneFailure") + "%"));
        rows.Add((a.Tlk(58351), I(d, "armourCheckPenalty").ToString()));
        float y = 0;
        foreach (var (caption, value) in rows)
        {
            _summary.Content.AddChild(new SheetLine { Assets = a, Caption = caption, Value = value, Position = new Vector2(22, y), Size = new Vector2(316, 20) });
            y += 20;
        }
        _summary.SetContentHeight(y + 3);
    }

    /// <summary>Skills, feats or spells: a row per entry; clicking one shows its description above (the tab's name before).</summary>
    void FillList(JsonElement d, int tab)
    {
        var a = Assets;
        _list.Clear();
        var entries = new List<JsonElement>();
        if (tab == 3)
        {
            if (_last is { } r && r.TryGetProperty("spells", out var sp)) entries = sp.EnumerateArray().ToList();
        }
        else if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty(tab == 1 ? "skills" : "feats", out var arr)) entries = arr.EnumerateArray().ToList();
        float y = 2;
        int index = 0;
        foreach (var e in entries)
        {
            int i = index++;
            var row = new SheetRow
            {
                Assets = a, Icon = a.Texture(S(e, "icon")), Name_ = S(e, "name"), Position = new Vector2(4, y), Size = new Vector2(334, 36),
                Right2 = tab == 1 ? (I(e, "total", -1) < 0 ? "-" : I(e, "total").ToString()) : "",
                Selected = i == _selected,
            };
            var desc = S(e, "description");
            var name = S(e, "name");
            row.Clicked = () => { _selected = i; foreach (var x in _list.Content.GetChildren().OfType<SheetRow>()) { x.Selected = x == row; x.QueueRedraw(); } ShowDescription(name, desc); };
            _list.Content.AddChild(row);
            y += 36;
        }
        _list.SetContentHeight(y + 2);
        // Before a row is chosen the description box shows the tab's name, centred (c39: "Skills", c41: "Spells").
        ShowDescription("", "", a.Tlk(TabNames[tab]));
    }

    void ShowDescription(string name, string text, string heading = "")
    {
        var f = Assets.Font(SheetKit.Body);
        foreach (var c in _desc.Content.GetChildren().OfType<GuiLabel>()) c.QueueFree();
        if (heading.Length > 0)
        {
            _descText.Value = "";
            var l = GuiLabel.Create(Assets, heading, SheetKit.Label, Colors.White, 0.5f, 0);
            l.Position = new Vector2(0, 22);
            l.Size = new Vector2(334, 20);
            _desc.Content.AddChild(l);
            _desc.SetContentHeight(0);
            return;
        }
        var body = (name.Length > 0 ? name + "\n\n" : "") + text;
        _descText.Value = body;
        int lines = f.Wrap(NwnText.Strip(body), _descText.Size.X).Count;
        _descText.Size = new Vector2(_descText.Size.X, lines * f.LineHeight + 4);
        _descText.QueueRedraw();
        _desc.SetContentHeight(_descText.Size.Y);
        _desc.ScrollTo(0);
    }

    /// <summary>The character page's model with the portrait where the model has its placeholder (gui_replace01).</summary>
    partial class PortraitView : GuiModelView
    {
        Texture2D? _portrait;
        public Texture2D? Portrait { get => _portrait; set { _portrait = value; QueueRedraw(); } }

        public override void _Draw()
        {
            if (Model == null) return;
            var pose = Model.Rest;
            // The scroll pane's backing planes run 32 units down (the whole list's length); the original shows the pane
            // black (c00), not their stretched box texture.
            GuiDraw.Model(this, Assets, pose, Origin, Colors.White, skip: m => m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase)
                || m.Points.Max(q => q.Y) - m.Points.Min(q => q.Y) > 600);
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
