using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Top-left: the player's portrait, name, level, hit points and experience, with the active
/// effect icons beneath. The level badge glows when a level-up is waiting; clicking it opens the level-up.
/// </summary>
public partial class PlayerFrame : PanelContainer
{
    public ClientSession Session = null!;
    public HudState State = null!;
    public Action? OpenCharacter, OpenLevelUp;
    public Func<uint, bool>? TargetPortrait;
    /// <summary>Effect icons go here (PlayerFx fills it).</summary>
    public HBoxContainer Effects = null!;

    TextureRect _portrait = null!;
    Label _name = null!, _level = null!;
    PanelContainer _badge = null!;
    Bar _hp = null!, _xp = null!;
    string _shownPortrait = "\0";
    double _pulse;
    StyleBoxFlat? _glow;

    public override void _Ready()
    {
        Ui.Glass(this, Styles.Hud(10, Palette.Radius));
        MouseFilter = MouseFilterEnum.Stop;
        var row = Ui.HBox(12);
        AddChild(row);

        var portraitHolder = new PanelContainer { MouseFilter = MouseFilterEnum.Pass, TooltipText = "Character (C)" };
        var ring = Styles.Box(Colors.Transparent, 9, Palette.AccentDim, 1, 2, 2);
        portraitHolder.AddThemeStyleboxOverride("panel", ring);
        _portrait = Portraits.View(new Vector2(64, 80), 7);
        portraitHolder.AddChild(_portrait);
        portraitHolder.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
            && TargetPortrait?.Invoke(Session.ControlledId) != true) OpenCharacter?.Invoke();
        };
        row.AddChild(portraitHolder);

        var col = Ui.VBox(6);
        col.CustomMinimumSize = new Vector2(236, 0);
        col.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(col);

        var top = Ui.HBox(8);
        col.AddChild(top);
        _name = Ui.Text("", Ui.TextStyle.Title, Palette.Text, 17);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _name.ClipText = true;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        top.AddChild(_name);
        _badge = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = "Level" };
        _badge.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.45f), 10, Palette.AccentDim, 1, 8, 1));
        _level = Ui.Text("", Ui.TextStyle.Caption, Palette.AccentBright, 12);
        _level.AddThemeFontOverride("font", Fonts.UiBold);
        _badge.AddChild(_level);
        _badge.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && State.CanLevelUp) OpenLevelUp?.Invoke(); };
        top.AddChild(_badge);

        _hp = new Bar { CustomMinimumSize = new Vector2(0, 20), FontSize = 12 };
        col.AddChild(_hp);
        _xp = new Bar
        {
            CustomMinimumSize = new Vector2(0, 5), ShowText = false, Radius = 2,
            Fill = Palette.Xp.Darkened(0.15f), FillTop = Palette.Xp.Lightened(0.3f), Trail = Palette.Xp, Track = new Color(0, 0, 0, 0.55f),
        };
        _xp.MouseFilter = MouseFilterEnum.Pass;
        col.AddChild(_xp);

        Effects = Ui.HBox(4);
        Effects.CustomMinimumSize = new Vector2(0, 0);
        col.AddChild(Effects);

        State.Changed += Refresh;
        Refresh();
    }

    void Refresh()
    {
        var self = State.Self;
        Visible = self != null;
        if (self == null) return;
        _name.Text = self.Name;
        _level.Text = State.CanLevelUp ? $"LEVEL {State.Level}  ▲" : $"LEVEL {State.Level}";
        _badge.TooltipText = State.CanLevelUp ? "A new level is waiting: click to level up" : $"Level {State.Level}";
        _hp.Set(Math.Max(0, self.Hp), self.MaxHp, self.TempHp);
        _hp.TextOverride = self.TempHp > 0 ? $"{self.Hp} (+{self.TempHp}) / {self.MaxHp}" : $"{self.Hp} / {self.MaxHp}";
        int span = Math.Max(1, State.XpNext - State.XpLevel);
        _xp.Set(Math.Clamp(State.Xp - State.XpLevel, 0, span), span);
        _xp.TooltipText = $"Experience {State.Xp:N0} / {State.XpNext:N0}";
        string key = self.Portrait + (self.Dead ? "!" : "");
        if (key != _shownPortrait)
        {
            _shownPortrait = key;
            Portraits.Show(_portrait, Session.Content, self.Portrait, self.Dead);
        }
    }

    public override void _Process(double delta)
    {
        if (!State.CanLevelUp)
        {
            if (_glow != null) { _glow = null; _badge.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.45f), 10, Palette.AccentDim, 1, 8, 1)); }
            return;
        }
        _pulse += delta * 3;
        float a = 0.65f + 0.35f * MathF.Sin((float)_pulse);
        _glow ??= Styles.Box(Palette.Accent, 10, Palette.AccentBright, 1, 8, 1);
        _glow.BgColor = Palette.Accent with { A = 0.25f + 0.2f * a };
        if (_badge.GetThemeStylebox("panel") != _glow) _badge.AddThemeStyleboxOverride("panel", _glow);
    }
}

/// <summary>Party members and associates under the player frame: portrait, name and health; click selects.</summary>
public partial class PartyFrames : VBoxContainer
{
    public ClientSession Session = null!;
    public HudState State = null!;
    public Func<uint, bool>? TargetPortrait;
    /// <summary>Right-click on a member: its radial menu (an associate's commands, docs/ASSOCIATE_COMMANDS.md).</summary>
    public Action<uint, Vector2>? OpenRadial;
    string _layout = "";
    readonly Dictionary<uint, (Bar Hp, TextureRect Portrait, Label Name, PanelContainer Row)> _rows = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        MouseFilter = MouseFilterEnum.Ignore;
        State.Changed += Refresh;
    }

    void Refresh()
    {
        var layout = string.Join(",", State.Party.Select(p => p.Id));
        if (layout != _layout)
        {
            _layout = layout;
            foreach (var c in GetChildren()) c.QueueFree();
            _rows.Clear();
            foreach (var p in State.Party) _rows[p.Id] = Build(p);
        }
        foreach (var p in State.Party)
        {
            if (!_rows.TryGetValue(p.Id, out var r)) continue;
            r.Hp.Set(Math.Max(0, p.Hp), p.MaxHp);
            r.Name.Text = (p.Id == State.Leader ? "★ " : "") + p.Name;
            r.Row.AddThemeStyleboxOverride("panel", Styles.Hud(6, 8) is var s && State.Selected == p.Id ? WithBorder(s) : s);
            Portraits.Show(r.Portrait, Session.Content, p.Portrait, p.Dead);
        }
    }

    static StyleBoxFlat WithBorder(StyleBoxFlat s) { s.BorderColor = Palette.Accent; return s; }

    (Bar, TextureRect, Label, PanelContainer) Build(Vitals p)
    {
        var row = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(214, 0) };
        Ui.Glass(row, Styles.Hud(6, 8));
        uint id = p.Id;
        row.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
                && TargetPortrait?.Invoke(id) != true) State.Select(id);
            else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right)
            {
                OpenRadial?.Invoke(id, right.GlobalPosition);
                row.AcceptEvent();
            }
        };
        var h = Ui.HBox(8);
        row.AddChild(h);
        var portrait = Portraits.View(new Vector2(30, 38), 5);
        h.AddChild(portrait);
        var col = Ui.VBox(4);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(col);
        var name = Ui.Text(p.Name, Ui.TextStyle.Small, Palette.Text, 13);
        name.ClipText = true;
        col.AddChild(name);
        var bar = new Bar { CustomMinimumSize = new Vector2(0, 7), ShowText = false, Radius = 3 };
        col.AddChild(bar);
        row.TooltipText = p.Associate switch { 1 => "Henchman", 2 => "Animal companion", 3 => "Familiar", 4 => "Summoned", 5 => "Dominated", _ => p.Pc ? "Party member" : "" };
        AddChild(row);
        return (bar, portrait, name, row);
    }
}

/// <summary>Top centre: the selected creature or object — name in its attitude colour, level and health.</summary>
public partial class TargetFrame : PanelContainer
{
    public ClientSession Session = null!;
    public HudState State = null!;
    TextureRect _portrait = null!;
    Label _name = null!, _sub = null!;
    Bar _hp = null!;
    string _shownPortrait = "\0";

    public override void _Ready()
    {
        Ui.Glass(this, Styles.Hud(8, Palette.Radius));
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(340, 0);
        var row = Ui.HBox(10);
        AddChild(row);
        _portrait = Portraits.View(new Vector2(40, 40), 20);
        row.AddChild(_portrait);
        var col = Ui.VBox(4);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(col);
        var top = Ui.HBox(8);
        col.AddChild(top);
        _name = Ui.Text("", Ui.TextStyle.Title, Palette.Text, 16);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _name.ClipText = true;
        top.AddChild(_name);
        _sub = Ui.Text("", Ui.TextStyle.Caption);
        top.AddChild(_sub);
        _hp = new Bar { CustomMinimumSize = new Vector2(0, 12), FontSize = 10 };
        col.AddChild(_hp);
        State.Changed += Refresh;
        Refresh();
    }

    static readonly string[] HealthWords = ["Dead", "Near death", "Badly wounded", "Wounded", "Barely injured", "Uninjured"];

    void Refresh()
    {
        var t = State.Target;
        Visible = t != null;
        if (t == null) return;
        var color = t.Attitude switch { "hostile" => Palette.Hostile, "friendly" => Palette.Friendly, "neutral" => Palette.Neutral, _ => Palette.Text };
        _name.Text = t.Name;
        _name.AddThemeColorOverride("font_color", color);
        _sub.Text = t.Level > 0 ? $"LEVEL {t.Level}" : "";
        _hp.Fill = t.Attitude == "friendly" ? Palette.Success.Darkened(0.25f) : t.Attitude == "neutral" ? Palette.Warning.Darkened(0.3f) : Palette.Health;
        _hp.FillTop = _hp.Fill.Lightened(0.35f);
        if (t.Hp >= 0) { _hp.Set(t.Hp, t.MaxHp); _hp.TextOverride = null; }
        else
        {
            _hp.Set(t.Health * 100, 100);
            int word = t.Dead ? 0 : t.Health >= 1 ? 5 : t.Health >= 0.75f ? 4 : t.Health >= 0.5f ? 3 : t.Health >= 0.25f ? 2 : 1;
            _hp.TextOverride = HealthWords[word];
        }
        bool hasPortrait = t.Portrait.Length > 0;
        _portrait.Visible = hasPortrait;
        if (hasPortrait && t.Portrait + t.Dead != _shownPortrait)
        {
            _shownPortrait = t.Portrait + t.Dead;
            Portraits.Show(_portrait, Session.Content, t.Portrait, t.Dead);
        }
    }
}
