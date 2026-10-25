using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using OpenWinter.Client.UI;
using OpenWinter.Client.UI.FrontEnd;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// The front end around a game: settings at startup, the main menu, the loading screen, the game menu
/// (Esc), options, the FPS readout and screenshots; and the development captures of those screens.
/// </summary>
public partial class Main
{
    /// <summary>Set before reloading the scene to come back to the main menu instead of the command line's game.</summary>
    static bool _returnToMenu;

    LoadingScreen _loading = null!;
    CanvasLayer _frontLayer = null!, _windowLayer = null!;
    GameMenu? _gameMenu;
    OptionsWindow? _options;
    SaveLoadPanel? _saves;

    /// <summary>The game menu (null on the main menu). <c>GameMenu.Toggle()</c> opens / closes it.</summary>
    public GameMenu? GameMenu => _gameMenu;

    /// <summary>True while single player should not advance the world (menus open, window in the background).</summary>
    bool WorldPaused =>
        (_gameMenu?.Pausing ?? false)
        || UI.Classic.ClassicGameMenu.Pausing
        || (_options is { Visible: true } && Settings.Current.Gameplay.PauseInMenus)
        || (!Settings.WindowFocused && Settings.Current.Gameplay.PauseOnFocusLoss);

    /// <summary>Settings, the overlay layers and the loading screen. First thing in _Ready.</summary>
    void InitFrontEnd()
    {
        if (_returnToMenu)
        {
            // Back from a game: ignore the command line's game arguments this time.
            _returnToMenu = false;
            string? exitTest = _args.GetValueOrDefault("capture-connection-failure")
                ?? (_args.ContainsKey("exit-test") ? _args.GetValueOrDefault("capture-frontend") : null);
            foreach (var k in _args.Keys.ToList())
                if (k is "module" or "connect" or "connect-nwn" or "character" or "area" or "load" or "dm" or "look" or "bench" or "exit-test" || k.StartsWith("capture")) _args.Remove(k);
            // --capture-frontend dir --exit-test: the menu we came back to is captured too.
            if (exitTest != null) { _args["capture-menu"] = exitTest + "/after_exit"; _args["menu-only"] = "true"; }
        }
        TakePortalJoin(); // a portal's Join: straight on to its server (Main.Portal.cs)
        bool capture = _args.Keys.Any(k => k.StartsWith("capture") || k is "look" or "bench" or "playtest");
        Settings.Initialize(GetTree(), locked: capture);
        // Run-only override for nameplate comparisons; captured settings are never saved.
        if (int.TryParse(_args.GetValueOrDefault("text-bubble-mode"), out var bubbleMode))
            Settings.Preview(Settings.Current.Clone().Also(s => s.Interface.TextBubbleMode = bubbleMode));
        if (bool.TryParse(_args.GetValueOrDefault("large-font"), out var largeFont))
            Settings.Preview(Settings.Current.Clone().Also(s => s.Interface.HighResolutionFont = largeFont));
        if (float.TryParse(_args.GetValueOrDefault("ui-scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var uiScale))
            Settings.Preview(Settings.Current.Clone().Also(s => s.Interface.UiScale = uiScale));
        // --ui classic|modern: the interface style for this run. Captures default to modern, which their baselines use.
        if ((_args.GetValueOrDefault("ui") ?? (capture ? "modern" : null)) is { } style)
            Settings.Preview(Settings.Current.Clone().Also(s => s.Interface.Style = style == "classic" ? "classic" : "modern"));
        WatchInterfaceStyle();
        _frontLayer = new CanvasLayer { Layer = 10, Name = "FrontEnd" };
        AddChild(_frontLayer);
        _windowLayer = new CanvasLayer { Layer = 11, Name = "FrontEndWindows" };
        AddChild(_windowLayer);
        _loading = new LoadingScreen { Name = "Loading" };
        AddChild(_loading);
        // The classic interface draws the original's loading screen over it.
        // Always present: it shows only while the interface style is Classic, so a switch takes effect at once.
        _loading.AddChild(new UI.Classic.ClassicLoading { Screen = _loading, Name = "ClassicLoading" });
        AddChild(new FpsOverlay { Name = "Fps" });
        AddChild(new ScreenshotKey { Name = "Screenshots" });
    }

    /// <summary>The title screen; choosing a game shows the loader, then starts it.</summary>
    /// <summary>The classic interface's view of the running game (kept in modern mode too, for a switch in play).</summary>
    UI.Classic.ClassicContext? _classic;
    MainMenu? _menu;
    bool _styleHooked;

    /// <summary>Follows Options → Interface style as it changes in a game (the main menu is OpenWinter's own in both styles).</summary>
    void WatchInterfaceStyle()
    {
        if (_styleHooked) return;
        _styleHooked = true;
        void Changed(Preferences.GameSettings _) => Callable.From(ApplyInterfaceStyle).CallDeferred();
        Settings.Changed += Changed;
        TreeExiting += () => Settings.Changed -= Changed;
    }

    void ApplyInterfaceStyle()
    {
        bool classic = Settings.Current.Interface.Classic;
        if (_classic == null) return;
        if (classic && UI.Classic.ClassicGame.Current == null) UI.Classic.ClassicGame.Attach(_classic);
        else if (!classic && UI.Classic.ClassicGame.Current != null) UI.Classic.ClassicGame.Remove();
    }

    void ShowMainMenu()
    {
        // OpenWinter's own menu in both interface styles: the original's title screen carries its publisher's logos
        // and branding, which OpenWinter doesn't show as its own front end.
        var menu = new MainMenu { Config = _cfg, Paths = _paths, Scene = _args.GetValueOrDefault("menu-scene") };
        _menu = menu;
        menu.Start = choice =>
        {
            foreach (var (k, v) in choice) _args[k] = v;
            var server = choice.GetValueOrDefault("connect") ?? choice.GetValueOrDefault("connect-nwn");
            string title = server ?? ModuleTitle(choice.GetValueOrDefault("module", ""));
            _loading.Present(server != null ? "Joining server" : choice.ContainsKey("load") ? "Loading saved game" : "Starting module", title,
                FrontEndContent.Base, status: server != null ? "Connecting…" : "Loading the module…");
            // Let the loader draw before the (synchronous) module load.
            _ = StartAfterFrames(menu);
        };
        AddChild(menu);
        if (_args.TryGetValue("capture-menu-switch", out var switchDir)) { _args.Remove("capture-menu-switch"); _ = CaptureMenuSwitch(switchDir); }
        if (_args.TryGetValue("capture-menu", out var menuDir)) _ = CaptureMenu(menuDir, menu);
    }

    /// <summary>A module's display name (its file name when it cannot be read).</summary>
    string ModuleTitle(string path)
    {
        var info = new FrontEndContent.ModuleInfo { Path = path, Group = "" };
        FrontEndContent.Describe(info, _paths, FrontEndContent.Base?.Resources.Talk);
        return info.Title;
    }

    /// <summary>--capture-menu-switch dir: the main menu, then switched to the other interface style and back.</summary>
    async Task CaptureMenuSwitch(string dir)
    {
        if (_args.ContainsKey("options-open")) { await CaptureOpenOptionsSwitch(dir); return; }
        if (_args.ContainsKey("options-gameplay")) { await CaptureOptionsGameplay(dir); return; }
        System.IO.Directory.CreateDirectory(dir);
        await Wait(4);
        bool classic = Settings.Current.Interface.Classic;
        await Shot(dir, classic ? "a_classic" : "a_modern");
        Settings.Preview(Settings.Current.Clone().Also(s => s.Interface.Style = classic ? "modern" : "classic"));
        await Wait(3);
        await Shot(dir, classic ? "b_modern" : "b_classic");
        Settings.Preview(Settings.Current.Clone().Also(s => s.Interface.Style = classic ? "classic" : "modern"));
        await Wait(3);
        await Shot(dir, classic ? "c_classic" : "c_modern");
        GetTree().Quit();
    }

    async Task StartAfterFrames(MainMenu menu)
    {
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        menu.QueueFree();
        StartGame();
    }

    /// <summary>Hooks the front end to a started game. Called at the end of StartGame.</summary>
    void AttachFrontEnd(Hud hud, SaveLoadPanel saves, CharacterSelectPanel select)
    {
        _saves = saves;
        bool singlePlayer = _server != null;
        bool campaign = _args.GetValueOrDefault("module", _cfg.Module).Replace('\\', '/').Contains("/data/nwm/", StringComparison.OrdinalIgnoreCase);
        _loading.Attach(_session, () => campaign);
        _loading.AreaHint += hint => _chatLog?.Feedback($"{(_session.Content?.Resources.TlkString(7166) is { Length: > 0 } s ? s : "Loading Screen")}: {hint}");
        _loading.Abort = ExitToMainMenu;
        var failure = new ConnectionFailureScreen { Connection = _session.Connection, Back = ExitToMainMenu };
        AddChild(failure);
        if (_args.TryGetValue("capture-connection-failure", out var failureDir)) _ = CaptureConnectionFailure(failureDir, failure);
        if (!_loading.Showing && !_args.ContainsKey("look") && !_args.ContainsKey("bench"))
            _loading.Present(singlePlayer ? "Starting module" : "Joining server", _args.GetValueOrDefault("connect") ?? _args.GetValueOrDefault("connect-nwn") ?? System.IO.Path.GetFileNameWithoutExtension(_args.GetValueOrDefault("module", _cfg.Module).TrimEnd('/', '\\')),
                FrontEndContent.Base, status: "Loading…");
        _gameMenu = new GameMenu { Name = "GameMenu", Session = _session, SinglePlayer = singlePlayer };
        _frontLayer.AddChild(_gameMenu);
        _options = OptionsWindow.Create();
        _windowLayer.AddChild(_options);
        _gameMenu.Options = () => _options.Open();
        _gameMenu.BeforeOpen = saves.TakeScreenshot;
        _gameMenu.SaveGame = () => saves.Open(SaveLoadPanel.Mode.Save, keepScreenshot: true);
        _gameMenu.LoadGame = () => saves.Open(SaveLoadPanel.Mode.Load);
        _gameMenu.ExitToMenu = ExitToMainMenu;
        _input.OpenGameMenu = _gameMenu.Toggle;
        _input.OpenOptions = () => _options.Open();
        // The selection screen covers the HUD (chat line, quickbar): it lives on the front-end layer.
        select.GetParent()?.RemoveChild(select);
        _frontLayer.AddChild(select);
        _frontLayer.MoveChild(select, 0);
        select.ExitToMenu = ExitToMainMenu;
        select.OpenOptions = () => _options.Open();
        // A game loaded from the menu: the server answers the login with the saved world (no selection screen).
        if (_args.ContainsKey("load")) select.Suppressed = true;
    }

    /// <summary>Leaves the game and shows the main menu (the scene is reloaded fresh).</summary>
    public void ExitToMainMenu()
    {
        try { _server?.World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose(); }
        catch (Exception e) when (e is System.IO.IOException or InvalidOperationException) { GD.PrintErr("[client] " + e.Message); }
        _session?.Connection.Disconnect();
        _returnToMenu = true;
        UI.Classic.ClassicGame.Detach();
        PlayerInput.Blocked = false;
        UI.DM.ChatInput.Typing = false;
        GetTree().ReloadCurrentScene();
    }

    // ------------------------------------------------------------------ captures

    /// <summary>Capture a real connection failure, then exercise returning to the menu.</summary>
    async Task CaptureConnectionFailure(string dir, ConnectionFailureScreen failure)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 300 && !failure.Showing; i++) await Wait(0.1);
        if (!failure.Showing) { GD.PrintErr("[capture] expected a connection failure"); GetTree().Quit(1); return; }
        await Wait(0.3);
        await Shot(dir, "connection_failure");
        failure.Back?.Invoke();
    }

    async Task CaptureMenu(string dir, MainMenu menu)
    {
        System.IO.Directory.CreateDirectory(dir);
        // Wait for the 3D backdrop (up to 20 s).
        for (int i = 0; i < 200 && !menu.StageReady; i++) await Wait(0.1);
        await Wait(2.2);
        await Shot(dir, "40_menu");
        if (_args.ContainsKey("menu-only")) { GetTree().Quit(); return; }
        if (_args.TryGetValue("start-module", out var startModule))
        {
            // New Game from the menu: the loader while the module starts, then character selection.
            menu.Start?.Invoke(new() { ["module"] = startModule });
            await Wait(0.4);
            await Shot(dir, "53_start_loading");
            for (int i = 0; i < 600 && (_session == null || _loading.Showing); i++) await Wait(0.1);
            await Wait(1.2);
            await Shot(dir, "54_start_select");
            GetTree().Quit();
            return;
        }
        if (_args.ContainsKey("continue"))
        {
            // Continue the latest save from the menu, through the loading screen, into the game.
            menu.Continue();
            await Wait(1.0);
            await Shot(dir, "51_continue_loading");
            for (int i = 0; i < 600 && (_session?.Controlled == null || _loading.Showing); i++) await Wait(0.1);
            await Wait(1.0);
            await Shot(dir, "52_continued");
            GetTree().Quit();
            return;
        }
        menu.Open("new");
        await Wait(3.0);
        await Shot(dir, "41_modules");
        menu.Open("load");
        await Wait(1.0);
        await Shot(dir, "42_load");
        menu.Open("multiplayer");
        await Wait(0.8);
        await Shot(dir, "43_multiplayer");
        menu.Open("options");
        await Wait(0.8);
        await Shot(dir, "44_options_graphics");
        if (menu.Screen?.FindChild("*", true, false) != null && FindOptions(menu.Screen) is { } view)
        {
            view.Show(OptionsView.Page.Audio);
            await Wait(0.5);
            await Shot(dir, "45_options_audio");
            view.Show(OptionsView.Page.Controls);
            await Wait(0.5);
            await Shot(dir, "46_options_controls");
            // Rebind "Attack" to I (Inventory's key): the key moves and the change is reported.
            var attack = view.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.TooltipText.StartsWith("Attack (primary"));
            if (attack != null)
            {
                attack.EmitSignal(BaseButton.SignalName.Pressed);
                await Wait(0.3);
                await Shot(dir, "46a_rebind_waiting");
                await PressKey(Key.I);
                await Wait(0.4);
                await Shot(dir, "46b_rebound");
            }
            view.Show(OptionsView.Page.Interface);
            await Wait(0.5);
            await Shot(dir, "47_options_interface");
            view.Show(OptionsView.Page.Gameplay);
            await Wait(0.5);
            await Shot(dir, "48_options_gameplay");
            view.Show(OptionsView.Page.Credits);
            await Wait(0.5);
            await Shot(dir, "49_options_credits");
        }
        menu.Open("credits");
        await Wait(0.8);
        await Shot(dir, "50_credits");
        GetTree().Quit();
    }

    async Task PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    static OptionsView? FindOptions(Node n) => n as OptionsView ?? n.GetChildren().Select(FindOptions).FirstOrDefault(x => x != null);

    /// <summary>--capture-frontend dir: the loading screen, the game menu, in-game options and save / load.</summary>
    async Task CaptureFrontEnd(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && _session.Controlled == null; i++) await Wait(0.1);
        await Wait(3.0);
        // The loader as an area transition would show it.
        if (_session.Area is { } area)
        {
            _loading.Present(_session.ModuleName, area.Info.Name, _session.Content, area.Info.LoadScreen, area.Info.Tileset, false, "Building the area…");
            await Wait(1.2);
            await Shot(dir, "60_loading");
            _loading.SetStatus("Downloading module content  ·  48.2 of 120.5 MB", 0.4f);
            await Wait(0.6);
            await Shot(dir, "61_loading_download");
            _loading.Finish(0);
            await Wait(1.0);
        }
        // Esc: first closes the open window, then brings up the game menu, then resumes.
        Inventory.Toggle();
        await Wait(0.6);
        await Shot(dir, "62a_inventory_open");
        await PressKey(Key.Escape);
        await Wait(0.5);
        GD.Print($"[capture] after Esc: inventory {(Inventory.Visible ? "open" : "closed")}, menu {(_gameMenu!.IsOpen ? "open" : "closed")}");
        await PressKey(Key.Escape);
        await Wait(0.8);
        GD.Print($"[capture] after 2nd Esc: menu {(_gameMenu.IsOpen ? "open" : "closed")}");
        await Shot(dir, "62_game_menu");
        await PressKey(Key.Escape);
        await Wait(0.5);
        GD.Print($"[capture] after 3rd Esc: menu {(_gameMenu.IsOpen ? "open" : "closed")}");
        _options!.Open(OptionsView.Page.Graphics);
        await Wait(0.8);
        await Shot(dir, "63_options_window");
        _options.Open(OptionsView.Page.Controls);
        await Wait(0.6);
        await Shot(dir, "64_options_controls");
        ((UiTheme.Window)_options).Close();
        await Wait(0.3);
        _saves!.Open(SaveLoadPanel.Mode.Save);
        await Wait(1.5);
        await Shot(dir, "65_save");
        _saves.SaveAs("Capture save");
        await Wait(2.5);
        await Shot(dir, "66_saved");
        _saves.Open(SaveLoadPanel.Mode.Load);
        await Wait(1.5);
        await Shot(dir, "67_load");
        _saves.Close();
        await Wait(0.3);
        _gameMenu.Open();
        await Wait(0.4);
        UI.Kit.Dialogs.Confirm(_gameMenu, "Exit to the main menu?", "Progress since your last save will be lost.", "Exit", () => { }, danger: true);
        await Wait(0.5);
        await Shot(dir, "68_confirm");
        if (_args.ContainsKey("exit-test")) { ExitToMainMenu(); return; }
        GetTree().Quit();
    }
}

static class SettingsExtensions
{
    public static Preferences.GameSettings Also(this Preferences.GameSettings s, Action<Preferences.GameSettings> change)
    {
        change(s);
        return s;
    }
}

/// <summary>Frames per second and frame time in the top-right corner (Options → Interface).</summary>
public partial class FpsOverlay : CanvasLayer
{
    Label _label = null!;
    double _acc;
    int _frames;
    double _worst;

    public override void _Ready()
    {
        Layer = 90;
        _label = UI.Kit.Ui.Overlay("", 13, UI.Kit.Palette.TextSecondary, UI.Kit.Fonts.UiMedium);
        _label.AnchorLeft = _label.AnchorRight = 1;
        _label.OffsetLeft = -220; _label.OffsetRight = -14; _label.OffsetTop = 8;
        _label.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_label);
        _label.Visible = Settings.Current.Interface.ShowFps;
        Settings.Changed += OnSettings;
    }

    public override void _ExitTree() => Settings.Changed -= OnSettings;

    void OnSettings(Preferences.GameSettings s) => _label.Visible = s.Interface.ShowFps;

    public override void _Process(double delta)
    {
        if (!_label.Visible) return;
        _acc += delta;
        _frames++;
        _worst = Math.Max(_worst, delta);
        if (_acc < 0.5) return;
        _label.Text = $"{_frames / _acc:0} FPS  ·  {_acc / _frames * 1000:0.0} ms  ·  max {_worst * 1000:0} ms";
        _acc = 0; _frames = 0; _worst = 0;
    }
}

/// <summary>The screenshot key: saves the frame to user://screenshots as a PNG.</summary>
public partial class ScreenshotKey : Node
{
    public override void _UnhandledInput(InputEvent e)
    {
        if (!InputBindings.Pressed(e, ActionCatalog.Screenshot)) return;
        GetViewport().SetInputAsHandled();
        var image = GetViewport().GetTexture().GetImage();
        var dir = ProjectSettings.GlobalizePath("user://screenshots");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"openwinter_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        image.SavePng(path);
        GD.Print($"[client] screenshot saved to {path}");
    }
}
