using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's portal prompt (ingame_portal on pnl_con_prtl_inf), centred like its message boxes: "Server Info",
/// the server box (ServerInfoEdit: "Checking For Server" and the address's host while the server is checked, then
/// what the server said), "Password:" over the password box (the script's password in plain text, editable), Cancel
/// and Join ("Join", dialog.tlk 8133, greyed until the server answers). Measured with mp_portal on nwmain; Esc does not
/// close it there. The logic is <see cref="PortalPrompt"/>'s.
/// </summary>
public partial class ClassicPortal : Control, IPortalView
{
    public ClassicAssets Assets = null!;
    GuiPanel _panel = null!;
    GuiList? _info;
    InfoLines? _lines;
    LineEdit _password = null!;
    GuiButton? _join;

    public event Action? Cancelled;
    public event Action? JoinPressed;
    public string Password => _password.Text;

    public static ClassicPortal? Create(Node host, ClassicAssets assets)
    {
        var panel = GuiPanel.FromLayout(assets, "ingame_portal");
        if (panel == null) return null;
        var p = new ClassicPortal { Assets = assets, _panel = panel, Name = "ClassicPortal", MouseFilter = MouseFilterEnum.Stop };
        p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        p.AddChild(panel);
        p._info = panel.Get<GuiList>("ServerInfoEdit");
        if (p._info != null)
        {
            // Its model (edit_con_port) has no scroll nodes and the original shows no bar. The lines are drawn from the
            // box's text0 node 17 px apart (mp_portal e_01: five lines at y 398, 415 ... 466), tighter than list rows.
            p._info.Bar.Visible = false;
            var at = p._info.Frame?.Node("text0") ?? new Vector2(5, 9);
            var end = p._info.Frame?.Node("text1") ?? p._info.Size;
            p._lines = new InfoLines { Assets = assets, Position = p._info.Position + at, Size = end - at };
            panel.AddChild(p._lines);
        }
        p._password = ClassicEdit(assets, panel.Get<GuiList>("PasswordEdit"));
        panel.AddChild(p._password);
        if (panel.Get<GuiButton>("CancelButton") is { } cancel) cancel.Pressed += () => p.Cancelled?.Invoke();
        p._join = panel.Get<GuiButton>("JoinButton");
        if (p._join != null)
        {
            // The layout says "Join Internet Game"; the game captions it "Join".
            p._join.Caption = assets.Tlk(8133) is { Length: > 0 } join ? join : "Join";
            p._join.Disabled = true;
            p._join.Pressed += () => p.JoinPressed?.Invoke();
        }
        host.AddChild(p);
        p.Resized += () => ClassicPopup.CenterOn(panel, p.Size);
        ClassicPopup.CenterOn(panel, p.Size);
        return p;
    }

    /// <summary>A line to type over a layout's edit box (as ClassicPopup.Edit does).</summary>
    static LineEdit ClassicEdit(ClassicAssets assets, GuiList? box)
    {
        var edit = new LineEdit { Flat = true, CaretBlink = true, ContextMenuEnabled = false, MaxLength = 64 };
        // The box's text is the layout font (fnt_galahad14, drawn in fnt_maintext at 16 px) as on the original.
        var font = ClassicText.For(assets, "fnt_maintext", 16);
        edit.AddThemeFontOverride("font", font.Font);
        edit.AddThemeFontSizeOverride("font_size", font.Size);
        edit.AddThemeColorOverride("font_color", Colors.White);
        edit.AddThemeColorOverride("caret_color", Colors.White);
        foreach (var style in new[] { "normal", "focus", "read_only" }) edit.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        if (box != null)
        {
            // The text sits as the original's does (mp_portal a_02: "secret" from x 486, its middle at y 405).
            edit.Position = box.Position + new Vector2(5, -4);
            edit.Size = new Vector2(box.Size.X - 10, box.Size.Y + 4);
            box.Bar.Visible = false;
        }
        return edit;
    }

    /// <summary>The password box has the focus, its caret before the text (mp_portal a_02).</summary>
    public void Show(PortalRequest request)
    {
        _password.Text = request.Password;
        _password.CaretColumn = 0;
        Callable.From(() => { if (IsInstanceValid(_password)) _password.GrabFocus(); }).CallDeferred();
    }

    public void SetStatus(IReadOnlyList<string> lines, bool canJoin)
    {
        if (_lines != null) { _lines.Lines = lines.ToList(); _lines.QueueRedraw(); }
        if (_join != null) _join.Disabled = !canJoin;
    }

    /// <summary>The server box's lines in the layout font, 17 px apart, clipped to the box.</summary>
    sealed partial class InfoLines : Control
    {
        public ClassicAssets Assets = null!;
        public List<string> Lines = new();
        public const float Spacing = 17;

        public InfoLines() { MouseFilter = MouseFilterEnum.Ignore; ClipContents = true; }

        public override void _Draw()
        {
            var font = Assets.Font("");
            for (int i = 0; i < Lines.Count; i++)
                font.DrawBox(this, new Rect2(0, i * Spacing - 1, Size.X, Spacing), NwnText.Strip(Lines[i]), Colors.White);
        }
    }

    public void Close() { if (IsInstanceValid(this)) QueueFree(); }

    /// <summary>Esc does not close the original's prompt (mp_portal d_esc), nor open the game menu behind it.</summary>
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape } && IsVisibleInTree()) GetViewport().SetInputAsHandled();
    }
}
