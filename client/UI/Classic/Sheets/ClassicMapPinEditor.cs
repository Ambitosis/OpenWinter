using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>The installed multiline map-pin dialog, shared by adding and editing personal pins.</summary>
public static class ClassicMapPinEditor
{
    public static ClassicPopup? Show(Node host, ClassicAssets assets, string text, Action<string> accept, Action? delete = null)
    {
        TextEdit? edit = null;
        var popup = ClassicPopup.Show(host, assets, "map_pin_text", "",
            ("OkButton", () => accept(edit?.Text ?? "")), ("DeleteButton", delete), ("CancelButton", null));
        if (popup == null) return null;
        if (popup.Panel.Get<GuiLabel>("Title") is { } title) title.Text = assets.Tlk(6394);
        if (popup.Panel.Get<GuiButton>("DeleteButton") is { } remove) remove.Disabled = delete == null;
        if (popup.Panel.Get<GuiList>("PinTextEdit") is not { } box) { popup.Cancel(); return null; }
        var a = box.Frame?.Node("text0") ?? new Vector2(6, 6);
        var b = box.Frame?.Node("text1") ?? a + box.ContentSize;
        var bounds = new Rect2(a, Vector2.Zero).Expand(b);
        edit = new TextEdit
        {
            Text = text, Position = box.Position + bounds.Position, Size = bounds.Size,
            WrapMode = TextEdit.LineWrappingMode.Boundary, CaretBlink = true,
        };
        var font = ClassicText.For(assets);
        edit.AddThemeFontOverride("font", font.Font);
        edit.AddThemeFontSizeOverride("font_size", font.Size);
        edit.AddThemeColorOverride("font_color", Colors.White);
        edit.AddThemeColorOverride("caret_color", Colors.White);
        foreach (var style in new[] { "normal", "focus", "read_only" }) edit.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        box.Bar.Visible = false;
        popup.Panel.AddChild(edit);
        void ValidateLength()
        {
            if (popup.Panel.Get<GuiButton>("OkButton") is not { } ok) return;
            ok.Disabled = edit.Text.Length > 4096;
            ok.SetMeta(ClassicTooltip.Meta, ok.Disabled ? "Shorten this note to 4096 characters or fewer." : "Save this personal map pin.");
        }
        edit.TextChanged += ValidateLength;
        ValidateLength();
        Callable.From(() => { if (GodotObject.IsInstanceValid(edit)) edit.GrabFocus(); }).CallDeferred();
        return popup;
    }
}
