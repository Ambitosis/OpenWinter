using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // Authored inventorylive scene; input stays in this viewport. No real cursor or focus changes.
    async Task CaptureInventoryLive(string dir)
    {
        void LiveMouse(InputEventMouse e) { _interaction.PointerOverride = e.Position; Input.ParseInputEvent(e); }
        void LiveButton(Vector2 at, bool down)
        {
            LiveMouse(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = down ? 0 : MouseButtonMask.Left });
            LiveMouse(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at, ButtonMask = down ? MouseButtonMask.Left : 0 });
        }
        async Task MoveLive(Vector2 from, Vector2 to)
        {
            for (int i = 1; i <= 15; i++)
            {
                var at = from.Lerp(to, i / 15f);
                LiveMouse(new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = (to - from) / 15, ButtonMask = MouseButtonMask.Left });
                await Wait(0.03);
            }
        }
        try
        {
            await Wait(6);
            var classic = ClassicInventory.Current;
            if (classic == null && !Inventory.Visible) Inventory.Toggle();
            await Wait(1);
            var data = classic?.Data ?? Inventory;
            var container = Descendants<ContainerPanel>(GetTree().Root).First();
            ItemEntry Loot(string name) => classic != null
                ? classic.Inventory.ContainerGrid.Items.Single(i => i.Name == name)
                : Descendants<ItemSlot>(container).First(s => s.IsVisibleInTree() && s.Item?.Name == name).Item!;
            Vector2 ClassicPoint(ClassicItemGrid grid, ItemEntry item) => grid.GetGlobalRect().Position
                + new Vector2((item.X + 0.5f) * grid.Cell.X, (grid.Rows - item.Y % grid.Rows - item.H + 0.5f) * grid.Cell.Y);
            Vector2 LootPoint(ItemEntry item) => classic != null ? ClassicPoint(classic.Inventory.ContainerGrid, item)
                : Descendants<ItemSlot>(container).First(s => s.IsVisibleInTree() && s.Item?.Id == item.Id).GetGlobalRect().Position + Vector2.One * 8;
            void Check(bool condition, string label) { GD.Print($"[inventory-live] {label}={condition}"); if (!condition) throw new InvalidOperationException(label); }
            await Shot(dir, "a_container");
            var potion = Loot("Live potion");
            var at = LootPoint(potion);
            LiveButton(at, true);
            await Wait(0.1);
            Check(!data.Carried.Any(i => i.Id == potion.Id), "press_does_not_loot");
            LiveButton(at, false);
            await Wait(0.6);
            Check(data.Carried.Any(i => i.Id == potion.Id && i.X == 0 && i.Y == 0), "click_auto_first_cell");
            Check(!GetViewport().GuiIsDragging(), "click_has_no_cursor_item");
            await Shot(dir, "b_clicked_potion");
            var arrows = Loot("Live arrows");
            var from = LootPoint(arrows);
            Vector2 destination;
            if (classic != null)
            {
                var grid = classic.Inventory.Grid;
                destination = grid.GetGlobalRect().Position + new Vector2(7.5f * grid.Cell.X, (grid.Rows - arrows.H + 0.5f) * grid.Cell.Y);
            }
            else
            {
                var grid = Descendants<ItemGrid>(Inventory).First(g => g.IsVisibleInTree());
                destination = grid.GetGlobalRect().Position + new Vector2(7.5f * grid.Cell, 0.5f * grid.Cell);
            }
            LiveButton(from, true);
            await MoveLive(from, destination);
            GD.Print($"[inventory-live] source={from} dragging={GetViewport().GuiIsDragging()} data={ItemDragging.Current(Inventory)?.Item.Name}");
            await Shot(dir, "b2_drag_in_progress");
            Check(!data.Carried.Any(i => i.Id == arrows.Id), "drag_does_not_auto_loot");
            LiveButton(destination, false);
            await Wait(0.7);
            GD.Print($"[inventory-live] destination={destination} pointer={GetViewport().GetMousePosition()} items=" + string.Join(";", data.Carried.Select(i => $"{i.Id}:{i.Name}:{i.X},{i.Y}")));
            await Shot(dir, "c_dragged_arrows");
            Check(data.Carried.Any(i => i.Id == arrows.Id && i.X == 7 && i.Y == 0 && i.Stack == 12), "drag_live_explicit_cell");
            if (classic == null)
            {
                var tiles = Descendants<ItemSlot>(Inventory).Where(s => s.IsVisibleInTree()).ToList();
                var source = tiles.First(s => s.Item?.Id == arrows.Id).GetGlobalRect().Position + Vector2.One * 8;
                var target = tiles.First(s => s.Item?.Id == potion.Id).GetGlobalRect().Position + Vector2.One * 8;
                LiveButton(source, true);
                await MoveLive(source, target);
                LiveButton(target, false);
                await Wait(0.7);
                Check(data.Carried.Any(i => i.Id == arrows.Id && i.X == 0 && i.Y == 0)
                    && data.Carried.Any(i => i.Id == potion.Id && i.X == 7 && i.Y == 0), "child_tile_swap_coordinates");
                await Shot(dir, "c2_child_tile_swap");
            }
            for (int i = 0; i < 100 && data.Carried.Any(i => i.Id == potion.Id); i++) await Wait(0.2);
            Check(!data.Carried.Any(i => i.Id == potion.Id), "deleted_potion_disappears");
            Check(classic?.Inventory.Visible ?? Inventory.Visible, "inventory_remains_open");
            await Shot(dir, "d_deleted_potion");
            if (classic != null) classic.Inventory.Close(); else { container.Close(); Inventory.Close(); }
            for (int i = 0; i < 60 && data.Carried.Any(i => i.Id == arrows.Id); i++) await Wait(0.2);
            Check(!data.Carried.Any(i => i.Id == arrows.Id), "closed_cache_updated");
            Check(!(classic?.Inventory.Visible ?? Inventory.Visible), "mutation_does_not_reopen");
            await Shot(dir, "e_closed_inventory");
            GD.Print("[inventory-live] PASS");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PrintErr("[inventory-live] FAIL " + ex); GetTree().Quit(1); }
    }
}
