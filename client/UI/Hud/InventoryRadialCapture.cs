using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Actual inventory right-clicks, native root positions, completed actions and context lifetime.</summary>
public static class InventoryRadialCapture
{
    public static async Task Run(string directory, ClientSession session, Interaction interaction, InventoryPanel inventory,
        RadialMenu radial, Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        if (DisplayServer.GetName() == "headless") shot = (_, _) => Task.CompletedTask;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        async Task Click(MouseButton button, Vector2 point)
        {
            interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await wait(.08);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = point, GlobalPosition = point });
            await wait(.08);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = point, GlobalPosition = point });
            await wait(.6);
        }
        void Key(Key key)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        }
        Vector2 ItemPoint(ItemEntry item)
        {
            if (ClassicInventory.Current is { } classic)
            {
                if (item.Slot >= 0) return classic.Inventory.Slots[item.Slot].GetGlobalRect().GetCenter();
                var grid = classic.Inventory.Grid;
                return grid.GetGlobalTransform() * new Vector2((item.X + item.W * .5f) * grid.Cell.X,
                    (grid.Rows - item.Y % grid.Rows - item.H * .5f) * grid.Cell.Y);
            }
            var tile = inventory.FindChildren("*", "Control", true, false).OfType<ItemSlot>()
                .Single(control => control.IsVisibleInTree() && control.Item?.Id == item.Id);
            return tile.GetGlobalRect().GetCenter();
        }
        async Task Open(ItemEntry item)
        {
            var point = ItemPoint(item);
            interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await wait(.08);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = point, GlobalPosition = point });
            await wait(.6);
            Check(radial.IsOpen && radial.TargetItemArt?.Id == item.Id, item.Name + " actual held-RMB item root");
        }
        async Task Release(Vector2 point)
        {
            interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Right });
            await wait(.08);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = point, GlobalPosition = point });
            await wait(.6);
        }
        async Task Refresh()
        {
            session.Request("inventory");
            await wait(.4);
        }
        try
        {
            interaction.PointerOverride = Vector2.Zero;
            await wait(4);
            Key(Godot.Key.I);
            await wait(.5);
            await Refresh();
            session.Request("spellbook");
            await wait(.4);
            uint potionId = inventory.Carried.Single(item => item.BaseItem == 49).Id;
            uint daggerId = inventory.Carried.Single(item => item.BaseItem == 22).Id;
            foreach (var item in new[] { inventory.Carried.Single(i => i.Id == potionId), inventory.Carried.Single(i => i.Id == daggerId),
                inventory.Carried.Single(i => i.BaseItem == 16), inventory.Equipped[1] })
            {
                await Open(item);
                var labels = radial.Labels;
                Check(labels.Count == 8 && labels[0] == "Examine" && labels[1] == "Cast Spell" && labels[4] == "Drop", item.Name + " N NE S native slots");
                if (item.Slot >= 0) Check(labels[6] == "Unequip" && labels[2] == "", "W Unequip and empty E");
                else if (item.BaseItem == 49)
                {
                    Check(labels[3].StartsWith("Use : ") && radial.Shown[3].Look!.Icons.Any(icon => icon.Equals("is_CurLgtW", StringComparison.OrdinalIgnoreCase)), "SE actual healing power icon");
                    Check(radial.TargetItemArt!.Stack == 5, "authoritative potion stack5 center");
                }
                else Check(labels[2] == "Equip" && labels[6] == (item.BaseItem == 22 ? "Equip Offhand" : ""), item.Name + " equipment directions");
                await shot(directory, item.Slot >= 0 ? "equipped_root" : item.BaseItem switch { 49 => "potion_root", 22 => "dagger_root", _ => "armor_root" });
                var center = radial.Centre;
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = center, GlobalPosition = center });
                await wait(.15);
                Check(radial.IsOpen, item.Name + " opening release at center keeps ring open");
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = center, GlobalPosition = center });
                await wait(.1);
                Check(!radial.IsOpen, item.Name + " second right press closes ring");
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = center, GlobalPosition = center });
                await wait(.1);
            }
            await Open(inventory.Carried.Single(i => i.Id == potionId));
            string power = radial.Labels[3];
            await Release(radial.WedgePoint(power)!.Value);
            Check(!radial.IsOpen, "power selection closes ring");
            await wait(4);
            await Refresh();
            Check(inventory.Carried.Single(i => i.Id == potionId).Stack == 4, "exact potion power consumed5 to4");
            await Open(inventory.Carried.Single(i => i.Id == daggerId));
            await Release(radial.WedgePoint("Equip Offhand")!.Value);
            await wait(1.2);
            await Refresh();
            Check(inventory.Equipped.TryGetValue(4, out var right) && right.Id == daggerId, "offhand command fills empty right hand");
            await Open(inventory.Equipped[4]);
            foreach (string label in new[] { "Cast Spell", "Wizard", "Level 1", "Magic Weapon" })
            {
                Check(radial.WedgePoint(label).HasValue, label + " inventory spell path present");
                if (label == "Cast Spell") await Release(radial.WedgePoint(label)!.Value);
                else await Click(MouseButton.Left, radial.WedgePoint(label)!.Value);
                GD.Print("[inventory-radial] after " + label + " open=" + radial.IsOpen + " path=" + string.Join("/", radial.Path) + " entries=" + string.Join("|", radial.Labels));
            }
            Check(!radial.IsOpen, "inventory spell closes ring after capturing owned target");
            await wait(7);
            await Refresh();
            Check(inventory.Equipped[4].Info!.Properties.Any(property => property.Temporary), "Magic Weapon applied to exact equipped inventory target");
            await Open(inventory.Equipped[1]);
            await Release(radial.WedgePoint("Unequip")!.Value);
            await wait(1.2);
            await Refresh();
            Check(!inventory.Equipped.ContainsKey(1), "robe unequipped");
            await Open(inventory.Carried.Single(i => i.Id == potionId));
            await Release(radial.WedgePoint("Drop")!.Value);
            await wait(2);
            await Refresh();
            Check(inventory.Carried.All(i => i.Id != potionId), "remaining potion stack dropped");
            await shot(directory, "actions_completed");
            await Open(inventory.Equipped[4]);
            Key(Godot.Key.I);
            await wait(.3);
            Check(!radial.IsOpen, "closing inventory closes its context ring");
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { display = DisplayServer.GetName(), checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[inventory-radial] {checks.Count} checks passed");
            quit(0);
        }
        catch (Exception exception)
        {
            await shot(directory, "failed");
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            GD.PrintErr("[inventory-radial] " + exception);
            quit(2);
        }
    }
}
