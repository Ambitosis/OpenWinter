using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Owned-client input/metadata regression for radialclient; never moves the real pointer.</summary>
public static class ObjectRadialCapture
{
    public static async Task Run(string directory, ClientSession session, Camera3D camera, Interaction interaction,
        RadialMenu radial, Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var records = new List<object>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Click(MouseButton button, Vector2 point)
        {
            interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await wait(0.05);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = point, GlobalPosition = point });
            await wait(0.05);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = point, GlobalPosition = point });
            await wait(0.35);
        }
        async Task Open(string tag, float height)
        {
            radial.Close();
            var target = session.Objects.Values.Single(v => v.Name.ToString().EndsWith("_" + tag));
            var point = camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * height);
            await Click(MouseButton.Right, point);
            Check(radial.IsOpen && radial.Target == target, "input target " + tag);
            await wait(0.2);
        }
        async Task Record(string name, bool door, bool locked, bool lockable, bool trap)
        {
            Check(radial.Labels.SequenceEqual(new[] { "Examine", "Cast Spell", door ? "Open" : "Use", "Trap", "", "Bash", locked ? "Unlock" : lockable || !door ? "Lock" : "", "" }), name + " compass slots");
            Check(radial.Shown[3].Disabled == !trap, name + " trap availability");
            Check(door && !locked && !lockable ? radial.Shown[6].Hidden : radial.Shown[6].Disabled == (!locked && !lockable), name + " lock availability");
            Check(radial.TargetPortrait.Length > 0, name + " portrait metadata");
            records.Add(new { name, portrait = radial.TargetPortrait, entries = radial.Shown.Select(e => new { e.Label, e.Hidden, e.Disabled, icons = e.Look?.Icons }).ToArray() });
            await shot(directory, name);
        }
        try
        {
            // Keep a synthetic position set throughout: submenu recentering must never warp the OS cursor.
            interaction.PointerOverride = Vector2.Zero;
            await wait(4);
            await Open("RC_CHEST", 0.7f);
            await Record("a_unlocked", false, false, false, false);
            radial.Close();
            await wait(4);
            await Open("RC_CHEST", 0.7f);
            await Record("b_locked", false, true, false, false);
            radial.Close();
            await wait(3);
            await Open("RC_CHEST", 0.7f);
            await Record("c_trapped", false, true, false, true);
            radial.Close();
            await wait(3);
            await Open("RC_CHEST", 0.7f);
            var targetId = radial.Target!.ObjectId;
            await Click(MouseButton.Left, radial.WedgePoint("Unlock")!.Value);
            Check(!radial.Visible, "unlock input closes radial");
            await wait(3);
            await Open("RC_CHEST", 0.7f);
            Check(radial.Target!.ObjectId == targetId && radial.Labels[6] == "Lock", "key unlock completed on same target");
            await shot(directory, "d_unlocked_with_key");
            radial.Close();
            await Open("RC_DOOR", 1.0f);
            await Record("e_door", true, false, false, false);
            radial.Close();
            await wait(6.5);
            await Open("RC_TABLE", 0.7f);
            await Record("f_usable", false, false, false, false);
            radial.Close();
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks, records }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[object-radial] {checks.Count} checks passed");
            quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr("[object-radial] " + exception);
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            quit(2);
        }
    }
}
