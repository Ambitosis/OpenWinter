using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// The in-game menu (Esc with no window open): the game blurred and dimmed behind, the module and area,
/// and Resume, Save Game, Load Game, Options, Exit to Main Menu, Quit. Single player pauses while it is
/// open (Options → Gameplay). Wire the actions; <see cref="Toggle"/> opens and closes it.
/// </summary>
public partial class GameMenu : Control
{
    public Action? SaveGame, LoadGame, Options, ExitToMenu;
    /// <summary>Runs before the menu appears (the save thumbnail is taken of the game without the menu).</summary>
    public Func<Task>? BeforeOpen;
    bool _opening;
    /// <summary>Single player (save / load available, the world pauses).</summary>
    public bool SinglePlayer = true;
    public ClientSession? Session;

    Label _module = null!, _area = null!, _paused = null!;
    VBoxContainer _rows = null!;
    MenuEntry _save = null!, _load = null!;
    ShaderMaterial _blur = null!;
    Tween? _tween;

    /// <summary>True while the menu is up.</summary>
    public bool IsOpen => Visible;

    const string BlurShader = """
        shader_type canvas_item;
        uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
        uniform float amount = 1.0;
        void fragment() {
            float lod = 3.2 * amount;
            vec2 o = SCREEN_PIXEL_SIZE * 10.0 * amount;
            vec3 c = textureLod(screen_tex, SCREEN_UV, lod).rgb * 0.36;
            c += textureLod(screen_tex, SCREEN_UV + vec2(o.x, 0.0), lod).rgb * 0.16;
            c += textureLod(screen_tex, SCREEN_UV - vec2(o.x, 0.0), lod).rgb * 0.16;
            c += textureLod(screen_tex, SCREEN_UV + vec2(0.0, o.y), lod).rgb * 0.16;
            c += textureLod(screen_tex, SCREEN_UV - vec2(0.0, o.y), lod).rgb * 0.16;
            float l = dot(c, vec3(0.299, 0.587, 0.114));
            c = mix(c, vec3(l), 0.45 * amount);
            // Darker on the left where the menu sits.
            float shade = mix(0.28, 0.62, smoothstep(0.0, 0.75, UV.x));
            c *= mix(1.0, shade, amount);
            COLOR = vec4(c, 1.0);
        }
        """;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Theme = GameTheme.Theme;
        _blur = new ShaderMaterial { Shader = new Shader { Code = BlurShader } };
        AddChild(new ColorRect { AnchorRight = 1, AnchorBottom = 1, Material = _blur, MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new Atmosphere { Smoke = 0, LeftShade = 0.3f, Vignette = 0.7f, Embers = 0.45f });

        var column = new VBoxContainer { AnchorBottom = 1, OffsetLeft = 92, OffsetRight = 92 + 460 };
        column.AddThemeConstantOverride("separation", 0);
        AddChild(column);
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.6f });
        _module = Ui.Text("", Ui.TextStyle.Caption, Palette.Accent, 13);
        _module.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiBold, SpacingGlyph = 3 });
        _module.ClipText = true;
        column.AddChild(_module);
        column.AddChild(new GlowTitle { Text = "Game Menu", FontSize = 50, Spacing = 0.02f, Font = Fonts.Display });
        var sub = Ui.HBox(Palette.Space2);
        _area = Ui.Text("", Ui.TextStyle.Secondary);
        sub.AddChild(_area);
        _paused = Ui.Text("PAUSED", Ui.TextStyle.Caption, Palette.Warning);
        _paused.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiBold, SpacingGlyph = 2 });
        var pausedChip = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        pausedChip.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Warning with { A = 0.1f }, 9, Palette.Warning with { A = 0.4f }, 1, 8, 1));
        pausedChip.AddChild(_paused);
        sub.AddChild(pausedChip);
        column.AddChild(sub);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });
        column.AddChild(new Divider { Color = Palette.Accent with { A = 0.7f }, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(360, 1), SizeFlagsHorizontal = SizeFlags.ShrinkBegin });
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });
        _rows = Ui.VBox(2);
        _rows.CustomMinimumSize = new Vector2(380, 0);
        _rows.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        column.AddChild(_rows);
        _rows.AddChild(MenuEntry.Create("Resume", null, Close, 22));
        _save = MenuEntry.Create("Save Game", null, () => Then(SaveGame), 22);
        _rows.AddChild(_save);
        _load = MenuEntry.Create("Load Game", null, () => Then(LoadGame), 22);
        _rows.AddChild(_load);
        _rows.AddChild(MenuEntry.Create("Options", null, () => Options?.Invoke(), 22));
        _rows.AddChild(MenuEntry.Create("Exit to Main Menu", null, ConfirmExit, 22));
        _rows.AddChild(MenuEntry.Create("Quit to Desktop", null, ConfirmQuit, 22));
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        var hints = FrontEndUi.KeyHints(("↑ ↓", "Navigate"), ("Enter", "Select"), ("Esc", "Resume"));
        hints.AnchorLeft = hints.AnchorRight = hints.AnchorTop = hints.AnchorBottom = 1;
        hints.OffsetLeft = -420; hints.OffsetRight = -64; hints.OffsetTop = -50; hints.OffsetBottom = -26;
        hints.Alignment = BoxContainer.AlignmentMode.End;
        AddChild(hints);
    }

    void Then(Action? action)
    {
        Close();
        action?.Invoke();
    }

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    public void Open() => _ = OpenAsync();

    async Task OpenAsync()
    {
        if (Visible || _opening) return;
        if (BeforeOpen != null)
        {
            _opening = true;
            try { await BeforeOpen(); }
            finally { _opening = false; }
        }
        _module.Text = NwnText.Strip(Session?.ModuleName ?? "").ToUpperInvariant();
        _area.Text = NwnText.Strip(Session?.Area?.Info.Name ?? "");
        _save.Disabled = _load.Disabled = !SinglePlayer || Session?.Controlled == null;
        _save.TooltipText = _load.TooltipText = SinglePlayer ? "" : "Saving is only available in single player.";
        bool pauses = SinglePlayer && Settings.Current.Gameplay.PauseInMenus;
        ((Control)_paused.GetParent()).Visible = pauses;
        Visible = true;
        PlayerInput.Blocked = true;
        _tween?.Kill();
        _blur.SetShaderParameter("amount", 0f);
        Modulate = Modulate with { A = 0 };
        _tween = CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _tween.TweenProperty(this, "modulate:a", 1f, 0.18);
        _tween.TweenMethod(Callable.From<float>(v => _blur.SetShaderParameter("amount", v)), 0f, 1f, 0.25);
        int i = 0;
        foreach (var row in _rows.GetChildren().OfType<Control>())
        {
            row.Modulate = row.Modulate with { A = 0 };
            _tween.TweenProperty(row, "modulate:a", 1f, 0.2).SetDelay(0.04 * i++);
        }
        Callable.From(() => ((Control)_rows.GetChild(0)).GrabFocus()).CallDeferred();
    }

    public void Close()
    {
        if (!Visible) return;
        _tween?.Kill();
        _tween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _tween.TweenProperty(this, "modulate:a", 0f, 0.14);
        _tween.TweenCallback(Callable.From(() =>
        {
            Visible = false;
            PlayerInput.Blocked = false;
        }));
        GetViewport().GuiReleaseFocus();
    }

    /// <summary>The world pauses (single player) while the menu, or a window opened from it, is up.</summary>
    public bool Pausing => SinglePlayer && Settings.Current.Gameplay.PauseInMenus && Visible;

    void ConfirmExit() => Dialogs.Confirm(this, "Exit to the main menu?",
        SinglePlayer ? "Progress since your last save will be lost." : "You will leave the server.", "Exit", () => ExitToMenu?.Invoke(), danger: true);

    void ConfirmQuit() => Dialogs.Confirm(this, "Quit to the desktop?",
        SinglePlayer ? "Progress since your last save will be lost." : "You will leave the server.", "Quit", () => GetTree().Quit(), danger: true);

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode == Key.Escape)
        {
            // A window opened from the menu (Options) closes first.
            if (!WindowStack.CloseTop()) Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        // Nothing reaches the game while the menu is up.
        if (e is InputEventKey) GetViewport().SetInputAsHandled();
    }
}
