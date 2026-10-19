using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // --capture-drop-freeze <dir>: drops equipped armour and backpack items through the client's drop request, with
    // move orders, WASD, a second drop and an equip issued at points through the bend, and checks that the bend
    // always ends (the action layer is not left playing a looping getlow/getlowlp with no end time). Then the chained
    // put-downs of CaptureDropChain (client/MainDropChainCapture.cs).
    async Task CaptureDropFreeze(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            GD.Print($"[drop-freeze] {name}: {ok}");
            if (!ok) throw new InvalidOperationException(name);
        }
        // A timed fixture: clicks in other windows must not pause the world (in memory only, not saved).
        Settings.Current.Gameplay.PauseOnFocusLoss = false;
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            Views.CreatureView View() => (Views.CreatureView)_session.Controlled!;
            string State()
            {
                var a = View().Animator;
                return a == null ? "no animator" : $"playing={a.IsActionPlaying} action={a.CaptureAction()?.Clip ?? "-"}";
            }
            Item Armour()
            {
                if (pc.Equipment.GetValueOrDefault(EquipSlot.Chest) is { } worn) return worn;
                var made = world.Inventory.CreateOn("nw_cloth001", pc, 1, "CAPTURE_ARMOUR")!;
                world.Inventory.Equip(pc, made, EquipSlot.Index(EquipSlot.Chest));
                return made;
            }
            async Task Settled(string phase)
            {
                // The server's put-down (1.5 s) and the bend's recovery fade are over well within this.
                await Wait(.2);
                for (int i = 0; i < 40 && pc.Actions.Count > 0; i++) await Wait(.1);
                await Wait(1.2);
                GD.Print($"[drop-freeze] {phase}: {State()}");
                if (phase == "equipped armour") await Shot(dir, "a_after_worn_drop");
                Check(phase + ": action layer ends", View().Animator is { } a && !a.IsActionPlaying && a.CaptureAction() == null);
            }
            async Task Reset()
            {
                // Fixture only: pick up whatever was dropped, wear the armour again and stand still.
                foreach (var item in world.AllObjects.OfType<Item>().Where(i => i.Tag is "CAPTURE_ARMOUR" or "CAPTURE_PACK" && i.Area != null).ToList())
                    world.Inventory.MoveTo(item, pc);
                if (world.ByTag("CAPTURE_ARMOUR").OfType<Item>().FirstOrDefault() is { } worn && pc.Equipment.GetValueOrDefault(EquipSlot.Chest) != worn)
                    world.Inventory.Equip(pc, worn, EquipSlot.Index(EquipSlot.Chest));
                _session.Send(new MoveInputMessage { Direction = System.Numerics.Vector3.Zero }.Write());
                await Wait(.8);
            }
            void MoveOrder()
            {
                float f = pc.Facing;
                var p = pc.Position + new System.Numerics.Vector3(MathF.Cos(f), MathF.Sin(f), 0) * 3f;
                _session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = p, Run = true }.Write());
            }

            var armour = Armour();
            armour.Tag = "CAPTURE_ARMOUR";
            await Wait(1);

            // Returns whether the armour ended on the ground.
            async Task<bool> DropEquipped(string phase, double at = -1, Action? interrupt = null, Action? release = null)
            {
                var item = Armour();
                item.Tag = "CAPTURE_ARMOUR";
                await Wait(.6);
                _session.Request("drop", new { item = item.Id });
                if (interrupt != null)
                {
                    await Wait(at);
                    interrupt();
                    if (release != null) { await Wait(.4); release(); }
                }
                await Settled(phase);
                bool dropped = item.Area != null;
                await Reset();
                return dropped;
            }

            if (_args.ContainsKey("drop-chain-only"))
            {
                await CaptureDropChain(dir, Check, () => View().Animator, Reset);
                passed = true;
                return;
            }
            Check("equipped armour lands on the ground", await DropEquipped("equipped armour"));
            var pack = world.Inventory.CreateOn("nw_it_mpotion001", pc, 1, "CAPTURE_PACK")!;
            await Wait(.4);
            _session.Request("drop", new { item = pack.Id });
            await Settled("backpack item");
            Check("backpack item lands on the ground", pack.Area != null);
            await Reset();
            foreach (double at in new[] { .15, .7, 1.1, 1.45 })
            {
                // Moving cancels the put-down and the item stays (parity/visual scene dropmove: keys at 0.1, 0.6 and
                // 1.0 s); at 1.45 s the server may already have put it down, so that one only has to end the bend.
                bool moved = await DropEquipped($"equipped armour, move order at {at:0.00}s", at, MoveOrder);
                bool keyed = await DropEquipped($"equipped armour, WASD at {at:0.00}s", at,
                    () => _session.Send(new MoveInputMessage { Direction = new System.Numerics.Vector3(0, 1, 0) }.Write()),
                    () => _session.Send(new MoveInputMessage { Direction = System.Numerics.Vector3.Zero }.Write()));
                if (at < 1.4) Check($"moving at {at:0.00}s keeps the armour", !moved && !keyed);
            }
            pack = world.ByTag("CAPTURE_PACK").OfType<Item>().First();
            await DropEquipped("equipped armour, second drop at 0.7s", .7, () => _session.Request("drop", new { item = pack.Id }));
            await Settled("second drop finishes");
            await Reset();
            var ring = world.Inventory.CreateOn("nw_it_mring001", pc, 1, "CAPTURE_PACK")!;
            await DropEquipped("equipped armour, equip at 0.7s", .7, () => _session.Request("equip", new { item = ring.Id }));
            await CaptureDropChain(dir, Check, () => View().Animator, Reset);
            passed = true;
            GD.Print("VISUAL|done");
        }
        catch (Exception error)
        {
            GD.PrintErr("[drop-freeze] " + error);
            await Shot(dir, "failed");
        }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "drop-freeze.json"),
                System.Text.Json.JsonSerializer.Serialize(new { passed, checks }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
