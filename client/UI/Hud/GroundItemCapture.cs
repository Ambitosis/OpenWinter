using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Actual owned-client ground-item radial input plus authenticated inventory observations.</summary>
public static class GroundItemCapture
{
    public static async Task Run(string directory, ClientSession session, Camera3D camera, Interaction interaction,
        RadialMenu radial, Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var snapshots = new List<JsonElement>();
        JsonElement? inventory = null;
        void Notify(NotifyMessage message)
        {
            if (message.Kind == "inventory-open") inventory = JsonDocument.Parse(message.Json).RootElement.Clone();
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Click(MouseButton button, Vector2 point)
        {
            interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await wait(.05);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = point, GlobalPosition = point });
            await wait(.05);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = point, GlobalPosition = point });
            await wait(.4);
        }
        async Task Phase(string name, int slot)
        {
            var target = session.Objects.Values.Single(o => o.DisplayName == "Ground " + name);
            uint id = target.ObjectId;
            await Click(MouseButton.Right, camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * .1f));
            Check(radial.IsOpen && radial.Target == target, name + " actual right-click target");
            Check(radial.Labels.SequenceEqual(new[] { "Examine", "Cast Spell", "Equip", "", "Pick up", "", "Equip Offhand", "" }), name + " native compass slots");
            await shot(directory, name + "_menu");
            await Click(MouseButton.Left, radial.WedgePoint(slot == 4 ? "Equip" : "Equip Offhand")!.Value);
            Check(!radial.IsOpen, name + " selection closes menu");
            await wait(3.5);
            inventory = null;
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.I, PhysicalKeycode = Key.I, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.I, PhysicalKeycode = Key.I, Pressed = false });
            session.Request("inventory");
            await wait(.4);
            Check(inventory.HasValue, name + " authoritative inventory received");
            var value = inventory!.Value;
            snapshots.Add(value);
            Check(value.GetProperty("equipped").EnumerateArray().Any(e => e.GetProperty("slot").GetInt32() == slot && e.GetProperty("id").GetUInt32() == id), name + " picked up and equipped in selected hand");
            if (slot == 5)
                Check(value.GetProperty("equipped").EnumerateArray().Any(e => e.GetProperty("slot").GetInt32() == 4 && e.GetProperty("id").GetUInt32() != id), "offhand preserves main hand");
            else
                Check(value.GetProperty("items").EnumerateArray().Any(e => e.GetProperty("baseItem").GetInt32() == 22), "main hand replacement keeps dagger in backpack");
            await shot(directory, name + "_equipped");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.I, PhysicalKeycode = Key.I, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.I, PhysicalKeycode = Key.I, Pressed = false });
            await wait(.3);
        }
        session.NotifyReceived += Notify;
        try
        {
            interaction.PointerOverride = Vector2.Zero;
            await wait(4);
            await Phase("main", 4);
            await wait(6);
            await Phase("offhand", 5);
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks, snapshots }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[ground-item] {checks.Count} checks passed");
            quit(0);
        }
        catch (Exception exception)
        {
            await shot(directory, "failed");
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            GD.PrintErr("[ground-item] " + exception);
            quit(2);
        }
        finally { session.NotifyReceived -= Notify; }
    }
}
