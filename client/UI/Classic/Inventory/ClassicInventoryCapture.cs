using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-classic-inventory dir (with --ui classic, at --resolution 1280x720 to compare with the original):
/// screenshots of the classic item windows driven by input events pushed into the viewport (the real mouse is
/// never moved): the inventory, a tooltip, a press-and-drag, a click pick-up carried over the grid and put down,
/// a page turn, the item menu, item examine, stack split, equipping by dragging, a store with its
/// confirmation, and a container. --dump-textures a,b saves interface textures beside them.
/// </summary>
public partial class Main
{
    async Task CaptureClassicInventory(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(2.0);
        if (_args.TryGetValue("dump-textures", out var dump) && _session.Content is { } content)
        {
            var assets = ClassicAssets.For(content);
            var texDir = System.IO.Path.Combine(dir, "tex");
            System.IO.Directory.CreateDirectory(texDir);
            foreach (var name in dump.Split(',', StringSplitOptions.RemoveEmptyEntries))
                assets.Texture(name)?.GetImage()?.SavePng(System.IO.Path.Combine(texDir, name + ".png"));
        }
        if (ClassicInventory.Current is not { } items) { GD.PrintErr("[classic-inv] not in classic mode (--ui classic)"); GetTree().Quit(); return; }
        if (_args.ContainsKey("classic-inv-scene")) { await ClassicInventoryScene(dir, items); return; }
        var inv = items.Inventory;
        Away();
        _input.ToggleInventory?.Invoke();
        await Wait(2.0);
        var data = items.Data;
        for (int tries = 0; tries < 20 && data.Carried.Count == 0 && _args.ContainsKey("classic-inv-tips"); tries++) await Wait(0.5);
        GD.Print($"[classic-inv] weight {data.Weight} normal {data.NormalLoad} heavy {data.HeavyLoad} gold {data.Gold} items {data.Carried.Count}");
        foreach (var i in data.Carried) GD.Print($"[classic-inv]   {i.Name} at {i.X},{i.Y} {i.W}x{i.H} icon {string.Join('/', i.Icon)}");
        await Shot(dir, "inv_01_inventory");
        if (_args.ContainsKey("classic-inv-tips"))
        {
            // Every item on the first page in turn, straight from one to the next: the hover text each shows.
            var g = inv.Grid;
            int n = 0;
            var at = new Vector2(900, 600);
            Vector2 Centre(ItemEntry item) => g.GetGlobalRect().Position + new Vector2((item.X + item.W / 2f) * g.Cell.X, (g.Rows - item.Y % g.Rows - item.H + item.H / 2f) * g.Cell.Y);
            var targets = g.Items.Where(i => i.Y < g.Rows).OrderBy(i => i.Y).ThenBy(i => i.X).Select(i => (i.Name, Centre(i)))
                .Concat(inv.Slots.Values.Where(s => s.Item != null).Select(s => (s.Item!.Name, s.GetGlobalRect().GetCenter()))).ToList();
            async Task Hover(string name, Vector2 to)
            {
                await MovePushed(at, to, false, 4);
                at = to;
                await Wait(0.05);
                string now = string.Join(" | ", ClassicHud.Current?.HoverShown ?? ["(none)"]);
                await Wait(0.6);
                GD.Print($"[classic-inv] tip {n} '{name}': at once '{now}', later '{string.Join(" | ", ClassicHud.Current?.HoverShown ?? ["(none)"])}' {ClassicHud.Current?.HoverPlacement}");
                await Shot(dir, $"tip_{n++:00}");
            }
            foreach (var (name, to) in targets) await Hover(name, to);
            // Carried and put down on a free cell, then hovered again where it landed.
            if (g.Items.FirstOrDefault(i => i.Y < g.Rows && i.W == 1 && i.H == 1) is { } small)
            {
                var from = Centre(small);
                var free = g.GetGlobalRect().Position + new Vector2(9.5f * g.Cell.X, 2.5f * g.Cell.Y);
                await MovePushed(at, from, false, 4);
                Button(from, MouseButton.Left, true);
                await Wait(0.05);
                Button(from, MouseButton.Left, false);
                await MovePushed(from, free, false, 6);
                Button(free, MouseButton.Left, true);
                await Wait(0.05);
                Button(free, MouseButton.Left, false);
                at = free;
                await Wait(1.0);
                await Hover(small.Name + " (moved)", free + new Vector2(1, 0));
            }
            GetTree().Quit();
            return;
        }

        var grid = inv.Grid;
        var first = grid.Items.OrderBy(i => i.Y).ThenBy(i => i.X).FirstOrDefault();
        Vector2 CellCentre(ClassicItemGrid g, int x, int topRow) => g.GetGlobalRect().Position + new Vector2((x + 0.5f) * g.Cell.X, (topRow + 0.5f) * g.Cell.Y);
        Vector2 ItemCentre(ClassicItemGrid g, ItemEntry i) => CellCentre(g, i.X, g.Rows - i.Y % g.Rows - i.H);
        if (first != null)
        {
            // Hover: the cells light up and the name shows.
            await MovePushed(new Vector2(640, 400), ItemCentre(grid, first), false);
            await Wait(1.2);
            await Shot(dir, "inv_02_tooltip");

            // Press and drag to the top-right cell of the page (free in a fresh character), then let go.
            var from = ItemCentre(grid, first);
            var to = CellCentre(grid, 9, 0);
            Button(from, MouseButton.Left, true);
            await MovePushed(from, to, true);
            await Wait(0.3);
            await Shot(dir, "inv_03_drag");
            Button(to, MouseButton.Left, false);
            await Wait(1.0);
            await Shot(dir, "inv_04_dropped");

            // Click to pick up (the original's way), carry it, click to put it down at the top-left.
            var moved = grid.Items.FirstOrDefault(i => i.Id == first.Id) ?? first;
            var at = ItemCentre(grid, moved);
            Button(at, MouseButton.Left, true);
            Button(at, MouseButton.Left, false);
            await Wait(0.2);
            var dest = CellCentre(grid, 0, 0);
            await MovePushed(at, dest, false);
            await Wait(0.3);
            await Shot(dir, "inv_05_carry");
            Button(dest, MouseButton.Left, true);
            Button(dest, MouseButton.Left, false);
            await Wait(1.0);
            await Shot(dir, "inv_06_put_down");
        }

        // Page tabs.
        grid.Page = 1;
        await Wait(0.3);
        await Shot(dir, "inv_07_page2");
        grid.Page = 0;
        await Wait(0.2);

        // Item menu and examine.
        var weapon = grid.Items.FirstOrDefault(i => i.Info?.Category == "weapon") ?? grid.Items.FirstOrDefault();
        if (weapon != null)
        {
            var p = ItemCentre(grid, weapon);
            Button(p, MouseButton.Right, true);
            Button(p, MouseButton.Right, false);
            await Wait(0.4);
            await Shot(dir, "inv_08_menu");
            items.Data.Radial?.Choose("Examine");
            await Wait(1.2);
            await Shot(dir, "inv_09_examine");
            items.Examine.Close();
        }

        // Stack split.
        var stack = grid.Items.FirstOrDefault(i => i.Stack > 1);
        if (stack != null)
        {
            items.Split.Show(stack);
            await Wait(0.4);
            await Shot(dir, "inv_10_split");
            items.Split.Close();
        }

        // Equip by dragging a weapon onto the right hand.
        var blade = grid.Items.FirstOrDefault(i => ClassicItems.Fits(i, 4));
        if (blade != null && inv.Slots.TryGetValue(4, out var hand))
        {
            var from = ItemCentre(grid, blade);
            var to = hand.GetGlobalRect().GetCenter();
            Button(from, MouseButton.Left, true);
            await MovePushed(from, to, true);
            await Wait(0.3);
            await Shot(dir, "inv_11_equip_drag");
            Button(to, MouseButton.Left, false);
            await Wait(1.2);
            await Shot(dir, "inv_12_equipped");
        }
        // A potion onto the quickbar (the action bar's "item" drag data).
        var potion = grid.Items.FirstOrDefault(i => i.Info?.Category == "consumable");
        // The classic quickbar when the HUD part is there, else the modern bar.
        var slots = Descendants<ClassicQuickSlot>(_hudLayer()).Select(q => ((Control)q, q.Index))
            .Concat(Descendants<UI.ActionBar.ActionSlotView>(ActionBar).Select(v => ((Control)v, v.Index)));
        var (barControl, barIndex) = slots.FirstOrDefault(v => ActionBar.Get(ActionBar.Page, v.Index) == null && v.Item1.IsVisibleInTree());
        if (potion != null && barControl != null)
        {
            var barSlot = new { Index = barIndex };
            var from = ItemCentre(grid, potion);
            var to = barControl.GetGlobalRect().GetCenter();
            Button(from, MouseButton.Left, true);
            await MovePushed(from, to, true, 20);
            Button(to, MouseButton.Left, false);
            await Wait(0.5);
            var slot = ActionBar.Get(ActionBar.Page, barSlot.Index);
            GD.Print($"[classic-inv] quickbar slot {barSlot.Index}: {slot?.Kind} {slot?.Item} (potion {potion.Id})");
            await Shot(dir, "inv_12b_quickbar");
            // The quickbar is saved per character: leave it as it was.
            ActionBar.Set(ActionBar.Page, barSlot.Index, null);
        }
        Away();
        inv.Close();
        await Wait(0.3);

        // Right-click on an item opens its radial menu whatever else is open (the original has no drop-down lists).
        // expectE: the label the original shows at E for that ring (measured, storeradial), checked and logged.
        async Task RightClickRing(ClassicItemGrid g, IEnumerable<ItemEntry> candidates, string shot, Func<ItemEntry, string>? expectE = null)
        {
            // The first item drawn on the grid's current page.
            var item = candidates.FirstOrDefault(i => g.GetGlobalRect().HasPoint(g.ScreenCentreOf(i)));
            if (item == null || items.Data.Radial is not { } radial) { GD.Print($"[classic-inv] ring {shot}: no item"); return; }
            var p = g.ScreenCentreOf(item);
            Button(p, MouseButton.Right, true);
            Button(p, MouseButton.Right, false);
            await Wait(0.5);
            bool menu = Descendants<ClassicMenu>(_hudLayer()).Any();
            GD.Print($"[classic-inv] ring {shot}: {item.Name} open={radial.IsOpen} dropdown={menu} labels=[{string.Join(", ", radial.Shown.Where(e => !e.Hidden).Select(e => e.Label))}]");
            if (expectE != null)
            {
                string want = expectE(item), got = radial.Labels.ElementAtOrDefault(2) ?? "";
                GD.Print($"[classic-inv] ring {shot} E: {(got == want ? "ok" : "MISMATCH")} (want '{want}', got '{got}')");
            }
            await Shot(dir, shot);
            radial.Close();
            await Wait(0.3);
        }

        // Store: wares beside the inventory; a confirmation.
        _session.Send(new ChatSendMessage { Text = "/store" + (_args.TryGetValue("store", out var tag) ? " " + tag : "") }.Write());
        await Wait(2.5);
        await Shot(dir, "inv_13_store");
        if (items.Store.IsOpen)
        {
            var sg = items.Store.Grid;
            var ware = sg.Items.FirstOrDefault(i => i.Y / sg.Rows == sg.Page);
            if (ware != null)
            {
                await MovePushed(new Vector2(640, 400), ItemCentre(items.Store.Grid, ware), false);
                await Wait(1.2);
                GD.Print($"[classic-inv] store hover over {GetViewport().GuiGetHoveredControl()?.GetPath()}: '{string.Join(" | ", ClassicHud.Current?.HoverShown ?? ["(none)"])}' ware '{ware.Name}'");
                await Shot(dir, "inv_14_store_hover");
                await RightClickRing(items.Store.Grid, items.Store.Grid.Items, "inv_14b_store_ware_ring", _ => "Buy");
                items.Confirm.Ask($"Buy {ware.Name} for {ware.Price:N0} gp?", () => { });
                await Wait(0.4);
                await Shot(dir, "inv_15_store_confirm");
                items.Confirm.Close();
                await Wait(0.6);
            }
            if (!inv.IsOpen) { _input.ToggleInventory?.Invoke(); await Wait(1.0); }
            await RightClickRing(inv.Grid, inv.Grid.Items, "inv_15c_store_pack_ring", i => i.IsBag ? "Open" : "Sell");
            Away();
            items.Store.Close();
            await Wait(0.5);
        }

        // A container in the top half of the inventory.
        _session.Send(new ChatSendMessage { Text = "/loot" }.Write());
        await Wait(3.0);
        await Shot(dir, "inv_16_container");
        if (inv.ContainerOpen)
        {
            await RightClickRing(inv.Grid, inv.Grid.Items, "inv_17_container_pack_ring");
            await RightClickRing(inv.ContainerGrid, inv.ContainerGrid.Items, "inv_18_container_item_ring");
            await RightClickRing(inv.Grid, inv.Grid.Items.Where(i => i.IsBag), "inv_19_bag_ring");
        }
        inv.Close();
        await Wait(0.3);
        GetTree().Quit();
    }

    /// <summary>
    /// --classic-inv-scene: follows parity/visual/scenes/classicui_inv (its script opens a store, then the player opens
    /// a chest) and takes the same shots at about the same moments: a_store, b_container, c_page2.
    /// </summary>
    async Task ClassicInventoryScene(string dir, ClassicItems items)
    {
        Away();
        _session.NotifyReceived += m => { if (m.Kind.StartsWith("container") || m.Kind.StartsWith("store")) GD.Print($"[classic-inv] notify {m.Kind}"); };
        await Wait(6.0); // the scene opens the store at 4 s (2 s already waited)
        await Shot(dir, "a_store");
        items.Store.Close();
        // The chest is opened at 11 s (the walk to it may take longer here).
        for (int i = 0; i < 100 && !items.Inventory.ContainerOpen; i++) await Wait(0.2);
        await Wait(1.0);
        await Shot(dir, "b_container");
        items.Inventory.Close();
        await Wait(2.0);
        _input.ToggleInventory?.Invoke();
        await Wait(1.0);
        _input.CycleInventory?.Invoke(1);
        await Wait(1.0);
        await Shot(dir, "c_page2");
        GetTree().Quit();
    }

    CanvasLayer _hudLayer() => GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<UI.Hud>().First();

    void Away() => Mouse(new InputEventMouseMotion { Position = new Vector2(900, 600), GlobalPosition = new Vector2(900, 600) });

    void Button(Vector2 p, MouseButton b, bool pressed) =>
        Mouse(new InputEventMouseButton { ButtonIndex = b, Pressed = pressed, Position = p, GlobalPosition = p, ButtonMask = pressed ? (b == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right) : 0 });

    /// <summary>Mouse motion events pushed into the viewport only (the real cursor stays where it is).</summary>
    async Task MovePushed(Vector2 from, Vector2 to, bool held, int steps = 10)
    {
        for (int i = 1; i <= steps; i++)
        {
            var p = from.Lerp(to, i / (float)steps);
            Mouse(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = (to - from) / steps, ButtonMask = held ? MouseButtonMask.Left : 0 });
            await Wait(0.03);
        }
    }
}
