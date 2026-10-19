using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The modern portal prompt (<see cref="PortalPrompt"/>): "Server Info" over a dimmed screen, the server box (the
/// check, then what the server said), the password box and Cancel / Join (Join waits for the server). Esc cancels;
/// Enter joins once Join is enabled.
/// </summary>
public partial class PortalDialog : Control, IPortalView
{
    Label _status = null!, _address = null!;
    LineEdit _password = null!;
    Button _join = null!;

    public event Action? Cancelled;
    public event Action? JoinPressed;
    public string Password => _password.Text;

    public static PortalDialog Create(Node parent)
    {
        var d = new PortalDialog { Name = "PortalDialog", MouseFilter = MouseFilterEnum.Stop, ZIndex = 100, Theme = GameTheme.Theme };
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
        col.AddChild(Ui.Text("Server Info", Ui.TextStyle.Title));
        col.AddChild(new Divider { Color = Palette.AccentDim, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
        d._address = Ui.Text("", Ui.TextStyle.Secondary);
        col.AddChild(d._address);
        var box = new PanelContainer { CustomMinimumSize = new Vector2(392, 96) };
        box.AddThemeStyleboxOverride("panel", Styles.Inset(10));
        d._status = Ui.Text("", Ui.TextStyle.Body);
        d._status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(d._status);
        col.AddChild(box);
        col.AddChild(Ui.Text("Password", Ui.TextStyle.Secondary));
        d._password = new LineEdit { Secret = false, CustomMinimumSize = new Vector2(392, 34) };
        d._password.TextSubmitted += _ => d.TryJoin();
        col.AddChild(d._password);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var row = Ui.HBox(Palette.Space2);
        row.Alignment = BoxContainer.AlignmentMode.End;
        col.AddChild(row);
        var cancel = Ui.Button("Cancel", () => d.Cancelled?.Invoke());
        cancel.CustomMinimumSize = new Vector2(110, 38);
        row.AddChild(cancel);
        d._join = Ui.Primary("Join", d.TryJoin);
        d._join.CustomMinimumSize = new Vector2(130, 38);
        d._join.Disabled = true;
        row.AddChild(d._join);

        parent.AddChild(d);
        return d;
    }

    void TryJoin()
    {
        if (!_join.Disabled) JoinPressed?.Invoke();
    }

    public void Show(PortalRequest request)
    {
        _address.Text = request.Address;
        _password.Text = request.Password;
    }

    public void SetStatus(IReadOnlyList<string> lines, bool canJoin)
    {
        _status.Text = string.Join("\n", lines);
        _join.Disabled = !canJoin;
    }

    public void Close() { if (IsInstanceValid(this)) QueueFree(); }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || !IsVisibleInTree()) return;
        if (k.Keycode == Key.Escape) Cancelled?.Invoke();
        else if (k.Keycode is Key.Enter or Key.KpEnter && !_password.HasFocus()) TryJoin();
        else return;
        GetViewport().SetInputAsHandled();
    }
}
