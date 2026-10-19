using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's message boxes, from their layouts on pnl_util_ok: okpanel (a message and OK), okcancelpanel,
/// yesnocancelpanel, okscrollpanel (a long message in a scrolling list) and util_editpopup (a message and a line
/// of text to type). Modal and centred on the screen; Enter is the first button, Esc the last (Cancel / OK).
/// <c>ClassicPopup.OkCancel(host, "Exit to the main menu?", () => ...)</c>.
/// </summary>
public partial class ClassicPopup : Control
{
    public GuiPanel Panel = null!;
    readonly List<(string Tag, Action? Action)> _buttons = new();
    LineEdit? _edit;
    Action<string>? _submit;

    /// <summary>The popup open on top, if any (scripted captures).</summary>
    public static ClassicPopup? Top { get; private set; }

    public static ClassicPopup? Ok(Node host, ClassicAssets assets, string message, Action? ok = null) =>
        Show(host, assets, "okpanel", message, ("OkButton", ok));

    public static ClassicPopup? OkCancel(Node host, ClassicAssets assets, string message, Action ok, Action? cancel = null, string? okText = null, string? cancelText = null)
    {
        var p = Show(host, assets, "okcancelpanel", message, ("OkButton", ok), ("CancelButton", cancel));
        p?.Caption("OkButton", okText);
        p?.Caption("CancelButton", cancelText);
        return p;
    }

    public static ClassicPopup? YesNoCancel(Node host, ClassicAssets assets, string message, Action yes, Action no, Action? cancel = null) =>
        Show(host, assets, "yesnocancelpanel", message, ("YesButton", yes), ("NoButton", no), ("CancelButton", cancel));

    /// <summary>A long message in the scrolling list of okscrollpanel.</summary>
    public static ClassicPopup? OkScroll(Node host, ClassicAssets assets, string message, Action? ok = null)
    {
        var p = Show(host, assets, "okscrollpanel", "", ("OkButton", ok));
        if (p?.Panel.Get<GuiList>("editscroll") is { } list) list.AddText(message, wrap: true);
        return p;
    }

    /// <summary>A message and a line to type (util_editpopup); OK passes the text.</summary>
    public static ClassicPopup? Edit(Node host, ClassicAssets assets, string message, string text, Action<string> ok, Action? cancel = null, int maxLength = 64)
    {
        var p = Show(host, assets, "util_editpopup", message, ("OkButton", null), ("CancelButton", cancel));
        if (p == null) return null;
        p._submit = ok;
        var box = p.Panel.Get<GuiList>("EditBox");
        var edit = new LineEdit
        {
            Text = text, MaxLength = maxLength, Flat = true, CaretBlink = true, ContextMenuEnabled = false,
            SelectAllOnFocus = true,
        };
        var font = ClassicText.For(assets);
        edit.AddThemeFontOverride("font", font.Font);
        edit.AddThemeFontSizeOverride("font_size", font.Size);
        edit.AddThemeColorOverride("font_color", Colors.White);
        edit.AddThemeColorOverride("caret_color", Colors.White);
        foreach (var style in new[] { "normal", "focus", "read_only" }) edit.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        if (box != null)
        {
            // In the list's text box, over its frame.
            edit.Position = box.Position + new Vector2(6, 2);
            edit.Size = new Vector2(box.Size.X - 12, box.Size.Y - 4);
            box.Bar.Visible = false;
        }
        p.Panel.AddChild(edit);
        edit.TextSubmitted += _ => p.Press(0);
        p._edit = edit;
        Callable.From(() => { if (IsInstanceValid(edit)) edit.GrabFocus(); }).CallDeferred();
        return p;
    }

    /// <summary>Shows a layout as a modal popup; buttons are (layout tag, action) in order. Null when the game has no such layout.</summary>
    public static ClassicPopup? Show(Node host, ClassicAssets assets, string layout, string message, params (string Tag, Action? Action)[] buttons)
    {
        var panel = GuiPanel.FromLayout(assets, layout);
        if (panel == null) return null;
        var p = new ClassicPopup { Panel = panel, Name = "Popup_" + layout, MouseFilter = MouseFilterEnum.Stop };
        p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        p.AddChild(panel);
        if (panel.Get<GuiLabel>("MessageLabel") is { } label)
        {
            label.Text = message;
            label.Wrap = true;
        }
        for (int i = 0; i < buttons.Length; i++)
        {
            p._buttons.Add(buttons[i]);
            int k = i;
            if (panel.Get<GuiButton>(buttons[i].Tag) is { } b) b.Pressed += () => p.Press(k);
        }
        host.AddChild(p);
        p.Resized += p.Center;
        p.Center();
        Top = p;
        return p;
    }

    void Caption(string tag, string? text)
    {
        if (text != null && Panel.Get<GuiButton>(tag) is { } b) b.Caption = text;
    }

    void Center() => CenterOn(Panel, Size);

    /// <summary>Puts a panel's model origin at the middle of an area, as the original centres its panels (labels hanging off the frame don't count).</summary>
    public static void CenterOn(GuiPanel panel, Vector2 area) => panel.Position = (area / 2 - panel.ModelOrigin).Floor();

    void Press(int i)
    {
        if (!IsInsideTree()) return;
        // Keyboard acceptance must honor the same disabled state as clicking.
        if (i < 0 || i >= _buttons.Count || Panel.Get<GuiButton>(_buttons[i].Tag)?.Disabled == true) return;
        if (Top == this) Top = null;
        QueueFree();
        if (i == 0 && _submit != null) _submit(_edit?.Text ?? "");
        else _buttons[i].Action?.Invoke();
    }

    /// <summary>Closes as its last button would (Cancel, or OK when it is the only one).</summary>
    public void Cancel() => Press(_buttons.Count - 1);

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || !IsVisibleInTree()) return;
        // Only the newest popup answers keys.
        if (GetParent() is { } parent && parent.GetChildren().OfType<ClassicPopup>().LastOrDefault() != this) return;
        if (k.Keycode == Key.Escape) Cancel();
        else if (k.Keycode is Key.Enter or Key.KpEnter) Press(0);
        else return;
        GetViewport().SetInputAsHandled();
    }
}
