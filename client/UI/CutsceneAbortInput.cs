using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Escape asks the module to cancel a cutscene; normal dialogue Escape remains with DialogPanel.</summary>
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
