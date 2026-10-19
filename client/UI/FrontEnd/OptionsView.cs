using OpenWinter.Client.Preferences;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// The options screen: a category rail (Graphics, Audio, Controls, Interface, Gameplay, Credits) and a
/// page of rows. Changes apply at once as a preview; Apply saves them, leaving without applying offers
/// to keep or discard; Reset restores the defaults of the page shown. Embedded in the main menu and
/// in the in-game <see cref="OptionsWindow"/>.
/// </summary>
public partial class OptionsView : VBoxContainer
{
    public enum Page { Graphics, Audio, Controls, Interface, Gameplay, Credits }

    /// <summary>Called when the view wants to be closed (after keep / discard was settled).</summary>
    public Action? Done;

    GameSettings _saved = null!, _work = null!;
    Page _page = Page.Graphics;
    readonly Dictionary<Page, Button> _tabs = new();
    VBoxContainer _content = null!;
    ScrollContainer _scroll = null!;
    Label _status = null!;
    Button _apply = null!, _reset = null!;
    Control _footer = null!;
    // Key capture: the action and slot waiting for a key, and its button.
    (string Action, int Slot, Button Button)? _capture;

    static readonly (Page Page, string Label, string Icon)[] Pages =
    {
        (Page.Graphics, "Graphics", "display"), (Page.Audio, "Audio", "sound"), (Page.Controls, "Controls", "keyboard"),
        (Page.Interface, "Interface", "eye"), (Page.Gameplay, "Gameplay", "dice"), (Page.Credits, "Credits", "book"),
    };

    public override void _Ready()
    {
        _saved = Settings.Current.Clone();
        _work = Settings.Current.Clone();
        AddThemeConstantOverride("separation", 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var body = Ui.HBox(Palette.Space4);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(body);

        // Category rail.
        var rail = Ui.VBox(4);
        rail.CustomMinimumSize = new Vector2(160, 0);
        body.AddChild(rail);
        var group = new ButtonGroup();
        foreach (var (page, label, icon) in Pages)
        {
            var b = RailButton(label, icon, group);
            b.Pressed += () => Show(page);
            rail.AddChild(b);
            _tabs[page] = b;
        }
        body.AddChild(new VSeparator());

        _scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(_scroll);
        _content = Ui.VBox(Palette.Space2);
        _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pad = Ui.Pad(_content, 0, 0, 16, 16);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(pad);

        // Footer.
        AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        AddChild(new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.None, CustomMinimumSize = new Vector2(0, 1) });
        AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        var footer = Ui.HBox(Palette.Space2);
        _footer = footer;
        AddChild(footer);
        _reset = Ui.Ghost("Reset page to defaults", ResetPage);
        _reset.Icon = Icons.Get("rest", 32);
        footer.AddChild(_reset);
        _status = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        _status.VerticalAlignment = VerticalAlignment.Center;
        footer.AddChild(_status);
        var revert = Ui.Button("Revert", Revert);
        revert.CustomMinimumSize = new Vector2(100, 38);
        footer.AddChild(revert);
        _apply = Ui.Primary("Apply", Apply);
        _apply.CustomMinimumSize = new Vector2(130, 38);
        footer.AddChild(_apply);
        Show(Page.Graphics);
        UpdateDirty();
    }

    static Button RailButton(string label, string icon, ButtonGroup group)
    {
        var b = new Button
        {
            Text = label, ToggleMode = true, ButtonGroup = group, Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.All,
            Icon = Icons.Get(icon, 40), ExpandIcon = false, CustomMinimumSize = new Vector2(0, 44), ThemeTypeVariation = "GhostButton",
        };
        b.AddThemeConstantOverride("icon_max_width", 20);
        b.AddThemeConstantOverride("h_separation", 12);
        b.AddThemeFontSizeOverride("font_size", Palette.FontLarge);
        b.AddThemeFontOverride("font", Fonts.UiMedium);
        var normal = Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 14, 8);
        var hover = Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, null, 0, 14, 8);
        var on = Styles.Box(Palette.Accent with { A = 0.12f }, Palette.RadiusSmall, null, 0, 14, 8);
        on.BorderColor = Palette.Accent;
        on.BorderWidthLeft = 3;
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.AddThemeStyleboxOverride("focus", hover);
        b.AddThemeColorOverride("font_color", Palette.TextSecondary);
        b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("font_hover_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("icon_normal_color", Palette.TextMuted);
        b.AddThemeColorOverride("icon_pressed_color", Palette.Accent);
        b.AddThemeColorOverride("icon_hover_pressed_color", Palette.Accent);
        return b;
    }

    public void Show(Page page)
    {
        _page = page;
        _capture = null;
        _tabs[page].ButtonPressed = true;
        Rebuild();
    }

    public Page Current => _page;

    void Rebuild() => Rebuild(false);

    void Rebuild(bool keepScroll)
    {
        int scroll = _scroll.ScrollVertical;
        FrontEndUi.Clear(_content);
        _reset.Visible = _page != Page.Credits;
        switch (_page)
        {
            case Page.Graphics: BuildGraphics(); break;
            case Page.Audio: BuildAudio(); break;
            case Page.Controls: BuildControls(); break;
            case Page.Interface: BuildInterface(); break;
            case Page.Gameplay: BuildGameplay(); break;
            case Page.Credits: _content.AddChild(new CreditsView { CustomMinimumSize = new Vector2(0, 520) }); break;
        }
        if (keepScroll) Callable.From(() => _scroll.ScrollVertical = scroll).CallDeferred();
        else _scroll.ScrollVertical = 0;
    }

    // ------------------------------------------------------------------ changes

    void Changed(bool rebuild = false)
    {
        _work.Normalize();
        Settings.Preview(_work.Clone());
        UpdateDirty();
        if (rebuild) Callable.From(() => Rebuild(true)).CallDeferred();
    }

    bool Dirty => _work.ToJson() != _saved.ToJson();

    void UpdateDirty()
    {
        _apply.Disabled = !Dirty;
        if (_status.Text.StartsWith("Unapplied") || _status.Text.Length == 0 || _status.Text.StartsWith("Settings saved"))
            _status.Text = Dirty ? "Unapplied changes" : "";
        _status.AddThemeColorOverride("font_color", Dirty ? Palette.Warning : Palette.TextMuted);
    }

    void Status(string text, Color? color = null)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", color ?? Palette.TextSecondary);
    }

    public void Apply()
    {
        Settings.Commit(_work.Clone());
        _saved = _work.Clone();
        UpdateDirty();
        Status("Settings saved", Palette.Success);
    }

    void Revert()
    {
        _work = _saved.Clone();
        Settings.Preview(_saved.Clone());
        Rebuild();
        UpdateDirty();
    }

    void ResetPage()
    {
        var d = new GameSettings();
        switch (_page)
        {
            case Page.Graphics: _work.Graphics = d.Graphics; break;
            case Page.Audio: _work.Audio = d.Audio; break;
            case Page.Controls: _work.Controls = d.Controls; break;
            case Page.Interface: _work.Interface = d.Interface; break;
            case Page.Gameplay: _work.Gameplay = d.Gameplay; break;
        }
        Changed(rebuild: true);
        Status($"{_page} reset to defaults");
    }

    /// <summary>Leaving: with unapplied changes, ask whether to keep them.</summary>
    public void RequestClose()
    {
        if (_capture != null) { EndCapture(); return; }
        if (!Dirty) { Done?.Invoke(); return; }
        Dialogs.Confirm(GetTree().Root, "Apply changes?", "Some options were changed but not applied.", "Apply", () =>
        {
            Apply();
            Done?.Invoke();
        }, cancel: "Discard", onCancel: () =>
        {
            Revert();
            Done?.Invoke();
        });
    }

    // ------------------------------------------------------------------ rows

    Control Section(string title)
    {
        var s = Ui.Section(title);
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, _content.GetChildCount() == 0 ? 0 : 14) });
        _content.AddChild(s);
        return s;
    }

    Control Row(string label, string? hint, Control control)
    {
        var row = new PanelContainer();
        var normal = Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 12, 7);
        var hover = Styles.Box(new Color(1, 1, 1, 0.035f), Palette.RadiusSmall, null, 0, 12, 7);
        row.AddThemeStyleboxOverride("panel", normal);
        row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hover);
        row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);
        var h = Ui.HBox(Palette.Space4);
        row.AddChild(h);
        var text = Ui.VBox(1);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.MouseFilter = MouseFilterEnum.Ignore;
        text.AddChild(Ui.Text(label, Ui.TextStyle.Body));
        if (hint != null)
        {
            var hl = Ui.Text(hint, Ui.TextStyle.Caption, Palette.TextMuted);
            hl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.AddChild(hl);
        }
        h.AddChild(text);
        control.CustomMinimumSize = new Vector2(Math.Max(control.CustomMinimumSize.X, 270), Math.Max(control.CustomMinimumSize.Y, 34));
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(control);
        _content.AddChild(row);
        return row;
    }

    Control Toggle(string label, string? hint, bool value, Action<bool> set)
    {
        var box = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var t = new CheckButton { ButtonPressed = value, FocusMode = FocusModeEnum.All, Text = value ? "On" : "Off" };
        t.AddThemeColorOverride("font_color", Palette.TextSecondary);
        t.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
        t.Toggled += on => { t.Text = on ? "On" : "Off"; set(on); Changed(); };
        box.AddChild(t);
        return Row(label, hint, box);
    }

    Control Slider(string label, string? hint, float value, float min, float max, float step, Func<float, string> format, Action<float> set)
    {
        var box = Ui.HBox(Palette.Space3);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.All };
        var v = Ui.Text(format(value), Ui.TextStyle.Strong, Palette.Text, Palette.FontSmall + 1);
        v.CustomMinimumSize = new Vector2(56, 0);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        s.ValueChanged += x => { v.Text = format((float)x); set((float)x); Changed(); };
        box.AddChild(s);
        box.AddChild(v);
        return Row(label, hint, box);
    }

    /// <summary>A ‹ value › stepper over a list of choices.</summary>
    Control Choice<T>(string label, string? hint, IReadOnlyList<(T Value, string Name)> options, T current, Action<T> set, bool rebuild = false, bool enabled = true)
    {
        int index = Math.Max(0, options.ToList().FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, current)));
        var box = Ui.HBox(4);
        var prev = IconButton.Create("arrows", "Previous", 34, 14);
        prev.Icon = GameTheme.Svg("<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14'><path d='M9 2L4 7l5 5' stroke='#ffffff' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/></svg>", 14);
        var next = IconButton.Create("arrows", "Next", 34, 14);
        next.Icon = GameTheme.Svg("<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14'><path d='M5 2l5 5-5 5' stroke='#ffffff' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/></svg>", 14);
        var valuePanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        valuePanel.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.3f), Palette.RadiusSmall, Palette.Line, 1, 10, 6));
        var v = Ui.Text(options[index].Name, Ui.TextStyle.Strong, enabled ? Palette.Text : Palette.TextMuted, Palette.FontSmall + 1);
        v.HorizontalAlignment = HorizontalAlignment.Center;
        valuePanel.AddChild(v);
        // Position pips under the value.
        var pips = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        pips.AddThemeConstantOverride("separation", 3);
        var stack = Ui.VBox(3);
        stack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stack.AddChild(valuePanel);
        if (options.Count <= 12)
        {
            for (int i = 0; i < options.Count; i++)
                pips.AddChild(new ColorRect { CustomMinimumSize = new Vector2(options.Count > 8 ? 8 : 14, 2), Color = i == index ? Palette.Accent : Palette.LineStrong, MouseFilter = MouseFilterEnum.Ignore });
            stack.AddChild(pips);
        }
        void Step(int d)
        {
            if (!enabled) return;
            index = (index + d + options.Count) % options.Count;
            v.Text = options[index].Name;
            for (int i = 0; i < pips.GetChildCount(); i++) ((ColorRect)pips.GetChild(i)).Color = i == index ? Palette.Accent : Palette.LineStrong;
            set(options[index].Value);
            Changed(rebuild);
        }
        prev.Pressed += () => Step(-1);
        next.Pressed += () => Step(1);
        prev.Disabled = next.Disabled = !enabled;
        // Clicking the value steps forward, right-click back; wheel scrolls.
        valuePanel.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true } mb)
            {
                if (mb.ButtonIndex == MouseButton.Left) Step(1);
                else if (mb.ButtonIndex == MouseButton.Right) Step(-1);
            }
        };
        valuePanel.MouseDefaultCursorShape = CursorShape.PointingHand;
        box.AddChild(prev);
        box.AddChild(stack);
        box.AddChild(next);
        return Row(label, hint, box);
    }

    static (T, string)[] Enum<T>(params (T, string)[] items) => items;

    // ------------------------------------------------------------------ pages

    void BuildGraphics()
    {
        var g = _work.Graphics;
        Section("Display");
        Choice("Window mode", "Borderless fills the screen and switches instantly; fullscreen takes the display exclusively.",
            Enum((WindowMode.Windowed, "Windowed"), (WindowMode.Borderless, "Borderless"), (WindowMode.Fullscreen, "Fullscreen")),
            g.WindowMode, v => g.WindowMode = v, rebuild: true);
        var sizes = GraphicsApply.Resolutions().Select(r => ((r.X, r.Y), $"{r.X} × {r.Y}")).ToList();
        sizes.Insert(0, ((0, 0), "Default (1600 × 900)"));
        Choice("Window size", g.WindowMode == WindowMode.Windowed ? null : "Only used in windowed mode; borderless and fullscreen use the screen's resolution.",
            sizes, (g.Width, g.Height), v => { g.Width = v.Item1; g.Height = v.Item2; }, enabled: g.WindowMode == WindowMode.Windowed);
        Toggle("Vertical sync", "Waits for the display's refresh: no tearing, slightly more input latency.", g.VSync, v => g.VSync = v);
        Choice("Frame rate limit", null, GraphicsSettings.FrameCaps.Select(c => (c, c == 0 ? "Unlimited" : $"{c} fps")).ToList(), g.FrameCap, v => g.FrameCap = v);

        Section("Quality");
        int preset = PresetOf(g);
        Choice("Quality preset", "Sets the options below; change any of them afterwards.",
            Enum((-1, "Custom"), (0, "Low"), (1, "Medium"), (2, "High"), (3, "Ultra")), preset, v => { if (v >= 0) g.ApplyPreset(v); }, rebuild: true);
        Slider("Render scale", "3D resolution relative to the window. Below 100% is faster; above sharpens (supersampling).",
            g.RenderScale, 0.5f, 2f, 0.05f, v => $"{v * 100:0}%", v => g.RenderScale = v);
        Choice("Upscaling", "How a reduced render scale is brought back up to the window.",
            Enum((Upscaler.Bilinear, "Bilinear"), (Upscaler.Fsr1, "AMD FSR 1"), (Upscaler.Fsr2, "AMD FSR 2")), g.Upscaler, v => g.Upscaler = v);
        Slider("Sharpening", "For FSR upscaling (0 is sharpest).", g.Sharpness, 0f, 2f, 0.1f, v => v.ToString("0.0"), v => g.Sharpness = v);
        Choice("Multisample antialiasing", "Smooths geometry edges (MSAA).",
            Enum((Msaa.Off, "Off"), (Msaa.X2, "2×"), (Msaa.X4, "4×"), (Msaa.X8, "8×")), g.Msaa, v => g.Msaa = v);
        Choice("Post-process antialiasing", "Also smooths texture edges (foliage, fences). TAA is temporal.",
            Enum((PostAntiAlias.Off, "Off"), (PostAntiAlias.Fxaa, "FXAA"), (PostAntiAlias.Smaa, "SMAA"), (PostAntiAlias.Taa, "TAA")), g.PostAntiAlias, v => g.PostAntiAlias = v);
        Choice("Shadows", null,
            Enum((ShadowQuality.Off, "Off"), (ShadowQuality.Low, "Low"), (ShadowQuality.Medium, "Medium"), (ShadowQuality.High, "High"), (ShadowQuality.Ultra, "Ultra")), g.Shadows, v => g.Shadows = v);
        Choice("Texture filtering", "Keeps ground textures sharp at shallow angles.",
            Enum((TextureFiltering.Bilinear, "Bilinear"), (TextureFiltering.Aniso2, "Anisotropic 2×"), (TextureFiltering.Aniso4, "Anisotropic 4×"), (TextureFiltering.Aniso8, "Anisotropic 8×"), (TextureFiltering.Aniso16, "Anisotropic 16×")),
            g.TextureFiltering, v => g.TextureFiltering = v);
        Toggle("Ambient occlusion", "Soft contact shadows in corners and under objects (SSAO).", g.Ssao, v => g.Ssao = v);
        Toggle("Bloom", "A soft glow around bright lights and spell effects.", g.Glow, v => g.Glow = v);
        Toggle("See-through walls", "Cut a hole through walls and roofs that hide you from the camera (the original's keyholing).", g.Keyholing, v => g.Keyholing = v);

        Section("Picture");
        Slider("Brightness", null, g.Brightness, 0.5f, 1.5f, 0.05f, v => $"{v * 100:0}%", v => g.Brightness = v);
        Slider("Gamma", "Raise it if dark areas look black; 2.2 is neutral.", g.Gamma, 1.6f, 2.8f, 0.05f, v => v.ToString("0.00"), v => g.Gamma = v);
    }

    static int PresetOf(GraphicsSettings g)
    {
        for (int level = 0; level <= 3; level++)
        {
            var p = new GraphicsSettings();
            p.ApplyPreset(level);
            if (p.Shadows == g.Shadows && p.Msaa == g.Msaa && p.PostAntiAlias == g.PostAntiAlias && p.Ssao == g.Ssao && p.Glow == g.Glow
                && p.TextureFiltering == g.TextureFiltering && Math.Abs(p.RenderScale - g.RenderScale) < 0.01f) return level;
        }
        return -1;
    }

    void BuildAudio()
    {
        var a = _work.Audio;
        Section("Volume");
        foreach (var (bus, label) in AudioSettings.Buses)
        {
            string b = bus;
            Slider(label, bus switch
            {
                "Ambient" => "Area ambience, placed sounds and weather.",
                "Effects" => "Combat, spells and the world's sound effects.",
                "Voice" => "Spoken dialogue.",
                "Interface" => "Clicks and interface cues.",
                _ => null,
            }, a.Get(b), 0, 1, 0.01f, v => $"{v * 100:0}%", v => a.Set(b, v));
        }
        Section("Behaviour");
        Slider("2D/3D bias", "Left favours interface and regional sounds; right favours sounds positioned in the world. Music is unchanged.",
            a.SpatialBias, 0.1f, 1.9f, 0.1f, v => $"{v:0.0}", v => a.SpatialBias = v);
        Toggle("Mute in the background", "Silences the game while another window has focus.", a.MuteWhenUnfocused, v => a.MuteWhenUnfocused = v);
    }

    void BuildInterface()
    {
        var i = _work.Interface;
        Section("Layout");
        Choice("Interface style", "Classic is the original game's interface, drawn from its own art; Modern is OpenWinter's redesigned one. Takes effect from the next menu or game.",
            new List<(string, string)> { ("classic", "Classic"), ("modern", "Modern") }, i.Style, v => i.Style = v);
        int auto = (int)MathF.Round(InterfaceSettings.AutoScale((int)GetTree().Root.Size.Y) * 100);
        Choice("Interface scale", "Automatic follows the window height (100% up to 1080 lines).",
            new[] { 0f, 0.75f, 0.85f, 1f, 1.1f, 1.25f, 1.5f, 1.75f, 2f }.Select(s => (s, s == 0 ? $"Automatic ({auto}%)" : $"{s * 100:0}%")).ToList(),
            i.UiScale, v => i.UiScale = v);
        Toggle("Frosted glass", "Panels blur what is behind them. Turn off on slower graphics cards.", i.Glass, v => i.Glass = v);
        var scenes = new List<(string, string)> { ("", "Default") };
        scenes.AddRange(MainMenu.Scenes.Select(s => (s.Id, s.Name)));
        scenes.Add(("module", "Your configured module"));
        scenes.Add(("none", "None (smoke and embers)"));
        Choice("Main menu scene", "The area seen behind the main menu (from the official campaign when installed).", scenes, i.MenuScene, v => i.MenuScene = v);
        Section("Information");
        Toggle("Show frame rate", "Frames per second and frame time in the top-right corner.", i.ShowFps, v => i.ShowFps = v);
        Toggle("Floating combat text", "Damage numbers and misses over creatures.", i.CombatText, v => i.CombatText = v);
        Choice("Text bubble mode", "Nameplates over creatures and hovered objects.",
            new List<(int, string)> { (0, "Off"), (1, "Text only"), (2, "Full (text, portrait, and bubble)") }, i.TextBubbleMode, v => i.TextBubbleMode = v);
        Toggle("Original cursors", "The classic gauntlet cursors (and any a module's haks provide); off uses the modern set.", i.OriginalCursors,
            v => { i.OriginalCursors = v; HudParts.Cursors.InstallPointer(); });
        Toggle("Quickbar key labels", "Show each classic quickbar slot's key (F1 ...) over what it holds too; the original shows it only on empty slots.",
            i.QuickbarKeyLabels, v => i.QuickbarKeyLabels = v);
        Slider("Chat text size", null, i.ChatFontSize, 11, 24, 1, v => $"{v:0} px", v => i.ChatFontSize = (int)v);
        Toggle("High-resolution font", "Use the original serif face for classic chat, conversations and creature labels. Takes effect in the next game.", i.HighResolutionFont, v => i.HighResolutionFont = v);
        Slider("Overhead text size", "What creatures say and floating text over them.", i.OverheadTextSize, 11, 30, 1, v => $"{v:0} px", v => i.OverheadTextSize = (int)v);
    }

    /// <summary>What each difficulty changes (measured on the original: docs/DIFFICULTY.md).</summary>
    const string DifficultyNote = "Single player. Easy: enemies' weapons hit you for less, yours hit harder, no critical hits on you. "
        + "Normal: no critical hits on you. Hardcore: full rules; your area spells can hurt your companions. Very Difficult: enemies' weapons hit harder.";

    void BuildGameplay()
    {
        var g = _work.Gameplay;
        // The original's game difficulty (settings.tml ruleset.difficulty, labels from dialog.tlk 66786-66792).
        Section("Difficulty");
        Choice("Game difficulty", DifficultyNote,
            new List<(int, string)> { (1, "Easy"), (2, "Normal"), (3, "D&D Hardcore Rules"), (4, "Very Difficult") }, g.Difficulty, v => g.Difficulty = v);
        Section("Camera");
        var modeKey = InputBindings.KeyText(ActionCatalog.CameraMode);
        Choice("Camera mode", "Top down stays where you turn it; chase swings round behind you as you move; driving stays right behind you."
            + (modeKey.Length > 0 ? $" {modeKey} switches between them." : ""),
            new[] { Gameplay.CameraMode.TopDown, Gameplay.CameraMode.Chase, Gameplay.CameraMode.Driving }.Select(m => ((int)m, Gameplay.CameraModes.Name(m))).ToList(),
            g.CameraMode, v => g.CameraMode = v);
        Slider("Mouse sensitivity", "Turning the camera with the middle mouse button.", g.CameraSensitivity, 0.25f, 3f, 0.05f, v => $"{v:0.00}×", v => g.CameraSensitivity = v);
        Toggle("Invert vertical", "Moving the mouse up tilts the camera down.", g.InvertY, v => g.InvertY = v);
        Slider("Keyboard turn speed", null, g.CameraKeySpeed, 0.25f, 3f, 0.05f, v => $"{v:0.00}×", v => g.CameraKeySpeed = v);
        Section("Movement");
        Toggle("Always walk", "Walk by default; hold the walk key to run.", g.AlwaysWalk, v => g.AlwaysWalk = v);
        Toggle("Camera collision", "Pull the camera in front of walls instead of fading them (the original fades them).", g.CameraCollision, v => g.CameraCollision = v);
        Section("Single player");
        Toggle("Pause in menus", "The game stops while the game menu or options are open.", g.PauseInMenus, v => g.PauseInMenus = v);
        Toggle("Pause in the background", "The game stops while another window has focus.", g.PauseOnFocusLoss, v => g.PauseOnFocusLoss = v);
        Section("Loading screens");
        Toggle("Show hints", "Gameplay hints while areas load.", g.LoadingHints, v => g.LoadingHints = v);
    }

    // ------------------------------------------------------------------ controls

    void BuildControls()
    {
        var set = new BindingSet(_work.Controls);
        var top = Ui.HBox(Palette.Space3);
        var note = Ui.Text("Click a key to change it, then press the new key. Esc cancels, Backspace clears. A key taken from another action is moved here.", Ui.TextStyle.Caption, Palette.TextMuted);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(note);
        _content.AddChild(top);
        var conflicts = set.Conflicts();
        foreach (var category in ActionCatalog.Categories)
        {
            Section(category);
            var head = Ui.HBox(Palette.Space2);
            head.AddChild(Ui.Spacer());
            foreach (var t in new[] { "Primary", "Alternate" })
            {
                var l = Ui.Text(t.ToUpperInvariant(), Ui.TextStyle.Caption, Palette.TextMuted);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                l.CustomMinimumSize = new Vector2(108, 0);
                head.AddChild(l);
            }
            head.AddChild(new Control { CustomMinimumSize = new Vector2(34, 0) });
            _content.AddChild(Ui.Pad(head, 12, 0, 12, 0));
            foreach (var action in ActionCatalog.All.Where(a => a.Category == category))
            {
                var keys = Ui.HBox(Palette.Space2);
                for (int slot = 0; slot < ActionCatalog.SlotsPerAction; slot++)
                    keys.AddChild(KeyButton(set, action, slot, conflicts.Any(c => (c.A == action.Id || c.B == action.Id) && set.Get(action.Id, slot) == c.Bind)));
                var reset = IconButton.Create("rest", "Restore the default keys", 30, 14, () =>
                {
                    set.Reset(action.Id);
                    Changed(rebuild: true);
                });
                reset.Modulate = set.IsCustom(action.Id) ? Colors.White : Colors.Transparent;
                reset.Disabled = !set.IsCustom(action.Id);
                keys.AddChild(reset);
                keys.CustomMinimumSize = new Vector2(262, 0);
                var row = Row(action.Label, null, keys);
                if (!action.Rebindable) row.TooltipText = "This key is fixed.";
            }
        }
    }

    Button KeyButton(BindingSet set, ActionDef action, int slot, bool conflict)
    {
        var bind = set.Get(action.Id, slot);
        var b = new Button
        {
            Text = bind?.Display(InputBindings.KeyName) ?? "—", CustomMinimumSize = new Vector2(108, 32), FocusMode = FocusModeEnum.All,
            Disabled = !action.Rebindable, ClipText = true,
        };
        var color = conflict ? Palette.Danger : bind == null ? Palette.TextMuted : Palette.Text;
        b.AddThemeColorOverride("font_color", color);
        b.AddThemeFontOverride("font", Fonts.UiBold);
        b.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        var normal = Styles.Box(new Color(0, 0, 0, 0.35f), Palette.RadiusSmall, conflict ? Palette.Danger : Palette.LineStrong, 1, 8, 4);
        normal.BorderWidthBottom = 2;
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("disabled", Styles.Box(new Color(0, 0, 0, 0.18f), Palette.RadiusSmall, Palette.Line, 1, 8, 4));
        b.TooltipText = action.Rebindable ? $"{action.Label} ({(slot == 0 ? "primary" : "alternate")} key)" : "This key is fixed.";
        b.Pressed += () => BeginCapture(action.Id, slot, b);
        return b;
    }

    void BeginCapture(string action, int slot, Button b)
    {
        if (_capture is { } old) old.Button.Text = new BindingSet(_work.Controls).Get(old.Action, old.Slot)?.Display(InputBindings.KeyName) ?? "—";
        _capture = (action, slot, b);
        b.Text = "Press a key…";
        b.AddThemeColorOverride("font_color", Palette.AccentBright);
        b.AddThemeStyleboxOverride("normal", Styles.Box(Palette.Accent with { A = 0.15f }, Palette.RadiusSmall, Palette.Accent, 1, 8, 4));
        b.AddThemeStyleboxOverride("hover", Styles.Box(Palette.Accent with { A = 0.15f }, Palette.RadiusSmall, Palette.Accent, 1, 8, 4));
        Status($"Press a key for \"{ActionCatalog.Find(action)?.Label}\" (Esc cancels, Backspace clears)", Palette.AccentBright);
    }

    void EndCapture()
    {
        _capture = null;
        Status("");
        UpdateDirty();
        Rebuild(true);
    }

    public override void _Input(InputEvent e)
    {
        if (_capture is not { } cap || !IsVisibleInTree()) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode == Key.Escape && !k.CtrlPressed && !k.ShiftPressed)
        {
            GetViewport().SetInputAsHandled();
            EndCapture();
            return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false } del && del.Keycode is Key.Backspace or Key.Delete && !del.CtrlPressed && !del.ShiftPressed && !del.AltPressed)
        {
            GetViewport().SetInputAsHandled();
            new BindingSet(_work.Controls).Assign(cap.Action, cap.Slot, null);
            _capture = null;
            Changed(rebuild: true);
            Status("Key cleared");
            return;
        }
        if (e is InputEventKey { Echo: true }) { GetViewport().SetInputAsHandled(); return; }
        // The click that started the capture (and plain left / right clicks) don't count.
        if (InputBindings.FromEvent(e) is not { } bind)
        {
            if (e is InputEventKey) GetViewport().SetInputAsHandled();
            return;
        }
        GetViewport().SetInputAsHandled();
        var set = new BindingSet(_work.Controls);
        var displaced = set.Assign(cap.Action, cap.Slot, bind);
        _capture = null;
        if (displaced == null)
        {
            Changed(rebuild: true);
            Status($"{bind.Display(InputBindings.KeyName)} is reserved and can't be bound.", Palette.Warning);
            return;
        }
        Changed(rebuild: true);
        var label = ActionCatalog.Find(cap.Action)?.Label;
        Status(displaced.Count == 0
            ? $"{label}: {bind.Display(InputBindings.KeyName)}"
            : $"{bind.Display(InputBindings.KeyName)} moved from {string.Join(", ", displaced.Select(d => ActionCatalog.Find(d)?.Label))} to {label}",
            displaced.Count == 0 ? Palette.Success : Palette.Warning);
    }

    /// <summary>True while waiting for a key (Esc must not close the window then).</summary>
    public bool Capturing => _capture != null;
}

/// <summary>Options in game: the <see cref="OptionsView"/> in a window.</summary>
public partial class OptionsWindow : UiTheme.Window
{
    public OptionsView View = null!;

    public static OptionsWindow Create()
    {
        var w = Create<OptionsWindow>("Options", new Vector2(1060, 700), "options");
        w.Theme = GameTheme.Theme;
        w.AnchorLeft = w.AnchorRight = w.AnchorTop = w.AnchorBottom = 0.5f;
        w.OffsetLeft = -530; w.OffsetRight = 530; w.OffsetTop = -350; w.OffsetBottom = 350;
        return w;
    }

    public override void _Ready()
    {
        Body.AddThemeConstantOverride("separation", 0);
        VisibilityChanged += () =>
        {
            if (!Visible) return;
            View?.QueueFree();
            View = new OptionsView { Done = () => base.Close() };
            Body.AddChild(View);
        };
    }

    /// <summary>Closing (Esc, the close button) asks about unapplied changes first.</summary>
    public override void Close()
    {
        if (View != null && IsInstanceValid(View) && Visible) View.RequestClose();
        else base.Close();
    }

    public void Open(OptionsView.Page page = OptionsView.Page.Graphics)
    {
        Visible = true;
        Callable.From(() => View?.Show(page)).CallDeferred();
    }
}
