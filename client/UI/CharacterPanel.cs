using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Character sheet (C): a header with portrait, name, race, classes, level and experience (with the
/// Level Up call to action); ability cards and saving throws; combat numbers; then Skills, Feats and
/// Details tabs. Every number explains itself in a tooltip. Filled from the server's "character-sheet" notify.
/// </summary>
public partial class CharacterPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    /// <summary>The Level Up button was pressed (opens the level-up window).</summary>
    public Action? LevelUpRequested;
    VBoxContainer _content = null!;
    Button? _levelUp;
    bool _canLevelUp;
    int _tab;
    string _skillFilter = "";
    bool _trainedOnly;
    JsonDocument? _last;
    readonly Dictionary<string, Texture2D?> _icons = new();

    static readonly string[] AbilityFull = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
    static readonly string[] AbilityAffects =
    {
        "Melee attack and damage rolls, how much you can carry, and Strength skills such as Discipline.",
        "Armour Class (up to your armour's limit), ranged attack rolls, Reflex saves, and skills such as Hide, Move Silently, Parry and Tumble.",
        "Hit points gained every level, Fortitude saves, and Concentration.",
        "Skill points every level, a wizard's spells (bonus spells and save DCs), and skills such as Lore, Search and Spellcraft.",
        "Will saves, the spells of clerics, druids, paladins and rangers, and skills such as Heal, Listen and Spot.",
        "The spells of sorcerers and bards, turning undead, and social skills such as Persuade, Bluff and Intimidate.",
    };
    static readonly Dictionary<int, string> FeatCategories = new()
    {
        [5] = "Class and racial", [1] = "Combat", [2] = "Active combat", [3] = "Defensive", [4] = "Magical", [6] = "Other",
    };

    public static CharacterPanel Create(ClientSession session)
    {
        var p = Create<CharacterPanel>("Character", new Vector2(1010, 690));
        p.Session = session;
        p.OffsetLeft = 40; p.OffsetTop = 70;
        return p;
    }

    public override void _Ready()
    {
        // The HUD is a CanvasLayer, which stops the root window's theme reaching its windows.
        Theme = GameTheme.Theme;
        _content = Ui.VBox(14);
        _content.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(_content);
        _content.AddChild(Ui.Text("Loading…", Ui.TextStyle.Secondary));
        CharacterSheetRefresh.Attach(this, Session);
        Session.NotifyReceived += OnNotify;
        TreeExiting += () => Session.NotifyReceived -= OnNotify;
    }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        Visible = true;
        Session.Request("character");
    }

    /// <summary>Shows a tab (0 skills, 1 feats, 2 details).</summary>
    public void ShowTab(int tab)
    {
        _tab = tab;
        if (_last != null) Fill(_last.RootElement);
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind == "xp-changed" && m.Subject == Session.ControlledId)
        {
            using var xp = JsonDocument.Parse(m.Json);
            _canLevelUp = xp.RootElement.TryGetProperty("canLevelUp", out var can) && can.ValueKind == JsonValueKind.True;
            if (_levelUp != null && IsInstanceValid(_levelUp)) _levelUp.Visible = _canLevelUp;
            if (Visible) Session.Request("character");
            return;
        }
        if (m.Kind != "character-sheet" || m.Subject != Session.ControlledId) return;
        if (_last?.RootElement.GetRawText() == m.Json) return;
        _last?.Dispose();
        _last = JsonDocument.Parse(m.Json);
        Fill(_last.RootElement);
    }

    /// <summary>
    /// A weapon's damage as the original's sheet gives it: the dice, what is added to them, then each damage property
    /// ("1-6 + 3, +1d10 Fire Damage, +5 Bludgeoning Damage").
    /// </summary>
    static string WeaponDamage(JsonElement weapon)
    {
        if (!weapon.TryGetProperty("diceMin", out _)) return $"{I(weapon, "min")}-{I(weapon, "max")}";
        int flat = I(weapon, "flat");
        var text = $"{I(weapon, "diceMin")}-{I(weapon, "diceMax")}" + (flat > 0 ? $" + {flat}" : flat < 0 ? $" - {-flat}" : "");
        if (weapon.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array)
            foreach (var b in bonuses.EnumerateArray()) text += $", +{S(b, "amount")} {S(b, "type")}";
        return text;
    }

    static string Signed(int v) => v >= 0 ? $"+{v}" : v.ToString();
    static int I(JsonElement e, string name, int fallback = 0) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    /// <summary>A string field with NWN colour tokens removed (module text carries &lt;cRGB&gt;...&lt;/c&gt;).</summary>
    static string S(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? NwnText.Strip(v.GetString() ?? "") : "";
    static bool B(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    Texture2D? GameIcon(string resref)
    {
        if (resref.Length == 0) return null;
        if (_icons.TryGetValue(resref, out var t)) return t;
        return _icons[resref] = ChargenModel.Icon(Session.Content, resref);
    }

    void Fill(JsonElement d)
    {
        var scrollOffsets = _content.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>()
            .ToDictionary(s => s.Name.ToString(), s => s.ScrollVertical);
        foreach (var c in _content.GetChildren()) { _content.RemoveChild(c); c.QueueFree(); }
        TitleLabel.Text = "Character";
        _content.AddChild(Header(d));
        var main = Ui.HBox(20);
        main.SizeFlagsVertical = SizeFlags.ExpandFill;
        _content.AddChild(main);
        main.AddChild(AbilityColumn(d));
        main.AddChild(CombatColumn(d));
        main.AddChild(Tabs(d));
        Callable.From(() =>
        {
            if (!IsInstanceValid(_content)) return;
            foreach (var scroll in _content.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>())
                if (scrollOffsets.TryGetValue(scroll.Name.ToString(), out int offset)) scroll.ScrollVertical = offset;
        }).CallDeferred();
    }

    // ------------------------------------------------------------------ header

    Control Header(JsonElement d)
    {
        var card = new PanelContainer();
        var bg = Styles.Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, 14, 12);
        card.AddThemeStyleboxOverride("panel", bg);
        var row = Ui.HBox(18);
        card.AddChild(row);

        // Portrait in a brass hairline frame.
        var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.AccentDim, 1, 2));
        var portrait = ChargenModel.Portrait(Session.Content, S(d, "portrait"), "l");
        if (portrait != null)
            frame.AddChild(new TextureRect { Texture = portrait, CustomMinimumSize = new Vector2(78, 100), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered });
        else
            frame.AddChild(Icons.Rect("character", 78, Palette.TextMuted));
        row.AddChild(frame);

        // Identity.
        var who = Ui.VBox(6);
        who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        who.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(who);
        var name = Ui.Text(S(d, "name"), Ui.TextStyle.Heading);
        name.ClipText = true;
        who.AddChild(name);
        var race = S(d, "race");
        who.AddChild(Ui.Text($"{S(d, "gender")} {race}   ·   {S(d, "alignment")}" + (S(d, "deity") is { Length: > 0 } deity ? $"   ·   {deity}" : ""), Ui.TextStyle.Secondary));
        var classes = Ui.HBox(8);
        foreach (var c in d.GetProperty("classes").EnumerateArray())
        {
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.3f), Palette.RadiusSmall, Palette.LineStrong, 1, 8, 3));
            var h = Ui.HBox(6);
            if (GameIcon(S(c, "icon")) is { } ci)
                h.AddChild(new TextureRect { Texture = ci, CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            h.AddChild(Ui.Text(S(c, "name"), Ui.TextStyle.Strong, null, Palette.FontSmall + 1));
            h.AddChild(Ui.Text(I(c, "level").ToString(), Ui.TextStyle.Strong, Palette.AccentBright, Palette.FontSmall + 1));
            chip.AddChild(h);
            classes.AddChild(chip);
        }
        who.AddChild(classes);

        // Level medallion, experience bar and the Level Up call to action.
        int level = I(d, "level"), xp = I(d, "xp"), next = I(d, "xpNext"), start = I(d, "xpLevel");
        _canLevelUp = xp >= next && level < 40;
        row.AddChild(new LevelMedallion { Level = level, Glow = _canLevelUp, CustomMinimumSize = new Vector2(76, 76), SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = $"Character level {level}" });
        var lvl = Ui.VBox(6);
        lvl.CustomMinimumSize = new Vector2(280, 0);
        lvl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(lvl);
        var top = Ui.HBox(8);
        var xpLabel = Ui.Text("EXPERIENCE", Ui.TextStyle.Caption, Palette.TextSecondary, 11);
        xpLabel.AddThemeFontOverride("font", Fonts.UiBold);
        xpLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(xpLabel);
        top.AddChild(Ui.Text(level >= 40 ? $"{xp:N0}" : $"{xp:N0} / {next:N0}", Ui.TextStyle.Strong, null, Palette.FontSmall));
        lvl.AddChild(top);
        var bar = new Bar
        {
            Fill = Palette.Xp.Darkened(0.3f), FillTop = Palette.Xp.Lightened(0.25f), ShowTrail = false, CustomMinimumSize = new Vector2(0, 10),
            ShowText = false, Radius = 5,
        };
        bar.Set(Math.Max(0, xp - start), Math.Max(1, next - start));
        lvl.AddChild(bar);
        var bottom = Ui.HBox(8);
        var hint = level >= 40 ? "Maximum level" : _canLevelUp ? $"Ready for level {level + 1}!" : $"{next - xp:N0} XP to level {level + 1}";
        var hl = Ui.Text(hint, Ui.TextStyle.Caption, _canLevelUp ? Palette.AccentBright : Palette.TextMuted);
        hl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bottom.AddChild(hl);
        _levelUp = Ui.Button("Level Up", () => LevelUpRequested?.Invoke(), "PrimaryButton", "levelup");
        _levelUp.CustomMinimumSize = new Vector2(128, 34);
        _levelUp.TooltipText = $"You have enough experience to reach level {level + 1}.";
        _levelUp.Visible = _canLevelUp;
        bottom.AddChild(_levelUp);
        lvl.AddChild(bottom);
        return card;
    }

    // ------------------------------------------------------------------ abilities and saves

    Control AbilityColumn(JsonElement d)
    {
        var col = Ui.VBox(10);
        col.CustomMinimumSize = new Vector2(252, 0);
        col.AddChild(Ui.Section("Abilities"));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        col.AddChild(grid);
        int i = 0;
        foreach (var a in d.GetProperty("abilities").EnumerateArray())
        {
            int index = i++;
            int score = I(a, "score"), bas = I(a, "base"), mod = I(a, "mod");
            var card = new TipPanel { CustomMinimumSize = new Vector2(122, 84), MouseFilter = MouseFilterEnum.Stop };
            card.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, 10, 8));
            Hover(card, Styles.Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, 10, 8), Styles.Box(Palette.Bg3, Palette.Radius, Palette.AccentDim, 1, 10, 8));
            var v = Ui.VBox(0);
            v.MouseFilter = MouseFilterEnum.Ignore;
            card.AddChild(v);
            var label = Ui.Text(AbilityFull[index].ToUpperInvariant(), Ui.TextStyle.Caption, Palette.TextSecondary, 11);
            label.AddThemeFontOverride("font", Fonts.UiBold);
            v.AddChild(label);
            var nums = Ui.HBox(8);
            nums.MouseFilter = MouseFilterEnum.Ignore;
            var color = score > bas ? Palette.Success : score < bas ? Palette.Danger : Palette.Text;
            var big = Ui.Text(score.ToString(), Ui.TextStyle.Display, color, 32);
            nums.AddChild(big);
            nums.AddChild(Ui.Spacer());
            var modChip = Chip.Create(Signed(mod), mod > 0 ? Palette.Accent : mod < 0 ? Palette.Danger : Palette.TextSecondary, fontSize: Palette.FontSmall);
            nums.AddChild(modChip);
            v.AddChild(nums);
            var rows = new List<(string, string, Color?)> { ("Modifier", Signed(mod), null), ("Base score", bas.ToString(), null) };
            if (score != bas) rows.Add(("From effects and items", Signed(score - bas), score > bas ? Palette.Success : Palette.Danger));
            card.Tooltip = () => Tip.Card(AbilityFull[index], $"Score {score}", AbilityAffects[index], rows: rows);
            grid.AddChild(card);
        }

        col.AddChild(Ui.Section("Saving throws"));
        var saves = Ui.HBox(8);
        col.AddChild(saves);
        var total = d.GetProperty("saves");
        var bases = d.TryGetProperty("baseSaves", out var bs) ? bs : default;
        var abil = d.GetProperty("abilities").EnumerateArray().Select(a => I(a, "mod")).ToArray();
        foreach (var (key, title, ability, what) in new[]
        {
            ("fortitude", "Fortitude", 2, "Resists poison, disease, death magic and other attacks on your body."),
            ("reflex", "Reflex", 1, "Lets you dodge area attacks such as fireballs and traps for half damage or less."),
            ("will", "Will", 4, "Resists charms, fear, compulsions and other attacks on your mind."),
        })
        {
            int value = I(total, key);
            int b = bases.ValueKind == JsonValueKind.Object ? I(bases, key) : value;
            int abilityMod = ability < abil.Length ? abil[ability] : 0;
            var tile = StatTile(title.ToUpperInvariant(), Signed(value), null, new Vector2(78, 64));
            tile.Tooltip = () => Tip.Card(title + " save", Signed(value), what, rows: new (string, string, Color?)[]
            {
                ("Class base", Signed(b), null), ($"{AbilityFull[ability]} modifier", Signed(abilityMod), null),
                ("Feats, items and effects", Signed(value - b - abilityMod), null),
            });
            saves.AddChild(tile);
        }
        return col;
    }

    // ------------------------------------------------------------------ combat

    Control CombatColumn(JsonElement d)
    {
        var col = Ui.VBox(10);
        col.CustomMinimumSize = new Vector2(232, 0);
        col.AddChild(Ui.Section("Combat"));
        var vitals = Ui.HBox(8);
        col.AddChild(vitals);

        int hp = I(d, "hp"), max = I(d, "maxHp"), temp = I(d, "tempHp");
        var hpTile = StatTile("HIT POINTS", hp.ToString(), "heart", new Vector2(112, 84), Palette.HealthBright, $"/ {max}");
        var hpBar = new Bar { CustomMinimumSize = new Vector2(0, 5), ShowText = false, Radius = 2, ShowTrail = false };
        hpBar.Set(hp, max, temp);
        ((VBoxContainer)hpTile.GetChild(0)).AddChild(hpBar);
        hpTile.Tooltip = () => Tip.Card("Hit points", $"{hp} of {max}", "When your hit points fall to zero you fall unconscious and die. Resting restores them.",
            rows: temp > 0 ? new (string, string, Color?)[] { ("Temporary", $"+{temp}", Palette.TempHealth) } : null);
        vitals.AddChild(hpTile);

        int ac = I(d, "ac");
        var acTile = StatTile("ARMOR CLASS", ac.ToString(), "shield", new Vector2(112, 84), Palette.Info);
        var parts = d.TryGetProperty("acParts", out var p) ? p : default;
        acTile.Tooltip = () =>
        {
            var rows = new List<(string, string, Color?)> { ("Base", "10", null) };
            if (parts.ValueKind == JsonValueKind.Object)
            {
                foreach (var (k, label) in new[] { ("armour", "Armour"), ("shield", "Shield"), ("dex", "Dexterity"), ("size", "Size"), ("natural", "Natural"), ("deflection", "Deflection"), ("dodge", "Dodge") })
                    if (I(parts, k) != 0) rows.Add((label, Signed(I(parts, k)), null));
                if (I(parts, "penalty") != 0) rows.Add(("Penalties", $"-{I(parts, "penalty")}", Palette.Danger));
            }
            return Tip.Card("Armor Class", $"AC {ac}", "How hard you are to hit: an attack hits when its roll plus attack bonus reaches your AC.", rows: rows);
        };
        vitals.AddChild(acTile);

        var list = Ui.VBox(2);
        col.AddChild(list);
        if (d.TryGetProperty("weapons", out var weapons))
            foreach (var hand in new[] { "main", "off" })
                if (weapons.TryGetProperty(hand, out var weapon) && weapon.ValueKind == JsonValueKind.Object)
                    list.AddChild(StatRow("combat", hand == "main" ? "Attack bonus" : "Off-hand attack", Signed(I(weapon, "attack")),
                        $"{S(weapon, "name")} - damage {WeaponDamage(weapon)}", "Weapon attack bonus"));
        int bab = I(d, "bab"), attacks = Math.Max(1, I(d, "attacks"));
        string babText = string.Join(" / ", Enumerable.Range(0, attacks).Select(k => Signed(bab - 5 * k)));
        list.AddChild(StatRow("combat", "Base attack", babText, $"{attacks} attack{(attacks == 1 ? "" : "s")} per round. Each extra attack in a round is made at -5.", "Base attack bonus"));
        list.AddChild(StatRow("magic", "Spell resistance", I(d, "spellResistance").ToString(), "Hostile spells must beat this number (d20 + caster level) to affect you.", "Spell resistance"));
        int asf = I(d, "arcaneFailure");
        list.AddChild(StatRow("book", "Arcane failure", asf + "%", "The chance that an arcane spell with a somatic component fails because of the armour and shield you wear.", "Arcane spell failure",
            asf > 0 ? Palette.Warning : null));
        int acp = I(d, "armourCheckPenalty");
        list.AddChild(StatRow("armor", "Armour check", acp > 0 ? $"-{acp}" : "0", "Penalty from armour and shield to skills marked with the armour icon (Hide, Move Silently, Tumble...).", "Armour check penalty",
            acp > 0 ? Palette.Warning : null));
        float run = d.TryGetProperty("runSpeed", out var rs) ? rs.GetSingle() : 0;
        list.AddChild(StatRow("boots", "Speed", $"{run:0.0} m/s", "How fast you run. Walking, stealth and detect mode are slower.", "Movement speed"));

        // Carried weight against capacity.
        int weight = I(d, "weight"), normal = I(d, "carryNormal"), heavy = I(d, "carryHeavy");
        var load = Ui.VBox(4);
        load.MouseFilter = MouseFilterEnum.Ignore;
        var loadRow = Ui.HBox(8);
        loadRow.MouseFilter = MouseFilterEnum.Ignore;
        loadRow.AddChild(Icons.Rect("inventory", 16, Palette.TextMuted));
        var ll = Ui.Text("Load", Ui.TextStyle.Small);
        ll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        loadRow.AddChild(ll);
        bool burdened = normal > 0 && weight > normal;
        loadRow.AddChild(Ui.Text(normal > 0 ? $"{weight / 10.0:0.#} / {normal / 10.0:0} lb" : $"{weight / 10.0:0.#} lb", Ui.TextStyle.Strong, burdened ? Palette.Warning : null, Palette.FontSmall + 1));
        load.AddChild(loadRow);
        if (normal > 0)
        {
            var loadBar = new Bar { CustomMinimumSize = new Vector2(0, 5), ShowText = false, Radius = 2, Fill = burdened ? Palette.Warning : Palette.AccentDim, FillTop = Palette.Accent, ShowTrail = false };
            loadBar.Set(weight, normal);
            load.AddChild(loadBar);
        }
        var loadTip = new TipPanel { MouseFilter = MouseFilterEnum.Stop };
        var loadNormal = Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 6, 5);
        loadTip.AddThemeStyleboxOverride("panel", loadNormal);
        Hover(loadTip, loadNormal, Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, null, 0, 6, 5));
        loadTip.AddChild(load);
        loadTip.Tooltip = () => Tip.Card("Load", $"{weight / 10.0:0.#} lb carried", "Carrying more than your Strength allows slows you down; far more and you can barely move.",
            rows: normal > 0 ? new (string, string, Color?)[] { ("Unencumbered up to", $"{normal / 10.0:0} lb", null), ("Overloaded beyond", $"{heavy / 10.0:0} lb", null) } : null);
        list.AddChild(loadTip);
        return col;
    }

    /// <summary>A tile: caption, big number (with an optional suffix), optional icon in the corner.</summary>
    static TipPanel StatTile(string caption, string value, string? icon, Vector2 size, Color? valueColor = null, string? suffix = null)
    {
        var tile = new TipPanel { CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Stop, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var normal = Styles.Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, 10, 8);
        tile.AddThemeStyleboxOverride("panel", normal);
        Hover(tile, normal, Styles.Box(Palette.Bg3, Palette.Radius, Palette.AccentDim, 1, 10, 8));
        var v = Ui.VBox(2);
        v.MouseFilter = MouseFilterEnum.Ignore;
        tile.AddChild(v);
        var head = Ui.HBox(4);
        head.MouseFilter = MouseFilterEnum.Ignore;
        var cap = Ui.Text(caption, Ui.TextStyle.Caption, Palette.TextSecondary, 10);
        cap.AddThemeFontOverride("font", Fonts.UiBold);
        cap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(cap);
        if (icon != null) head.AddChild(Icons.Rect(icon, 16, (valueColor ?? Palette.Accent) with { A = 0.8f }));
        v.AddChild(head);
        var nums = Ui.HBox(4);
        nums.MouseFilter = MouseFilterEnum.Ignore;
        nums.AddChild(Ui.Text(value, Ui.TextStyle.Display, valueColor ?? Palette.Text, 26));
        if (suffix != null)
        {
            var s = Ui.Text(suffix, Ui.TextStyle.Small, Palette.TextMuted);
            s.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            nums.AddChild(s);
        }
        v.AddChild(nums);
        return tile;
    }

    static TipPanel StatRow(string icon, string label, string value, string explain, string title, Color? color = null)
    {
        var row = new TipPanel { MouseFilter = MouseFilterEnum.Stop };
        var normal = Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 6, 5);
        row.AddThemeStyleboxOverride("panel", normal);
        Hover(row, normal, Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, null, 0, 6, 5));
        var h = Ui.HBox(8);
        h.MouseFilter = MouseFilterEnum.Ignore;
        h.AddChild(Icons.Rect(icon, 16, Palette.TextMuted));
        var l = Ui.Text(label, Ui.TextStyle.Small);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(l);
        h.AddChild(Ui.Text(value, Ui.TextStyle.Strong, color, Palette.FontSmall + 1));
        row.AddChild(h);
        row.Tooltip = () => Tip.Card(title, value, explain);
        return row;
    }

    static void Hover(Control c, StyleBox normal, StyleBox hover)
    {
        c.MouseEntered += () => c.AddThemeStyleboxOverride("panel", hover);
        c.MouseExited += () => c.AddThemeStyleboxOverride("panel", normal);
    }

    // ------------------------------------------------------------------ tabs

    Control Tabs(JsonElement d)
    {
        var tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        tabs.AddChild(SkillsTab(d));
        tabs.AddChild(FeatsTab(d));
        tabs.AddChild(DetailsTab(d));
        tabs.SetTabTitle(0, "Skills");
        tabs.SetTabTitle(1, "Feats");
        tabs.SetTabTitle(2, "Details");
        tabs.CurrentTab = Math.Clamp(_tab, 0, 2);
        tabs.TabChanged += t => _tab = (int)t;
        return tabs;
    }

    Control SkillsTab(JsonElement d)
    {
        var box = Ui.VBox(8);
        box.Name = "Skills";
        var tools = Ui.HBox(10);
        var search = new LineEdit { PlaceholderText = "Search skills", Text = _skillFilter, ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tools.AddChild(search);
        var trained = new CheckBox { Text = "Trained only", ButtonPressed = _trainedOnly, FocusMode = FocusModeEnum.None };
        trained.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        tools.AddChild(trained);
        box.AddChild(tools);

        var head = Ui.HBox(8);
        head.AddChild(new Control { CustomMinimumSize = new Vector2(26, 0) });
        var hn = Ui.Text("SKILL", Ui.TextStyle.Caption);
        hn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(hn);
        head.AddChild(Fixed(Ui.Text("RANKS", Ui.TextStyle.Caption), 52, HorizontalAlignment.Right));
        head.AddChild(Fixed(Ui.Text("TOTAL", Ui.TextStyle.Caption), 52, HorizontalAlignment.Right));
        box.AddChild(Ui.Pad(head, 8, 0, 14, 0));

        var scroll = new ScrollContainer { Name = "SkillsList", SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var list = Ui.VBox(1);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        int acp = I(d, "armourCheckPenalty");
        var abil = d.GetProperty("abilities").EnumerateArray().ToDictionary(a => S(a, "name"), a => I(a, "mod"));
        var rows = new List<(Control Row, string Name, bool Trained)>();
        foreach (var s in d.GetProperty("skills").EnumerateArray())
        {
            string name = S(s, "name"), key = S(s, "ability");
            int ranks = I(s, "ranks"), total = I(s, "total");
            bool armour = B(s, "armourCheck");
            var row = new TipPanel { MouseFilter = MouseFilterEnum.Stop };
            var normal = Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 8, 4);
            row.AddThemeStyleboxOverride("panel", normal);
            Hover(row, normal, Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, null, 0, 8, 4));
            var h = Ui.HBox(8);
            h.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(h);
            var icon = GameIcon(S(s, "icon"));
            h.AddChild(new TextureRect { Texture = icon, CustomMinimumSize = new Vector2(26, 26), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
            var n = Ui.Text(name, ranks > 0 ? Ui.TextStyle.Body : Ui.TextStyle.Secondary, null, Palette.FontSmall + 1);
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            n.ClipText = true;
            h.AddChild(n);
            if (armour) h.AddChild(Icons.Rect("armor", 14, acp > 0 ? Palette.Warning : Palette.TextMuted with { A = 0.5f }));
            if (key.Length > 0) h.AddChild(Fixed(Chip.Create(key, Palette.TextSecondary, fontSize: 10), 36, HorizontalAlignment.Center));
            h.AddChild(Fixed(Ui.Text(ranks.ToString(), Ui.TextStyle.Secondary, ranks > 0 ? Palette.TextSecondary : Palette.TextMuted, Palette.FontSmall + 1), 40, HorizontalAlignment.Right));
            var tl = Ui.Text(total < 0 ? "—" : total.ToString(), Ui.TextStyle.Number, total < 0 ? Palette.TextMuted : ranks > 0 ? Palette.AccentBright : Palette.Text, Palette.FontLarge);
            h.AddChild(Fixed(tl, 46, HorizontalAlignment.Right));
            int abilityMod = abil.GetValueOrDefault(key);
            row.Tooltip = () =>
            {
                var r = new List<(string, string, Color?)> { ("Ranks", ranks.ToString(), null) };
                if (key.Length > 0) r.Add(($"{key} modifier", Signed(abilityMod), null));
                if (armour && acp > 0) r.Add(("Armour check penalty", $"-{acp}", Palette.Warning));
                if (total >= 0)
                {
                    int other = total - ranks - abilityMod + (armour ? acp : 0);
                    if (other != 0) r.Add(("Feats, items and effects", Signed(other), null));
                    r.Add(("Total", total.ToString(), Palette.AccentBright));
                }
                var sub = (key.Length > 0 ? $"Key ability: {key}" : "") + (armour ? "   ·   Armour check penalty applies" : "") + (total < 0 ? "   ·   Needs training" : "");
                return Tip.Card(name, sub, S(s, "description"), icon, r);
            };
            list.AddChild(row);
            rows.Add((row, name, ranks > 0));
        }
        void Apply()
        {
            foreach (var (row, name, isTrained) in rows)
                row.Visible = (!_trainedOnly || isTrained) && (_skillFilter.Length == 0 || name.Contains(_skillFilter, StringComparison.OrdinalIgnoreCase));
        }
        search.TextChanged += t => { _skillFilter = t.Trim(); Apply(); };
        trained.Toggled += on => { _trainedOnly = on; Apply(); };
        Apply();
        return box;
    }

    static Control Fixed(Control c, int width, HorizontalAlignment align)
    {
        c.CustomMinimumSize = new Vector2(width, c.CustomMinimumSize.Y);
        if (c is Label l) l.HorizontalAlignment = align;
        c.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return c;
    }

    Control FeatsTab(JsonElement d)
    {
        var scroll = new ScrollContainer { Name = "Feats", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = Ui.VBox(10);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var padded = Ui.Pad(list, 0, 2, 10, 4);
        padded.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(padded);
        var feats = d.GetProperty("feats").EnumerateArray().ToList();
        var order = FeatCategories.Keys.ToList();
        foreach (var group in feats.GroupBy(f => FeatCategories.ContainsKey(I(f, "category")) ? I(f, "category") : 6).OrderBy(g => order.IndexOf(g.Key)))
        {
            list.AddChild(Ui.Section($"{FeatCategories[group.Key]}  ·  {group.Count()}"));
            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            list.AddChild(grid);
            foreach (var f in group)
            {
                string name = S(f, "name");
                var icon = GameIcon(S(f, "icon"));
                var tile = new TipPanel { MouseFilter = MouseFilterEnum.Stop, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
                var normal = Styles.Box(Palette.Bg2, Palette.RadiusSmall, Palette.Line, 1, 6, 4);
                tile.AddThemeStyleboxOverride("panel", normal);
                Hover(tile, normal, Styles.Box(Palette.Bg3, Palette.RadiusSmall, Palette.AccentDim, 1, 6, 4));
                var h = Ui.HBox(8);
                h.MouseFilter = MouseFilterEnum.Ignore;
                tile.AddChild(h);
                var iconFrame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
                iconFrame.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.4f), 4, Palette.Line, 1, 1));
                iconFrame.AddChild(new TextureRect { Texture = icon, CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
                h.AddChild(iconFrame);
                var n = Ui.Text(name, Ui.TextStyle.Body, null, Palette.FontSmall + 1);
                n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                n.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                n.CustomMinimumSize = new Vector2(110, 0);
                h.AddChild(n);
                int category = I(f, "category");
                tile.Tooltip = () => Tip.Card(name, FeatCategories.GetValueOrDefault(category, "Other") + " feat", S(f, "description"), icon, width: 380);
                grid.AddChild(tile);
            }
        }
        if (feats.Count == 0) list.AddChild(Ui.Text("No feats.", Ui.TextStyle.Secondary));
        return scroll;
    }

    Control DetailsTab(JsonElement d)
    {
        var scroll = new ScrollContainer { Name = "Details", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var box = Ui.VBox(12);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var padded = Ui.Pad(box, 0, 2, 10, 4);
        padded.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(padded);
        var top = Ui.HBox(18);
        box.AddChild(top);
        var facts = Ui.VBox(4);
        facts.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(facts);
        facts.AddChild(Ui.Section("Identity"));
        void Fact(string label, string value)
        {
            if (value.Length == 0) return;
            var row = Ui.HBox(8);
            var l = Ui.Text(label, Ui.TextStyle.Small);
            l.CustomMinimumSize = new Vector2(84, 0);
            row.AddChild(l);
            var v = Ui.Text(value, Ui.TextStyle.Body, null, Palette.FontSmall + 1);
            v.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            v.CustomMinimumSize = new Vector2(150, 0);
            row.AddChild(v);
            facts.AddChild(row);
        }
        Fact("Race", S(d, "baseRace") is { Length: > 0 } br ? br : S(d, "race"));
        Fact("Subrace", S(d, "subrace"));
        Fact("Gender", S(d, "gender"));
        Fact("Age", I(d, "age") > 0 ? I(d, "age").ToString() : "");
        Fact("Deity", S(d, "deity") is { Length: > 0 } deity ? deity : "None");
        Fact("Alignment", S(d, "alignment"));

        // Alignment grid with the character's place in it.
        var align = Ui.VBox(4);
        top.AddChild(align);
        align.AddChild(Ui.Section("Alignment"));
        align.AddChild(new AlignmentGrid { LawChaos = I(d, "lawChaos", 50), GoodEvil = I(d, "goodEvil", 50), CustomMinimumSize = new Vector2(150, 150), TooltipText = $"Law–chaos {I(d, "lawChaos", 50)}, good–evil {I(d, "goodEvil", 50)}" });

        box.AddChild(Ui.Section("Description"));
        var text = S(d, "description");
        var desc = Ui.Text(text.Length > 0 ? text : "No description.", text.Length > 0 ? Ui.TextStyle.Reading : Ui.TextStyle.ReadingItalic, null, 18);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        desc.CustomMinimumSize = new Vector2(360, 0);
        box.AddChild(desc);
        return scroll;
    }

    /// <summary>The nine alignments as a 3x3 grid (lawful at the left, good at the top) with a marker at the character's values.</summary>
    partial class AlignmentGrid : Control
    {
        public int LawChaos = 50, GoodEvil = 50;

        public override void _Draw()
        {
            float cell = Math.Min(Size.X, Size.Y) / 3f;
            var font = Fonts.UiBold;
            string[] labels = { "LG", "NG", "CG", "LN", "N", "CN", "LE", "NE", "CE" };
            int col = LawChaos >= 70 ? 0 : LawChaos <= 30 ? 2 : 1, row = GoodEvil >= 70 ? 0 : GoodEvil <= 30 ? 2 : 1;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    var rect = new Rect2(c * cell + 2, r * cell + 2, cell - 4, cell - 4);
                    bool on = r == row && c == col;
                    DrawStyleBox(Styles.Box(on ? Palette.Accent with { A = 0.22f } : new Color(0, 0, 0, 0.3f), 6, on ? Palette.Accent : Palette.Line, 1), rect);
                    var s = labels[r * 3 + c];
                    var w = font.GetStringSize(s, HorizontalAlignment.Left, -1, 12).X;
                    DrawString(font, rect.GetCenter() + new Vector2(-w / 2, 4), s, HorizontalAlignment.Left, -1, 12, on ? Palette.AccentBright : Palette.TextMuted);
                }
            // Precise position: law (100) at the left, good (100) at the top.
            var p = new Vector2((100 - LawChaos) / 100f * cell * 3, (100 - GoodEvil) / 100f * cell * 3);
            p = p.Clamp(new Vector2(6, 6), new Vector2(cell * 3 - 6, cell * 3 - 6));
            DrawCircle(p, 5, Palette.AccentBright);
            DrawArc(p, 7, 0, Mathf.Tau, 24, Palette.TextOnAccent, 1.5f, true);
        }
    }

    /// <summary>The character level in a brass ring; glows when a level up is waiting.</summary>
    partial class LevelMedallion : Control
    {
        public int Level;
        public bool Glow;

        public override void _Draw()
        {
            var c = Size / 2;
            float r = Math.Min(Size.X, Size.Y) / 2 - 3;
            if (Glow) { DrawCircle(c, r + 3, Palette.AccentGlow); DrawCircle(c, r + 1.5f, Palette.AccentGlow); }
            DrawCircle(c, r, Palette.Bg0);
            DrawArc(c, r, 0, Mathf.Tau, 64, Glow ? Palette.AccentBright : Palette.Accent, 2f, true);
            DrawArc(c, r - 5, 0, Mathf.Tau, 64, Palette.AccentDim with { A = 0.6f }, 1f, true);
            var font = Fonts.DisplayBold;
            var cap = Fonts.UiBold;
            string s = Level.ToString();
            int size = s.Length > 1 ? 24 : 28;
            var w = font.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
            DrawString(font, new Vector2(c.X - w / 2, c.Y + 19), s, HorizontalAlignment.Left, -1, size, Palette.AccentBright);
            var lw = cap.GetStringSize("LEVEL", HorizontalAlignment.Left, -1, 9).X;
            DrawString(cap, new Vector2(c.X - lw / 2, c.Y - 11), "LEVEL", HorizontalAlignment.Left, -1, 9, Palette.TextSecondary);
        }
    }
}
