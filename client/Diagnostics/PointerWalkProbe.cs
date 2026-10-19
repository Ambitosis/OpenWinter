using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using Godot;

namespace OpenWinter.Client.Diagnostics;

/// <summary>Headless real-Godot regression: --path client res://Diagnostics/PointerWalkProbe.tscn.</summary>
public partial class PointerWalkProbe : Node
{
    int _checks;
    void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        _checks++;
    }

    static void KeyEvent(Key key, bool down)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down, ShiftPressed = key == Key.Shift && down });
        Input.FlushBufferedEvents();
    }

    public override void _Ready()
    {
        try
        {
            InputBindings.Register(Settings.Bindings);
            using var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false };
            KeyEvent(Key.Shift, true);
            Check(PlayerInput.WalkHeld, "positive: Shift keydown reaches InputMap");
            click.ShiftPressed = true;
            for (int i = 0; i < 20; i++) Check(PlayerInput.WalkAtPointer(click), "Shift click walks");
            // Deliberately omit key-up: the event says Shift is released but Godot's action stays latched.
            click.ShiftPressed = false;
            Check(PlayerInput.WalkHeld, "positive: missed release leaves stale action");
            Check(!PlayerInput.WalkAtPointer(click), "unmodified click must run despite stale Shift action");
            Settings.Current.Gameplay.AlwaysWalk = true;
            Check(PlayerInput.WalkAtPointer(click), "Always Walk is preserved");
            click.ShiftPressed = true;
            Check(!PlayerInput.WalkAtPointer(click), "Shift inverts Always Walk");
            Settings.Current.Gameplay.AlwaysWalk = false;
            KeyEvent(Key.Shift, false);
            click.ShiftPressed = false;
            Check(!PlayerInput.WalkHeld && !PlayerInput.WalkAtPointer(click), "normal release also runs");
            Settings.Current.Controls.Bindings[ActionCatalog.Walk] = [new KeyBind("Z", ctrl: true)];
            InputBindings.Register(Settings.Bindings);
            KeyEvent(Key.Z, true);
            Check(!PlayerInput.WalkAtPointer(click), "rebound chord requires Ctrl");
            click.CtrlPressed = true;
            Check(PlayerInput.WalkAtPointer(click), "rebound Ctrl+Z walks");
            KeyEvent(Key.Z, false);
            Check(!PlayerInput.WalkAtPointer(click), "rebound key release runs");
            Settings.Current.Controls.Bindings[ActionCatalog.Walk] = [new KeyBind("Shift"), new KeyBind("Z")];
            InputBindings.Register(Settings.Bindings);
            KeyEvent(Key.Z, true);
            Check(PlayerInput.WalkAtPointer(click), "secondary binding remains usable");
            KeyEvent(Key.Z, false);
            GD.Print($"[pointer-walk] {_checks} checks passed");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
