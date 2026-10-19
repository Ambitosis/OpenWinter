using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>
/// Level-up from the character sheet: class (prestige classes with their unmet requirements),
/// ability increase, class options, skills, feats, spells, confirm. The server plans and checks
/// every step ("levelup-data" / "levelup-plan") and applies the level ("levelup-apply").
/// Laid out by <see cref="WizardFrame"/> like character creation.
/// </summary>
public partial class LevelUpWindow : UiTheme.Window
{
    public enum Step { Class, Ability, Options, Skills, Feats, Spells, Confirm }
    static readonly string[] StepNames = { "Class", "Ability", "Class options", "Skills", "Feats", "Spells", "Confirm" };
    static readonly string[] StepHints =
    {
        "Advance a class you have, or take a new one. Prestige classes appear once you meet their requirements.",
        "Every fourth level raises one ability score by one point.",
        "Choices this class makes at its first level.",
        "Spend this level's skill points. Class skills cost one point per rank, cross-class skills two.",
        "Choose the feats this level gives you. Double-click to add or remove.",
        "Choose the spells this level adds.",
        "Check the new level, then confirm it.",
    };

    public ClientSession Session = null!;
    ChargenModel? _model;
    LevelSteps? _steps;
    LevelChoice _choice = new();
    public LevelChoice Choice => _choice;
    LevelPlanView? _plan;
    JsonElement? _summary;
    string? _pendingAutoPart;
    Step _step;
    public Step Current => _step;
    readonly Dictionary<(string, int), (string Name, string Text)> _descCache = new();
    (string, int) _describing;
    WizardFrame _f = null!;
    List<Step> _shownSteps = new();

    public static LevelUpWindow Create(ClientSession session)
    {
        var w = Create<LevelUpWindow>("Level up", new Vector2(1180, 680));
        w.Session = session;
        // Opaque: these windows sit over the selection screen and the character sheet.
        w.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg1, Palette.RadiusLarge, Palette.LineStrong, 1, 0, 0, shadow: 22, shadowAlpha: 0.5f));
        w.Material = null;
        w.AnchorLeft = w.AnchorRight = w.AnchorTop = w.AnchorBottom = 0.5f;
        w.OffsetLeft = -590; w.OffsetRight = 590; w.OffsetTop = -340; w.OffsetBottom = 340;
        return w;
    }

    public override void _Ready()
    {
        // The HUD is a CanvasLayer, which stops the root window's theme reaching its windows.
        Theme = GameTheme.Theme;
        _f = WizardFrame.Build(Body, "Level up", summary: true);
        _f.Cancel.Pressed += () => Close();
        _f.Back.Pressed += () => Go(-1);
        _f.Next.Pressed += () => { if (_step == Step.Confirm) Apply(); else Go(+1); };
        _f.Recommend.Pressed += () => Recommend(_step switch { Step.Skills => "skills", Step.Feats => "feats", Step.Spells => "spells", _ => "all" });
        _f.Rail.StepClicked += i => { if (i < _shownSteps.Count) GoTo(_shownSteps[i]); };
        _f.SetDescription("Level up", "Choose a class to advance. Select a class to read about it; classes you can't take yet show what they need.", null);
        Session.NotifyReceived += OnNotify;
    }

    /// <summary>Opens at the class step for the controlled character.</summary>
    public void Open()
    {
        Visible = true;
        _choice = new LevelChoice();
        _plan = null;
        _step = Step.Class;
        if (_model == null) Session.Request("chargen-data");
        Session.Request("levelup-data");
        Render();
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "chargen-data":
                if (_model != null) return;
                _model = ChargenModel.Parse(m.Json);
                _steps = new LevelSteps
                {
                    Model = _model, Content = Session.Content, Choice = () => _choice, Plan = () => _plan,
                    Changed = () => { Validate(); Render(); }, Describe = Describe, Recommend = Recommend,
                };
                if (Visible) Render();
                break;
            case "levelup-data" when Visible:
                _summary = JsonDocument.Parse(m.Json).RootElement.Clone();
                Render();
                break;
            case "levelup-plan" when Visible:
            {
                using var doc = JsonDocument.Parse(m.Json);
                _plan = LevelPlanView.Parse(doc.RootElement);
                if (_plan.Auto is { } auto && _pendingAutoPart != null)
                {
                    var completed = LevelChoice.FromJson(auto);
                    if (_pendingAutoPart is "skills" or "all") _choice.Skills = completed.Skills;
                    if (_pendingAutoPart is "feats" or "all") _choice.Feats = completed.Feats;
                    if (_pendingAutoPart is "spells" or "all") _choice.Spells = completed.Spells;
                    if (_pendingAutoPart == "all") { _choice.Ability = completed.Ability; _choice.Domain1 = completed.Domain1; _choice.Domain2 = completed.Domain2; _choice.School = completed.School; }
                    _pendingAutoPart = null;
                    Validate();
                }
                Render();
                break;
            }
            case "levelup-result" when Visible:
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                if (r.GetProperty("ok").GetBoolean()) { Close(); return; }
                _f.ShowErrors(r.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("message").GetString() ?? ""));
                break;
            }
            case "chargen-describe":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                var key = (r.GetProperty("kind").GetString() ?? "", r.GetProperty("id").GetInt32());
                _descCache[key] = (r.GetProperty("name").GetString() ?? "", r.GetProperty("text").GetString() ?? "");
                if (_describing == key) ShowDescription(key);
                break;
            }
        }
    }

    void Validate(bool auto = false) => Session.Request("levelup-data", new { choice = _choice.ToJson(), auto });

    public void Recommend(string part)
    {
        _pendingAutoPart = part;
        if (part is "skills" or "all") _choice.Skills.Clear();
        if (part is "feats" or "all") _choice.Feats.Clear();
        if (part is "spells" or "all") _choice.Spells.Clear();
        Validate(auto: true);
    }

    void Describe(string kind, int id)
    {
        _describing = (kind, id);
        if (_descCache.ContainsKey((kind, id))) ShowDescription((kind, id));
        else Session.Request("chargen-describe", new { kind, id });
    }

    void ShowDescription((string Kind, int Id) key)
    {
        var (name, text) = _descCache[key];
        string icon = key.Kind switch
        {
            "feat" => _model?.Feats.GetValueOrDefault(key.Id)?.Icon ?? "",
            "spell" => _model?.Spells.GetValueOrDefault(key.Id)?.Icon ?? "",
            "skill" => _model?.Skills.GetValueOrDefault(key.Id)?.Icon ?? "",
            "domain" => _model?.Domains.GetValueOrDefault(key.Id)?.Icon ?? "",
            _ => "",
        };
        _f.SetDescription(name, text, ChargenModel.Icon(Session.Content, icon));
    }

    bool StepShown(Step s) => s switch
    {
        Step.Ability => _plan?.AbilityIncrease == true,
        Step.Options => _plan?.HasOptions == true,
        Step.Spells => _plan?.HasSpells == true,
        _ => true,
    };

    List<string> StepErrors(Step s)
    {
        var p = _plan;
        if (s == Step.Class) return _choice.Class < 0 ? new() { "Choose a class." } : p?.ErrorsFor("class", "prereq", "alignment").ToList() ?? new();
        if (p == null) return new() { "Waiting for the server..." };
        return s switch
        {
            Step.Ability => p.ErrorsFor("ability").ToList(),
            Step.Options => p.ErrorsFor("domains", "school", "familiar", "companion").ToList(),
            Step.Skills => p.ErrorsFor("skills").ToList(),
            Step.Feats => p.ErrorsFor("feats", "feat-prereq", "feats-unspent").ToList(),
            Step.Spells => p.ErrorsFor("spells").ToList(),
            Step.Confirm => p.Errors.Select(e => e.Message).ToList(),
            _ => new(),
        };
    }

    void Go(int dir)
    {
        if (dir > 0 && StepErrors(_step) is { Count: > 0 } errs) { _f.ShowErrors(errs); return; }
        var s = _step;
        do s = (Step)Math.Clamp((int)s + dir, 0, (int)Step.Confirm);
        while (!StepShown(s) && s != Step.Confirm && s != Step.Class);
        GoTo(s);
    }

    public void GoTo(Step s)
    {
        _step = s;
        Render();
    }

    public void SetClass(int cls)
    {
        if (_choice.Class != cls) _choice = new LevelChoice { Class = cls };
        if (_model != null && _model.Classes.TryGetValue(cls, out var c)) _f.SetDescription(c.Name, c.Description, ChargenModel.Icon(Session.Content, c.Icon));
        Validate();
        Render();
    }

    public void SetAbility(int a)
    {
        _choice.Ability = a;
        _choice.Skills.Clear();
        _choice.Feats.Clear();
        Validate();
        Render();
    }

    void Render()
    {
        if (!IsInsideTree()) return;
        _shownSteps = Enumerable.Range(0, (int)Step.Confirm + 1).Select(i => (Step)i).Where(StepShown).ToList();
        var entries = new List<StepRail.Entry>();
        for (int i = 0; i < _shownSteps.Count; i++)
        {
            var s = _shownSteps[i];
            bool reachable = s <= _step || !_shownSteps.Take(i).Any(x => StepErrors(x).Count > 0);
            entries.Add(new StepRail.Entry(StepNames[(int)s], s < _step, s == _step, reachable));
        }
        int level = _summary is { } sum ? sum.GetProperty("level").GetInt32() + 1 : 0;
        _f.Caption.Text = level > 0 ? $"LEVEL UP  ·  LEVEL {level}" : "LEVEL UP";
        _f.SetSteps(entries, Math.Max(0, _shownSteps.IndexOf(_step)), StepNames[(int)_step], StepHints[(int)_step]);
        TitleLabel.Text = level > 0 ? $"Level up  —  level {level}" : "Level up";
        RenderSummary(level);

        var content = _f.Content;
        foreach (var c in content.GetChildren()) { content.RemoveChild(c); c.QueueFree(); }
        if (_model == null || _steps == null || _summary == null) content.AddChild(Ui.Text("Loading…", Ui.TextStyle.Secondary));
        else switch (_step)
        {
            case Step.Class: RenderClasses(); break;
            case Step.Ability: RenderAbility(); break;
            case Step.Options: content.AddChild(_steps.Options()); break;
            case Step.Skills: content.AddChild(_steps.Skills()); break;
            case Step.Feats: content.AddChild(_steps.Feats()); break;
            case Step.Spells: content.AddChild(_steps.Spells()); break;
            case Step.Confirm: RenderConfirm(); break;
        }
        _f.Back.Disabled = _step == Step.Class;
        _f.Next.Text = _step == Step.Confirm ? "Level up" : "Next";
        _f.Recommend.Visible = _model?.Recommended == true && _step is Step.Skills or Step.Feats or Step.Spells;
        _f.ShowErrors(Array.Empty<string>());
    }

    void RenderSummary(int level)
    {
        var box = _f.SummaryBox;
        foreach (var c in box.GetChildren()) { box.RemoveChild(c); c.QueueFree(); }
        if (_summary is not { } s || _model == null) { _f.SummaryCard.Visible = false; return; }
        _f.SummaryCard.Visible = true;
        var row = Ui.HBox(12);
        box.AddChild(row);
        row.AddChild(new LevelBadge { Level = level, CustomMinimumSize = new Vector2(56, 56) });
        var v = Ui.VBox(2);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(v);
        v.AddChild(Ui.Text(_choice.Class >= 0 ? $"{_model.ClassName(_choice.Class)} {(_plan?.NewClassLevel > 0 ? _plan.NewClassLevel : "")}" : "Choose a class", Ui.TextStyle.Title, _choice.Class >= 0 ? null : Palette.TextMuted));
        v.AddChild(Ui.Text($"Character level {level}", Ui.TextStyle.Small));
        if (s.TryGetProperty("xp", out var xp) && s.TryGetProperty("nextLevelXp", out var next))
            v.AddChild(Ui.Text($"{xp.GetInt32():N0} / {next.GetInt32():N0} XP", Ui.TextStyle.Caption));
    }

    void RenderClasses()
    {
        var m = _model!;
        var rows = _summary!.Value.GetProperty("classes").EnumerateArray()
            .Select(c => (Id: c.GetProperty("id").GetInt32(), Level: c.GetProperty("level").GetInt32(), Available: c.GetProperty("available").GetBoolean(),
                Reasons: c.GetProperty("reasons").EnumerateArray().Select(x => x.GetString() ?? "").ToList()))
            .OrderByDescending(c => c.Level).ThenByDescending(c => c.Available).ThenBy(c => m.ClassName(c.Id)).ToList();
        var (scroll, grid) = WizardFrame.CardGrid(2);
        _f.Content.AddChild(scroll);
        bool newHeader = false;
        foreach (var c in rows)
        {
            var e = m.Classes.GetValueOrDefault(c.Id);
            string sub = c.Level > 0 ? $"Level {c.Level}  →  {c.Level + 1}" : c.Available ? $"New class  ·  d{e?.Int("hitDie")} hit die" : "Requirements not met";
            var card = SelectCard.Create(m.ClassName(c.Id), sub, ChargenModel.Icon(Session.Content, e?.Icon ?? ""), 36);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.Selected = c.Id == _choice.Class;
            card.Disabled = !c.Available;
            if (e?.Bool("prestige") == true) card.Trailing.AddChild(Chip.Create("Prestige", Palette.Plot));
            if (c.Level > 0) card.Trailing.AddChild(Chip.Create("Current", Palette.Accent));
            var cls = c;
            if (!c.Available)
                card.Tooltip = () => Tip.Card(m.ClassName(cls.Id), "Not available yet", footer: string.Join("\n", cls.Reasons), width: 300);
            card.Pressed += () =>
            {
                if (cls.Available) SetClass(cls.Id);
                else _f.SetDescription(m.ClassName(cls.Id), "Not available yet:\n" + string.Join("\n", cls.Reasons.Select(r => "•  " + r)) + "\n\n" + (e?.Description ?? ""), ChargenModel.Icon(Session.Content, e?.Icon ?? ""));
            };
            card.DoubleClicked += () => { if (cls.Available) { SetClass(cls.Id); Go(+1); } };
            if (c.Level == 0 && !newHeader) newHeader = true;
            grid.AddChild(card);
        }
    }

    void RenderAbility()
    {
        var abilities = _summary!.Value.GetProperty("abilities").EnumerateArray().Select(a => a.GetInt32()).ToArray();
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _f.Content.AddChild(grid);
        for (int i = 0; i < 6; i++)
        {
            int a = i;
            bool on = _choice.Ability == a;
            var card = SelectCard.Create(ChargenModel.AbilityNames[i], on ? $"{abilities[i]}  →  {abilities[i] + 1}" : abilities[i].ToString(), vertical: true, pad: 16);
            card.CustomMinimumSize = new Vector2(0, 92);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.Selected = on;
            card.Pressed += () => SetAbility(a);
            grid.AddChild(card);
        }
    }

    void RenderConfirm()
    {
        var m = _model!;
        var p = _plan;
        if (p == null) return;
        var box = Ui.VBox(14);
        _f.Content.AddChild(box);
        var hero = new PanelContainer();
        hero.AddThemeStyleboxOverride("panel", Styles.Card(14));
        box.AddChild(hero);
        var row = Ui.HBox(16);
        hero.AddChild(row);
        if (ChargenModel.Icon(Session.Content, m.Classes.GetValueOrDefault(_choice.Class)?.Icon ?? "") is { } icon)
            row.AddChild(new TextureRect { Texture = icon, CustomMinimumSize = new Vector2(56, 56), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var v = Ui.VBox(4);
        row.AddChild(v);
        v.AddChild(Ui.Text($"{m.ClassName(_choice.Class)} {p.NewClassLevel}", Ui.TextStyle.Heading));
        v.AddChild(Ui.Text($"Character level {p.NewLevel}   ·   hit points up to +{p.HitDie} plus Constitution", Ui.TextStyle.Secondary));
        if (p.Abilities.Length == 6)
        {
            var stats = Ui.HBox(8);
            foreach (var (label, value) in new[] { ("BAB", ChargenModel.Signed(p.Bab)), ("FORT", ChargenModel.Signed(p.Fortitude)), ("REF", ChargenModel.Signed(p.Reflex)), ("WILL", ChargenModel.Signed(p.Will)) })
            {
                var tile = new PanelContainer { CustomMinimumSize = new Vector2(70, 0) };
                tile.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.RadiusSmall, Palette.Line, 1, 8, 5));
                var t = Ui.VBox(0);
                var l = Ui.Text(label, Ui.TextStyle.Caption, Palette.TextSecondary, 10);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                t.AddChild(l);
                var n = Ui.Text(value, Ui.TextStyle.Display, null, 20);
                n.HorizontalAlignment = HorizontalAlignment.Center;
                t.AddChild(n);
                tile.AddChild(t);
                stats.AddChild(tile);
            }
            box.AddChild(stats);
        }
        void Line(string title, IEnumerable<string> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            box.AddChild(Ui.Section(title));
            var text = Ui.Text(string.Join("   ·   ", list), Ui.TextStyle.Body, null, Palette.FontSmall + 1);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = new Vector2(420, 0);
            box.AddChild(text);
        }
        if (_choice.Ability >= 0) Line("Ability increase", new[] { $"{ChargenModel.AbilityNames[_choice.Ability]} +1" });
        Line("Skills" + (p.SkillLeft > 0 ? $"  ·  {p.SkillLeft} points saved" : ""), _choice.Skills.Where(k => k.Value > 0).Select(k => $"{m.SkillName(k.Key)} +{k.Value}"));
        Line("Feats", p.Granted.Concat(_choice.Feats).Select(m.FeatName));
        Line("Spells", _choice.Spells.SelectMany(k => k.Value).Select(m.SpellName));
        if (p.Errors.Count > 0)
        {
            var e = new PanelContainer();
            e.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Warning with { A = 0.08f }, Palette.RadiusSmall, Palette.Warning with { A = 0.4f }, 1, 12, 8));
            var t = Ui.Text("Still to do:\n" + string.Join("\n", p.Errors.Select(x => "•  " + x.Message).Distinct().Take(6)), Ui.TextStyle.Small, Palette.Warning);
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            t.CustomMinimumSize = new Vector2(380, 0);
            e.AddChild(t);
            box.AddChild(e);
        }
    }

    public void Apply()
    {
        if (StepErrors(Step.Confirm) is { Count: > 0 } errs) { _f.ShowErrors(errs); return; }
        Session.Request("levelup-apply", new { choice = _choice.ToJson() });
    }

    /// <summary>The new level in a brass ring.</summary>
    partial class LevelBadge : Control
    {
        public int Level;

        public override void _Draw()
        {
            var c = Size / 2;
            float r = Math.Min(Size.X, Size.Y) / 2 - 2;
            DrawCircle(c, r + 2, Palette.AccentGlow);
            DrawCircle(c, r, Palette.Bg0);
            DrawArc(c, r, 0, Mathf.Tau, 48, Palette.AccentBright, 2f, true);
            var font = Fonts.DisplayBold;
            var s = Level.ToString();
            var w = font.GetStringSize(s, HorizontalAlignment.Left, -1, 24).X;
            DrawString(font, new Vector2(c.X - w / 2, c.Y + 9), s, HorizontalAlignment.Left, -1, 24, Palette.AccentBright);
        }
    }
}
