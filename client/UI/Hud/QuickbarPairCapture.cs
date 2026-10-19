using System.Text.Json;
using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Game.Objects;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    async Task CaptureQuickbarPair(string dir)
    {
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            if (!ok) throw new InvalidOperationException(name);
        }
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            var main = pc.Equipment[EquipSlot.RightHand];
            var off = pc.Equipment[EquipSlot.LeftHand];
            Check("installed cleric pair", main.ResRef == "nw_wblmml007" && off.ResRef == "nw_ashmsw010");
            ActionBar.SetBasePage(0);
            ActionBar.Set(0, 4, new BarSlot { Kind = "item", Paired = true, Item = main.Id, SecondaryItem = off.Id });
            ActionBar.Book.Refresh();
            await Wait(1);
            foreach (var item in pc.Inventory.ToList()) world.Destroy(item);
            var createdMain = world.Inventory.CreateOn("nw_wblcl001", pc, 1, "capture_bind_main")!;
            var createdOff = world.Inventory.CreateOn("nw_ashsw001", pc, 1, "capture_bind_off")!;
            var classic = ClassicInventory.Current;
            bool oldView = InventorySettings.ModernView;
            InventorySettings.ModernView = false;
            if (classic != null) classic.Inventory.Open(); else Inventory.Toggle();
            _session.Request("inventory"); await Wait(.6);
            ActionBar.Set(0, 11, null);
            var grid = classic == null ? Descendants<ItemGrid>(Inventory).Single(g => g.DragSource == "pack") : null;
            var destination = classic != null
                ? Descendants<ClassicQuickSlot>(this).Single(s => s.Index == 11 && s.IsVisibleInTree()).GetGlobalRect().GetCenter()
                : ActionBar.SlotViews[11].GetGlobalRect().GetCenter();
            async Task Bind(Item item)
            {
                var entry = Inventory.Carried.Single(i => i.Id == item.Id);
                var from = classic != null
                    ? classic.Inventory.Grid.GetGlobalRect().Position + new Vector2((entry.X + .5f) * classic.Inventory.Grid.Cell.X,
                        (classic.Inventory.Grid.Rows - entry.Y % classic.Inventory.Grid.Rows - entry.H + .5f) * classic.Inventory.Grid.Cell.Y)
                    : grid!.GetGlobalRect().Position + new Vector2((entry.X + .5f) * grid.Cell, (entry.Y + .5f) * grid.Cell);
                await MovePushed(destination, from, false, 3);
                await Wait(.1);
                Button(from, MouseButton.Left, true);
                await MovePushed(from, destination, true);
                Check("inventory to quickbar drag", GetViewport().GuiIsDragging());
                Button(destination, MouseButton.Left, false);
                await Wait(.7);
                GD.Print($"[quickbar-pair] bound {item.Id}: {JsonSerializer.Serialize(ActionBar.Get(0, 11))}; main mask {ActionBar.Book.EquipableSlots(createdMain.Id)}, off mask {ActionBar.Book.EquipableSlots(createdOff.Id)}");
            }
            await Bind(createdMain);
            Check("main shortcut created", ActionBar.Get(0, 11) is { Paired: false } first && first.Item == createdMain.Id);
            await Bind(createdOff);
            Check("offhand drop creates pair", ActionBar.Get(0, 11) is { Paired: true } bound
                && bound.Item == createdMain.Id && bound.SecondaryItem == createdOff.Id);
            await Shot(dir, "a_created_pair");
            if (classic != null) classic.Inventory.Close(); else Inventory.Toggle();
            InventorySettings.ModernView = oldView;
            await Wait(.3);
            void KeyTap(Key key)
            {
                GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
                GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            }
            KeyTap(Key.F12); await Wait(1);
            Check("created pair activates", pc.Equipment.GetValueOrDefault(EquipSlot.RightHand) == createdMain
                && pc.Equipment.GetValueOrDefault(EquipSlot.LeftHand) == createdOff);
            await Shot(dir, "a_created_pair_equipped");
            KeyTap(Key.F12); await Wait(1);
            Check("created pair toggles off", !pc.Equipment.ContainsKey(EquipSlot.RightHand) && !pc.Equipment.ContainsKey(EquipSlot.LeftHand));
            world.Inventory.Equip(pc, main, 4); world.Inventory.Equip(pc, off, 5); await Wait(.3);
            bool Both() => pc.Equipment.GetValueOrDefault(EquipSlot.RightHand) == main && pc.Equipment.GetValueOrDefault(EquipSlot.LeftHand) == off;
            bool Empty() => !pc.Equipment.ContainsKey(EquipSlot.RightHand) && !pc.Equipment.ContainsKey(EquipSlot.LeftHand);
            async Task Activate(string phase, bool on)
            {
                KeyTap(Key.F5);
                await Wait(.15);
                Check(phase + " equipment", on ? Both() : Empty());
                var view = (CreatureView)_session.Controlled!;
                Check(phase + " authored draw action", view.Animator?.CaptureAction()?.Clip == "drwright");
                for (int frame = 0; frame < 30 && view.Animator?.CaptureAction() is { Position: < .36 }; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var drawn = view.CurrentAppearance!.Equipment.Where(e => e.Slot is 16 or 32).ToArray();
                Check(phase + " draw event updates held models", on
                    ? drawn.Any(e => e.Slot == 16 && e.BaseItem == main.BaseItem) && drawn.Any(e => e.Slot == 32 && e.BaseItem == off.BaseItem)
                    : drawn.Length == 0);
                await Shot(dir, phase + "_transition");
                await Wait(.7);
                var look = ActionBar.User.Look(ActionBar.Get(0, 4)!);
                Check(phase + " pair art and toggle", look.SecondaryIcons.Length > 0 && look.Icons.Length > 0 && look.Toggled == on && look.Usable);
                var held = view.CurrentAppearance!.Equipment.Where(e => e.Slot is 16 or 32).ToArray();
                Check(phase + " settled held models", on
                    ? held.Any(e => e.Slot == 16 && e.BaseItem == main.BaseItem) && held.Any(e => e.Slot == 32 && e.BaseItem == off.BaseItem)
                    : held.Length == 0);
                await Shot(dir, phase + "_settled");
            }
            // A draw requests item-info for non-power equipment; wait for both icon responses.
            ActionBar.User.Look(ActionBar.Get(0, 4)!); await Wait(.5);
            await Shot(dir, "a_pair");
            await Activate("b_remove", false);
            await Activate("c_restore", true);
            var otherMain = world.Inventory.CreateOn("nw_wblcl001", pc, 1, "capture_other_main")!;
            var otherOff = world.Inventory.CreateOn("nw_ashsw001", pc, 1, "capture_other_off")!;
            Check("other equipment setup", world.Inventory.Equip(pc, otherMain, 4) && world.Inventory.Equip(pc, otherOff, 5));
            await Wait(.3);
            await Activate("d_replace", true);
            world.Inventory.Unequip(pc, off); await Wait(.3);
            await Activate("e_only_main", true);
            world.Inventory.Unequip(pc, main); await Wait(.3);
            await Activate("f_only_offhand", true);
            await Activate("g_remove_again", false);
            var container = new Placeable { HasInventory = true };
            world.Register(container);
            world.Inventory.MoveTo(off, container, merge: false);
            await Wait(.5);
            Check("ownership loss clears pair", ActionBar.Get(0, 4) == null);
            world.Inventory.MoveTo(off, pc, merge: false); await Wait(.3);
            Check("ownership return does not recreate pair", ActionBar.Get(0, 4) == null);
            await Shot(dir, "h_cleared");

            // Exercise the inventory entry points, not only the paired shortcut.
            InventorySettings.ModernView = false;
            if (classic != null) classic.Inventory.Open(); else Inventory.Toggle();
            _session.Request("inventory"); await Wait(.7);
            Vector2 PackPoint(Item item)
            {
                var entry = Inventory.Carried.Single(i => i.Id == item.Id);
                return classic != null
                    ? classic.Inventory.Grid.GetGlobalRect().Position + new Vector2((entry.X + .5f) * classic.Inventory.Grid.Cell.X,
                        (classic.Inventory.Grid.Rows - entry.Y % classic.Inventory.Grid.Rows - entry.H + .5f) * classic.Inventory.Grid.Cell.Y)
                    : grid!.GetGlobalRect().Position + new Vector2((entry.X + .5f) * grid.Cell, (entry.Y + .5f) * grid.Cell);
            }
            Vector2 HandPoint(int slot = 4) => classic != null ? classic.Inventory.Slots[slot].GetGlobalRect().GetCenter()
                : Descendants<ItemSlot>(Inventory).Single(s => s.EquipSlot == slot && s.IsVisibleInTree()).GetGlobalRect().GetCenter();
            async Task DoubleClick(Vector2 at)
            {
                await MovePushed(at, at, false, 3);
                GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left,
                    Pressed = true, DoubleClick = true, Position = at, GlobalPosition = at });
                Button(at, MouseButton.Left, false);
            }
            async Task ManualDraw(string phase, bool on, Item? item = null, int slot = 4)
            {
                item ??= createdMain;
                int bit = EquipSlot.Bit(slot);
                await Wait(.12);
                var view = (CreatureView)_session.Controlled!;
                Check(phase + " authoritative hand", on ? pc.Equipment.GetValueOrDefault(bit) == item
                    : !pc.Equipment.ContainsKey(bit));
                Check(phase + " shared draw animation", view.Animator?.CaptureAction()?.Clip == (slot == 4 ? "drwright" : "drwleft"));
                for (int frame = 0; frame < 30 && view.Animator?.CaptureAction() is { Position: < .36 }; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(phase + " held model at draw event", on
                    ? view.CurrentAppearance!.Equipment.Any(e => e.Slot == bit && e.BaseItem == item.BaseItem)
                    : !view.CurrentAppearance!.Equipment.Any(e => e.Slot == bit));
                await Shot(dir, phase);
                await Wait(.65);
                Check(phase + " draw completes", view.Animator?.CaptureAction() == null);
            }
            await DoubleClick(PackPoint(createdMain));
            await ManualDraw("i_inventory_double_equip", true);
            await DoubleClick(HandPoint());
            await ManualDraw("j_inventory_double_unequip", false);
            var dragFrom = PackPoint(createdMain);
            var dragTo = HandPoint();
            await MovePushed(dragTo, dragFrom, false, 3);
            Button(dragFrom, MouseButton.Left, true);
            await MovePushed(dragFrom, dragTo, true);
            Check("inventory paperdoll drag started", GetViewport().GuiIsDragging());
            Button(dragTo, MouseButton.Left, false);
            await ManualDraw("k_inventory_drag_equip", true);
            var cell = world.Inventory.FirstFit(pc, createdMain)!.Value;
            var back = classic != null
                ? classic.Inventory.Grid.GetGlobalRect().Position + new Vector2((cell.X + .5f) * classic.Inventory.Grid.Cell.X,
                    (classic.Inventory.Grid.Rows - cell.Y % classic.Inventory.Grid.Rows - Inventory.Equipped[4].H + .5f) * classic.Inventory.Grid.Cell.Y)
                : grid!.GetGlobalRect().Position + new Vector2((cell.X + .5f) * grid.Cell, (cell.Y + .5f) * grid.Cell);
            Button(HandPoint(), MouseButton.Left, true);
            await MovePushed(HandPoint(), back, true);
            Check("inventory backpack drag started", GetViewport().GuiIsDragging());
            Button(back, MouseButton.Left, false);
            await ManualDraw("l_inventory_drag_unequip", false);
            Check("unequip keeps selected backpack cell", (createdMain.InvX, createdMain.InvY) == cell);
            await DoubleClick(PackPoint(createdOff));
            await ManualDraw("m_inventory_left_equip", true, createdOff, 5);
            await DoubleClick(HandPoint(5));
            await ManualDraw("n_inventory_left_unequip", false, createdOff, 5);
            InventorySettings.ModernView = oldView;
            passed = true;
            GD.Print("VISUAL|done|1");
        }
        catch (Exception error) { GD.PushError("[quickbar-pair] " + error); await Shot(dir, "failed"); }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "pair-state.json"), JsonSerializer.Serialize(new { passed, checks }, new JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
