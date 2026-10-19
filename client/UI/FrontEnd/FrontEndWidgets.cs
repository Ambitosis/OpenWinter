using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// A main / game menu entry: a "MenuRow" button with a display-face title, an optional caption line
/// under it, a brass bar on hover, focus or selection, and a small slide to the right when active.
/// Keyboard focusable (arrows move between entries, Enter activates).
/// </summary>
public partial class MenuEntry : Button
{
    Label _title = null!;
    Label? _caption;
    Control _inner = null!;
    bool _active;

    public static MenuEntry Create(string title, string? caption = null, Action? pressed = null, int size = 24)
    {
        var b = new MenuEntry { ThemeTypeVariation = "MenuRow", FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(0, caption != null ? 62 : 50) };
        var idle = Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 20, 8);
        // Hover / keyboard focus: a faint wash; the open entry (pressed): a brass bar too.
        var hot = Styles.Box(new Color(1f, 0.92f, 0.75f, 0.06f), Palette.RadiusSmall, null, 0, 20, 8);
        var open = Styles.Box(new Color(0.83f, 0.65f, 0.34f, 0.13f), Palette.RadiusSmall, null, 0, 20, 8);
        open.BorderColor = Palette.Accent;
        open.BorderWidthLeft = 3;
        foreach (var state in new[] { "normal", "disabled" }) b.AddThemeStyleboxOverride(state, idle);
        foreach (var state in new[] { "hover", "focus" }) b.AddThemeStyleboxOverride(state, hot);
        foreach (var state in new[] { "pressed", "hover_pressed" }) b.AddThemeStyleboxOverride(state, open);
        b._inner = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 22, OffsetRight = -12, Alignment = BoxContainer.AlignmentMode.Center };
        b._inner.AddThemeConstantOverride("separation", 1);
        b._title = Ui.Text(title, Ui.TextStyle.Title, Palette.Text, size);
        b._title.AddThemeFontOverride("font", Fonts.Display);
        b._inner.AddChild(b._title);
        if (caption != null)
        {
            b._caption = Ui.Text(caption, Ui.TextStyle.Caption, Palette.TextMuted, Palette.FontCaption);
            b._caption.ClipText = true;
            b._inner.AddChild(b._caption);
        }
        b.AddChild(b._inner);
        if (pressed != null) b.Pressed += pressed;
        b.MouseEntered += () => { if (!b.Disabled) b.GrabFocus(); };
        b.FocusEntered += () => b.Refresh();
        b.FocusExited += () => b.Refresh();
        b.MouseExited += () => b.Refresh();
        b.Toggled += _ => b.Refresh();
        return b;
    }

    public string Caption
    {
        set { if (_caption != null) { _caption.Text = value; _caption.Visible = value.Length > 0; } }
    }

    /// <summary>Shows the entry as the open one (its screen is showing) or not.</summary>
    public void SetActive(bool on)
    {
        if (ToggleMode) SetPressedNoSignal(on);
        Refresh();
    }

    void Refresh()
    {
        bool active = HasFocus() || ButtonPressed;
        if (active == _active) return;
        _active = active;
        _title.AddThemeColorOverride("font_color", Disabled ? Palette.TextMuted : active ? Palette.AccentBright : Palette.Text);
        var tw = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(_inner, "offset_left", active ? 30f : 22f, 0.14);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationReady) _title.AddThemeColorOverride("font_color", Disabled ? Palette.TextMuted : Palette.Text);
    }
}

/// <summary>Bits shared by the full-screen front-end screens.</summary>
public static class FrontEndUi
{
    /// <summary>A screen header: a small brass kicker, a display title and a muted line under it.</summary>
    public static Control Header(string kicker, string title, string? subtitle = null)
    {
        var col = Ui.VBox(2);
        if (kicker.Length > 0)
        {
            var k = Ui.Text(kicker.ToUpperInvariant(), Ui.TextStyle.Caption, Palette.Accent);
            k.AddThemeFontOverride("font", Fonts.UiBold);
            k.AddThemeConstantOverride("line_spacing", 0);
            col.AddChild(k);
        }
        col.AddChild(Ui.Text(title, Ui.TextStyle.Heading, Palette.Text, 32));
        if (subtitle != null) col.AddChild(Ui.Text(subtitle, Ui.TextStyle.Secondary, Palette.TextSecondary, Palette.FontSmall + 1));
        return col;
    }

    /// <summary>Key hints for the bottom of a screen: ("Enter", "Select"), ("Esc", "Back").</summary>
    public static HBoxContainer KeyHints(params (string Key, string Label)[] hints)
    {
        var row = Ui.HBox(Palette.Space4);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var (key, label) in hints)
        {
            if (key.Length == 0) continue; // unbound
            var h = Ui.HBox(6);
            h.MouseFilter = Control.MouseFilterEnum.Ignore;
            foreach (var k in key.Split(' ')) h.AddChild(KeyCap.Create(k, 11));
            h.AddChild(Ui.Text(label, Ui.TextStyle.Caption, Palette.TextSecondary));
            row.AddChild(h);
        }
        return row;
    }

    /// <summary>A selectable card's three looks.</summary>
    public static (StyleBoxFlat Normal, StyleBoxFlat Hover, StyleBoxFlat Selected) CardStyles(int pad = 10)
    {
        var normal = Styles.Box(new Color(1, 1, 1, 0.025f), Palette.Radius, Palette.Line, 1, pad);
        var hover = Styles.Box(new Color(1, 1, 1, 0.055f), Palette.Radius, Palette.LineStrong, 1, pad);
        var selected = Styles.Box(Palette.Accent with { A = 0.09f }, Palette.Radius, Palette.Accent, 1, pad, shadow: 10, shadowAlpha: 0.35f);
        return (normal, hover, selected);
    }

    /// <summary>A small rounded chip ("Official campaign", "3 haks").</summary>
    public static Control Chip(string text, Color? color = null)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var c = color ?? Palette.TextSecondary;
        p.AddThemeStyleboxOverride("panel", Styles.Box(c with { A = 0.10f }, 10, c with { A = 0.35f }, 1, 9, 3));
        var l = Ui.Text(text, Ui.TextStyle.Caption, c, Palette.FontCaption);
        l.AddThemeFontOverride("font", Fonts.UiMedium);
        p.AddChild(l);
        return p;
    }

    /// <summary>Removes and frees a container's children now (a plain QueueFree leaves them in the layout for a frame).</summary>
    public static void Clear(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            n.RemoveChild(c);
            c.QueueFree();
        }
    }

    /// <summary>Fades a control in (for children of containers, whose position the container owns).</summary>
    public static void FadeIn(Control c, double duration = 0.2)
    {
        c.Modulate = c.Modulate with { A = 0 };
        c.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).TweenProperty(c, "modulate:a", 1f, duration);
    }

    /// <summary>Fades a freely placed control in with a short rise.</summary>
    public static void Reveal(Control c, float dx = 0, float dy = 10, double duration = 0.22)
    {
        c.Modulate = c.Modulate with { A = 0 };
        var target = c.Position;
        c.Position = target + new Vector2(dx, dy);
        var tw = c.CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(c, "modulate:a", 1f, duration);
        tw.TweenProperty(c, "position", target, duration);
    }

    /// <summary>A full-rect texture that covers its box (cropping), with a darkening gradient toward the bottom.</summary>
    public static Control CoverImage(Texture2D? tex, float bottomShade = 0.85f, int radius = 0)
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        var img = new TextureRect
        {
            Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "Image",
        };
        root.AddChild(img);
        var shade = new ColorRect
        {
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = "shader_type canvas_item; uniform float amount = 0.85; uniform float radius = 0.0; " +
                        "void fragment() { float a = smoothstep(0.25, 1.0, UV.y) * amount; COLOR = vec4(0.03, 0.03, 0.045, a); }",
                },
            },
        };
        ((ShaderMaterial)shade.Material).SetShaderParameter("amount", bottomShade);
        root.AddChild(shade);
        return root;
    }
}
