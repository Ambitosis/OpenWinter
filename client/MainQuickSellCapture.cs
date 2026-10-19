using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Game.Objects;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --capture-items dir --quick-sell-only --ui classic: with a store open, Shift-click on a backpack item sells it at
    /// once (no confirmation), and Shift-click on a stack sells the stack instead of splitting it.
    /// </summary>
    async Task CaptureQuickSell(string dir)
    {
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            GD.Print($"[quick-sell] {name}: {ok}");
            if (!ok) throw new InvalidOperationException(name);
        }
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            var store = new Store { Template = world.Resources.ReadGff("nw_storgenral001", OpenWinter.Aurora.Resources.ResType.Utm)! };
            world.Register(store);
            world.AddToArea(store, pc.Area!, pc.Position, 0);
            world.Inventory.CreateOn("nw_it_mpotion001", pc, 3, "");
            world.Inventory.CreateOn("nw_wswdg001", pc, 1, "");
            world.Inventory.OpenStore(store, pc, 0, 0);
            await Wait(1.5);
            var items = ClassicInventory.Current!;
            Check("store and inventory open", items.Store.Visible && items.Inventory.IsOpen);
            async Task<bool> ShiftClick(ItemEntry entry)
            {
                var at = items.Inventory.Grid.ScreenCentreOf(entry);
                Mouse(new InputEventMouseMotion { Position = at, GlobalPosition = at });
                await Wait(0.1);
                Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at, ShiftPressed = true, ButtonMask = MouseButtonMask.Left });
                Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at, ShiftPressed = true });
                await Wait(0.8);
                return world.Get<Item>(entry.Id) is not { } left || world.Inventory.Owner(left) != pc;
            }
            var single = items.Data.Carried.First(i => i.Stack <= 1 && items.Store.CanSell(i));
            int gold = pc.Gold;
            Check("shift-click sells a single item at once", await ShiftClick(single) && pc.Gold > gold && !items.Confirm.IsOpen);
            await Shot(dir, "a_sold_single");
            var stack = items.Data.Carried.First(i => i.Stack > 1 && items.Store.CanSell(i));
            gold = pc.Gold;
            Check("shift-click sells a whole stack instead of splitting it", await ShiftClick(stack) && pc.Gold > gold && !items.Confirm.IsOpen && !items.Split.IsOpen);
            await Shot(dir, "b_sold_stack");
            passed = true;
        }
        catch (Exception e) { GD.PrintErr(e); }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "quick-sell.json"),
                System.Text.Json.JsonSerializer.Serialize(checks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
