using OpenWinter.Client.UI;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using Godot;

namespace OpenWinter.Client;

/// <summary>Real key/portrait/queue clicks with authoritative resource checks, in either UI.</summary>
public partial class Main
{
    async Task CaptureActionQueue(string dir)
    {
        void Check(bool value, string name)
        {
            GD.Print($"[action-queue] {name}={value}");
            if (!value) throw new InvalidOperationException(name);
        }
        async Task KeyPress(Key key)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            await Wait(.05);
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key });
            await Wait(.08);
        }
        async Task Click(Vector2 point)
        {
            _interaction.PointerOverride = point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
            await Wait(.05);
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
            await Wait(.12);
        }
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            var cls = pc.Classes.First(c => c.Class == 2);
            cls.Memorized[1].Clear();
            cls.Memorized[1].AddRange([(50, 0, true), (6, 0, true), (50, 0, true)]);
            ActionQueues.Clear(world, pc);
            var potion = world.Inventory.CreateOn("nw_it_mpotion001", pc, 3, "queue_capture_potion")!;
            ActionBar.Book.Refresh(); _session.Request("spellbook");
            await Wait(1);
            ActionBar.SetBasePage(0);
            ActionBar.Set(0, 0, new BarSlot { Kind = "spell", Id = 50, Class = 2 });
            ActionBar.Set(0, 1, new BarSlot { Kind = "spell", Id = 6, Class = 2 });
            ActionBar.Set(0, 2, new BarSlot { Kind = "item", Item = potion.Id });
            bool classic = UI.Classic.ClassicGame.Current != null;
            var hud = GetChildren().OfType<Hud>().Single();
            Vector2 portrait = classic ? new Vector2(1245, 50) : hud.Player.GlobalPosition + new Vector2(44, 50);
            async Task Cancel(int index)
            {
                Vector2 point = classic ? new Vector2(22 + index * 43, 20)
                    : hud.Queue.GetChildren().OfType<Button>().ElementAt(index).GetGlobalRect().GetCenter();
                await Click(point); await Wait(.3);
            }
            await KeyPress(Key.F1); await Click(portrait);
            await KeyPress(Key.F2); await Click(portrait);
            await KeyPress(Key.F1); await Click(portrait);
            await Wait(.3);
            await Shot(dir, "01_three_spells");
            GD.Print($"[action-queue] queued={pc.Actions.Count}, shown={_hudState!.Queue.Count}, targeting={_interaction.Targeting != null}, commandable={pc.Commandable}");
            Check(pc.Actions.Count == 3 && _hudState.Queue.Count == 3, "three_spell_commands");
            await Cancel(1);
            Check(pc.Actions.Count == 2, "middle_removed_only");
            await Wait(5);
            await Shot(dir, "02_waiting_next_spell");
            await Wait(8);
            Check(pc.Actions.Count == 0 && cls.Memorized[1].Select(s => s.Ready).SequenceEqual(new[] { false, true, false }), "two_spells_resolved_bless_unspent");
            await KeyPress(Key.F3); await KeyPress(Key.F3); await KeyPress(Key.F3);
            await Wait(.3);
            Check(pc.Actions.Count == 3 && _hudState!.Queue.Count == 3, "three_potion_commands");
            Check(_hudState.Queue.All(a => a.Spell == 32 && a.Icon.Length > 0), "potion_effect_icons");
            await Shot(dir, "03_three_potions");
            await Cancel(1);
            Check(pc.Actions.Count == 2, "potion_middle_removed_only");
            await Wait(9);
            Check(pc.Actions.Count == 0 && potion.StackSize == 1, "two_potions_consumed");
            await Shot(dir, "04_queue_empty");
            GD.Print("[action-queue] PASS"); GD.Print("VISUAL|done|1"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[action-queue] " + e); GetTree().Quit(1); }
    }
}
