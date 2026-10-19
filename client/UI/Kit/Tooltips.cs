using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// Rich tooltips: a card with an optional icon, title, subtitle, key/value rows and prose, built
/// on demand by the controls below (Godot asks for the tooltip when the pointer rests on them).
/// </summary>
public static class Tip
{
    /// <summary>The standard rich tooltip body.</summary>
    public static Control Card(string title, string? subtitle = null, string? body = null, Texture2D? icon = null,
        IEnumerable<(string Label, string Value, Color? Color)>? rows = null, string? footer = null, int width = 340, Color? titleColor = null)
    {
        var box = Ui.VBox(6);
        box.CustomMinimumSize = new Vector2(width, 0);
        box.MouseFilter = Control.MouseFilterEnum.Ignore;
        var head = Ui.HBox(10);
        head.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (icon != null)
            head.AddChild(new TextureRect
            {
                Texture = icon, CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        var names = Ui.VBox(0);
        names.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        names.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        names.MouseFilter = Control.MouseFilterEnum.Ignore;
        var t = Ui.Text(NwnText.Strip(title), Ui.TextStyle.Title, titleColor, Palette.FontLarge + 1);
        t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        t.CustomMinimumSize = new Vector2(width - (icon != null ? 50 : 0), 0);
        names.AddChild(t);
        if (!string.IsNullOrEmpty(subtitle)) names.AddChild(Ui.Text(subtitle, Ui.TextStyle.Caption, Palette.TextSecondary));
        head.AddChild(names);
        box.AddChild(head);
        var rowList = rows?.ToList();
        if (rowList is { Count: > 0 })
        {
            box.AddChild(new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
            foreach (var (label, value, color) in rowList)
            {
                var row = Ui.HBox(8);
                row.MouseFilter = Control.MouseFilterEnum.Ignore;
                var l = Ui.Text(label, Ui.TextStyle.Small);
                l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(l);
                row.AddChild(Ui.Text(value, Ui.TextStyle.Strong, color, Palette.FontSmall));
                box.AddChild(row);
            }
        }
        if (!string.IsNullOrWhiteSpace(body))
        {
            box.AddChild(new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
            var text = Ui.Text(Trim(NwnText.Strip(body!), 900), Ui.TextStyle.Reading, Palette.Text, 16);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = new Vector2(width, 0);
            box.AddChild(text);
        }
        if (!string.IsNullOrEmpty(footer))
        {
            var f = Ui.Text(footer, Ui.TextStyle.Caption, Palette.AccentDim.Lightened(0.3f));
            f.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            f.CustomMinimumSize = new Vector2(width, 0);
            box.AddChild(f);
        }
        // Its own opaque card, so whatever is behind the tooltip never shows through the text.
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", Styles.Box(new Color("0d0f14"), 6, null, 0, 2, 2));
        card.AddChild(box);
        return card;
    }

    /// <summary>Long descriptions are cut at a paragraph or sentence so tooltips stay a sensible size.</summary>
    public static string Trim(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max) return text;
        int cut = text.LastIndexOf('\n', max);
        if (cut < max / 2) cut = text.LastIndexOf(". ", max, StringComparison.Ordinal) + 1;
        if (cut < max / 2) cut = max;
        return text[..cut].TrimEnd() + " …";
    }
}

/// <summary>A panel whose tooltip is built by <see cref="Tip"/> (set <see cref="Tooltip"/>).</summary>
public partial class TipPanel : PanelContainer
{
    Func<Control?>? _tooltip;
    public Func<Control?>? Tooltip
    {
        get => _tooltip;
        set { _tooltip = value; TooltipText = value != null ? " " : ""; }
    }
    public override GodotObject? _MakeCustomTooltip(string forText) => _tooltip?.Invoke();
}

/// <summary>A button whose tooltip is built by <see cref="Tip"/> (set <see cref="Tooltip"/>).</summary>
public partial class TipButton : Button
{
    Func<Control?>? _tooltip;
    public Func<Control?>? Tooltip
    {
        get => _tooltip;
        set { _tooltip = value; TooltipText = value != null ? " " : ""; }
    }
    public override GodotObject? _MakeCustomTooltip(string forText) => _tooltip?.Invoke();
}
