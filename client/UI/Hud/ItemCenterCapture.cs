using System.Text.Json;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Real radial input with authoritative item art in both layouts.</summary>
public static class ItemCenterCapture
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
        void Escape()
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
        }
        try
        {
            interaction.PointerOverride = Vector2.Zero;
            await wait(4);
            foreach (var name in new[] { "Sword", "Armor" })
            {
                var target = session.Objects.Values.Single(o => o.DisplayName == "Center " + name);
                await Click(MouseButton.Right, camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * .1f));
                Check(radial.IsOpen && radial.Target == target, name + " actual item right-click");
                var icons = radial.TargetItemIcons;
                Check(icons.Length > 0 && icons.All(icon => session.Content!.Textures.Get(icon) != null), name + " all authoritative art resolves");
                if (name == "Sword") Check(icons.SequenceEqual(new[] { "iwswls_b_061", "iwswls_m_011", "iwswls_t_011" }), "native composite weapon identity");
                else Check(icons.All(icon => icon.StartsWith("iconplt:", StringComparison.Ordinal)), "armor retains item palette layers");
                var art = radial.TargetItemArt!;
                var canvas = Classic.ClassicItemArt.IconSize(Classic.ClassicAssets.For(session.Content!), art);
                Check(canvas == (name == "Sword" ? new Vector2(32, 128) : new Vector2(64, 96)), name + " native unpadded inventory canvas");
                await shot(directory, name.ToLowerInvariant() + "_root");
                await Click(MouseButton.Left, radial.WedgePoint("Cast Spell")!.Value);
                Check(radial.Path.Count > 1 && radial.ParentEntry != null, name + " submenu replaces root center");
                await shot(directory, name.ToLowerInvariant() + "_submenu");
                Escape();
                await wait(.3);
                Check(!radial.IsOpen, name + " escape closes item context");
            }
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[item-center] {checks.Count} checks passed");
            quit(0);
        }
        catch (Exception exception)
        {
            await shot(directory, "failed");
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            GD.PrintErr("[item-center] " + exception);
            quit(2);
        }
    }
}
