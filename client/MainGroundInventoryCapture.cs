using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // --capture-items <dir> --inventory-ground-only, on inventorydrop's authored flat scene.
    async Task CaptureGroundInventory(string dir)
    {
        var checks = new List<object>();
        var notices = new List<(string Kind, uint Subject)>();
        bool passed = false, oldView = InventorySettings.ModernView;
        void Notify(NotifyMessage message) => notices.Add((message.Kind, message.Subject));
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            GD.Print($"[ground-inventory] {name}: {ok}");
            if (!ok) throw new InvalidOperationException(name);
        }
        async Task ClickPoint(Vector2 point)
        {
            _interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await Wait(.05);
            Button(point, MouseButton.Left, true); await Wait(.05);
            Button(point, MouseButton.Left, false); await Wait(.25);
        }
        async Task Until(Func<bool> predicate, string label)
        {
            for (int i = 0; i < 150 && !predicate(); i++) await Wait(.1);
            Check(label, predicate());
        }
        _session.NotifyReceived += Notify;
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            pc.Feats.Add(46);
            var classic = ClassicInventory.Current;
            // Keep the world subject in the modern panel's uncovered upper-left area.
            // Its full-width paperdoll otherwise covers items after the camera follows an approach.
            if (classic == null) { _camera.HOffset = 4.5f; _camera.VOffset = -1.5f; }
            if (classic != null) classic.Inventory.Open(); else Inventory.Toggle();
            InventorySettings.ModernView = false;
            await Wait(.8);
            var grid = classic == null ? Descendants<ItemGrid>(Inventory).First(g => g.DragSource == "pack") : null;
            Vector2 PackCell(int x, int y, int h = 1) => classic != null
                ? classic.Inventory.Grid.GetGlobalRect().Position + new Vector2((x + .5f) * classic.Inventory.Grid.Cell.X,
                    (classic.Inventory.Grid.Rows - y - h + .5f) * classic.Inventory.Grid.Cell.Y)
                : grid!.GetGlobalRect().Position + new Vector2((x + .5f) * grid.Cell, (y + .5f) * grid.Cell);
            Vector2 WorldPoint(Item item) => _camera.UnprojectPosition(_session.Objects[item.Id].GlobalPosition + Vector3.Up * .1f);
            var terrain = classic != null ? new Vector2(1030, 365) : new Vector2(150, 250);
            var destination = _interaction.PickGround(terrain)!.Value;
            var owned = world.Inventory.CreateOn("nw_wswdg001", pc, 1, "CAPTURE_GROUND")!;
            _session.Request("inventory"); await Wait(.5);
            var entry = Inventory.Carried.Single(i => i.Id == owned.Id);
            var skeleton = ((Views.CreatureView)_session.Controlled!).Skeleton!;
            int rootBone = skeleton.FindBone("rootdummy");
            float standingHeight = skeleton.GetBonePosePosition(rootBone).Z;
            var from = PackCell(entry.X, entry.Y, entry.H);
            Button(from, MouseButton.Left, true);
            await MovePushed(from, terrain, true);
            Button(terrain, MouseButton.Left, false); await Wait(.15);
            Check("world drag keeps selected terrain", pc.Actions.OfType<PutDownItemAction>().Any(a => a.Destination is { } d
                && System.Numerics.Vector3.Distance(d.Position, destination) < .03f));
            Check("world drag retains item while approaching", world.Inventory.Owner(owned) == pc);
            await Shot(dir, "a_drop_approach");
            await Until(() => notices.Any(n => n.Kind == "object-use" && n.Subject == pc.Id), "drop bend begins after approach");
            await Wait(.4);
            Check("drop bend clip plays", _session.Controlled is Views.CreatureView { Animator: { } animator }
                && animator.CaptureAction()?.Clip is "getlow" or "getlowlp"
                && skeleton.GetBonePosePosition(rootBone).Z < standingHeight - .25f);
            await Shot(dir, "b_drop_bend");
            await Wait(.55);
            Check("drop low pose continues until transfer", _session.Controlled is Views.CreatureView { Animator: { } heldAnimator }
                && heldAnimator.CaptureAction()?.Clip == "getlowlp"
                && skeleton.GetBonePosePosition(rootBone).Z < standingHeight - .25f
                && world.Inventory.Owner(owned) == pc);
            await Shot(dir, "b1_drop_hold");
            float lowHeight = skeleton.GetBonePosePosition(rootBone).Z;
            await Until(() => owned.Area != null, "world drop completes");
            Check("world item stays at cursor terrain", System.Numerics.Vector3.Distance(owned.Position, destination) < .03f);
            Check("one placement acknowledgement", notices.Count(n => n.Kind == "item-placed") == 1);
            await Wait(.2);
            Check("drop recovers after transfer", skeleton.GetBonePosePosition(rootBone).Z > lowHeight + .05f
                && skeleton.GetBonePosePosition(rootBone).Z < standingHeight - .03f);
            await Shot(dir, "b2_drop_recovery");
            await Wait(.3);
            var start = pc.Position;
            await ClickPoint(WorldPoint(owned));
            Check("open inventory ground click carries artwork", GetViewport().GuiIsDragging() && ItemDragging.Current(Inventory)?.Source == "ground");
            Check("carrying leaves item on ground and player still", owned.Area != null && pc.Position == start && pc.Actions.Count == 0);
            await Shot(dir, "c_ground_carry");
            VisualKey(27, true); VisualKey(27, false); await Wait(.2);
            Check("escape cancels without acquisition", !GetViewport().GuiIsDragging() && owned.Area != null);
            await ClickPoint(WorldPoint(owned));
            var pack = PackCell(7, 2, entry.H);
            await MovePushed(WorldPoint(owned), pack, false);
            await ClickPoint(pack);
            Check("backpack choice queues pickup", pc.Actions.OfType<PickUpItemAction>().Any(a => a.Cell == (7, 2)));
            await Wait(.3);
            await Shot(dir, "d0_pickup_bend");
            await Until(() => world.Inventory.Owner(owned) == pc, "pickup to chosen backpack completes");
            Check("chosen backpack cell retained", owned.InvX == 7 && owned.InvY == 2);
            await Wait(.4); await Shot(dir, "d_picked_to_cell");

            // Reset only fixture setup; subsequent acquisition again runs through real world input.
            var point = _interaction.PickGround(terrain)!.Value;
            world.Inventory.Drop(owned, new Location(pc.Area!.Id, point, 0), pc);
            await Wait(.5);
            await ClickPoint(WorldPoint(owned));
            Check("equipment carry exposes valid paperdoll target", GetViewport().GuiIsDragging());
            Control slot = classic != null ? classic.Inventory.Slots[4]
                : Descendants<ItemSlot>(Inventory).Single(s => s.EquipSlot == 4);
            await MovePushed(WorldPoint(owned), slot.GetGlobalRect().GetCenter(), false);
            await Shot(dir, "e_equip_carry");
            await ClickPoint(slot.GetGlobalRect().GetCenter());
            await Until(() => pc.Equipment.GetValueOrDefault(EquipSlot.RightHand) == owned, "ground paperdoll choice acquires and equips");
            await Wait(.4);
            await Shot(dir, "f_equipped");

            if (classic != null) classic.Inventory.Close(); else Inventory.Close();
            await Wait(.2);
            var potion = world.Items.FromBlueprint("nw_it_mpotion001")!;
            world.Register(potion);
            world.Inventory.Drop(potion, new Location(pc.Area!.Id, _interaction.PickGround(terrain)!.Value, 0));
            await Wait(.5);
            int acquisitions = notices.Count(n => n.Kind == "item-acquired");
            await ClickPoint(WorldPoint(potion));
            Check("closed inventory ground click starts pickup", !GetViewport().GuiIsDragging() && pc.Actions.OfType<PickUpItemAction>().Any());
            await Until(() => world.Inventory.Owner(potion) == pc, "closed inventory pickup completes");
            Check("closed pickup acknowledges acquisition once", notices.Count(n => n.Kind == "item-acquired") == acquisitions + 1);
            await Shot(dir, "g_closed_pickup");
            await Wait(.65);
            float forward = pc.Facing;
            var backPoint = pc.Position - new System.Numerics.Vector3(MathF.Cos(forward), MathF.Sin(forward), 0) * .8f;
            var backPixel = _camera.UnprojectPosition(_session.Space.ToGlobal(new Vector3(backPoint.X, backPoint.Y, backPoint.Z)));
            await ClickPoint(backPixel);
            Check("short ground click uses backward movement", pc.Mode == MovementMode.Backward
                && _session.Controlled is Views.CreatureView { Animator: { } backAnimator }
                && backAnimator.DriveMode == MovementMode.Backward);
            await Shot(dir, "h_short_backstep");
            await Until(() => pc.Actions.Count == 0, "short backstep completes");
            Check("short backstep reaches point facing away from travel",
                System.Numerics.Vector3.Distance(pc.Position, backPoint) < .08f
                && MathF.Abs(MathF.IEEERemainder(pc.Facing - forward, MathF.Tau)) < .08f);
            passed = true;
            GD.Print("VISUAL|done");
        }
        catch (Exception error)
        {
            GD.PrintErr("[ground-inventory] " + error);
            await Shot(dir, "failed");
        }
        finally
        {
            _session.NotifyReceived -= Notify;
            InventorySettings.ModernView = oldView;
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ground-inventory.json"),
                System.Text.Json.JsonSerializer.Serialize(new { passed, checks }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
