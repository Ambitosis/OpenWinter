using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// Text macros on the classic quickbar (visual scene quickbargaps, docs/QUICKBAR_RADIAL.md): the slot shows no icon,
/// its label in white right-aligned at the count anchor where a potion's "3" goes ("Hail" ends 28 px right of F10's
/// centre, its top at y 690 of 720), and its key in grey as on an empty slot; the tooltip reads "F10 : Hail: Hail and
/// well met".
/// </summary>
public static class ClassicQuickbarText
{
    /// <summary>The most characters the label and command prompts take.</summary>
    public const int PromptLength = Game.Requests.QuickbarRequests.MacroTextLimit;

    public static void DrawMacro(CanvasItem ci, ClassicAssets assets, GuiModel.Pose pose, Vector2 origin, BarSlot slot)
    {
        var font = assets.Font(ClassicChatStyle.FontName);
        var at = (pose.Node("count1") ?? new Vector2(66, -16)) + origin;
        string label = slot.Name;
        // A label wider than the slot starts at its left (29 px left of the centre) and stops at the last whole
        // character that fits ("A long macro label" shows "A long m", quickbargaps h_assigned).
        float width = font.Measure(label);
        if (width > LabelWidth)
        {
            int n = label.Length;
            while (n > 0 && font.Measure(label[..n]) > LabelWidth) n--;
            label = label[..n];
            font.Draw(ci, new Vector2(at.X - LabelWidth, at.Y - font.Height), label, Colors.White);
            return;
        }
        font.Draw(ci, new Vector2(at.X - width, at.Y - font.Height), label, Colors.White);
    }

    /// <summary>From the slot's left text edge to the count anchor (F11: x 914 to 971).</summary>
    const float LabelWidth = 57;

    public static string Tooltip(string key, BarSlot slot) => $"{key} : {slot.Name}: {slot.Text}";

    /// <summary>Keeps a prompt's line within its box: characters that would run past the right edge are refused.</summary>
    public static void FitToWidth(LineEdit edit)
    {
        string kept = edit.Text;
        edit.TextChanged += text =>
        {
            var font = edit.GetThemeFont("font");
            int size = edit.GetThemeFontSize("font_size");
            if (font == null || edit.Size.X <= 0 || font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X <= edit.Size.X - 4) { kept = text; return; }
            int caret = Math.Min(edit.CaretColumn, kept.Length);
            edit.Text = kept;
            edit.CaretColumn = caret;
        };
    }
}
