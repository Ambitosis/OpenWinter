using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.FrontEnd;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's in-game options menu (options_main on pnl_opt_gen), centred: Load, Save and Quit in the top row,
/// Game Options, Resolution and UI Scaling, Change Key Settings, Exit to Main Menu and Resume Game. As in the
/// original the HUD steps aside while it is up and single player pauses (Options → Gameplay). The option pages
/// open the Options window; Load and Save the save / load window (both from the modern interface for now).
/// </summary>
public partial class ClassicGameMenu : Control
{
    public ClassicContext Ctx = null!;
    public GuiPanel Panel = null!;
    public bool SinglePlayer;
    bool _opening, _hudWasVisible;

    /// <summary>The open menu pauses single player (read by Main's world clock).</summary>
    public static bool Pausing => Current is { } m && IsInstanceValid(m) && m.Visible && m.SinglePlayer && Settings.Current.Gameplay.PauseInMenus;
    public static ClassicGameMenu? Current { get; private set; }

    public bool IsOpen => Visible;
    /// <summary>The original draws its build number in grey (measured).</summary>
    static readonly Color BuildColor = Color.Color8(102, 102, 102);

    public override void _Ready()
    {
        Current = this;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Panel = GuiPanel.FromLayout(Ctx.Assets, "options_main") ?? GuiPanel.FromModel(Ctx.Assets, "pnl_opt_gen");
        AddChild(Panel);
        Resized += Center;
        // As the original shows it: the build in the title bar, its own captions for the renamed buttons, no
        // separate sound page (sound is under Game Options there; here it opens the Options window's audio page).
        if (Panel.Get<GuiLabel>("BuildNumber") is { } build)
        {
            // The original's build string overflows its box and so ends at the box's x; a shorter one would sit
            // at the box's far edge, off the frame: keep it where the original's ends.
            build.Text = "OpenWinter";
            build.Color = BuildColor;
            build.Position -= new Vector2(build.Size.X, 0);
        }
        Caption("VideoOptionsButton", "Resolution and UI Scaling");
        Caption("ControlsButton", "Change Key Settings");
        Caption("SaveCharButton", "Quit");
        if (Panel.Get<GuiButton>("SoundOptionsButton") is { } sound) sound.Visible = false;
        Panel.ButtonPressed += OnButton;
        if (Panel.Get<GuiButton>("XButton") is { } x) x.Pressed += Close;
    }

    void Caption(string tag, string text)
    {
        if (Panel.Get<GuiButton>(tag) is { } b) b.Caption = text;
    }

    void Center() => ClassicPopup.CenterOn(Panel, Size);

    void OnButton(string tag)
    {
        var menu = Ctx.GameMenu;
        switch (tag)
        {
            case "ResumeGameButton": Close(); break;
            case "LoadButton": Then(menu?.LoadGame); break;
            case "SaveButton": Then(menu?.SaveGame); break;
            case "SaveCharButton": ConfirmQuit(); break;
            case "GameButton": Options(OptionsView.Page.Gameplay); break;
            case "VideoOptionsButton": Options(OptionsView.Page.Graphics); break;
            case "SoundOptionsButton": Options(OptionsView.Page.Audio); break;
            case "ControlsButton": Options(OptionsView.Page.Controls); break;
            case "ExitButton": ConfirmExit(); break;
        }
    }

    void Then(Action? action)
    {
        Close();
        action?.Invoke();
    }

    OptionsWindow? _options;

    void Options(OptionsView.Page page)
    {
        _options ??= Ctx.Main.FindChildren("*", "", true, false).OfType<OptionsWindow>().FirstOrDefault();
        if (_options != null) _options.Open(page);
        else Ctx.GameMenu?.Options?.Invoke();
    }

    void ConfirmExit() => ClassicPopup.OkCancel(this, Ctx.Assets,
        SinglePlayer ? "Exit to the main menu? Progress since your last save will be lost." : "Exit to the main menu? You will leave the server.",
        () => Ctx.Main.ExitToMainMenu());

    void ConfirmQuit() => ClassicPopup.OkCancel(this, Ctx.Assets,
        SinglePlayer ? "Quit to the desktop? Progress since your last save will be lost." : "Quit to the desktop? You will leave the server.",
        () => GetTree().Quit());

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    public void Open() => _ = OpenAsync();

    async Task OpenAsync()
    {
        if (Visible || _opening) return;
        // The save thumbnail is taken of the game without the menu (as the modern menu does).
        if (Ctx.GameMenu?.BeforeOpen is { } before)
        {
            _opening = true;
            try { await before(); }
            finally { _opening = false; }
        }
        bool canSave = SinglePlayer && Ctx.Session.Controlled != null;
        if (Panel.Get<GuiButton>("SaveButton") is { } save) save.Disabled = !canSave;
        if (Panel.Get<GuiButton>("LoadButton") is { } load) load.Disabled = !canSave;
        _hudWasVisible = Ctx.Hud.Visible;
        Ctx.Hud.Visible = false;
        Visible = true;
        GetParent()?.MoveChild(this, -1);
        Center();
        PlayerInput.Blocked = true;
    }

    public void Close()
    {
        if (!Visible) return;
        foreach (var p in GetChildren().OfType<ClassicPopup>()) p.QueueFree();
        Visible = false;
        if (_hudWasVisible) Ctx.Hud.Visible = true;
        PlayerInput.Blocked = false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode == Key.Escape)
        {
            // A window opened from the menu (Options, save / load) closes first.
            var top = WindowStack.Windows.LastOrDefault(w => IsInstanceValid(w) && w.Visible && w.Closable);
            if (top != null) top.Close();
            else Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        // Nothing reaches the game while the menu is up.
        if (e is InputEventKey) GetViewport().SetInputAsHandled();
    }
}
