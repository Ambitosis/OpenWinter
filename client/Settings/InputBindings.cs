using Godot;

namespace OpenWinter.Client.Preferences;

/// <summary>
/// Registers every <see cref="ActionCatalog"/> action in Godot's InputMap with the player's keys (physical
/// keys, so WASD stays under the left hand on any layout), and helpers to test and show them.
/// Use <see cref="Pressed"/> for one-shot actions (exact modifiers: F5 is not Ctrl+F5) and
/// <c>Input.IsActionPressed(id)</c> for held ones (movement, camera).
/// </summary>
public static class InputBindings
{
    public static void Register(BindingSet set)
    {
        foreach (var action in ActionCatalog.All)
        {
            if (!InputMap.HasAction(action.Id)) InputMap.AddAction(action.Id, 0.2f);
            InputMap.ActionEraseEvents(action.Id);
            foreach (var bind in set.Get(action.Id))
                if (ToEvent(bind) is { } e) InputMap.ActionAddEvent(action.Id, e);
        }
    }

    public static InputEvent? ToEvent(KeyBind b)
    {
        if (b.IsEmpty) return null;
        if (b.IsMouse && int.TryParse(b.Key[5..], out var button))
            return new InputEventMouseButton { ButtonIndex = (MouseButton)button, CtrlPressed = b.Ctrl, ShiftPressed = b.Shift, AltPressed = b.Alt, Pressed = true };
        if (!Enum.TryParse<Key>(b.Key, out var key)) return null;
        return new InputEventKey { PhysicalKeycode = key, CtrlPressed = b.Ctrl, ShiftPressed = b.Shift, AltPressed = b.Alt, Pressed = true };
    }

    /// <summary>
    /// The binding a key or mouse press would make (for the rebinding prompt), or null for events that
    /// can't be bound (motion, a modifier still held, left / right click, which the game uses for itself).
    /// </summary>
    public static KeyBind? FromEvent(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey k:
            {
                var key = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
                if (key == Key.None) return null;
                string name = key.ToString();
                bool modifier = KeyBind.IsModifierKey(name);
                // A modifier on its own binds when it is let go (so Ctrl+X can still be pressed).
                if (modifier && k.Pressed) return null;
                if (!modifier && !k.Pressed) return null;
                return new KeyBind(name, ctrl: k.CtrlPressed && name != "Ctrl", shift: k.ShiftPressed && name != "Shift", alt: k.AltPressed && name != "Alt");
            }
            case InputEventMouseButton { Pressed: true } mb when mb.ButtonIndex is not (MouseButton.Left or MouseButton.Right):
                return new KeyBind("Mouse" + (int)mb.ButtonIndex, ctrl: mb.CtrlPressed, shift: mb.ShiftPressed, alt: mb.AltPressed);
        }
        return null;
    }

    /// <summary>True when the event presses this action with exactly its modifiers (no key repeat).</summary>
    public static bool Pressed(InputEvent e, string action) =>
        InputMap.HasAction(action) && e.IsActionPressed(action, allowEcho: false, exactMatch: true);

    /// <summary>Held binding at a pointer event. Its modifier flags are current even if a key-up was missed.</summary>
    public static bool HeldAtPointer(string action, InputEventWithModifiers pointer)
    {
        foreach (var bind in Settings.Bindings.Get(action))
        {
            if (bind.Ctrl && !pointer.CtrlPressed || bind.Shift && !pointer.ShiftPressed || bind.Alt && !pointer.AltPressed) continue;
            bool held = bind.Key switch
            {
                "Shift" => pointer.ShiftPressed,
                "Ctrl" => pointer.CtrlPressed,
                "Alt" => pointer.AltPressed,
                "Meta" => pointer.MetaPressed,
                _ => ToEvent(bind) switch
                {
                    InputEventKey key => Input.IsPhysicalKeyPressed(key.PhysicalKeycode),
                    InputEventMouseButton button => Input.IsMouseButtonPressed(button.ButtonIndex),
                    _ => false,
                },
            };
            if (held) return true;
        }
        return false;
    }

    /// <summary>The layout's name for a physical key ("Z" for the W key on AZERTY); special keys by their usual names.</summary>
    public static string KeyName(string key)
    {
        if (key.Length == 1 && Enum.TryParse<Key>(key, out var k))
        {
            var local = DisplayServer.KeyboardGetKeycodeFromPhysical(k);
            if (local != Key.None) return OS.GetKeycodeString(local);
        }
        return KeyBind.Name(key);
    }

    /// <summary>The first key of an action for hints and key caps ("I", "Ctrl+F5"); empty when unbound.</summary>
    public static string KeyText(string action) => Settings.Bindings.Get(action, 0)?.Display(KeyName) ?? "";
}
