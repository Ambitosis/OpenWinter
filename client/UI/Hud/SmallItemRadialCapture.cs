using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Actual potion/ammunition target input, native compass slots and final pickup ownership.</summary>
public static class SmallItemRadialCapture
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
            await wait(.5);
        }
        void InventoryKey()
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.I, PhysicalKeycode = Key.I, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.I, PhysicalKeycode = Key.I, Pressed = false });
        }
        session.NotifyReceived += Notify;
        try
        {
            interaction.PointerOverride = Vector2.Zero;
            await wait(4);
            foreach (var name in new[] { "Potion", "Arrow" })
            {
                var target = session.Objects.Values.Single(o => o.DisplayName == name + " Target");
                uint id = target.ObjectId;
                await Click(MouseButton.Right, camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * .1f));
                Check(radial.IsOpen && radial.Target == target, name + " actual item right-click");
                Check(radial.Labels.SequenceEqual(new[] { "Examine", "Cast Spell", name == "Arrow" ? "Equip" : "", "", "Pick up", "", "", "" }), name + " native compass slots");
                Check(radial.TargetItemArt?.Stack == (name == "Arrow" ? 99 : 1), name + " authoritative center stack");
                await shot(directory, name.ToLowerInvariant() + "_root");
                await Click(MouseButton.Left, radial.WedgePoint("Pick up")!.Value);
                Check(!radial.IsOpen, name + " pickup selection closes radial");
                await wait(4);
                inventory = null;
                InventoryKey();
                session.Request("inventory");
                await wait(.4);
                Check(inventory.HasValue, name + " authoritative inventory received");
                var value = inventory!.Value;
                snapshots.Add(value);
                Check(value.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetUInt32() == id), name + " exact ground item now in backpack");
                Check(!value.GetProperty("equipped").EnumerateArray().Any(item => item.GetProperty("id").GetUInt32() == id), name + " pickup does not equip");
                await shot(directory, name.ToLowerInvariant() + "_picked_up");
                InventoryKey();
                await wait(.3);
            }
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks, snapshots }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[small-item-radial] {checks.Count} checks passed");
            quit(0);
        }
        catch (Exception exception)
        {
            await shot(directory, "failed");
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            GD.PrintErr("[small-item-radial] " + exception);
            quit(2);
        }
        finally { session.NotifyReceived -= Notify; }
    }
}
