using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// A modal question over a dimmed screen: title, text, confirm and cancel. Esc or a click outside
/// cancels; Enter confirms. <c>Dialogs.Confirm(parent, "Quit?", "Unsaved progress will be lost.", "Quit", onYes, danger: true)</c>.
/// </summary>
public partial class Dialogs : Control
{
    Action? _yes, _no;

    public static Dialogs Confirm(Node parent, string title, string text, string confirm, Action onConfirm, bool danger = false, string cancel = "Cancel", Action? onCancel = null)
    {
        var d = new Dialogs { _yes = onConfirm, _no = onCancel, MouseFilter = MouseFilterEnum.Stop, ZIndex = 100, Theme = GameTheme.Theme };
        d.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        d.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        panel.AddThemeStyleboxOverride("panel", Styles.Window(24));
        Styles.ApplyGlass(panel, 0.92f);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        center.AddChild(panel);
        d.AddChild(center);

        var col = Ui.VBox(Palette.Space3);
        panel.AddChild(col);
        var head = Ui.Text(title, Ui.TextStyle.Title);
        col.AddChild(head);
        col.AddChild(new Divider { Color = Palette.AccentDim, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
        var body = Ui.Text(text, Ui.TextStyle.Secondary);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.CustomMinimumSize = new Vector2(392, 0);
        col.AddChild(body);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var row = Ui.HBox(Palette.Space2);
        row.Alignment = BoxContainer.AlignmentMode.End;
        col.AddChild(row);
        var no = Ui.Button(cancel, () => d.Finish(false));
        no.CustomMinimumSize = new Vector2(110, 38);
        row.AddChild(no);
        var yes = Ui.Primary(confirm, () => d.Finish(true));
        yes.CustomMinimumSize = new Vector2(130, 38);
        if (danger)
        {
            yes.AddThemeStyleboxOverride("normal", Styles.Box(Palette.Danger.Darkened(0.25f), Palette.RadiusSmall, Palette.Danger, 1, 18, 8));
            yes.AddThemeStyleboxOverride("hover", Styles.Box(Palette.Danger, Palette.RadiusSmall, Palette.Danger.Lightened(0.2f), 1, 18, 8));
            yes.AddThemeColorOverride("font_color", Palette.Text);
            yes.AddThemeColorOverride("font_hover_color", Colors.White);
        }
        row.AddChild(yes);

        parent.AddChild(d);
        panel.PivotOffset = new Vector2(220, 80);
        d.Modulate = d.Modulate with { A = 0 };
        var tw = d.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(d, "modulate:a", 1f, 0.14);
        return d;
    }

    void Finish(bool yes)
    {
        if (!IsInsideTree()) return;
        QueueFree();
        (yes ? _yes : _no)?.Invoke();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { Finish(false); AcceptEvent(); }
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.Escape) Finish(false);
        else if (k.Keycode is Key.Enter or Key.KpEnter) Finish(true);
        else return;
        GetViewport().SetInputAsHandled();
    }
}
