using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>A small rounded tag ("Class skill", "x2", "Armour check").</summary>
public static class Chip
{
    public static PanelContainer Create(string text, Color color, bool solid = false, string? tooltip = null, int fontSize = Palette.FontCaption)
    {
        var p = new PanelContainer { MouseFilter = tooltip != null ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore, TooltipText = tooltip ?? "" };
        p.AddThemeStyleboxOverride("panel", Styles.Box(solid ? color : color with { A = 0.13f }, 4, solid ? null : color with { A = 0.45f }, 1, 6, 1));
        var l = Ui.Text(text, Ui.TextStyle.Caption, solid ? Palette.TextOnAccent : color.Lightened(0.15f), fontSize);
        l.AddThemeFontOverride("font", Fonts.UiBold);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        p.AddChild(l);
        p.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return p;
    }
}

/// <summary>
/// A selectable card (race, class, alignment, package...): icon, title, subtitle, optional trailing
/// controls; brass outline and glow when selected, lighter on hover. Rich tooltip via <see cref="TipPanel.Tooltip"/>.
/// </summary>
public partial class SelectCard : TipPanel
{
    public event Action? Pressed;
    public event Action? DoubleClicked;
    public HBoxContainer Trailing = null!;
    /// <summary>The column holding the title and subtitle (extra lines can be added under them).</summary>
    public VBoxContainer Body = null!;
    public Label TitleLabel = null!;
    public Label? SubtitleLabel;
    bool _selected, _hover, _disabled;

    public bool Selected { get => _selected; set { _selected = value; Restyle(); } }
    public bool Disabled { get => _disabled; set { _disabled = value; Restyle(); } }

    public static SelectCard Create(string title, string? subtitle = null, Texture2D? icon = null, int iconSize = 40, bool vertical = false, int pad = 12)
    {
        var c = new SelectCard { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        c._pad = pad;
        BoxContainer row = vertical ? Ui.VBox(6) : Ui.HBox(12);
        row.MouseFilter = MouseFilterEnum.Ignore;
        if (vertical) ((VBoxContainer)row).Alignment = BoxContainer.AlignmentMode.Center;
        c.AddChild(row);
        if (icon != null)
        {
            var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = vertical ? SizeFlags.ShrinkCenter : SizeFlags.Fill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            frame.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.35f), Palette.RadiusSmall, Palette.Line, 1, 2));
            frame.AddChild(new TextureRect
            {
                Texture = icon, CustomMinimumSize = new Vector2(iconSize, iconSize), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
            });
            row.AddChild(frame);
        }
        var names = c.Body = Ui.VBox(1);
        names.MouseFilter = MouseFilterEnum.Ignore;
        names.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        names.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        c.TitleLabel = Ui.Text(title, Ui.TextStyle.Strong, null, Palette.FontLarge - 1);
        if (vertical) c.TitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        names.AddChild(c.TitleLabel);
        if (!string.IsNullOrEmpty(subtitle))
        {
            c.SubtitleLabel = Ui.Text(subtitle, Ui.TextStyle.Small);
            c.SubtitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (vertical) c.SubtitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            names.AddChild(c.SubtitleLabel);
        }
        row.AddChild(names);
        c.Trailing = Ui.HBox(6);
        c.Trailing.MouseFilter = MouseFilterEnum.Ignore;
        c.Trailing.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(c.Trailing);
        c.MouseEntered += () => { c._hover = true; c.Restyle(); };
        c.MouseExited += () => { c._hover = false; c.Restyle(); };
        c.Restyle();
        return c;
    }

    int _pad = 12;

    void Restyle()
    {
        StyleBoxFlat s;
        if (_selected)
        {
            s = Styles.Box(new Color(0.20f, 0.17f, 0.11f), Palette.Radius, Palette.Accent, 1, _pad);
            s.ShadowColor = Palette.AccentGlow;
            s.ShadowSize = 10;
        }
        else if (_hover && !_disabled) s = Styles.Box(Palette.Bg3, Palette.Radius, Palette.AccentDim, 1, _pad);
        else s = Styles.Box(Palette.Bg2, Palette.Radius, Palette.Line, 1, _pad);
        AddThemeStyleboxOverride("panel", s);
        if (TitleLabel != null) TitleLabel.AddThemeColorOverride("font_color", _selected ? Palette.AccentBright : _disabled ? Palette.TextMuted : Palette.Text);
        Modulate = _disabled && !_selected ? new Color(1, 1, 1, 0.55f) : Colors.White;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
        {
            if (mb.DoubleClick) DoubleClicked?.Invoke();
            else Pressed?.Invoke();
            AcceptEvent();
        }
    }
}

/// <summary>A row of pill-shaped toggle tabs ("Cantrips", "1", "2"...), one selected; optional count badges.</summary>
public partial class PillTabs : HBoxContainer
{
    public event Action<int>? Changed;
    readonly List<Button> _buttons = new();
    public int Selected { get; private set; } = -1;

    public PillTabs() { AddThemeConstantOverride("separation", 6); }

    public Button Add(string text, string? tooltip = null, bool dim = false)
    {
        int index = _buttons.Count;
        var b = new Button { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = tooltip ?? "", MouseDefaultCursorShape = CursorShape.PointingHand };
        var normal = Styles.Box(new Color(0, 0, 0, 0.25f), 14, Palette.LineStrong, 1, 12, 5);
        var hover = Styles.Box(Palette.Bg3, 14, Palette.AccentDim, 1, 12, 5);
        var on = Styles.Box(Palette.Accent with { A = 0.2f }, 14, Palette.Accent, 1, 12, 5);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.AddThemeStyleboxOverride("disabled", normal);
        b.AddThemeColorOverride("font_color", dim ? Palette.TextMuted : Palette.TextSecondary);
        b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("font_hover_pressed_color", Palette.AccentBright);
        b.AddThemeFontOverride("font", Fonts.UiMedium);
        b.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        b.Pressed += () => Select(index);
        _buttons.Add(b);
        AddChild(b);
        return b;
    }

    public void Select(int index, bool notify = true)
    {
        Selected = index;
        for (int i = 0; i < _buttons.Count; i++) _buttons[i].SetPressedNoSignal(i == index);
        if (notify) Changed?.Invoke(index);
    }

    public void Clear()
    {
        foreach (var b in _buttons) { RemoveChild(b); b.QueueFree(); }
        _buttons.Clear();
        Selected = -1;
    }

    public int Count => _buttons.Count;
}

/// <summary>
/// The left rail of a multi-step wizard: numbered steps joined by a line; done steps show a check,
/// the current one is brass, steps not yet reachable are dimmed. Click a reachable step to go there.
/// </summary>
public partial class StepRail : Control
{
    public sealed record Entry(string Label, bool Done, bool Current, bool Reachable, bool HasError = false);

    public event Action<int>? StepClicked;
    List<Entry> _entries = new();
    int _hover = -1;
    public const int RowHeight = 36;
    const float CircleX = 18, Radius = 11;

    public StepRail()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(190, 0);
    }

    public void Set(IEnumerable<Entry> entries)
    {
        _entries = entries.ToList();
        CustomMinimumSize = new Vector2(CustomMinimumSize.X, _entries.Count * RowHeight);
        QueueRedraw();
    }

    int At(Vector2 p) => p.Y < 0 ? -1 : (int)(p.Y / RowHeight) is var i && i < _entries.Count ? i : -1;

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            int h = At(mm.Position);
            if (h != _hover) { _hover = h; QueueRedraw(); }
            MouseDefaultCursorShape = h >= 0 && _entries[h].Reachable ? CursorShape.PointingHand : CursorShape.Arrow;
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
        {
            int i = At(mb.Position);
            if (i >= 0 && _entries[i].Reachable && !_entries[i].Current) StepClicked?.Invoke(i);
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover >= 0) { _hover = -1; QueueRedraw(); }
    }

    public override void _Draw()
    {
        var font = Fonts.UiMedium;
        var bold = Fonts.UiBold;
        // Connector line behind the circles: brass up to the current step.
        int current = _entries.FindIndex(e => e.Current);
        for (int i = 0; i < _entries.Count - 1; i++)
        {
            float y0 = i * RowHeight + RowHeight / 2f + Radius, y1 = (i + 1) * RowHeight + RowHeight / 2f - Radius;
            DrawLine(new Vector2(CircleX, y0), new Vector2(CircleX, y1), i < current ? Palette.AccentDim : Palette.LineStrong, 2);
        }
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            float cy = i * RowHeight + RowHeight / 2f;
            var row = new Rect2(0, i * RowHeight + 3, Size.X, RowHeight - 6);
            if (e.Current) DrawStyleBox(Styles.Box(Palette.Accent with { A = 0.12f }, Palette.RadiusSmall, null, 0), row);
            else if (i == _hover && e.Reachable) DrawStyleBox(Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, null, 0), row);
            var center = new Vector2(CircleX, cy);
            Color text;
            if (e.Current)
            {
                DrawCircle(center, Radius + 4, Palette.AccentGlow);
                DrawCircle(center, Radius, Palette.Accent);
                Number(bold, center, (i + 1).ToString(), Palette.TextOnAccent);
                text = Palette.AccentBright;
            }
            else if (e.Done && !e.HasError)
            {
                DrawCircle(center, Radius, Palette.AccentDim with { A = 0.55f });
                DrawArc(center, Radius, 0, Mathf.Tau, 32, Palette.Accent, 1.2f, true);
                // Check mark.
                DrawPolyline(new[] { center + new Vector2(-4.5f, 0.5f), center + new Vector2(-1.2f, 3.8f), center + new Vector2(4.8f, -3.6f) }, Palette.AccentBright, 2f, true);
                text = Palette.Text;
            }
            else
            {
                DrawCircle(center, Radius, Palette.Bg1);
                DrawArc(center, Radius, 0, Mathf.Tau, 32, e.HasError ? Palette.Danger : e.Reachable ? Palette.TextSecondary with { A = 0.6f } : Palette.LineStrong, 1.2f, true);
                Number(bold, center, (i + 1).ToString(), e.Reachable ? Palette.TextSecondary : Palette.TextMuted);
                text = e.Reachable ? Palette.TextSecondary : Palette.TextMuted;
            }
            int size = Palette.FontBody;
            float baseline = cy + (font.GetAscent(size) - font.GetDescent(size)) / 2f;
            DrawString(e.Current ? bold : font, new Vector2(CircleX + Radius + 14, baseline), e.Label, HorizontalAlignment.Left, Size.X - CircleX - Radius - 18, size, text);
        }
    }

    void Number(Font font, Vector2 center, string s, Color color)
    {
        const int size = 11;
        var w = font.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        DrawString(font, new Vector2(center.X - w / 2f, center.Y + (font.GetAscent(size) - font.GetDescent(size)) / 2f), s, HorizontalAlignment.Left, -1, size, color);
    }
}
