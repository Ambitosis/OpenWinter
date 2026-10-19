using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Short notifications that slide in under the target frame (journal updated, level-up ready,
/// experience, game saved) and fade after a few seconds. Repeats of the same message merge.
/// </summary>
public partial class Toasts : VBoxContainer
{
    public static Toasts? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        AddThemeConstantOverride("separation", 8);
        MouseFilter = MouseFilterEnum.Ignore;
        Alignment = AlignmentMode.Begin;
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public void Show(string title, string body = "", string icon = "book", Color? accent = null, double seconds = 4)
    {
        foreach (var c in GetChildren())
            if (c is Control old && old.GetMeta("key", "").AsString() == title + body) { RemoveChild(old); old.QueueFree(); }
        var color = accent ?? Palette.Accent;
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        card.SetMeta("key", title + body);
        var style = Styles.Hud(10, 8);
        style.BorderColor = color with { A = 0.55f };
        style.BorderWidthLeft = 3;
        Ui.Glass(card, style);
        var row = Ui.HBox(12);
        card.AddChild(row);
        row.AddChild(Icons.Rect(icon, 22, color));
        var col = Ui.VBox(1);
        row.AddChild(col);
        col.AddChild(Ui.Text(title, Ui.TextStyle.Strong, Palette.Text, 14));
        if (body.Length > 0)
        {
            var b = Ui.Text(body, Ui.TextStyle.Small, Palette.TextSecondary, 13);
            b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            b.CustomMinimumSize = new Vector2(Math.Min(360, Math.Max(180, body.Length * 6.5f)), 0);
            col.AddChild(b);
        }
        AddChild(card);
        // Taken out at once: QueueFree alone leaves the child counted until the frame ends, and five toasts in one
        // frame (a henchman's death and a level-up in killxp3) then looped here for ever, eating memory.
        while (GetChildCount() > 4)
        {
            var oldest = GetChild(0);
            RemoveChild(oldest);
            oldest.QueueFree();
        }
        card.Modulate = card.Modulate with { A = 0 };
        var tw = card.CreateTween();
        tw.TweenProperty(card, "modulate:a", 1f, 0.18).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(seconds);
        tw.TweenProperty(card, "modulate:a", 0f, 0.5);
        tw.TweenCallback(Callable.From(card.QueueFree));
    }
}

/// <summary>The area's name across the upper screen when you arrive, then it fades.</summary>
public partial class AreaTitle : VBoxContainer
{
    Label _title = null!, _sub = null!;
    Tween? _tween;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 6);
        Alignment = AlignmentMode.Center;
        _title = Ui.Overlay("", 44, Palette.AccentBright, Fonts.DisplayBold);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_title);
        var line = new Divider { Color = Palette.Accent, CustomMinimumSize = new Vector2(420, 1), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        AddChild(line);
        _sub = Ui.Overlay("", 15, Palette.TextSecondary, Fonts.UiMedium);
        _sub.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_sub);
        Modulate = Modulate with { A = 0 };
    }

    public void Show(string title, string subtitle)
    {
        if (title.Length == 0) return;
        _title.Text = title;
        // Long area names shrink to fit the screen.
        _title.AddThemeFontSizeOverride("font_size", Math.Clamp((int)(44 * 28f / Math.Max(28, title.Length)), 24, 44));
        _sub.Text = subtitle.ToUpperInvariant();
        _sub.Visible = subtitle.Length > 0;
        _tween?.Kill();
        Modulate = Modulate with { A = 0 };
        _tween = CreateTween();
        _tween.TweenInterval(0.4);
        _tween.TweenProperty(this, "modulate:a", 1f, 0.7).SetEase(Tween.EaseType.Out);
        _tween.TweenInterval(2.4);
        _tween.TweenProperty(this, "modulate:a", 0f, 1.2).SetEase(Tween.EaseType.In);
    }
}

/// <summary>Bottom-right menu buttons with their keys: character, inventory, spells, journal, map, rest, menu.</summary>
public partial class HudMenu : PanelContainer
{
    public readonly List<(string Icon, string Name, string Key, Action Action)> Entries = new();
    readonly Dictionary<string, IconButton> _buttons = new();

    public override void _Ready()
    {
        Ui.Glass(this, Styles.Hud(5, Palette.Radius));
        var row = Ui.HBox(2);
        AddChild(row);
        foreach (var (icon, name, key, action) in Entries)
        {
            var b = IconButton.Create(icon, $"{name}  ({key})", 42, 24, action);
            _buttons[name] = b;
            var holder = new Control { CustomMinimumSize = new Vector2(42, 42) };
            holder.AddChild(b);
            var cap = KeyCap.Create(key, 9);
            cap.Position = new Vector2(27, 27);
            holder.AddChild(cap);
            row.AddChild(holder);
        }
    }

    /// <summary>Highlights a button (e.g. Character when a level-up is waiting).</summary>
    public void Attention(string name, bool on)
    {
        if (_buttons.TryGetValue(name, out var b)) b.AddThemeColorOverride("icon_normal_color", on ? Palette.AccentBright : Palette.TextSecondary);
    }
}
