using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>
/// New character wizard, NWN style: gender, race, portrait, class, alignment, abilities (point
/// buy), package, class options, skills, feats, spells, appearance (live 3D preview), details,
/// finish. Every rule comes from the server ("chargen-data", "chargen-validate" plans), so a
/// module's hak 2DAs decide what is offered; the server builds and saves the character.
/// Laid out by <see cref="WizardFrame"/>: step rail, step content, summary and descriptions.
/// </summary>
public partial class CharGenWindow : UiTheme.Window
{
    public enum Step { Gender, Race, Portrait, Class, Alignment, Abilities, Package, Options, Skills, Feats, Spells, Appearance, Details, Finish }
    static readonly string[] StepNames = { "Gender", "Race", "Portrait", "Class", "Alignment", "Abilities", "Package", "Class options", "Skills", "Feats", "Spells", "Appearance", "Details", "Finish" };
    static readonly string[] StepHints =
    {
        "Gender decides the body models, portraits and voices offered. Both are equal in the rules.",
        "Your race sets ability adjustments, racial feats, skill points and the favoured class. Double-click to choose and continue.",
        "The picture other players see for your character.",
        "Your first class. Prestige classes open up later, through level-up.",
        "Your character's moral and ethical outlook. Some classes require particular alignments.",
        "Spend your points on the six abilities. Higher scores cost more; racial adjustments apply on top.",
        "A package is a ready-made build for your class. Recommended lets it choose your skills, feats and spells.",
        "Choices your class makes at its first level.",
        "Class skills cost one point per rank, cross-class skills two. Unspent points are saved for later levels.",
        "Select a feat to read about it; double-click to add or remove it.",
        "Choose the spells your character starts with.",
        "Head, body, colours and voice. Drag the preview to turn your character.",
        "Your name is how other players will know you. Deity, age and description are for role-play.",
        "Check your character, then create it. It is saved to your vault and enters the game.",
    };

    public ClientSession Session = null!;
    /// <summary>The player left the wizard (back to character selection).</summary>
    public Action? Cancelled;

    ChargenModel? _model;
    public ChargenModel? Model => _model;
    readonly NewCharacter _n = new();
    public NewCharacter Character => _n;
    LevelPlanView? _plan;
    public LevelPlanView? Plan => _plan;
    List<(string Code, string Message)> _identity = new();
    int _pointsSpent, _budget;
    Step _step = Step.Gender;
    public Step Current => _step;
    readonly List<(int Id, string ResRef)> _portraits = new();
    (int Race, int Gender) _portraitsFor = (-1, -1);
    readonly Dictionary<(string, int), (string Name, string Text)> _descCache = new();
    (string Kind, int Id) _describing;
    string? _pendingAutoPart;
    bool _creating;
    LevelSteps _steps = null!;
    WizardFrame _f = null!;
    List<Step> _shownSteps = new();
    PreviewStage? _stage;
    string _previewKey = "";
    /// <summary>The live 3D preview of the character (shown on every step).</summary>
    public CreaturePreview? Preview => _stage?.Preview;

    public static CharGenWindow Create(ClientSession session)
    {
        var w = Create<CharGenWindow>("New character", new Vector2(1240, 700));
        w.Session = session;
        // Opaque: these windows sit over the selection screen and the character sheet.
        w.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg1, Palette.RadiusLarge, Palette.LineStrong, 1, 0, 0, shadow: 22, shadowAlpha: 0.5f));
        w.Material = null;
        w.AnchorLeft = w.AnchorRight = w.AnchorTop = w.AnchorBottom = 0.5f;
        w.OffsetLeft = -620; w.OffsetRight = 620; w.OffsetTop = -350; w.OffsetBottom = 350;
        w.Closed += () => w.Cancelled?.Invoke();
        return w;
    }

    public override void _Ready()
    {
        // The HUD is a CanvasLayer, which stops the root window's theme reaching its windows.
        Theme = GameTheme.Theme;
        _f = WizardFrame.Build(Body, "New character", summary: true);
        _f.Cancel.Pressed += () => Close();
        _f.Back.Pressed += () => Go(-1);
        _f.Next.Pressed += () => { if (_step == Step.Finish) Finish(); else Go(+1); };
        _f.Recommend.Pressed += OnRecommend;
        _f.Rail.StepClicked += i => { if (i < _shownSteps.Count) GoTo(_shownSteps[i]); };
        // The character preview lives in the side column for the whole wizard, so every choice shows on the
        // model as it is made.
        _stage = PreviewStage.Create(Session.Content, zoomButtons: false);
        _f.Stage.AddChild(_stage);
        Session.NotifyReceived += OnNotify;
    }

    /// <summary>Opens the wizard from the start (data is fetched once).</summary>
    public void Open()
    {
        Visible = true;
        _creating = false;
        if (_model == null) Session.Request("chargen-data");
        else Render();
    }

    void OnNotify(NotifyMessage m)
    {
        if (!Visible && m.Kind != "chargen-data") return;
        switch (m.Kind)
        {
            case "chargen-data":
                _model = ChargenModel.Parse(m.Json);
                _steps = new LevelSteps
                {
                    Model = _model, Content = Session.Content, Choice = () => _n.Level, Plan = () => _plan,
                    Changed = () => { Validate(); Render(); }, Describe = Describe, Recommend = part => Recommend(part),
                };
                if (_n.Race < 0) Defaults();
                Render();
                break;
            case "chargen-portraits":
            {
                using var doc = JsonDocument.Parse(m.Json);
                _portraits.Clear();
                foreach (var p in doc.RootElement.GetProperty("portraits").EnumerateArray())
                    _portraits.Add((p.GetProperty("id").GetInt32(), p.GetProperty("resref").GetString() ?? ""));
                if (_n.PortraitId < 0 && _n.Portrait.Length == 0 && _portraits.Count > 0) _n.PortraitId = _portraits[0].Id;
                if (_step == Step.Portrait) Render();
                RenderSummary();
                break;
            }
            case "chargen-plan":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var root = doc.RootElement;
                _plan = LevelPlanView.Parse(root.GetProperty("plan"));
                _identity = root.GetProperty("identity").EnumerateArray().Select(e => (e.GetProperty("code").GetString() ?? "", e.GetProperty("message").GetString() ?? "")).ToList();
                _pointsSpent = root.GetProperty("pointsSpent").GetInt32();
                _budget = root.GetProperty("pointsBudget").GetInt32();
                if (_plan.Auto is { } auto && _pendingAutoPart != null)
                {
                    var completed = LevelChoice.FromJson(auto);
                    if (_pendingAutoPart is "skills" or "all") _n.Level.Skills = completed.Skills;
                    if (_pendingAutoPart is "feats" or "all") _n.Level.Feats = completed.Feats;
                    if (_pendingAutoPart is "spells" or "all") _n.Level.Spells = completed.Spells;
                    if (_pendingAutoPart == "all")
                    {
                        _n.Level.Domain1 = completed.Domain1; _n.Level.Domain2 = completed.Domain2; _n.Level.School = completed.School;
                        _n.Level.Familiar = completed.Familiar; _n.Level.Companion = completed.Companion;
                    }
                    _pendingAutoPart = null;
                    Validate();
                }
                // Steps that don't show the plan only refresh the step list and summary (the preview stays).
                if (_step is Step.Appearance or Step.Details or Step.Gender or Step.Race or Step.Portrait) RenderChrome();
                else Render();
                break;
            }
            case "chargen-describe":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                var key = (r.GetProperty("kind").GetString() ?? "", r.GetProperty("id").GetInt32());
                _descCache[key] = (r.GetProperty("name").GetString() ?? "", r.GetProperty("text").GetString() ?? "");
                if (_describing == key) ShowDescription(key.Item1, key.Item2);
                break;
            }
            case "chargen-result":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                _creating = false;
                if (r.GetProperty("ok").GetBoolean()) { Visible = false; return; }
                _f.ShowErrors(r.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("message").GetString() ?? ""));
                _f.Next.Disabled = false;
                break;
            }
        }
    }

    void Defaults()
    {
        var m = _model!;
        _n.Gender = 0;
        _n.Race = m.Races.Keys.Contains(6) ? 6 : m.Races.Keys.FirstOrDefault(-1);
        _n.Abilities = Enumerable.Repeat(m.AbilityMin, 6).ToArray();
        _n.FirstName = "";
        _n.SoundSet = DefaultSoundSet();
        _n.BodyParts = OpenWinter.Game.Progression.CharacterFactory.DefaultBodyParts();
        _n.Colors = new[] { 0, 0, 0, 0 };
    }

    int DefaultSoundSet() => _model?.SoundSets.Values.Where(s => s.Int("gender") == _n.Gender).Select(s => s.Id).DefaultIfEmpty(-1).First() ?? -1;

    // ------------------------------------------------------------------ server round trips

    /// <summary>Asks the server for the plan of the build so far.</summary>
    void Validate(bool auto = false) =>
        Session.Request("chargen-validate", new { character = _n.ToJson(), auto });

    /// <summary>Fills one part (skills, feats, spells, or "all") from the class package.</summary>
    public void Recommend(string part)
    {
        _pendingAutoPart = part;
        if (part is "skills" or "all") _n.Level.Skills.Clear();
        if (part is "feats" or "all") _n.Level.Feats.Clear();
        if (part is "spells" or "all") _n.Level.Spells.Clear();
        Validate(auto: true);
    }

    void OnRecommend()
    {
        switch (_step)
        {
            case Step.Abilities: RecommendAbilities(); break;
            case Step.Package: case Step.Options: Recommend("all"); break;
            case Step.Skills: Recommend("skills"); break;
            case Step.Feats: Recommend("feats"); break;
            case Step.Spells: Recommend("spells"); break;
        }
    }

    void Describe(string kind, int id)
    {
        _describing = (kind, id);
        if (_descCache.ContainsKey((kind, id))) ShowDescription(kind, id);
        else Session.Request("chargen-describe", new { kind, id });
    }

    void ShowDescription(string kind, int id)
    {
        var (name, text) = _descCache[(kind, id)];
        string icon = kind switch
        {
            "feat" => _model?.Feats.GetValueOrDefault(id)?.Icon ?? "",
            "spell" => _model?.Spells.GetValueOrDefault(id)?.Icon ?? "",
            "skill" => _model?.Skills.GetValueOrDefault(id)?.Icon ?? "",
            "domain" => _model?.Domains.GetValueOrDefault(id)?.Icon ?? "",
            "class" => _model?.Classes.GetValueOrDefault(id)?.Icon ?? "",
            _ => "",
        };
        SetDescription(name, text, icon);
    }

    /// <summary>A step's help text, shown when entering the step (entry descriptions replace it while there).</summary>
    void StepHelp(string title, string text)
    {
        if (_helpFor == _step) return;
        _helpFor = _step;
        _describing = ("", -1);
        SetDescription(title, text);
    }
    Step _helpFor = (Step)(-1);

    void SetDescription(string title, string text, string icon = "") =>
        _f.SetDescription(title, text, ChargenModel.Icon(Session.Content, icon));

    // ------------------------------------------------------------------ navigation

    bool StepShown(Step s) => s switch
    {
        Step.Options => _plan?.HasOptions == true,
        Step.Spells => _plan?.HasSpells == true,
        _ => true,
    };

    /// <summary>Why the player can't leave a step yet (empty when they can).</summary>
    List<string> StepErrors(Step s)
    {
        var p = _plan;
        var list = new List<string>();
        switch (s)
        {
            case Step.Race: if (_n.Race < 0) list.Add("Choose a race."); break;
            case Step.Portrait: if (_n.PortraitId < 0 && _n.Portrait.Length == 0) list.Add("Choose a portrait."); break;
            case Step.Class:
                if (_n.Level.Class < 0) list.Add("Choose a class.");
                else if (p != null) list.AddRange(p.ErrorsFor("class", "prereq"));
                break;
            case Step.Alignment: if (p != null) list.AddRange(p.ErrorsFor("alignment")); break;
            case Step.Abilities: list.AddRange(_identity.Where(e => e.Code == "abilities").Select(e => e.Message)); break;
            case Step.Options: if (p != null) list.AddRange(p.ErrorsFor("domains", "school", "familiar", "companion")); break;
            case Step.Skills: if (p != null) list.AddRange(p.ErrorsFor("skills")); break;
            case Step.Feats: if (p != null) list.AddRange(p.ErrorsFor("feats", "feat-prereq", "feats-unspent")); break;
            case Step.Spells: if (p != null) list.AddRange(p.ErrorsFor("spells")); break;
            case Step.Details: if (_n.FirstName.Trim().Length == 0) list.Add("Enter a first name."); break;
            case Step.Finish:
                list.AddRange(_identity.Select(e => e.Message));
                if (p != null) list.AddRange(p.Errors.Select(e => e.Message));
                break;
        }
        return list;
    }

    void Go(int dir)
    {
        if (dir > 0 && StepErrors(_step) is { Count: > 0 } errs) { _f.ShowErrors(errs); return; }
        var s = _step;
        do s = (Step)Math.Clamp((int)s + dir, 0, (int)Step.Finish);
        while (!StepShown(s) && s != Step.Finish && s != Step.Gender);
        GoTo(s);
    }

    public void GoTo(Step s)
    {
        _step = s;
        if (s == Step.Portrait && _portraitsFor != (_n.Race, _n.Gender)) RequestPortraits();
        if (s >= Step.Abilities) Validate();
        Render();
    }

    void RequestPortraits()
    {
        _portraitsFor = (_n.Race, _n.Gender);
        _portraits.Clear();
        Session.Request("chargen-portraits", new { race = _n.Race, gender = _n.Gender });
    }

    // ------------------------------------------------------------------ rendering

    void Render()
    {
        if (_model == null || !IsInsideTree()) return;
        RenderChrome();
        SyncPreview();
        var content = _f.Content;
        foreach (var c in content.GetChildren()) { content.RemoveChild(c); c.QueueFree(); }
        _appearanceControls = null;
        _colourPicker = null;
        switch (_step)
        {
            case Step.Gender: RenderGender(); break;
            case Step.Race: RenderRace(); break;
            case Step.Portrait: RenderPortrait(); break;
            case Step.Class: RenderClass(); break;
            case Step.Alignment: RenderAlignment(); break;
            case Step.Abilities: RenderAbilities(); break;
            case Step.Package: RenderPackage(); break;
            case Step.Options:
                content.AddChild(_steps.Options());
                StepHelp("Class options", "Choices your class makes at its first level: a cleric's domains, a wizard's school of magic, a familiar or animal companion.");
                break;
            case Step.Skills:
                content.AddChild(_steps.Skills());
                StepHelp("Skills", "Class skills cost one point per rank, cross-class skills two. Ranks are capped at level + 3 (half that for skills none of your classes has). Unspent points are saved for later levels.");
                break;
            case Step.Feats:
                content.AddChild(_steps.Feats());
                StepHelp("Feats", "Select a feat to read about it; double-click to add or remove it. Feats whose requirements you don't meet are not listed.");
                break;
            case Step.Spells:
                content.AddChild(_steps.Spells());
                StepHelp("Spells", "Click spells to learn them; their descriptions show here.");
                break;
            case Step.Appearance: RenderAppearance(); break;
            case Step.Details: RenderDetails(); break;
            case Step.Finish: RenderFinish(); break;
        }
        _f.ShowErrors(Array.Empty<string>());
    }

    /// <summary>Step rail, navigation buttons and summary (the step's content stays).</summary>
    void RenderChrome()
    {
        if (_model == null || !IsInsideTree()) return;
        _shownSteps = Enumerable.Range(0, (int)Step.Finish + 1).Select(i => (Step)i).Where(StepShown).ToList();
        var entries = new List<StepRail.Entry>();
        for (int i = 0; i < _shownSteps.Count; i++)
        {
            var s = _shownSteps[i];
            bool reachable = s <= _step || _shownSteps.Take(i).All(x => StepErrors(x).Count == 0);
            entries.Add(new StepRail.Entry(StepNames[(int)s], s < _step, s == _step, reachable, s < _step && StepErrors(s).Count > 0));
        }
        _f.SetSteps(entries, Math.Max(0, _shownSteps.IndexOf(_step)), StepNames[(int)_step], StepHints[(int)_step]);
        TitleLabel.Text = "New character";
        _f.Back.Disabled = _step == Step.Gender;
        _f.Next.Text = _step == Step.Finish ? "Create character" : "Next";
        _f.Next.Disabled = _creating;
        _f.Recommend.Visible = _model.Recommended && _step is Step.Abilities or Step.Package or Step.Skills or Step.Feats or Step.Spells
            || _model.Recommended && _step == Step.Options;
        // The stage is tall where looks are the point and shorter where the description matters;
        // on the appearance step it takes the whole side column.
        bool looks = _step is Step.Gender or Step.Portrait or Step.Details or Step.Finish;
        _f.SetStage(looks ? 290 : 230, _step == Step.Appearance);
        if (_stage != null) _stage.ZoomButtons = _step == Step.Appearance;
        _f.Recommend.TooltipText = _step == Step.Abilities ? "Scores your class suggests, fitted to your points" : "Let your class package choose for you";
        RenderSummary();
    }

    void RenderSummary()
    {
        if (_model == null) return;
        var box = _f.SummaryBox;
        foreach (var c in box.GetChildren()) { box.RemoveChild(c); c.QueueFree(); }
        var row = Ui.HBox(12);
        box.AddChild(row);
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.AccentDim, 1, 2));
        var tex = ChargenModel.Portrait(Session.Content, PortraitResRef(), "l");
        if (tex != null) frame.AddChild(new TextureRect { Texture = tex, CustomMinimumSize = new Vector2(60, 78), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered });
        else frame.AddChild(Icons.Rect("character", 60, Palette.TextMuted));
        row.AddChild(frame);
        var who = Ui.VBox(2);
        who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        who.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(who);
        var name = Ui.Text(_n.FullName.Length > 0 ? _n.FullName : "Unnamed hero", Ui.TextStyle.Title, _n.FullName.Length > 0 ? null : Palette.TextMuted);
        name.ClipText = true;
        name.CustomMinimumSize = new Vector2(200, 0);
        who.AddChild(name);
        who.AddChild(Ui.Text($"{(_n.Gender == 1 ? "Female" : "Male")} {_model.Races.GetValueOrDefault(_n.Race)?.Name}", Ui.TextStyle.Small));
        if (_n.Level.Class >= 0) who.AddChild(Ui.Text(_model.ClassName(_n.Level.Class) + " 1", Ui.TextStyle.Small, Palette.Text));
        if (_step > Step.Alignment) who.AddChild(Ui.Text(_model.Alignments.FirstOrDefault(a => a.LawChaos == _n.LawChaos && a.GoodEvil == _n.GoodEvil).Name ?? "", Ui.TextStyle.Small));
        if (_plan is { Abilities.Length: 6 } p && _step > Step.Abilities)
        {
            var grid = new GridContainer { Columns = 6 };
            grid.AddThemeConstantOverride("h_separation", 4);
            for (int i = 0; i < 6; i++)
            {
                var cell = Ui.VBox(0);
                cell.CustomMinimumSize = new Vector2(46, 0);
                var l = Ui.Text(ChargenModel.AbilityShort[i], Ui.TextStyle.Caption, Palette.TextSecondary, 10);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                cell.AddChild(l);
                var v = Ui.Text(p.Abilities[i].ToString(), Ui.TextStyle.Strong, null, Palette.FontLarge);
                v.HorizontalAlignment = HorizontalAlignment.Center;
                cell.AddChild(v);
                grid.AddChild(cell);
            }
            box.AddChild(new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.Both, CustomMinimumSize = new Vector2(0, 1) });
            box.AddChild(grid);
        }
    }

    string PortraitResRef() =>
        _n.PortraitId >= 0 ? _portraits.FirstOrDefault(p => p.Id == _n.PortraitId).ResRef ?? "" : _n.Portrait;

    void RenderGender()
    {
        var row = Ui.HBox(16);
        _f.Content.AddChild(row);
        foreach (var (g, name, sub) in new[] { (0, "Male", "He / him"), (1, "Female", "She / her") })
        {
            var card = SelectCard.Create(name, sub, Icons.Get("character", 128), 72, vertical: true, pad: 20);
            card.CustomMinimumSize = new Vector2(200, 190);
            card.Selected = _n.Gender == g;
            card.Pressed += () => SetGender(g);
            card.DoubleClicked += () => { SetGender(g); Go(+1); };
            row.AddChild(card);
        }
        SetDescription("Gender", "Male and female characters are equal in the rules; gender changes appearance, portraits and voice.");
    }

    public void SetGender(int g)
    {
        if (_n.Gender != g)
        {
            _n.Gender = g;
            _n.PortraitId = -1;
            _n.Portrait = "";
            _n.SoundSet = DefaultSoundSet();
            _n.BodyParts[0] = 1;
        }
        Render();
    }

    void RenderRace()
    {
        var m = _model!;
        var (scroll, grid) = WizardFrame.CardGrid(2);
        _f.Content.AddChild(scroll);
        foreach (var r in m.Races.Values.OrderBy(r => r.Id))
        {
            int fav = r.Int("favored", -1);
            var card = SelectCard.Create(r.Name, fav >= 0 ? $"Favoured class: {m.ClassName(fav)}" : "Favoured class: any");
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.CustomMinimumSize = new Vector2(0, 92);
            var adj = r.Ints("adjust");
            var chips = Ui.HBox(6);
            chips.MouseFilter = MouseFilterEnum.Ignore;
            for (int i = 0; i < 6 && i < adj.Length; i++)
                if (adj[i] != 0) chips.AddChild(Chip.Create($"{ChargenModel.AbilityShort[i]} {ChargenModel.Signed(adj[i])}", adj[i] > 0 ? Palette.Success : Palette.Danger));
            if (chips.GetChildCount() == 0) chips.AddChild(Chip.Create("No adjustments", Palette.TextSecondary));
            card.Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
            card.Body.AddChild(chips);
            card.Selected = r.Id == _n.Race;
            int id = r.Id;
            card.Pressed += () => SetRace(id);
            card.DoubleClicked += () => { SetRace(id); Go(+1); };
            grid.AddChild(card);
        }
        if (m.Races.TryGetValue(_n.Race, out var sel)) ShowRace(sel);
    }

    void ShowRace(ChargenEntry r)
    {
        var m = _model!;
        int fav = r.Int("favored", -1);
        SetDescription(r.Name, r.Description + $"\n\nAbility points: {r.Int("budget")}" + (fav >= 0 ? $"\nFavoured class: {m.ClassName(fav)}" : "\nFavoured class: any"));
    }

    public void SetRace(int race)
    {
        if (_n.Race != race)
        {
            _n.Race = race;
            _n.PortraitId = -1;
            _n.Portrait = "";
            _n.BodyParts[0] = 1;
            _n.Level = new LevelChoice { Class = _n.Level.Class, Package = _n.Level.Package };
        }
        if (_model!.Races.TryGetValue(race, out var r)) ShowRace(r);
        Render();
    }

    void RenderPortrait()
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _f.Content.AddChild(scroll);
        var grid = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(Ui.Pad(grid, 4, 4, 12, 4));
        ((MarginContainer)grid.GetParent()).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (_portraits.Count == 0) grid.AddChild(Ui.Text("Loading portraits…", Ui.TextStyle.Secondary));
        foreach (var (id, resref) in _portraits)
        {
            var tex = ChargenModel.Portrait(Session.Content, resref, "l"); // 128x200, drawn smaller: sharp even at high UI scales
            if (tex == null) continue; // listed in portraits.2da but not installed
            bool on = _n.PortraitId == id;
            var b = new Button { CustomMinimumSize = new Vector2(82, 108), ToggleMode = true, ButtonPressed = on, FocusMode = FocusModeEnum.None, TooltipText = resref, MouseDefaultCursorShape = CursorShape.PointingHand };
            var normal = Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.Line, 1, 3);
            var hover = Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.AccentDim, 1, 3);
            var sel = Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.AccentBright, 2, 3);
            sel.ShadowColor = Palette.AccentGlow;
            sel.ShadowSize = 10;
            b.AddThemeStyleboxOverride("normal", on ? sel : normal);
            b.AddThemeStyleboxOverride("hover", on ? sel : hover);
            b.AddThemeStyleboxOverride("pressed", sel);
            b.AddThemeStyleboxOverride("hover_pressed", sel);
            b.AddChild(new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 3, OffsetTop = 3, OffsetRight = -3, OffsetBottom = -3, MouseFilter = MouseFilterEnum.Ignore });
            int pid = id;
            b.Pressed += () => SetPortrait(pid);
            grid.AddChild(b);
        }
        if (_model!.CustomPortraits)
        {
            var row = Ui.HBox(10);
            row.AddChild(Ui.Text("Custom portrait", Ui.TextStyle.Small));
            var edit = new LineEdit { Text = _n.PortraitId < 0 ? _n.Portrait : "", PlaceholderText = "po_...", CustomMinimumSize = new Vector2(220, 0), MaxLength = 16 };
            edit.TextSubmitted += t => { _n.PortraitId = -1; _n.Portrait = t.Trim().ToLowerInvariant(); Render(); };
            row.AddChild(edit);
            row.AddChild(Ui.Text("Press Enter to use it", Ui.TextStyle.Caption));
            _f.Content.AddChild(row);
        }
        SetDescription("Portrait", "The picture other players see for your character, in the party list, on the character sheet and in conversations.");
    }

    public void SetPortrait(int id)
    {
        _n.PortraitId = id;
        _n.Portrait = "";
        Render();
    }

    public IReadOnlyList<(int Id, string ResRef)> Portraits => _portraits;

    void RenderClass()
    {
        var m = _model!;
        var (scroll, grid) = WizardFrame.CardGrid(2);
        _f.Content.AddChild(scroll);
        foreach (var c in m.Classes.Values.Where(c => c.Bool("player") && !c.Bool("prestige")).OrderBy(c => c.Name))
        {
            var card = SelectCard.Create(c.Name, $"d{c.Int("hitDie")} hit die  ·  {c.Int("skillPoints")} + INT skill points", ChargenModel.Icon(Session.Content, c.Icon), 38);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (c.Bool("caster")) card.Trailing.AddChild(Chip.Create("Spells", Palette.Magic));
            if (m.Races.GetValueOrDefault(_n.Race)?.Int("favored", -1) == c.Id) card.Trailing.AddChild(Chip.Create("Favoured", Palette.Accent, tooltip: "Your race's favoured class: no multiclass experience penalty"));
            card.Selected = c.Id == _n.Level.Class;
            int id = c.Id;
            card.Pressed += () => SetClass(id);
            card.DoubleClicked += () => { SetClass(id); Go(+1); };
            grid.AddChild(card);
        }
        if (m.Classes.TryGetValue(_n.Level.Class, out var sel)) ShowClass(sel);
    }

    void ShowClass(ChargenEntry c)
    {
        var m = _model!;
        var allowed = c.Bools("alignments");
        string align = allowed.All(x => x) ? "any" : string.Join(", ", m.Alignments.Where((a, i) => i < allowed.Length && allowed[i]).Select(a => a.Name));
        SetDescription(c.Name, $"Hit die: d{c.Int("hitDie")}\nSkill points: {c.Int("skillPoints")} + INT per level\nAlignment: {align}\n\n{c.Description}", c.Icon);
    }

    public void SetClass(int cls)
    {
        var m = _model!;
        if (_n.Level.Class != cls)
        {
            int pkg = m.Classes.TryGetValue(cls, out var c) ? c.Int("defaultPackage", -1) : -1;
            _n.Level = new LevelChoice { Class = cls, Package = pkg };
            // Keep the alignment legal for the class.
            var allowed = m.Classes.GetValueOrDefault(cls)?.Bools("alignments") ?? Array.Empty<bool>();
            int current = m.Alignments.FindIndex(a => a.LawChaos == _n.LawChaos && a.GoodEvil == _n.GoodEvil);
            if (current < 0 || current >= allowed.Length || !allowed[current])
            {
                int first = Array.IndexOf(allowed, true);
                if (first >= 0) { _n.LawChaos = m.Alignments[first].LawChaos; _n.GoodEvil = m.Alignments[first].GoodEvil; }
            }
        }
        if (m.Classes.TryGetValue(cls, out var e)) ShowClass(e);
        Validate();
        Render();
    }

    static readonly Dictionary<string, string> AlignmentBlurbs = new()
    {
        ["Lawful Good"] = "Honour and compassion", ["Neutral Good"] = "Kindness above rules", ["Chaotic Good"] = "Freedom and goodwill",
        ["Lawful Neutral"] = "Order and tradition", ["True Neutral"] = "Balance in all things", ["Chaotic Neutral"] = "Following one's whims",
        ["Lawful Evil"] = "Tyranny with a code", ["Neutral Evil"] = "Selfish and ruthless", ["Chaotic Evil"] = "Cruelty and destruction",
    };

    void RenderAlignment()
    {
        var m = _model!;
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _f.Content.AddChild(grid);
        var cls = m.Classes.GetValueOrDefault(_n.Level.Class);
        var allowed = cls?.Bools("alignments") ?? Array.Empty<bool>();
        for (int i = 0; i < m.Alignments.Count; i++)
        {
            var (lc, ge, name) = m.Alignments[i];
            var card = SelectCard.Create(name, AlignmentBlurbs.GetValueOrDefault(name, ""), vertical: true, pad: 16);
            card.CustomMinimumSize = new Vector2(0, 96);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.Selected = _n.LawChaos == lc && _n.GoodEvil == ge;
            bool ok = i >= allowed.Length || allowed[i];
            card.Disabled = !ok;
            if (!ok) card.Tooltip = () => Tip.Card(name, "Not allowed", footer: $"{cls?.Name} characters can't be {name.ToLowerInvariant()}.", width: 240);
            else card.Pressed += () => SetAlignment(lc, ge);
            grid.AddChild(card);
        }
        SetDescription("Alignment", "Lawful characters respect order and tradition; chaotic ones value freedom. Good characters protect others; evil ones harm them for gain. Neutral characters keep to the middle.\n\nSome classes need a particular alignment: paladins are lawful good, monks lawful, druids neutral on at least one axis, and bards and barbarians can't be lawful.");
    }

    public void SetAlignment(int lc, int ge)
    {
        _n.LawChaos = lc;
        _n.GoodEvil = ge;
        Validate();
        Render();
    }

    static readonly string[] AbilityBlurbs =
    {
        "Melee attacks and damage, carrying", "Armour class, ranged attacks, Reflex", "Hit points, Fortitude",
        "Skill points, wizard spells", "Will, divine spells, Spot and Listen", "Social skills, sorcerer and bard spells",
    };

    void RenderAbilities()
    {
        var m = _model!;
        var race = m.Races.GetValueOrDefault(_n.Race);
        var adj = race?.Ints("adjust") ?? new int[6];
        int budget = race?.Int("budget", 30) ?? 30;
        int spent = Enumerable.Range(0, 6).Sum(i => m.CostOf(_n.Abilities[i]));
        int left = budget - spent;

        // Points remaining.
        var top = new PanelContainer();
        top.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.Radius, left == 0 ? Palette.Success with { A = 0.5f } : Palette.Line, 1, 16, 10));
        var tr = Ui.HBox(12);
        top.AddChild(tr);
        var tl = Ui.VBox(0);
        tl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tl.AddChild(Ui.Text("POINTS REMAINING", Ui.TextStyle.Caption, Palette.TextSecondary));
        tl.AddChild(Ui.Text($"Scores start at {m.AbilityMin} and go up to {m.AbilityMax} before racial adjustments.", Ui.TextStyle.Small));
        tr.AddChild(tl);
        tr.AddChild(Ui.Text(left.ToString(), Ui.TextStyle.Display, left == 0 ? Palette.Success : left < 0 ? Palette.Danger : Palette.AccentBright, 34));
        var of = Ui.Text($"/ {budget}", Ui.TextStyle.Secondary);
        of.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        tr.AddChild(of);
        _f.Content.AddChild(top);

        var list = Ui.VBox(6);
        _f.Content.AddChild(list);
        for (int i = 0; i < 6; i++)
        {
            int a = i;
            var card = new TipPanel { MouseFilter = MouseFilterEnum.Stop };
            var normal = Styles.Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, 14, 6);
            card.AddThemeStyleboxOverride("panel", normal);
            card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg3, Palette.Radius, Palette.AccentDim, 1, 14, 6));
            card.MouseExited += () => card.AddThemeStyleboxOverride("panel", normal);
            var row = Ui.HBox(12);
            row.MouseFilter = MouseFilterEnum.Ignore;
            card.AddChild(row);
            var names = Ui.VBox(0);
            names.MouseFilter = MouseFilterEnum.Ignore;
            names.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            names.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            names.AddChild(Ui.Text(ChargenModel.AbilityNames[i], Ui.TextStyle.Strong));
            names.AddChild(Ui.Text(AbilityBlurbs[i], Ui.TextStyle.Caption));
            row.AddChild(names);
            int nextCost = m.CostOf(_n.Abilities[i] + 1) - m.CostOf(_n.Abilities[i]);
            var minus = RoundButton("−", _n.Abilities[i] <= m.AbilityMin, () => SetAbility(a, _n.Abilities[a] - 1), "Lower");
            row.AddChild(minus);
            var bas = Ui.Text(_n.Abilities[i].ToString(), Ui.TextStyle.Display, Palette.Text, 26);
            bas.HorizontalAlignment = HorizontalAlignment.Center;
            bas.CustomMinimumSize = new Vector2(44, 0);
            row.AddChild(bas);
            var plus = RoundButton("+", _n.Abilities[i] >= m.AbilityMax || spent + nextCost > budget, () => SetAbility(a, _n.Abilities[a] + 1),
                _n.Abilities[i] >= m.AbilityMax ? "At the maximum" : $"Raise (costs {nextCost} point{(nextCost == 1 ? "" : "s")})");
            row.AddChild(plus);
            var racial = Ui.HBox(0);
            racial.CustomMinimumSize = new Vector2(64, 0);
            racial.Alignment = BoxContainer.AlignmentMode.Center;
            if (adj.Length > i && adj[i] != 0) racial.AddChild(Chip.Create($"{ChargenModel.Signed(adj[i])} race", adj[i] > 0 ? Palette.Success : Palette.Danger));
            row.AddChild(racial);
            int score = _n.Abilities[i] + (adj.Length > i ? adj[i] : 0);
            var arrow = Ui.Text("→", Ui.TextStyle.Secondary, Palette.TextMuted);
            arrow.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(arrow);
            var total = Ui.Text(score.ToString(), Ui.TextStyle.Display, Palette.AccentBright, 26);
            total.HorizontalAlignment = HorizontalAlignment.Center;
            total.CustomMinimumSize = new Vector2(44, 0);
            row.AddChild(total);
            int mod = ChargenModel.Modifier(score);
            var modBox = Ui.HBox(0);
            modBox.CustomMinimumSize = new Vector2(44, 0);
            modBox.Alignment = BoxContainer.AlignmentMode.Center;
            modBox.AddChild(Chip.Create(ChargenModel.Signed(mod), mod > 0 ? Palette.Accent : mod < 0 ? Palette.Danger : Palette.TextSecondary, fontSize: Palette.FontSmall));
            row.AddChild(modBox);
            list.AddChild(card);
        }
        SetDescription("Ability scores", "Strength: melee attack and damage, carrying.\nDexterity: armour class, ranged attacks, reflexes.\nConstitution: hit points, fortitude.\nIntelligence: skill points, wizard spells.\nWisdom: will, cleric and druid spells.\nCharisma: social skills, sorcerer and bard spells.\n\nYour class's important abilities are listed in its description. Recommended picks the scores your class suggests.");
    }

    static Button RoundButton(string text, bool disabled, Action pressed, string tooltip)
    {
        var b = new Button { Text = text, Disabled = disabled, CustomMinimumSize = new Vector2(34, 34), FocusMode = FocusModeEnum.None, TooltipText = tooltip, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseDefaultCursorShape = CursorShape.PointingHand };
        b.AddThemeStyleboxOverride("normal", Styles.Box(new Color(0, 0, 0, 0.3f), 17, Palette.LineStrong, 1, 0, 0));
        b.AddThemeStyleboxOverride("hover", Styles.Box(Palette.Bg4, 17, Palette.Accent, 1, 0, 0));
        b.AddThemeStyleboxOverride("pressed", Styles.Box(Palette.AccentDim, 17, Palette.Accent, 1, 0, 0));
        b.AddThemeStyleboxOverride("disabled", Styles.Box(Colors.Transparent, 17, Palette.Line, 1, 0, 0));
        b.AddThemeFontOverride("font", Fonts.UiBold);
        b.AddThemeFontSizeOverride("font_size", 18);
        b.Pressed += pressed;
        return b;
    }

    public void SetAbility(int a, int score)
    {
        var m = _model!;
        _n.Abilities[a] = Math.Clamp(score, m.AbilityMin, m.AbilityMax);
        ResetLevelPicks();
        Validate();
        Render();
    }

    public void SetAbilities(int[] scores)
    {
        _n.Abilities = (int[])scores.Clone();
        ResetLevelPicks();
        Validate();
        Render();
    }

    /// <summary>The class's suggested scores (classes.2da), lowered until they fit the point budget.</summary>
    public void RecommendAbilities()
    {
        var m = _model!;
        var c = m.Classes.GetValueOrDefault(_n.Level.Class);
        var rec = c?.Ints("recommended") is { Length: 6 } r ? r : Enumerable.Repeat(10, 6).ToArray();
        var scores = rec.Select(v => Math.Clamp(v, m.AbilityMin, m.AbilityMax)).ToArray();
        int budget = m.Races.GetValueOrDefault(_n.Race)?.Int("budget", 30) ?? 30;
        while (scores.Sum(m.CostOf) > budget)
        {
            int hi = Array.IndexOf(scores, scores.Max());
            scores[hi]--;
        }
        // Spend what's left on the scores the class suggests highest.
        foreach (int i in Enumerable.Range(0, 6).OrderByDescending(i => rec[i]))
            while (scores[i] < m.AbilityMax && scores.Select((v, k) => k == i ? v + 1 : v).Sum(m.CostOf) <= budget && scores[i] < rec[i] + 2) scores[i]++;
        SetAbilities(scores);
    }

    void ResetLevelPicks()
    {
        _n.Level.Skills.Clear();
        _n.Level.Feats.Clear();
        _n.Level.Spells.Clear();
    }

    void RenderPackage()
    {
        var m = _model!;
        var (scroll, grid) = WizardFrame.CardGrid(1, 8);
        _f.Content.AddChild(scroll);
        var ids = m.Packages.Values.Where(p => p.Int("class", -1) == _n.Level.Class).OrderBy(p => p.Id).ToList();
        foreach (var p in ids)
        {
            var desc = NwnText.Strip(p.Description).Replace('\n', ' ').Trim();
            var card = SelectCard.Create(p.Name, Tip.Trim(desc, 120));
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (card.SubtitleLabel != null) card.SubtitleLabel.CustomMinimumSize = new Vector2(380, 0);
            card.Selected = p.Id == _n.Level.Package;
            if (p.Id == m.Classes.GetValueOrDefault(_n.Level.Class)?.Int("defaultPackage", -1)) card.Trailing.AddChild(Chip.Create("Default", Palette.Accent));
            var pkg = p;
            card.Pressed += () => { _n.Level.Package = pkg.Id; SetDescription(pkg.Name, pkg.Description); Render(); };
            grid.AddChild(card);
        }
        var sel = ids.FirstOrDefault(p => p.Id == _n.Level.Package);
        if (sel != null) SetDescription(sel.Name, sel.Description);
    }

    // ------------------------------------------------------------------ appearance

    static readonly string[] ColorNames = { "Skin", "Hair", "Tattoo 1", "Tattoo 2" };
    static readonly string[] ColorPalettes = { "pal_skin01", "pal_hair01", "pal_tattoo01", "pal_tattoo01" };
    readonly Dictionary<string, List<int>> _headCache = new();

    /// <summary>Head numbers with models for the current race, gender and phenotype.</summary>
    List<int> Heads()
    {
        var content = Session.Content;
        if (content == null) return new List<int> { 1 };
        var race = content.Resources.TwoDA("appearance").Get(AppearanceRow(), "RACE").ToLowerInvariant();
        string prefix = $"p{(_n.Gender == 1 ? 'f' : 'm')}{race}{_n.Phenotype}_head";
        if (_headCache.TryGetValue(prefix, out var cached)) return cached;
        var heads = content.Resources.All(ResType.Mdl).Select(k => k.ResRef.ToLowerInvariant())
            .Where(r => r.StartsWith(prefix) && int.TryParse(r[prefix.Length..], out _)).Select(r => int.Parse(r[prefix.Length..])).Distinct().OrderBy(x => x).ToList();
        if (heads.Count == 0) heads.Add(1);
        return _headCache[prefix] = heads;
    }

    int AppearanceRow() => _n.Appearance >= 0 ? _n.Appearance : _model?.Races.GetValueOrDefault(_n.Race)?.Int("appearance") ?? 6;

    Appearance PreviewAppearance() => new()
    {
        Type = AppearanceRow(), Gender = (byte)_n.Gender, Phenotype = (byte)_n.Phenotype,
        BodyParts = _n.BodyParts.Select(p => (ushort)p).ToArray(), Colors = _n.Colors.Select(c => (byte)c).ToArray(),
        Wings = _n.Wings, Tail = _n.Tail,
    };

    /// <summary>Shows the character as built so far on the side stage (rebuilt only when the look changed).</summary>
    void SyncPreview()
    {
        if (_stage == null || _model == null || _n.Race < 0) return;
        _stage.Preview.Content ??= Session.Content;
        var a = PreviewAppearance();
        string key = $"{a.Type}/{a.Gender}/{a.Phenotype}/{string.Join(",", a.BodyParts)}/{string.Join(",", a.Colors)}/{a.Wings}/{a.Tail}";
        if (key == _previewKey) return;
        _previewKey = key;
        _stage.Preview.Show(a);
    }

    void RenderAppearance()
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _f.Content.AddChild(scroll);
        _appearanceScroll = scroll;
        _appearanceControls = Ui.VBox(12);
        _appearanceControls.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(Ui.Pad(_appearanceControls, 0, 0, 12, 0));
        ((MarginContainer)_appearanceControls.GetParent()).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildAppearanceControls();
        SetDescription("Appearance", "Choose your character's head, body type, colours and voice. Armour and clothing will cover most of the body in play.\n\nDrag the preview to turn the character.");
    }

    VBoxContainer? _appearanceControls;
    ScrollContainer? _appearanceScroll;
    ColourPicker? _colourPicker;
    int _colourChannel;

    void BuildAppearanceControls()
    {
        var m = _model!;
        var controls = _appearanceControls!;
        foreach (var c in controls.GetChildren()) { controls.RemoveChild(c); c.QueueFree(); }
        var group = Group(controls, "Head and body");
        // Two columns: head and body type, then wings and tail.
        var body = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("h_separation", 18);
        body.AddThemeConstantOverride("v_separation", 8);
        group.AddChild(body);
        var heads = Heads();
        body.AddChild(Stepper("Head", _n.BodyParts[0], heads, v => { _n.BodyParts[0] = v; Zoom(1); RefreshPreview(); }));
        if (m.Phenotypes.Count > 1)
        {
            var pheno = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Body type" };
            foreach (var p in m.Phenotypes.Values.OrderBy(p => p.Id)) { pheno.AddItem(p.Name, p.Id); if (p.Id == _n.Phenotype) pheno.Select(pheno.ItemCount - 1); }
            pheno.ItemSelected += i => { _n.Phenotype = pheno.GetItemId((int)i); _n.BodyParts[0] = Math.Max(1, _n.BodyParts[0]); Zoom(0); RefreshPreview(); };
            body.AddChild(Labeled("Body", pheno));
        }
        if (m.Wings > 1) body.AddChild(Stepper("Wings", _n.Wings, Enumerable.Range(0, Math.Min(m.Wings, 100)).ToList(), v => { _n.Wings = v; Zoom(0); RefreshPreview(); }));
        if (m.Tails > 1) body.AddChild(Stepper("Tail", _n.Tail, Enumerable.Range(0, Math.Min(m.Tails, 100)).ToList(), v => { _n.Tail = v; Zoom(0); RefreshPreview(); }));

        var colours = Group(controls, "Colours");
        _colourPicker = ColourPicker.Create(Session.Content, ColorNames, ColorPalettes, _n.Colors, ColourPicked, _colourChannel);
        _colourPicker.ChannelChanged = ch => { _colourChannel = ch; Zoom(ch == 1 ? 1 : 0); };
        colours.AddChild(_colourPicker);

        var voice = Group(controls, "Voice");
        var voices = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "The voice set your character speaks with" };
        foreach (var s in m.SoundSets.Values.Where(s => s.Int("gender") == _n.Gender).OrderBy(s => s.Name)) { voices.AddItem(s.Name, s.Id); if (s.Id == _n.SoundSet) voices.Select(voices.ItemCount - 1); }
        voices.ItemSelected += i => _n.SoundSet = voices.GetItemId((int)i);
        voice.AddChild(Labeled("Voice set", voices));
    }

    void Zoom(float z) { if (_stage != null) _stage.Preview.Zoom = z; }

    static VBoxContainer Group(VBoxContainer parent, string title)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Styles.Card(12));
        var v = Ui.VBox(8);
        card.AddChild(v);
        v.AddChild(Ui.Section(title));
        parent.AddChild(card);
        return v;
    }

    /// <summary>After a head or body change: the preview, and the controls (the head list depends on the body).</summary>
    void RefreshPreview()
    {
        SyncPreview();
        if (_step == Step.Appearance && _appearanceControls != null && IsInstanceValid(_appearanceControls)) BuildAppearanceControls();
    }

    static HBoxContainer Labeled(string label, Control c)
    {
        if (c is OptionButton ob) { ob.ClipText = true; ob.FitToLongestItem = false; ob.CustomMinimumSize = new Vector2(150, 0); }
        var row = Ui.HBox(10);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var l = Ui.Text(label, Ui.TextStyle.Small);
        l.CustomMinimumSize = new Vector2(50, 0);
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(l);
        c.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(c);
        return row;
    }

    static HBoxContainer Stepper(string label, int value, List<int> values, Action<int> set)
    {
        int idx = Math.Max(0, values.IndexOf(value));
        var box = Ui.HBox(8);
        var prev = RoundButton("‹", false, () => set(values[(idx - 1 + values.Count) % values.Count]), "Previous");
        var next = RoundButton("›", false, () => set(values[(idx + 1) % values.Count]), "Next");
        box.AddChild(prev);
        var l = Ui.Text($"{value}", Ui.TextStyle.Title);
        l.CustomMinimumSize = new Vector2(46, 0);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        box.AddChild(l);
        box.AddChild(next);
        var count = Ui.Text($"{idx + 1} of {values.Count}", Ui.TextStyle.Caption);
        count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        box.AddChild(count);
        return Labeled(label, box);
    }

    /// <summary>The colour picker chose a colour: the preview follows at once (the controls stay, so the palette keeps its scroll).</summary>
    void ColourPicked(int channel, int value)
    {
        _n.Colors[channel] = value;
        _colourChannel = channel;
        SyncPreview();
    }

    /// <summary>Sets a colour channel (skin, hair, tattoo 1, tattoo 2) as the picker would.</summary>
    public void SetColor(int channel, int value)
    {
        if (_colourPicker != null && IsInstanceValid(_colourPicker) && _colourPicker.IsInsideTree())
        {
            if (_colourPicker.Channel != channel) _colourPicker.SelectChannel(channel);
            _colourPicker.Pick(value);
            ScrollToColour(_colourPicker);
        }
        else ColourPicked(channel, value);
    }

    /// <summary>Brings the chosen swatch into view (it may be among the extended colours further down), once the palette is laid out.</summary>
    async void ScrollToColour(ColourPicker picker)
    {
        for (int i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (IsInstanceValid(picker) && picker.SelectedSwatch is { } sw && _appearanceScroll != null && IsInstanceValid(_appearanceScroll))
            _appearanceScroll.EnsureControlVisible(sw);
    }

    /// <summary>Shows a colour channel's palette in the picker.</summary>
    public void SelectColourChannel(int channel)
    {
        if (_colourPicker != null && IsInstanceValid(_colourPicker)) _colourPicker.SelectChannel(channel);
        else _colourChannel = channel;
    }

    public void SetHead(int head)
    {
        _n.BodyParts[0] = head;
        Zoom(1);
        RefreshPreview();
    }

    public List<int> AvailableHeads => Heads();

    // ------------------------------------------------------------------ details and finish

    void RenderDetails()
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Styles.Card(16));
        _f.Content.AddChild(card);
        var form = Ui.VBox(12);
        card.AddChild(form);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 10);
        form.AddChild(grid);
        void Field(string label, string value, Action<string> set, int max = 40, string placeholder = "", bool required = false)
        {
            var l = Ui.Text(label + (required ? "  *" : ""), Ui.TextStyle.Small, required ? Palette.Text : null);
            l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            l.CustomMinimumSize = new Vector2(96, 0);
            grid.AddChild(l);
            var e = new LineEdit { Text = value, CustomMinimumSize = new Vector2(340, 36), MaxLength = max, PlaceholderText = placeholder };
            e.TextChanged += t => { set(t); if (required) RenderSummary(); };
            grid.AddChild(e);
        }
        Field("First name", _n.FirstName, t => _n.FirstName = t, placeholder: "Required", required: true);
        Field("Last name", _n.LastName, t => _n.LastName = t, placeholder: "Optional");
        Field("Age", _n.Age >= 0 ? _n.Age.ToString() : (_model!.Races.GetValueOrDefault(_n.Race)?.Int("age") ?? 20).ToString(), t => _n.Age = int.TryParse(t, out var a) ? a : -1, 4);
        Field("Deity", _n.Deity, t => _n.Deity = t, 64, "None");
        Field("Subrace", _n.Subrace, t => _n.Subrace = t, 64, "None");
        form.AddChild(Ui.Section("Description"));
        var desc = new TextEdit { Text = _n.Description, CustomMinimumSize = new Vector2(0, 170), WrapMode = TextEdit.LineWrappingMode.Boundary, PlaceholderText = "What others see when they examine your character." };
        desc.AddThemeFontOverride("font", Fonts.Reading);
        desc.AddThemeFontSizeOverride("font_size", 17);
        desc.TextChanged += () => _n.Description = desc.Text;
        form.AddChild(desc);
        SetDescription("Details", "Your character's name is how other players will know you. Deity, age and description are for role-play.");
    }

    public void SetName(string first, string last)
    {
        _n.FirstName = first;
        _n.LastName = last;
        Render();
    }

    public void SetDetails(string deity, string description)
    {
        _n.Deity = deity;
        _n.Description = description;
        Render();
    }

    void RenderFinish()
    {
        var m = _model!;
        var p = _plan;
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _f.Content.AddChild(scroll);
        var box = Ui.VBox(14);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(Ui.Pad(box, 0, 0, 12, 0));
        ((MarginContainer)box.GetParent()).SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var hero = new PanelContainer();
        hero.AddThemeStyleboxOverride("panel", Styles.Card(14));
        box.AddChild(hero);
        var top = Ui.HBox(18);
        hero.AddChild(top);
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.Accent, 1, 2));
        frame.AddChild(new TextureRect { Texture = ChargenModel.Portrait(Session.Content, PortraitResRef(), "l"), CustomMinimumSize = new Vector2(110, 142), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered });
        top.AddChild(frame);
        var info = Ui.VBox(8);
        info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(info);
        info.AddChild(Ui.Text(_n.FullName.Length > 0 ? _n.FullName : "Unnamed hero", Ui.TextStyle.Heading));
        var align = m.Alignments.FirstOrDefault(a => a.LawChaos == _n.LawChaos && a.GoodEvil == _n.GoodEvil).Name;
        info.AddChild(Ui.Text($"{(_n.Gender == 1 ? "Female" : "Male")} {m.Races.GetValueOrDefault(_n.Race)?.Name}   ·   {align}" + (_n.Deity.Length > 0 ? $"   ·   {_n.Deity}" : ""), Ui.TextStyle.Secondary));
        var chips = Ui.HBox(8);
        var cls = m.Classes.GetValueOrDefault(_n.Level.Class);
        chips.AddChild(ClassChip(cls));
        info.AddChild(chips);
        if (p is { Abilities.Length: 6 })
        {
            int hp = Math.Max(1, p.HitDie + ChargenModel.Modifier(p.Abilities[2]));
            var stats = Ui.HBox(8);
            foreach (var (label, value) in new[] { ("HP", hp.ToString()), ("BAB", ChargenModel.Signed(p.Bab)), ("FORT", ChargenModel.Signed(p.Fortitude)), ("REF", ChargenModel.Signed(p.Reflex)), ("WILL", ChargenModel.Signed(p.Will)) })
                stats.AddChild(MiniStat(label, value));
            info.AddChild(stats);
        }

        if (p is { Abilities.Length: 6 })
        {
            var abilities = Ui.HBox(8);
            for (int i = 0; i < 6; i++)
            {
                var t = MiniStat(ChargenModel.AbilityShort[i], p.Abilities[i].ToString(), ChargenModel.Signed(ChargenModel.Modifier(p.Abilities[i])));
                t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                abilities.AddChild(t);
            }
            box.AddChild(abilities);
        }
        var skills = _n.Level.Skills.Where(k => k.Value > 0).Select(k => (Name: $"{m.SkillName(k.Key)} {k.Value}", Icon: m.Skills.GetValueOrDefault(k.Key)?.Icon ?? "")).ToList();
        ChipSection(box, "Skills" + (p != null && p.SkillLeft > 0 ? $"  ·  {p.SkillLeft} points saved" : ""), skills, "None");
        var feats = (p?.Granted ?? new List<int>()).Concat(_n.Level.Feats).Distinct().Select(f => (Name: m.FeatName(f), Icon: m.Feats.GetValueOrDefault(f)?.Icon ?? "")).ToList();
        ChipSection(box, "Feats", feats, "None");
        var spells = _n.Level.Spells.SelectMany(k => k.Value).Select(s => (Name: m.SpellName(s), Icon: m.Spells.GetValueOrDefault(s)?.Icon ?? "")).ToList();
        if (spells.Count > 0) ChipSection(box, "Spells", spells, "");
        var errors = StepErrors(Step.Finish);
        if (errors.Count > 0)
        {
            var e = new PanelContainer();
            e.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Warning with { A = 0.08f }, Palette.RadiusSmall, Palette.Warning with { A = 0.4f }, 1, 12, 8));
            var t = Ui.Text("Still to do:\n" + string.Join("\n", errors.Distinct().Take(6).Select(x => "•  " + x)), Ui.TextStyle.Small, Palette.Warning);
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            t.CustomMinimumSize = new Vector2(380, 0);
            e.AddChild(t);
            box.AddChild(e);
        }
        SetDescription("Finish", "Check your character. \"Create character\" saves it to your vault and enters the game.");
    }

    Control ClassChip(ChargenEntry? cls)
    {
        var chip = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        chip.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.3f), Palette.RadiusSmall, Palette.LineStrong, 1, 8, 3));
        var h = Ui.HBox(6);
        if (ChargenModel.Icon(Session.Content, cls?.Icon ?? "") is { } ci)
            h.AddChild(new TextureRect { Texture = ci, CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        h.AddChild(Ui.Text(cls?.Name ?? "", Ui.TextStyle.Strong, null, Palette.FontSmall + 1));
        h.AddChild(Ui.Text("1", Ui.TextStyle.Strong, Palette.AccentBright, Palette.FontSmall + 1));
        chip.AddChild(h);
        return chip;
    }

    static PanelContainer MiniStat(string label, string value, string? sub = null)
    {
        var tile = new PanelContainer { CustomMinimumSize = new Vector2(64, 0) };
        tile.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.RadiusSmall, Palette.Line, 1, 8, 5));
        var v = Ui.VBox(0);
        var l = Ui.Text(label, Ui.TextStyle.Caption, Palette.TextSecondary, 10);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(l);
        var n = Ui.Text(value, Ui.TextStyle.Display, null, 20);
        n.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(n);
        if (sub != null)
        {
            var s = Ui.Text(sub, Ui.TextStyle.Caption, Palette.Accent);
            s.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(s);
        }
        tile.AddChild(v);
        return tile;
    }

    void ChipSection(VBoxContainer box, string title, List<(string Name, string Icon)> items, string empty)
    {
        box.AddChild(Ui.Section(title));
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 6);
        flow.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(flow);
        if (items.Count == 0 && empty.Length > 0) flow.AddChild(Ui.Text(empty, Ui.TextStyle.Small, Palette.TextMuted));
        foreach (var (name, icon) in items)
        {
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.RadiusSmall, Palette.Line, 1, 6, 3));
            var h = Ui.HBox(6);
            if (ChargenModel.Icon(Session.Content, icon) is { } tex)
                h.AddChild(new TextureRect { Texture = tex, CustomMinimumSize = new Vector2(20, 20), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var l = Ui.Text(name, Ui.TextStyle.Small, Palette.Text);
            l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            h.AddChild(l);
            chip.AddChild(h);
            flow.AddChild(chip);
        }
    }

    /// <summary>Asks the server to build and save the character and enter the game.</summary>
    public void Finish()
    {
        var errors = StepErrors(Step.Finish);
        if (errors.Count > 0) { _f.ShowErrors(errors); return; }
        _creating = true;
        _f.Next.Disabled = true;
        Session.Request("chargen-create", new { character = _n.ToJson() });
    }
}
