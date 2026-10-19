using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // --capture-items <dir> --inventory-placement-only, on the sandbox inventorydrop scene.
    async Task CaptureInventoryPlacement(string dir)
    {
        var checks = new List<object>();
        var clickTrace = new List<object>();
        bool passed = false, oldView = InventorySettings.ModernView;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            GD.Print($"[inventory-placement] {name}: {ok}");
            if (!ok) throw new InvalidOperationException(name);
        }
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            foreach (var item in pc.Inventory.ToList()) world.Destroy(item);
            var chest = new Placeable { HasInventory = true, Useable = true, CurrentHitPoints = 50, HitPoints = 50 };
            world.AddToArea(chest, pc.Area!, pc.Position + System.Numerics.Vector3.UnitX, 0);
            var shield = world.Inventory.CreateOn("nw_ashsw001", pc, 1, "placement_shield")!;
            var club = world.Inventory.CreateOn("nw_wblcl001", pc, 1, "placement_club")!;
            var classic = ClassicInventory.Current;
            InventorySettings.ModernView = false;
            if (classic != null) classic.Inventory.Open(); else Inventory.Toggle();
            _session.Request("inventory"); await Wait(.6);
            await Shot(dir, "a_paperdoll");
            world.System<ContainerService>().Show(pc, chest);
            _session.Request("inventory");
            await Wait(1);
            var pack = classic == null ? Descendants<ItemGrid>(Inventory).Single(g => g.DragSource == "pack") : null;
            var container = classic == null ? Descendants<ItemGrid>(this).Single(g => g.DragSource == "container" && g.IsVisibleInTree()) : null;
            Vector2 PackPoint(Item item, int grabX = 0, int grabY = 0)
            {
                var entry = Inventory.Carried.Single(i => i.Id == item.Id);
                return classic != null
                    ? classic.Inventory.Grid.GetGlobalRect().Position + new Vector2((entry.X + grabX + .5f) * classic.Inventory.Grid.Cell.X,
                        (classic.Inventory.Grid.Rows - entry.Y % classic.Inventory.Grid.Rows - entry.H + grabY + .5f) * classic.Inventory.Grid.Cell.Y)
                    : pack!.GetGlobalRect().Position + new Vector2((entry.X + grabX + .5f) * pack.Cell, (entry.Y + grabY + .5f) * pack.Cell);
            }
            Vector2 ContainerPoint(int x, int top) => classic != null
                ? classic.Inventory.ContainerGrid.GetGlobalRect().Position + new Vector2((x + .25f) * classic.Inventory.ContainerGrid.Cell.X, (top + .25f) * classic.Inventory.ContainerGrid.Cell.Y)
                : container!.GetGlobalRect().Position + new Vector2((x + .25f) * container.Cell, (top + .25f) * container.Cell);
            async Task Begin(Item item, Vector2 to, int grabX = 0, int grabY = 0)
            {
                var from = PackPoint(item, grabX, grabY);
                Button(from, MouseButton.Left, true);
                await MovePushed(from, to, true);
                await Wait(.12);
                Check("actual drag active", GetViewport().GuiIsDragging());
            }
            async Task Release(Vector2 at)
            {
                Button(at, MouseButton.Left, false);
                await Wait(.55);
            }
            if (classic != null)
            {
                // Exercise the viewport's drag routing without equipping or moving server items.
                // Timing is deliberately short; only the explicit double-click event may activate.
                var grid = classic.Inventory.Grid;
                var onActivate = grid.Activated;
                var onDrop = grid.Dropped;
                var onClick = grid.Clicked;
                int activated = 0, dropped = 0;
                uint activatedItem = 0;
                grid.Activated = item => { activated++; activatedItem = item.Id; };
                grid.Dropped = (_, _, _) => dropped++;
                grid.Clicked = _ => false;
                void Press(Vector2 point, bool doubleClick) => Mouse(new InputEventMouseButton
                {
                    Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
                    Pressed = true, ButtonMask = MouseButtonMask.Left, DoubleClick = doubleClick,
                });
                try
                {
                    var from = PackPoint(shield);
                    var empty = grid.GetGlobalRect().Position + new Vector2((grid.Columns - .5f) * grid.Cell.X, .5f * grid.Cell.Y);
                    Check("double-click fixture destination is empty", !grid.Items.Any(i =>
                        i.X <= grid.Columns - 1 && i.X + i.W > grid.Columns - 1 && i.Y % grid.Rows <= grid.Rows - 1 && i.Y % grid.Rows + i.H > grid.Rows - 1));
                    foreach (string variant in new[] { "unflagged_return", "flagged_same_item", "flagged_empty_cell", "held_return" })
                    {
                        activated = dropped = 0; activatedItem = 0;
                        Press(from, false);
                        if (variant == "held_return")
                        {
                            var away = from + new Vector2(36, 0);
                            await MovePushed(from, away, true, 4);
                            Check(variant + " begins a held drag", GetViewport().GuiIsDragging());
                            await MovePushed(away, from, true, 4);
                            Button(from, MouseButton.Left, false);
                        }
                        else
                        {
                            Button(from, MouseButton.Left, false);
                            await Wait(.03);
                            Check(variant + " begins click carry", GetViewport().GuiIsDragging());
                            var destination = variant == "flagged_empty_cell" ? empty : from;
                            await MovePushed(from, destination, false, 2);
                            Press(destination, variant != "unflagged_return");
                            Button(destination, MouseButton.Left, false);
                        }
                        await Wait(.08);
                        bool genuine = variant == "flagged_same_item";
                        clickTrace.Add(new { variant, activated, activatedItem, dropped, dragging = GetViewport().GuiIsDragging() });
                        Check(variant + " routes exactly once", activated == (genuine ? 1 : 0) && dropped == (genuine ? 0 : 1));
                        Check(variant + " ends carry without server equip", !GetViewport().GuiIsDragging()
                            && world.Inventory.Holder(shield) == pc && (!genuine || activatedItem == shield.Id));
                    }
                    await Shot(dir, "a_doubleclick_routing");
                }
                finally
                {
                    grid.Activated = onActivate;
                    grid.Dropped = onDrop;
                    grid.Clicked = onClick;
                }
            }
            Check("shield footprint is two by two", world.Inventory.Footprint(shield) == (2, 2));
            await Shot(dir, "a_before");
            var edge = ContainerPoint(0, 0);
            await Begin(shield, edge, 1, 1);
            var edgeTarget = classic != null ? classic.Inventory.ContainerGrid.CaptureDropTarget : container!.CaptureDropTarget;
            Check("edge preview retains full clamped footprint", edgeTarget is { X: 0, Item2: 0, W: 2, H: 2, Ok: true });
            await Shot(dir, "b_edge_preview");
            await Release(edge);
            Check("edge drop reaches clamped cell", world.Inventory.Holder(shield) == chest && shield.InvX == 0 && shield.InvY == (classic != null ? 3 : 0));
            await Shot(dir, "c_edge_placed");
            await Begin(club, edge);
            var overlapTarget = classic != null ? classic.Inventory.ContainerGrid.CaptureDropTarget : container!.CaptureDropTarget;
            Check("occupied preview remains requested accepted footprint", overlapTarget is { X: 0, Item2: 0, W: 1, H: 3, Ok: true });
            await Shot(dir, "d_occupied_preview");
            await Release(edge);
            Check("occupied drop uses first free cells", world.Inventory.Holder(club) == chest && (club.InvX, club.InvY) == (classic != null ? (0, 0) : (2, 0)));
            Check("occupied drop leaves shield in place", (shield.InvX, shield.InvY) == (classic != null ? (0, 3) : (0, 0)));
            await Shot(dir, "e_relocated");

            if (classic != null)
            {
                // Held dragging has no target mouse-down. Click-to-carry adds a complete
                // click on the occupied target, whose release must not loot the blocker.
                foreach (string variant in new[] { "normal", "rapid", "duplicate_release" })
                {
                    world.Inventory.MoveTo(club, pc, merge: false);
                    world.Inventory.MoveToCell(shield, chest, 0, 3);
                    world.System<ContainerService>().Refresh(pc);
                    _session.Request("inventory"); await Wait(.35);
                    int clicked = 0, dropped = 0;
                    var target = classic.Inventory.ContainerGrid;
                    var onClick = target.Clicked;
                    var onDrop = target.Dropped;
                    target.Clicked = item => { clicked++; return onClick?.Invoke(item) == true; };
                    target.Dropped = (drag, x, y) => { dropped++; onDrop?.Invoke(drag, x, y); };
                    void Trace(string phase) => clickTrace.Add(new
                    {
                        variant, phase, clicked, dropped, dragging = GetViewport().GuiIsDragging(),
                        shieldHolder = world.Inventory.Holder(shield)?.Id,
                        clubHolder = world.Inventory.Holder(club)?.Id,
                        shieldCell = new[] { shield.InvX, shield.InvY }, clubCell = new[] { club.InvX, club.InvY },
                    });
                    var from = PackPoint(club);
                    Button(from, MouseButton.Left, true);
                    if (variant == "normal") await Wait(.04);
                    Button(from, MouseButton.Left, false);
                    await Wait(.06);
                    Check($"{variant} click picks up club without transfer", GetViewport().GuiIsDragging() && world.Inventory.Holder(club) == pc);
                    await MovePushed(from, edge, false, variant == "normal" ? 10 : 2);
                    Trace("before_target_press");
                    Button(edge, MouseButton.Left, true);
                    Trace("after_target_press");
                    if (variant == "normal") await Wait(.1);
                    Button(edge, MouseButton.Left, false);
                    Trace("after_target_release");
                    if (variant == "duplicate_release")
                    {
                        await Wait(.02);
                        Button(edge, MouseButton.Left, false);
                        Trace("after_duplicate_release");
                    }
                    await Wait(.4);
                    Trace("settled");
                    await Shot(dir, "e_click_" + variant);
                    Check($"{variant} click drop moves club into chest", world.Inventory.Holder(club) == chest && (club.InvX, club.InvY) == (0, 0));
                    Check($"{variant} target release does not loot shield", world.Inventory.Holder(shield) == chest && (shield.InvX, shield.InvY) == (0, 3));
                    Check($"{variant} occupied drop dispatched once without click", dropped == 1 && clicked == 0 && !GetViewport().GuiIsDragging());
                }
                // A subsequent independent click must still take the item normally.
                Button(edge, MouseButton.Left, true); await Wait(.04);
                Button(edge, MouseButton.Left, false); await Wait(.4);
                Check("ordinary chest click still takes shield", world.Inventory.Holder(shield) == pc);
            }

            foreach (var item in chest.Inventory.ToList()) world.Destroy(item);
            for (int i = 0; i < 35; i++) world.Inventory.CreateOn("nw_wswdg001", chest, 1, "chest_filler" + i);
            var extra = world.Inventory.CreateOn("nw_wswdg001", pc, 1, "placement_extra")!;
            world.System<ContainerService>().Refresh(pc);
            _session.Request("inventory"); await Wait(.7);
            await Begin(extra, edge);
            await Release(edge);
            Check("full chest opens another page", world.Inventory.Holder(extra) == chest && (extra.InvX, extra.InvY) == (0, 5) && chest.Inventory.Count == 36);
            if (classic != null)
            {
                Check("classic container exposes second page", classic.Inventory.ContainerGrid.PageCount == 2);
                Click(((GuiPanel)classic.Inventory.ContainerGrid.GetParent()).Get<GuiButton>("RightButton")!, MouseButton.Left);
            }
            else
                for (int i = 0; i < 8; i++) { Button(edge, MouseButton.WheelDown, true); Button(edge, MouseButton.WheelDown, false); }
            await Wait(.3);
            await Shot(dir, "f_second_page");
            world.System<ContainerService>().Close(pc);
            await Wait(.3);
            var bag = world.Inventory.CreateOn("nw_it_contain001", pc, 1, "placement_bag")!;
            for (int i = 0; i < 35; i++) world.Inventory.CreateOn("nw_wswdg001", bag, 1, "bag_filler" + i);
            var refused = world.Inventory.CreateOn("nw_wblcl001", pc, 1, "placement_refused")!;
            _session.Request("inventory"); await Wait(.6);
            if (classic != null) { classic.Inventory.Open(); classic.Inventory.OpenBag(Inventory.Carried.Single(i => i.Id == bag.Id)); }
            else { if (!Inventory.Visible) Inventory.Toggle(); Inventory.OpenBag(Inventory.Carried.Single(i => i.Id == bag.Id)); }
            await Wait(.4);
            var bagGrid = classic == null ? Descendants<ItemGrid>(this).Single(g => g.DragSource == "bag" && g.IsVisibleInTree()) : null;
            var bagPoint = classic != null ? classic.Inventory.ContainerGrid.GetGlobalRect().Position + classic.Inventory.ContainerGrid.Cell * .5f
                : bagGrid!.GetGlobalRect().Position + Vector2.One * bagGrid.Cell * .5f;
            await Begin(refused, bagPoint);
            var bagTarget = classic != null ? classic.Inventory.ContainerGrid.CaptureDropTarget : bagGrid!.CaptureDropTarget;
            Check("full bag preview rejects incoming item", bagTarget is { Ok: false });
            await Shot(dir, "g_full_bag_preview");
            await Release(bagPoint);
            Check("full bag drop preserves source ownership", world.Inventory.Holder(refused) == pc && bag.Inventory.Count == 35);
            Check("bag remains single page", classic != null ? classic.Inventory.ContainerGrid.PageCount == 1 : bagGrid!.Spec.Pages == 1);
            await Shot(dir, "h_bag_unchanged");
            if (classic != null)
            {
                pc.Gold = 198; _session.Request("inventory"); await Wait(.2);
                Click(classic.Inventory.Panel.Get<GuiButton>("GoldButton")!, MouseButton.Left);
                await Wait(.1);
                Check("gold amount starts empty", classic.Split.Visible && classic.Split.Count == 0);
                Descendants<ClassicNumberLabel>(classic.Split).Single().ResetCaret();
                await Shot(dir, "i_gold_caret");
            }
            passed = true;
            GD.Print("VISUAL|done");
        }
        catch (Exception error)
        {
            GD.PrintErr("[inventory-placement] " + error);
            await Shot(dir, "failed");
        }
        finally
        {
            InventorySettings.ModernView = oldView;
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "inventory-placement.json"),
                System.Text.Json.JsonSerializer.Serialize(new { passed, checks, clickTrace }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
