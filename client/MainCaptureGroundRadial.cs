using OpenWinter.Client.UI;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    async Task CaptureGroundRadial(string dir)
    {
        void Check(bool ok, string label) { GD.Print($"[ground-radial] {label}={ok}"); if (!ok) throw new InvalidOperationException(label); }
        async Task Click(Vector2 at, MouseButton button)
        {
            _interaction.PointerOverride = at;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            await Wait(0.1);
            var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right;
            Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button, ButtonMask = mask, Pressed = true });
            await Wait(0.1);
            Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button });
            await Wait(0.3);
        }
        try
        {
            await Wait(5);
            var radial = Descendants<RadialMenu>(GetTree().Root).First();
            System.Numerics.Vector3? chosen = null;
            var open = _interaction.GroundMenu;
            Check(open != null, "ground_callback_installed");
            _interaction.GroundMenu = (at, point) => { chosen = point; open!(at, point); };
            var screen = new Vector2(900, 420);
            await Click(screen, MouseButton.Right);
            Check(radial.IsOpen && radial.Target == null && chosen != null, "right_click_ground_opens");
            await Wait(0.5);
            Check(radial.IsOpen, "ground_menu_survives_update");
            Check(radial.Labels.Contains("Cast Spell") && radial.Labels.Contains("Special Abilities"), "ground_categories");
            await Shot(dir, "a_ground_root");
            if (_args.ContainsKey("tile-actions-only"))
            {
                var actor = _server!.World.Get<OpenWinter.Game.Objects.Creature>(_session.ControlledId)!;
                int count = 0;
                foreach (var (id, label) in new[] { (1, "Use #1"), (3, "Lay on Hands"), (8, "Move To") })
                {
                    if (count > 0) await Click(screen, MouseButton.Right);
                    Check(radial.WedgePoint("More Actions") != null, "tile_category_" + id);
                    await Click(radial.WedgePoint("More Actions")!.Value, MouseButton.Left);
                    Check(radial.Shown.Count == 8 && radial.Shown[id - 1].Label == label, "fixed_tile_slot_" + id);
                    Check(radial.Shown[1].Hidden && radial.Shown[3].Hidden, "tile_holes_" + id);
                    if (count == 0) await Shot(dir, "b_tile_actions");
                    await Click(radial.WedgePoint(label)!.Value, MouseButton.Left);
                    Check(!radial.IsOpen, "tile_action_closes_" + id);
                    await Wait(0.15);
                    Check(actor.Locals.Ints.GetValueOrDefault("tile_last_id") == id && actor.Locals.Ints.GetValueOrDefault("tile_count") == ++count, "module_event_" + id);
                    var at = actor.Locals.Locations["tile_last_pos"].Position;
                    Check(System.Numerics.Vector3.Distance(at, chosen!.Value) < 0.02f, "tile_position_" + id);
                    GD.Print($"[ground-radial] tile id={id} selected={chosen} event={at}");
                }
                await Shot(dir, "c_tile_events_complete");
                GD.Print("[ground-radial] PASS"); GetTree().Quit(); return;
            }
            if (_args.ContainsKey("ground-move-only"))
            {
                var mover = _server!.World.Get<OpenWinter.Game.Objects.Creature>(_session.ControlledId)!;
                var start = mover.Position;
                Check(radial.WedgePoint("Move To") != null, "move_to_available");
                await Click(radial.WedgePoint("Move To")!.Value, MouseButton.Left);
                Check(!radial.IsOpen, "move_to_closes_menu");
                var action = mover.Actions.OfType<OpenWinter.Game.MoveAction>().FirstOrDefault();
                Check(action != null && !action.Run, "authoritative_walk_action");
                Check(action != null && System.Numerics.Vector3.Distance(action.Target, chosen!.Value) < 0.02f, "move_target_retained_after_close");
                await Shot(dir, "b_ground_walking");
                for (int i = 0; i < 80 && mover.Actions.Count > 0; i++) await Wait(0.1);
                Check(System.Numerics.Vector3.Distance(mover.Position, chosen!.Value) < 0.35f, "arrives_at_chosen_point");
                Check(System.Numerics.Vector3.Distance(start, mover.Position) > 1, "actually_moved");
                GD.Print($"[ground-radial] walk start={start} selected={chosen} arrived={mover.Position}");
                await Shot(dir, "c_ground_arrival");
                GD.Print("[ground-radial] PASS");
                GetTree().Quit();
                return;
            }
            foreach (var label in new[] { "Cast Spell", "Wizard", "Level 1" })
            {
                Check(radial.WedgePoint(label) != null, "has_" + label);
                await Click(radial.WedgePoint(label)!.Value, MouseButton.Left);
            }
            Check(!radial.Labels.Contains("Magic Missile"), "creature_only_spell_filtered");
            Check(radial.WedgePoint("Grease") != null, "ground_spell_available");
            await Shot(dir, "b_ground_spells");
            await Click(radial.WedgePoint("Grease")!.Value, MouseButton.Left);
            Check(!radial.IsOpen && _interaction.Targeting == null, "uses_selected_point_without_retargeting");
            var pc = _server!.World.Get<OpenWinter.Game.Objects.Creature>(_session.ControlledId)!;
            for (int i = 0; i < 70 && pc.Locals.Ints.GetValueOrDefault("ground_cast") == 0; i++) await Wait(0.1);
            Check(pc.Locals.Ints.GetValueOrDefault("ground_cast") == 1, "actual_spell_impact");
            var observed = pc.Locals.Locations["ground_cast_location"].Position;
            GD.Print($"[ground-radial] selected={chosen} impact={observed}");
            Check(System.Numerics.Vector3.Distance(observed, chosen!.Value) < 0.02f, "authoritative_target_matches_ground");
            await Shot(dir, "c_ground_cast");
            await Click(screen, MouseButton.Right);
            Check(radial.IsOpen, "reopens_after_cast");
            var player = _server.Sessions.Single(s => s.Creature == pc);
            player.Known.Clear();
            _server.EnterArea(player, player.Area!, pc.Position, pc.Facing, alreadyPlaced: true);
            await Wait(1);
            Check(!radial.IsOpen && _session.ControlledId == pc.Id, "area_reload_closes_same_player_ground_context");
            GD.Print("[ground-radial] PASS");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[ground-radial] " + e); GetTree().Quit(1); }
    }
}
