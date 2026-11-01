using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Whether typed keys belong to a text box: the chat line, or any focused editable LineEdit / TextEdit (journal notes,
/// map pin notes, NUI text fields). Key bindings that are polled rather than delivered as events (movement, camera)
/// stand still meanwhile, as in the original.
/// </summary>
public static class TextFocus
{
    public static bool Active(Viewport viewport) =>
        DM.ChatInput.Typing || viewport.GuiGetFocusOwner() is LineEdit { Editable: true } or TextEdit { Editable: true };
}
