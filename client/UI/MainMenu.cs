using OpenWinter.Aurora.Resources;
using OpenWinter.Client.UI.FrontEnd;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The title screen. A module area slowly circled by the camera (or, until it is built or when there is
/// none, drifting smoke) under a vignette with rising embers; the title and menu on the left; screens
/// (New Game, Load Game, Multiplayer, Options, Credits) open in a glass panel on the right. Keyboard:
/// arrows move, Enter selects, Esc goes back. Choosing hands the main scene the same arguments the
/// command line would (module / load / connect / name / dm).
/// </summary>
public partial class MainMenu : Control
{
    public LocalConfig Config = null!;
    public GamePaths Paths = null!;
    /// <summary>Called with the chosen arguments (module / load / connect / name / dm ...).</summary>
    public Action<Dictionary<string, string>>? Start;
    /// <summary>Module and area for the 3D backdrop ("path|area"); null picks the configured module's most scenic area.</summary>
    public string? Scene;

    GamePathsHolder _paths = null!;
    MenuStage? _stage;
    Atmosphere _atmosphere = null!;
    ColorRect _fade = null!;
    VBoxContainer _rows = null!;
    PanelContainer _host = null!;
    Control? _screen;
    string _screenId = "";
    readonly Dictionary<string, MenuEntry> _entries = new();
    MenuEntry? _continue;
    SaveEntry? _latest;
    Label _sceneCaption = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = GameTheme.Theme; // the root window's theme does not reach through the 3D main node
        _paths = new GamePathsHolder { Paths = Paths, Config = Config };
        FrontEndContent.Begin(Paths);

        // Backdrop: the 3D stage (if any), smoke and embers over it.
        _atmosphere = new Atmosphere();
        AddChild(_atmosphere);
        StartStage();

        // Left column: title, menu, footer.
        var column = new VBoxContainer { AnchorBottom = 1, OffsetLeft = 92, OffsetRight = 92 + 470, OffsetTop = 0, OffsetBottom = 0 };
        column.AddThemeConstantOverride("separation", 0);
        AddChild(column);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40), SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.55f });
        var title = new GlowTitle { Text = "OPENWINTER", FontSize = 88, Spacing = 0.1f };
        column.AddChild(title);
        // Narrow screens (or a large interface scale): a smaller title so the screens on the right have room.
        void Fit()
        {
            int size = GetViewportRect().Size.X >= 1500 ? 88 : 68;
            if (title.FontSize == size) return;
            title.FontSize = size;
            title.CustomMinimumSize = Vector2.Zero;
            title._Ready();
            title.QueueRedraw();
        }
        Resized += Fit;
        Callable.From(Fit).CallDeferred();
        var kicker = Ui.Text("A NEVERWINTER NIGHTS ENGINE", Ui.TextStyle.Caption, Palette.TextSecondary, 13);
        column.AddChild(Ui.Pad(Tracked(kicker, 5), 6, 2, 0, 0));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 22) });
        column.AddChild(new Divider { Color = Palette.Accent with { A = 0.7f }, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(380, 1), SizeFlagsHorizontal = SizeFlags.ShrinkBegin });
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 26) });

        _rows = Ui.VBox(2);
        _rows.CustomMinimumSize = new Vector2(360, 0);
        _rows.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        column.AddChild(_rows);
        var group = new ButtonGroup { AllowUnpress = true };
        _latest = SafeLatestSave();
        _continue = Entry("continue", "Continue", _latest == null ? null : ContinueCaption(_latest), Continue, null);
        _continue.Visible = _latest != null;
        Entry("new", "New Game", null, () => Open("new"), group);
        Entry("load", "Load Game", null, () => Open("load"), group);
        Entry("multiplayer", "Multiplayer", null, () => Open("multiplayer"), group);
        Entry("options", "Options", null, () => Open("options"), group);
        Entry("credits", "Credits", null, () => Open("credits"), group);
        Entry("quit", "Quit", null, () => GetTree().Quit(), null);

        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _sceneCaption = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        column.AddChild(_sceneCaption);
        column.AddChild(Ui.Text("OpenWinter  ·  " + OpenWinter.Net.NetworkVersion.Current.Engine, Ui.TextStyle.Caption, Palette.TextMuted with { A = 0.7f }));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });

        // Right: the screen host.
        _host = new PanelContainer { AnchorLeft = 0.36f, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 0, OffsetRight = -64, OffsetTop = 64, OffsetBottom = -76, Visible = false };
        _host.AddThemeStyleboxOverride("panel", Styles.Window(26));
        Styles.ApplyGlass(_host, 0.86f);
        AddChild(_host);

        var hints = FrontEndUi.KeyHints(("↑ ↓", "Navigate"), ("Enter", "Select"), ("Esc", "Back"));
        hints.AnchorLeft = hints.AnchorRight = hints.AnchorTop = hints.AnchorBottom = 1;
        hints.OffsetLeft = -420; hints.OffsetRight = -64; hints.OffsetTop = -50; hints.OffsetBottom = -26;
        hints.Alignment = BoxContainer.AlignmentMode.End;
        AddChild(hints);

        // Fade in from black; the menu entries appear one after another.
        _fade = new ColorRect { Color = Colors.Black, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_fade);
        var tw = CreateTween();
        tw.TweenProperty(_fade, "color:a", 0f, 0.9).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        int i = 0;
        foreach (var row in _rows.GetChildren().OfType<Control>())
        {
            row.Modulate = row.Modulate with { A = 0 };
            var t = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            t.TweenProperty(row, "modulate:a", 1f, 0.35).SetDelay(0.25 + i * 0.05);
            i++;
        }
        Callable.From(() => (_continue is { Visible: true } ? _continue : _entries["new"]).GrabFocus()).CallDeferred();
    }

    static Control Tracked(Label l, int spacing)
    {
        // Letter-spaced small caps: a font variation with extra glyph spacing.
        l.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiMedium, SpacingGlyph = spacing });
        return l;
    }

    MenuEntry Entry(string id, string title, string? caption, Action pressed, ButtonGroup? group)
    {
        var e = MenuEntry.Create(title, caption, pressed);
        if (group != null) { e.ToggleMode = true; e.ButtonGroup = group; }
        _rows.AddChild(e);
        _entries[id] = e;
        return e;
    }

    /// <summary>Backdrop scenes from the official campaign (used when installed), in order of preference.</summary>
    public static readonly (string Id, string Name, string Module, string Area, bool Night)[] Scenes =
    {
        ("city-night", "Neverwinter by night", "Chapter1.nwm", "map_m1q1a", true),
        ("city", "Neverwinter, City Core", "Chapter1.nwm", "map_m1q1a", false),
        ("hilltop", "Hilltop foothills", "XP1-Chapter 1.nwm", "q1rural", false),
        ("waterdeep", "Waterdeep docks", "XP2_Chapter1.nwm", "waterdeep", true),
        ("eastroad", "The East Road", "Chapter2.nwm", "east_road", false),
    };

    /// <summary>The module file and area for a scene setting ("" default, "module", "none", or a scene id); null for none.</summary>
    public static (string Module, string Area, bool Night)? ResolveScene(string setting, LocalConfig config, GamePaths paths)
    {
        if (setting == "none") return null;
        if (setting == "module") return config.Module.Length > 0 ? (config.Module, "", false) : null;
        var campaign = System.IO.Path.Combine(paths.GameRoot, "data", "nwm");
        foreach (var s in Scenes.Where(s => setting.Length == 0 || s.Id == setting))
        {
            var file = System.IO.Path.Combine(campaign, s.Module);
            if (System.IO.File.Exists(file)) return (file, s.Area, s.Night);
        }
        return config.Module.Length > 0 ? (config.Module, "", false) : null;
    }

    void StartStage()
    {
        string module, area;
        bool night = false;
        if (Scene is { Length: > 0 } scene)
        {
            // --menu-scene "module|area[|night]"
            var parts = scene.Split('|');
            module = parts[0];
            area = parts.Length > 1 ? parts[1] : "";
            night = parts.Length > 2 && parts[2] == "night";
        }
        else if (ResolveScene(Settings.Current.Interface.MenuScene, Config, Paths) is { } chosen) (module, area, night) = chosen;
        else return;
        if (module.Length == 0 || (!System.IO.File.Exists(module) && !System.IO.Directory.Exists(module))) return;
        var stage = new MenuStage();
        _stage = stage;
        AddChild(stage);
        stage.Ready_ += ok =>
        {
            if (!ok || !IsInstanceValid(this)) return;
            _sceneCaption.Text = stage.AreaName.Length > 0 ? NwnText.Strip(stage.AreaName) : "";
            // Cross-fade from smoke to the scene.
            var tw = CreateTween();
            tw.TweenMethod(Callable.From<float>(v => { _atmosphere.Smoke = v; _atmosphere.Refresh(); }), 1f, 0f, 1.6).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
        };
        stage.Load(Paths, module, area.Length > 0 ? area : null, night);
    }

    /// <summary>True once the 3D backdrop is showing (captures wait for it).</summary>
    public bool StageReady => _atmosphere.Smoke < 0.01f || _stage == null;

    // ------------------------------------------------------------------ continue

    SaveEntry? SafeLatestSave()
    {
        try { return LoadGameScreen.ReadSaves(_paths.SavesDir).OrderByDescending(s => s.SavedAt).FirstOrDefault(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { return null; }
    }

    static string ContinueCaption(SaveEntry s) =>
        string.Join("  ·  ", new[] { s.Name, s.Area, FrontEndContent.FormatAgo(s.SavedAt) }.Where(x => x.Length > 0));

    /// <summary>Continues the latest save (the Continue entry; scripted captures).</summary>
    public void Continue()
    {
        if (_latest != null) LoadSave(_latest);
    }

    /// <summary>Loads a save (its module found by path, else by name), or says the module is missing.</summary>
    public void LoadSave(SaveEntry s)
    {
        var module = ResolveModule(s);
        if (module == null)
        {
            Dialogs.Confirm(this, "Module not found", $"The module this game was saved in ({(s.Module.Length > 0 ? s.Module : "unknown")}) is not in your modules folder or the campaign.", "OK", () => { }, cancel: "Close");
            return;
        }
        Start?.Invoke(new() { ["module"] = module, ["load"] = s.Folder });
    }

    /// <summary>The module a save belongs to: the stored path when it is in a module folder, else a module whose name matches.</summary>
    string? ResolveModule(SaveEntry s)
    {
        if (Paths.IsModulePath(s.ModulePath) && (System.IO.File.Exists(s.ModulePath) || System.IO.Directory.Exists(s.ModulePath))) return s.ModulePath;
        var talk = FrontEndContent.BaseTalk(Paths);
        foreach (var m in FrontEndContent.FindModules(Paths))
        {
            FrontEndContent.Describe(m, Paths, talk);
            if (m.Name.Equals(s.Module, StringComparison.OrdinalIgnoreCase)) return m.Path;
        }
        return null;
    }

    // ------------------------------------------------------------------ screens

    /// <summary>Opens the module browser (scripted captures).</summary>
    public void ShowModules() => Open("new");

    /// <summary>Opens a screen by id: new, load, multiplayer, options, credits.</summary>
    public void Open(string id)
    {
        if (_screenId == id && _screen != null) { RequestCloseScreen(); return; }
        if (_screen is OptionsHost { View: { } options } && options.IsInsideTree())
        {
            // Leaving options with unapplied changes asks first.
            string next = id;
            options.Done = () => { _screenId = ""; ReplaceScreen(next); };
            options.RequestClose();
            return;
        }
        ReplaceScreen(id);
    }

    void ReplaceScreen(string id)
    {
        _screen?.QueueFree();
        _screen = null;
        Control screen = id switch
        {
            "new" => new ModuleBrowser { Paths = _paths, Play = m => Start?.Invoke(new() { ["module"] = m.Path }) },
            "load" => new LoadGameScreen { Paths = _paths, Load = LoadSave },
            "multiplayer" => new MultiplayerScreen { Join = args => Start?.Invoke(args) },
            "options" => OptionsScreen(),
            "credits" => CreditsScreen(),
            _ => new Control(),
        };
        _screenId = id;
        _screen = screen;
        _host.AddChild(screen);
        if (!_host.Visible)
        {
            _host.Visible = true;
            FrontEndUi.Reveal(_host, 24, 0, 0.24);
        }
        else FrontEndUi.FadeIn(screen, 0.2);
        foreach (var (key, e) in _entries) e.SetActive(key == id);
    }

    Control OptionsScreen()
    {
        var col = Ui.VBox(Palette.Space4);
        col.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(FrontEndUi.Header("Settings", "Options"));
        var view = new OptionsView { Done = CloseScreen };
        col.AddChild(view);
        return new OptionsHost(col, view);
    }

    static Control CreditsScreen()
    {
        var col = Ui.VBox(Palette.Space4);
        col.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(FrontEndUi.Header("About", "Credits"));
        col.AddChild(new CreditsView());
        return col;
    }

    void RequestCloseScreen()
    {
        if (_screen is OptionsHost { View: var v }) { v.Done = CloseScreen; v.RequestClose(); }
        else CloseScreen();
    }

    void CloseScreen()
    {
        if (_screen == null) return;
        var screen = _screen;
        string id = _screenId;
        _screen = null;
        _screenId = "";
        foreach (var e in _entries.Values) e.SetActive(false);
        var tw = _host.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(_host, "modulate:a", 0f, 0.14);
        tw.TweenCallback(Callable.From(() =>
        {
            screen.QueueFree();
            if (_screen == null) _host.Visible = false;
            _host.Modulate = Colors.White;
        }));
        (_entries.GetValueOrDefault(id) ?? _entries["new"]).GrabFocus();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _screen != null)
        {
            RequestCloseScreen();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The options screen, so leaving it can ask the view about unapplied changes.</summary>
    sealed partial class OptionsHost : MarginContainer
    {
        public OptionsView View = null!;
        public OptionsHost() { }
        public OptionsHost(Control content, OptionsView view)
        {
            View = view;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            AddChild(content);
        }
    }

    /// <summary>The screen shown (captures).</summary>
    public Control? Screen => _screen;
}
