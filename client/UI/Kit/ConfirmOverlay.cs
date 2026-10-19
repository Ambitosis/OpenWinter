using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// A modal question laid over one window (not the whole screen): dims the window, shows a small
/// card with a title, message, optional extra control (a quantity slider) and Cancel / confirm.
/// Enter confirms, Esc cancels.
/// </summary>
public partial class ConfirmOverlay : PanelContainer
{
    Action? _confirm;
    public Action? Cancelled;

    public static ConfirmOverlay Show(Control host, string title, string message, string confirmText, Action confirm, Control? extra = null, bool danger = false)
    {
        foreach (var old in host.GetChildren().OfType<ConfirmOverlay>()) old.QueueFree();
        var o = new ConfirmOverlay { _confirm = confirm, MouseFilter = MouseFilterEnum.Stop };
        o.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Scrim, Palette.RadiusLarge, null, 0, 0, 0));
        o.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        o.AddChild(center);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
        card.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.Radius, Palette.AccentDim with { A = 0.6f }, 1, 20, 18, shadow: 18, shadowAlpha: 0.55f));
        center.AddChild(card);
        var v = Ui.VBox(12);
        card.AddChild(v);
        v.AddChild(Ui.Text(title, Ui.TextStyle.Title));
        if (message.Length > 0)
        {
            var m = Ui.Text(message, Ui.TextStyle.Secondary);
            m.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            m.CustomMinimumSize = new Vector2(300, 0);
            v.AddChild(m);
        }
        if (extra != null) v.AddChild(extra);
        var buttons = Ui.HBox(8);
        buttons.Alignment = BoxContainer.AlignmentMode.End;
        buttons.AddChild(Ui.Ghost("Cancel", o.Cancel));
        var ok = Ui.Primary(confirmText, o.Confirm);
        if (danger) ok.Modulate = new Color(1f, 0.75f, 0.7f);
        buttons.AddChild(ok);
        v.AddChild(buttons);
        host.AddChild(o);
        o.Modulate = o.Modulate with { A = 0 };
        o.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).TweenProperty(o, "modulate:a", 1f, 0.12);
        return o;
    }

    public void Confirm()
    {
        var c = _confirm;
        _confirm = null;
        QueueFree();
        c?.Invoke();
    }

    public void Cancel()
    {
        _confirm = null;
        QueueFree();
        Cancelled?.Invoke();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true } k || !IsVisibleInTree()) return;
        if (k.Keycode is Key.Enter or Key.KpEnter) { Confirm(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.Escape) { Cancel(); GetViewport().SetInputAsHandled(); }
    }
}
