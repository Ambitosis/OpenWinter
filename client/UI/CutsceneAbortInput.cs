using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Escape in cutscene mode asks the module to cancel the cutscene, also while a conversation is shown (which
/// Escape then leaves open); outside cutscene mode a conversation's Escape stays with DialogPanel.
/// </summary>
public partial class CutsceneAbortInput : Node
{
    public ClientSession Session = null!;
    public Func<bool> CanRequest = null!;

    public override void _UnhandledInput(InputEvent e)
    {
        if (!CanRequest() || e is not InputEventKey { Pressed: true, Echo: false } key
            || (key.PhysicalKeycode != Key.Escape && key.Keycode != Key.Escape)) return;
        Session.Request("cutscene-abort");
        GetViewport().SetInputAsHandled();
    }
}
