using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>Small building blocks shared by every window and the HUD.</summary>
public static class Ui
{
    public enum TextStyle { Body, Secondary, Caption, Small, Strong, Number, Title, Heading, Display, Reading, ReadingItalic }

    public static Label Text(string text, TextStyle style = TextStyle.Body, Color? color = null, int size = 0)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        Style(l, style, color, size);
        return l;
    }

    public static void Style(Control c, TextStyle style, Color? color = null, int size = 0)
    {
        var (font, fontSize, fontColor) = style switch
        {
            TextStyle.Secondary => (Fonts.Ui, Palette.FontBody, Palette.TextSecondary),
            TextStyle.Caption => (Fonts.UiMedium, Palette.FontCaption, Palette.TextMuted),
            TextStyle.Small => (Fonts.Ui, Palette.FontSmall, Palette.TextSecondary),
            TextStyle.Strong => (Fonts.UiBold, Palette.FontBody, Palette.Text),
            TextStyle.Number => (Fonts.UiBold, Palette.FontLarge, Palette.Text),
            TextStyle.Title => (Fonts.Display, Palette.FontTitle, Palette.AccentBright),
            TextStyle.Heading => (Fonts.Display, Palette.FontHeading, Palette.AccentBright),
            TextStyle.Display => (Fonts.DisplayBold, Palette.FontDisplay, Palette.AccentBright),
            TextStyle.Reading => (Fonts.Reading, 19, Palette.Text),
            TextStyle.ReadingItalic => (Fonts.ReadingItalic, 19, Palette.TextSecondary),
            _ => (Fonts.Ui, Palette.FontBody, Palette.Text),
        };
        c.AddThemeFontOverride("font", font);
        c.AddThemeFontSizeOverride("font_size", size > 0 ? size : fontSize);
        c.AddThemeColorOverride("font_color", color ?? fontColor);
    }

    /// <summary>Outlined text for over the 3D view (names, floating numbers).</summary>
    public static Label Overlay(string text, int size = 16, Color? color = null, Font? font = null)
    {
        var l = Text(text, TextStyle.Strong, color, size);
        if (font != null) l.AddThemeFontOverride("font", font);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        l.AddThemeConstantOverride("outline_size", 5);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        l.AddThemeConstantOverride("shadow_offset_y", 2);
        return l;
    }

    /// <summary>A section label in small caps with a hairline after it.</summary>
    public static Control Section(string title)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        var l = Text(title.ToUpperInvariant(), TextStyle.Caption, Palette.AccentDim.Lightened(0.25f));
        l.AddThemeFontOverride("font", Fonts.UiBold);
        row.AddChild(l);
        var line = new Divider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Fade = Divider.FadeMode.End, Color = Palette.LineStrong };
        row.AddChild(line);
        return row;
    }

    public static Button Button(string text, Action? pressed = null, string variation = "", string? icon = null)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, ThemeTypeVariation = variation };
        if (icon != null) { b.Icon = Icons.Get(icon, 40); b.ExpandIcon = false; }
        if (pressed != null) b.Pressed += pressed;
        return b;
    }

    public static Button Primary(string text, Action? pressed = null) => Button(text, pressed, "PrimaryButton");
    public static Button Ghost(string text, Action? pressed = null) => Button(text, pressed, "GhostButton");

    public static MarginContainer Pad(Control child, int all) => Pad(child, all, all, all, all);
    public static MarginContainer Pad(Control child, int left, int top, int right, int bottom)
    {
        var m = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_bottom", bottom);
        m.AddChild(child);
        return m;
    }

    public static VBoxContainer VBox(int separation = Palette.Space2) { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", separation); return v; }
    public static HBoxContainer HBox(int separation = Palette.Space2) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", separation); return h; }
    public static Control Spacer() => new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>"Key: value" row for stat lists.</summary>
    public static HBoxContainer Stat(string name, string value, Color? valueColor = null)
    {
        var row = HBox(6);
        var n = Text(name, TextStyle.Secondary, size: Palette.FontSmall + 1);
        n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(n);
        row.AddChild(Text(value, TextStyle.Strong, valueColor, Palette.FontSmall + 1));
        return row;
    }

    /// <summary>Adds the kit's frosted-glass panel background to a container.</summary>
    public static T Glass<T>(T panel, StyleBox? style = null, float opacity = 0.84f) where T : Control
    {
        panel.AddThemeStyleboxOverride("panel", style ?? Styles.Hud());
        Styles.ApplyGlass(panel, opacity);
        return panel;
    }

    /// <summary>Fades and slides a control in (used when windows and toasts appear).</summary>
    public static void AnimateIn(Control c, float rise = 8, double duration = 0.16)
    {
        c.Modulate = c.Modulate with { A = 0 };
        var start = c.Position;
        c.Position = start + new Vector2(0, rise);
        var tw = c.CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(c, "modulate:a", 1f, duration);
        tw.TweenProperty(c, "position", start, duration);
    }
}

/// <summary>A hairline that fades out at one or both ends (gold under titles, grey between sections).</summary>
public partial class Divider : Control
{
    public enum FadeMode { Both, End, None }
    public Color Color = Palette.AccentDim;
    public FadeMode Fade = FadeMode.Both;
    public int Thickness = 1;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(CustomMinimumSize.X, Math.Max(CustomMinimumSize.Y, Thickness));
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        float w = Size.X, y = Size.Y / 2f;
        const int steps = 32;
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps;
            float a = Fade switch
            {
                FadeMode.Both => 1 - MathF.Abs(t * 2 - 1),
                FadeMode.End => 1 - t,
                _ => 1,
            };
            DrawRect(new Rect2(w * i / steps, y - Thickness / 2f, w / steps + 0.5f, Thickness), Color with { A = Color.A * MathF.Min(1, a * 1.6f) });
        }
    }
}

/// <summary>
/// A resource bar (hit points, experience): rounded track, fill, a lighter "trail" that catches up
/// after damage, an optional second segment (temporary hit points) and centred text.
/// </summary>
public partial class Bar : Control
{
    public Color Fill = Palette.Health, FillTop = Palette.HealthBright, Trail = Palette.HealthTrail, Extra = Palette.TempHealth;
    public Color Track = new(0, 0, 0, 0.5f);
    public float Radius = 4;
    public bool ShowText = true;
    /// <summary>False for static meters (XP, load) that shouldn't animate a trail when first shown.</summary>
    public bool ShowTrail = true;
    public int FontSize = 12;
    public string? TextOverride;
    float _value = 1, _trail = 1, _extra;
    float _max = 1, _current;

    public void Set(float current, float max, float extra = 0)
    {
        _current = current;
        _max = Math.Max(1, max);
        float v = Math.Clamp(current / _max, 0, 1);
        if (v > _trail) _trail = v;
        _value = v;
        _extra = Math.Clamp(extra / _max, 0, 1);
        QueueRedraw();
    }

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }

    public override void _Process(double delta)
    {
        if (_trail > _value)
        {
            _trail = Math.Max(_value, _trail - (float)delta * 0.5f);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Rounded(Track, Palette.LineStrong), r);
        var inner = r.Grow(-1);
        if (ShowTrail && _trail > _value) DrawStyleBox(Rounded(Trail with { A = 0.55f }), new Rect2(inner.Position, new Vector2(inner.Size.X * _trail, inner.Size.Y)));
        if (_value > 0)
        {
            var fill = new Rect2(inner.Position, new Vector2(Math.Max(Radius * 2, inner.Size.X * _value), inner.Size.Y));
            DrawStyleBox(Rounded(Fill), fill);
            // Lighter top half for a little depth.
            DrawStyleBox(Rounded(FillTop with { A = 0.35f }), new Rect2(fill.Position, new Vector2(fill.Size.X, fill.Size.Y * 0.45f)));
        }
        if (_extra > 0)
        {
            float x = inner.Size.X * Math.Min(1, _value);
            DrawStyleBox(Rounded(Extra with { A = 0.85f }), new Rect2(inner.Position + new Vector2(Math.Max(0, x - inner.Size.X * _extra), 0), new Vector2(inner.Size.X * _extra, inner.Size.Y)));
        }
        if (ShowText && Size.Y >= 10)
        {
            var text = TextOverride ?? $"{(int)_current} / {(int)_max}";
            var font = Fonts.UiBold;
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize);
            var pos = new Vector2((Size.X - size.X) / 2, (Size.Y + font.GetAscent(FontSize) - font.GetDescent(FontSize)) / 2);
            DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, FontSize, 4, new Color(0, 0, 0, 0.7f));
            DrawString(font, pos, text, HorizontalAlignment.Left, -1, FontSize, Palette.Text);
        }
    }

    StyleBoxFlat Rounded(Color c, Color? border = null)
    {
        var s = new StyleBoxFlat { BgColor = c, AntiAliasing = true };
        s.SetCornerRadiusAll((int)Radius);
        if (border is { } b) { s.BorderColor = b; s.SetBorderWidthAll(1); }
        return s;
    }
}

/// <summary>A flat square button showing one tinted icon (toolbars, close buttons, the HUD menu).</summary>
public partial class IconButton : Button
{
    public static IconButton Create(string icon, string tooltip, int size = 36, int iconSize = 20, Action? pressed = null)
    {
        var b = new IconButton
        {
            Icon = Icons.Get(icon, iconSize * 2), TooltipText = tooltip, FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(size, size), IconAlignment = HorizontalAlignment.Center, ExpandIcon = true,
            ThemeTypeVariation = "GhostButton",
        };
        int pad = (size - iconSize) / 2;
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            var s = Styles.Box(state switch
            {
                "hover" => new Color(1, 1, 1, 0.07f),
                "pressed" or "hover_pressed" => Palette.Accent with { A = 0.16f },
                _ => Colors.Transparent,
            }, Palette.RadiusSmall, null, 0, pad, pad);
            b.AddThemeStyleboxOverride(state, s);
        }
        b.AddThemeColorOverride("icon_normal_color", Palette.TextSecondary);
        b.AddThemeColorOverride("icon_hover_color", Palette.AccentBright);
        b.AddThemeColorOverride("icon_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("icon_hover_pressed_color", Palette.AccentBright);
        if (pressed != null) b.Pressed += pressed;
        return b;
    }
}

/// <summary>A keyboard key shown as a small key cap ("I", "F1", "Esc").</summary>
public partial class KeyCap : PanelContainer
{
    public static KeyCap Create(string key, int fontSize = 10)
    {
        var k = new KeyCap { MouseFilter = MouseFilterEnum.Ignore };
        var s = Styles.Box(new Color(0, 0, 0, 0.55f), 4, Palette.LineStrong, 1, 4, 1);
        s.BorderWidthBottom = 2;
        k.AddThemeStyleboxOverride("panel", s);
        var l = Ui.Text(key, Ui.TextStyle.Caption, Palette.TextSecondary, fontSize);
        l.AddThemeFontOverride("font", Fonts.UiBold);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        k.AddChild(l);
        return k;
    }
}
