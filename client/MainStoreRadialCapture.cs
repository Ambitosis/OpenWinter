using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Objects;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --capture-items dir --store-radial-only [--ui classic]: the item rings with a store open, as the original's
    /// (visual scene `storeradial`): a backpack sword has Sell at E (no Equip) and nothing at W, a stack
    /// Sell E and Split W, a ware Examine N and Buy E only; choosing Sell and Buy on the ring trades (after the
    /// confirmation). Writes store-radial.json and shots a_sword_ring, b_sell_confirm, c_stack_ring, d_ware_ring.
    /// </summary>
    async Task CaptureStoreRadial(string dir)
    {
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            GD.Print($"[store-radial] {name}: {ok}");
            if (!ok) throw new InvalidOperationException(name);
        }
        void Pointer(Vector2 at, MouseButtonMask held = 0)
        {
            _interaction.PointerOverride = at;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = held });
        }
        void Right(Vector2 at, bool down) => Input.ParseInputEvent(new InputEventMouseButton
        { ButtonIndex = MouseButton.Right, Pressed = down, Position = at, GlobalPosition = at, ButtonMask = down ? MouseButtonMask.Right : 0 });
        void Enter()
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false });
        }
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            var store = new Store { Template = world.Resources.ReadGff("nw_storgenral001", OpenWinter.Aurora.Resources.ResType.Utm)! };
            world.Register(store);
            world.AddToArea(store, pc.Area!, pc.Position, 0);
            world.Inventory.CreateOn("nw_wswls001", pc, 1, "");
            world.Inventory.CreateOn("nw_it_mpotion001", pc, 3, "");
            pc.Gold = 5000;
            world.Inventory.OpenStore(store, pc, 0, 0);
            await Wait(1.5);
            _session.Request("inventory");
            await Wait(0.5);
            var radial = Inventory.Radial!;
            var classic = ClassicInventory.Current;
            Check("store open", classic != null ? classic.Store.Visible && classic.Inventory.IsOpen : Store.Visible);

            // Opens the ring on an item at a point with a held right button; the opening release at the centre keeps it.
            async Task Open(ItemEntry item, Vector2 at)
            {
                Pointer(at);
                await Wait(0.08);
                Right(at, true);
                await Wait(0.5);
                Check(item.Name + " ring open", radial.IsOpen);
            }
            async Task Close()
            {
                var c = radial.Centre;
                Right(c, false);
                await Wait(0.15);
                Right(c, true);
                await Wait(0.1);
                Right(c, false);
                await Wait(0.2);
            }
            async Task Choose(string label)
            {
                var at = radial.WedgePoint(label)!.Value;
                Pointer(at, MouseButtonMask.Right);
                await Wait(0.1);
                Right(at, false);
                await Wait(0.6);
            }
            Vector2 PackPoint(ItemEntry item) => classic != null
                ? classic.Inventory.Grid.ScreenCentreOf(item)
                : Store.FindChildren("*", "Control", true, false).OfType<ItemSlot>()
                    .First(s => s.IsVisibleInTree() && s.Item?.Id == item.Id).GetGlobalRect().GetCenter();
            string[] Labels() => radial.Shown.Select(e => e.Hidden ? "" : e.Label).ToArray();

            var sword = Inventory.Carried.First(i => i.BaseItem == 1);
            await Open(sword, PackPoint(sword));
            var l = Labels();
            GD.Print("[store-radial] sword ring: " + string.Join(" | ", l));
            Check("sword: Examine N, Cast Spell NE, Sell E, Drop S, nothing W", l[0] == "Examine" && l[1] == "Cast Spell"
                && l[2] == "Sell" && l[4] == "Drop" && l[6] == "" && !l.Contains("Equip"));
            await Shot(dir, "a_sword_ring");
            int gold = pc.Gold;
            await Choose("Sell");
            bool asked = classic == null || classic.Confirm.IsOpen;
            await Shot(dir, "b_sell_confirm");
            if (classic != null) Enter();
            await Wait(1.2);
            bool gone = world.Get<Item>(sword.Id) is not { } sold || world.Inventory.Owner(sold) != pc;
            Check("ring Sell sells the sword", asked && gone);
            Check("and pays for it", pc.Gold > gold);

            _session.Request("inventory");
            await Wait(0.5);
            var stack = Inventory.Carried.First(i => i.Stack > 1);
            await Open(stack, PackPoint(stack));
            l = Labels();
            GD.Print("[store-radial] stack ring: " + string.Join(" | ", l));
            Check("stack: Sell E, Split W, its power SE", l[2] == "Sell" && l[6] == "Split" && l[3].StartsWith("Use"));
            await Shot(dir, "c_stack_ring");
            await Close();
            Check("ring closed", !radial.IsOpen);

            // A ware: the classic store's grid, or the first ware shown in the modern store (grid tile or list row).
            ItemEntry ware;
            Vector2 wareAt;
            if (classic != null)
            {
                var grid = classic.Store.Grid;
                ware = grid.Items.First(i => grid.GetGlobalRect().HasPoint(grid.ScreenCentreOf(i)));
                wareAt = grid.ScreenCentreOf(ware);
            }
            else
            {
                var carried = Inventory.Carried.Select(i => i.Id).ToHashSet();
                var shown = Store.FindChildren("*", "Control", true, false).OfType<Control>()
                    .Select(c => (Control: c, Item: c is ItemSlot slot ? slot.Item : c is ItemRow row ? row.Item : null))
                    .First(x => x.Item != null && x.Control.IsVisibleInTree() && !carried.Contains(x.Item.Id) && x.Item.Price > 0);
                ware = shown.Item!;
                wareAt = shown.Control.GetGlobalRect().GetCenter();
            }
            {
                await Open(ware, wareAt);
                l = Labels();
                GD.Print("[store-radial] ware ring: " + string.Join(" | ", l));
                Check("ware: Examine N, Buy E, nothing else", l[0] == "Examine" && l[2] == "Buy" && l.Count(x => x.Length > 0) == 2);
                await Shot(dir, "d_ware_ring");
                int before = pc.Gold;
                await Choose("Buy");
                if (classic != null)
                {
                    Check("ring Buy asks first", classic.Confirm.IsOpen);
                    Enter();
                }
                await Wait(1.2);
                Check("ring Buy buys the ware", pc.Gold < before);
            }
            passed = true;
        }
        catch (Exception e) { GD.PrintErr(e); await Shot(dir, "failed"); }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "store-radial.json"),
                System.Text.Json.JsonSerializer.Serialize(checks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
