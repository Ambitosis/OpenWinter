using System.Text.Json;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Owned viewport input proving the trained trap ring in either interface.</summary>
public static class TrapRadialCapture
{
    public static async Task Run(string directory, ClientSession session, Camera3D camera, Interaction interaction,
        RadialMenu radial, Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Move(Vector2 point)
        {
            interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await wait(.1);
        }
        async Task Click(MouseButton button, Vector2 point)
        {
            await Move(point);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = point, GlobalPosition = point });
            await wait(.05);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = point, GlobalPosition = point });
            await wait(.4);
        }
        try
        {
            interaction.PointerOverride = Vector2.Zero;
            await wait(4);
            var target = session.Objects.Values.Single(v => v.Name.ToString().EndsWith("_RT_CHEST"));
            await Click(MouseButton.Right, camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * .7f));
            Check(radial.IsOpen && radial.Target == target, "actual chest right-click");
            Check(!radial.Shown[3].Disabled, "trained trap submenu available");
            await shot(directory, "root");
            await Click(MouseButton.Left, radial.WedgePoint("Trap")!.Value);
            Check(radial.Path.Count == 2 && radial.ParentEntry?.Label == "Trap", "submenu replaces root portrait with parent icon");
            Check(interaction.PointerOverride!.Value.DistanceTo(radial.Centre) < 1, "submenu recenters synthetic pointer");
            Check(radial.Labels.SequenceEqual(new[] { "Examine", "", "Disable Trap", "", "Flag Trap", "", "Recover Trap", "" }), "native cardinal order and labels");
            var entries = new[] { (0, "ir_examine", 102), (2, "ir_dis_trap", 0), (4, "ir_flag_trap", 100), (6, "ir_rec_trap", 101) };
            foreach (var (index, icon, sub) in entries)
            {
                var entry = radial.Shown[index];
                Check(entry.Look?.Icons.SequenceEqual(new[] { icon }) == true, entry.Label + " native icon");
                Check(entry.Slot?.Sub == sub && !entry.Disabled, entry.Label + " correct enabled skill command");
                await Move(radial.WedgePoint(entry.Label)!.Value);
                await wait(.4);
                await shot(directory, "direction_" + index);
            }
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[trap-radial] {checks.Count} checks passed");
            quit(0);
        }
        catch (Exception exception)
        {
            await shot(directory, "failed");
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            GD.PrintErr("[trap-radial] " + exception);
            quit(2);
        }
    }
}
