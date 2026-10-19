using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI;
using OpenWinter.Client.UI.FrontEnd;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-controls dir: the original's keys at work, pressed as real key events: Options → Controls
/// (every category), the radial menu on self (Num 0) and its numpad navigation, the camera keys and modes,
/// the hidden interface (H), inventory pages ([ / ]), the player list (P) and latched tooltips (Tab twice).
/// </summary>
public partial class Main
{
    async Task CaptureControls(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(3.0);

        // Options → Controls, scrolled through every category.
        _options!.Open(OptionsView.Page.Controls);
        await Wait(0.8);
        var scroll = Descendants<ScrollContainer>(_options).OrderByDescending(s => s.Size.Y).First();
        for (int page = 0; page < 8; page++)
        {
            await Shot(dir, $"c{page:00}_options_controls");
            int before = scroll.ScrollVertical;
            scroll.ScrollVertical += (int)(scroll.Size.Y * 0.9f);
            await Wait(0.3);
            if (scroll.ScrollVertical == before) break;
        }
        ((UI.UiTheme.Window)_options).Close();
        await Wait(0.5);

        // Tab twice quickly: the names stay up after the key is let go.
        await Press(Key.Tab);
        await Press(Key.Tab);
        await Wait(0.6);
        await Shot(dir, "c09_tooltips_latched");
        await Press(Key.Tab);

        // The radial menu on yourself, then the numpad: 6 picks the right-hand wedge, 5 goes back, 0 closes.
        await Press(Key.Kp0);
        await Wait(0.6);
        await Shot(dir, "c10_radial_self");
        await Press(Key.Kp6);
        await Wait(0.6);
        await Shot(dir, "c11_radial_numpad_right");
        await Press(Key.Kp5);
        await Wait(0.4);
        await Shot(dir, "c12_radial_numpad_back");
        await Press(Key.Kp0);
        await Wait(0.4);

        // Camera: zoom out full (End), up full (Insert), then the modes (Num *) while walking a curve.
        await Press(Key.End);
        await Wait(1.0);
        await Shot(dir, "c20_zoom_out_full");
        await Press(Key.Insert);
        await Wait(0.8);
        await Shot(dir, "c21_camera_up_full");
        await Press(Key.Home);
        await Press(Key.Delete);
        await Wait(0.8);
        await Shot(dir, "c22_zoom_in_full_down_full");
        _camera.Distance = 9;
        _camera.Pitch = Mathf.DegToRad(-40);
        foreach (var (mode, dx, dy) in new[] { (CameraMode.Chase, 1f, 0.4f), (CameraMode.Driving, -1f, 0.4f) })
        {
            while (_camera.Mode != mode) await Press(Key.KpMultiply);
            float before = _camera.Yaw;
            // Walk off at an angle to the view (as a click to the side would); the camera should come round behind.
            _input.Scripted = (new Vector2(dx, dy).Normalized(), false);
            await Wait(1.2);
            _input.Scripted = null;
            await Wait(0.6);
            GD.Print($"[capture-controls] {mode}: camera yaw {Mathf.RadToDeg(before):0} -> {Mathf.RadToDeg(_camera.Yaw):0} degrees");
            await Shot(dir, mode == CameraMode.Chase ? "c23_camera_chase" : "c24_camera_driving");
        }
        while (_camera.Mode != CameraMode.TopDown) await Press(Key.KpMultiply);
        await Wait(0.5);

        // H hides everything, H again brings it back.
        await Press(Key.H);
        await Wait(0.4);
        await Shot(dir, "c31_interface_hidden");
        await Press(Key.H);
        await Wait(0.4);

        // Inventory, then ] for its next page; the player list.
        await Press(Key.I);
        await Wait(1.5);
        await Press(Key.Bracketright);
        await Wait(0.5);
        await Shot(dir, "c40_inventory_next_page");
        await Press(Key.I);
        await Press(Key.P);
        await Wait(0.8);
        await Shot(dir, "c41_player_list");
        await Press(Key.P);

        // Space pauses single player (the server's "Paused" banner).
        await Press(Key.Space);
        await Wait(0.6);
        await Shot(dir, "c42_paused");
        await Press(Key.Space);
        await Wait(0.3);
        GetTree().Quit();
    }

    /// <summary>A key press and release through the real input path (InputMap actions, unhandled input).</summary>
    async Task Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
