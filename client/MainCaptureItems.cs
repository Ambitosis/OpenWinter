using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-items dir: screenshots of the item windows (inventory, tooltips, filters, item menu,
/// drag in progress, split, store with a purchase confirmation, loot, examine cards), driven by
/// real mouse events so hover, tooltips and drag and drop are exercised.
/// </summary>
public partial class Main
{
    async Task CaptureItems(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        if (_args.ContainsKey("inventory-placement-only")) { await CaptureInventoryPlacement(dir); return; }
        if (_args.ContainsKey("inventory-ground-only")) { await CaptureGroundInventory(dir); return; }
        if (_args.ContainsKey("gold-placement-only")) { await CaptureGoldPlacement(dir); return; }
        if (_args.ContainsKey("quick-sell-only")) { await CaptureQuickSell(dir); return; }
        if (_args.ContainsKey("store-radial-only")) { await CaptureStoreRadial(dir); return; }
        if (_args.ContainsKey("inventory-drop-only")) { await CaptureInventoryDrop(dir); return; }
        if (_args.ContainsKey("inventory-live-only")) { await CaptureInventoryLive(dir); return; }
        if (_args.ContainsKey("ground-radial-only")) { await CaptureGroundRadial(dir); return; }
        await Wait(2.0);
        Inventory.Toggle();
        await Wait(2.5);
        await Shot(dir, "items_01_inventory");

        // Tooltip on the first weapon in the backpack, then on the equipped armour.
        var pack = Descendants<ItemSlot>(Inventory).Where(s => s.Item != null && s.EquipSlot < 0).ToList();
        var weapon = pack.FirstOrDefault(s => s.Item!.Info?.Category == "weapon" && s.Item.Info.Properties.Count > 0) ?? pack.FirstOrDefault();
        if (weapon != null) { await Hover(weapon); await Shot(dir, "items_02_tooltip_weapon"); }
        var armour = Descendants<ItemSlot>(Inventory).FirstOrDefault(s => s.EquipSlot == 1 && s.Item != null);
        if (armour != null) { await Hover(armour); await Shot(dir, "items_03_tooltip_armour"); }
        var potion = pack.FirstOrDefault(s => s.Item!.Info?.Category == "consumable");
        if (potion != null) { await Hover(potion); await Shot(dir, "items_04_tooltip_consumable"); }

        // Filter tab and the item menu.
        Inventory.ShowFilter(ItemFilter.Weapons);
        await Wait(0.4);
        var first = Descendants<ItemSlot>(Inventory).FirstOrDefault(s => s.Item != null && s.EquipSlot < 0 && s.Item.Filter == ItemFilter.Weapons);
        if (first != null)
        {
            Click(first, MouseButton.Right);
            await Wait(0.4);
            await Shot(dir, "items_05_filter_menu");
            foreach (var menu in Descendants<PopupMenu>(Inventory)) menu.Hide();
            await Wait(0.2);
        }
        Inventory.ShowFilter(ItemFilter.All);
        await Wait(0.3);

        // Dragging in the grid: a free place (green), an occupied one (red), then really moving it.
        var grid = Descendants<ItemGrid>(Inventory).FirstOrDefault();
        var tile = grid != null ? Descendants<ItemSlot>(grid).FirstOrDefault(s => s.Item != null) : null;
        if (grid != null && tile != null)
        {
            var item = tile.Item!;
            (int X, int Y)? free = null, busy = null;
            var others = grid.Items.Where(o => o.Id != item.Id && o.X >= 0).ToList();
            bool Overlaps(ItemEntry o, int x, int y) => x < o.X + o.W && o.X < x + item.W && y < o.Y + o.H && o.Y < y + item.H;
            for (int y = 0; y + item.H <= 6 && free == null; y++)
                for (int x = 9 - item.W + 1; x >= 0 && free == null; x--)
                    if (!others.Any(o => Overlaps(o, x, y)) && !(x == item.X && y == item.Y)) free = (x, y);
            busy = others.Where(o => o.W != item.W || o.H != item.H).Select(o => ((int, int)?)(o.X, o.Y)).FirstOrDefault();
            Vector2 CellCenter((int X, int Y) c) => grid.GetGlobalRect().Position + new Vector2((c.X + 0.5f) * grid.Cell, (c.Y + 0.5f) * grid.Cell);
            var from = tile.GetGlobalRect().Position + new Vector2(grid.Cell / 2, grid.Cell / 2);
            Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = from, GlobalPosition = from, ButtonMask = MouseButtonMask.Left });
            if (busy is { } b)
            {
                await MoveMouse(from, CellCenter(b), true);
                await Wait(0.3);
                await Shot(dir, "items_06b_drag_blocked");
                from = CellCenter(b);
            }
            if (free is { } f)
            {
                await MoveMouse(from, CellCenter(f), true);
                await Wait(0.3);
                await Shot(dir, "items_06_drag_fits");
                var at = CellCenter(f);
                Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
                await Wait(0.8);
                await Shot(dir, "items_06c_moved");
            }
            else
            {
                GetViewport().GuiCancelDrag();
                Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = from, GlobalPosition = from });
            }
        }

        // The other pages and the modern view of the same items.
        Inventory.ShowPage(1);
        await Wait(0.3);
        await Shot(dir, "items_06d_page2");
        Inventory.ShowPage(0);
        InventorySettings.ModernView = true;
        await Wait(0.5);
        await Shot(dir, "items_06e_modern");
        InventorySettings.ModernView = false;
        await Wait(0.3);

        // A bag, when there is one.
        var bag = Inventory.Carried.FirstOrDefault(i => i.IsBag);
        if (bag != null)
        {
            Inventory.OpenBag(bag);
            await Wait(0.6);
            await Shot(dir, "items_06f_bag");
        }

        // Store: wares beside your pack; a tooltip with the price; a purchase confirmation.
        Inventory.Close();
        _session.Send(new ChatSendMessage { Text = "/store" + (_args.TryGetValue("store", out var tag) ? " " + tag : "") }.Write());
        await Wait(2.0);
        await Shot(dir, "items_07_store");
        // Classic: the store grid by category; hover a ware, select it, the confirmation card.
        var storeGrid = Descendants<ItemGrid>(Store).FirstOrDefault(g => g.DragSource == "store");
        var ware = storeGrid != null ? Descendants<ItemSlot>(storeGrid).Where(t => t.Item != null).OrderByDescending(t => t.Item!.Info?.Properties.Count ?? 0).FirstOrDefault() : null;
        if (ware != null)
        {
            await Hover(ware);
            await Shot(dir, "items_08_store_tooltip");
            Click(ware, MouseButton.Left);
            await Wait(0.3);
            var w = ware.Item!;
            ConfirmOverlay.Show(Store, "Confirm purchase", $"Buy {w.Name} for {ItemUi.Gp(w.Price)}? You will have {ItemUi.Gp(Math.Max(0, Inventory.Gold - w.Price))} left.", "Buy", () => { });
            await Wait(0.4);
            await Shot(dir, "items_09_store_confirm");
            foreach (var o in Descendants<ConfirmOverlay>(Store)) o.Cancel();
            await Wait(0.2);
        }
        var myGrid = Descendants<ItemGrid>(Store).FirstOrDefault(g => g.DragSource == "pack");
        var mine = myGrid != null ? Descendants<ItemSlot>(myGrid).FirstOrDefault(t => t.Item != null) : null;
        if (mine != null) { await Hover(mine); await Shot(dir, "items_10_store_sell_tooltip"); }
        InventorySettings.ModernView = true;
        await Wait(0.5);
        await Shot(dir, "items_10b_store_modern");
        InventorySettings.ModernView = false;
        await Wait(0.2);
        Store.Close();
        await Wait(0.3);

        // Loot beside the inventory.
        _session.Send(new ChatSendMessage { Text = "/loot" }.Write());
        await Wait(3.0);
        Inventory.Toggle();
        await Wait(1.5);
        var loot = Descendants<ContainerPanel>(this).FirstOrDefault();
        var lootSlot = loot != null ? Descendants<ItemSlot>(loot).FirstOrDefault(s => s.Item != null) : null;
        if (lootSlot != null) await Hover(lootSlot);
        await Shot(dir, "items_11_loot");
        loot?.Close();
        Inventory.Close();

        // Examine cards: an item, yourself, the nearest creature.
        var examined = Inventory.Equipped.GetValueOrDefault(4) ?? Inventory.Carried.FirstOrDefault();
        if (examined != null)
        {
            Examine.Examine(examined.Id);
            await Wait(1.0);
            await Shot(dir, "items_12_examine_item");
        }
        var me = _session.Controlled;
        var npc = _session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me)
            .OrderBy(v => v.GlobalPosition.DistanceTo(me!.GlobalPosition)).FirstOrDefault();
        Examine.Examine(npc?.ObjectId ?? _session.ControlledId);
        await Wait(1.0);
        await Shot(dir, "items_13_examine_creature");
        Examine.Close();

        // Split a stack.
        Inventory.Toggle();
        await Wait(1.0);
        var stack = Descendants<ItemSlot>(Inventory).FirstOrDefault(s => s.Item?.Stack > 1);
        if (stack != null)
        {
            Inventory.ShowSplit(stack.Item!);
            await Wait(0.4);
            await Shot(dir, "items_14_split");
        }
        GetTree().Quit();
    }

    static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        foreach (var c in root.GetChildren())
        {
            if (c is T t) yield return t;
            foreach (var d in Descendants<T>(c)) yield return d;
        }
    }

    static Vector2 Center(Control c) => c.GetGlobalRect().GetCenter();

    async Task MoveMouse(Vector2 from, Vector2 to, bool held, int steps = 10)
    {
        for (int i = 1; i <= steps; i++)
        {
            var p = from.Lerp(to, i / (float)steps);
            Mouse(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = (to - from) / steps, ButtonMask = held ? MouseButtonMask.Left : 0 });
            await Wait(0.03);
        }
    }

    void Mouse(InputEvent e)
    {
        if (e is InputEventMouse mouse) _interaction.PointerOverride = mouse.Position;
        GetViewport().PushInput(e);
    }

    async Task Hover(Control c)
    {
        var away = new Vector2(4, 4);
        Mouse(new InputEventMouseMotion { Position = away, GlobalPosition = away });
        await Wait(0.15);
        var p = Center(c);
        Mouse(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = new Vector2(1, 0) });
        await Wait(1.1);
    }

    void Click(Control c, MouseButton button, bool doubleClick = false)
    {
        var p = Center(c);
        var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right;
        Mouse(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = p, GlobalPosition = p, DoubleClick = doubleClick, ButtonMask = mask });
        Mouse(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = p, GlobalPosition = p });
    }
}
