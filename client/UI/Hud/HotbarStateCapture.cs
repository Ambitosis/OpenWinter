using System.Text.Json;
using OpenWinter.Client.UI;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using Godot;

namespace OpenWinter.Client;

/// <summary>Local authored UI regression: authenticated cast/rest/cast and consume/replenish a shortcut.</summary>
public partial class Main
{
    async Task CaptureHotbarState(string dir)
    {
        if (OS.GetCmdlineUserArgs().Contains("--quickbar-possession-only")) { await CaptureQuickbarPossession(dir); return; }
        if (OS.GetCmdlineUserArgs().Contains("--quickbar-reload-only")) { await CaptureQuickbarReload(dir); return; }
        if (OS.GetCmdlineUserArgs().Contains("--quickbar-pair-only")) { await CaptureQuickbarPair(dir); return; }
        try
        {
            await Wait(3);
            var world = _server?.World ?? throw new InvalidOperationException("Requires a local sandbox server.");
            var pc = world.Get<Creature>(_session.ControlledId)!;
            if (!pc.IsPC || pc.Classes.All(c => c.Class != 2)) throw new InvalidOperationException("Requires the stock cleric PC.");
            var cls = pc.Classes.First(c => c.Class == 2);
            for (int level = 0; level < 10; level++) cls.Memorized[level].Clear();
            CreatureRules.RestoreSpells(world, pc);
            CreatureRules.SyncHitPoints(world, pc);
            foreach (var old in world.Inventory.AllItems(pc).Where(i => i.BaseItem == 49).ToList()) world.Destroy(old);
            var potion = world.Inventory.CreateOn("nw_it_mpotion001", pc, 3, "hotbar_capture_potion")
                ?? throw new InvalidOperationException("Missing stock potion.");
            ActionBar.Book.Refresh(); _session.Request("spellbook");
            await Wait(1.5);
            ActionBar.SetBasePage(0);
            for (int i = 0; i < ActionBar.Slots; i++) ActionBar.Set(0, i, null);
            ActionBar.Set(0, 0, new BarSlot { Kind = "spell", Id = 6, Class = 2 });
            ActionBar.Set(0, 1, new BarSlot { Kind = "item", Item = potion.Id });
            ActionBar.Set(0, 4, new BarSlot { Kind = "spell", Id = 46, Class = 2 }); // Doom: never prepared in this fixture.
            var records = new List<object>();
            async Task Record(string name, int uses, int slotIndex = 1)
            {
                var slot = ActionBar.Get(0, slotIndex)!;
                var look = ActionBar.User.Look(slot);
                if (look.Uses != uses || look.Icons.Length == 0 || look.Usable != (uses > 0))
                    throw new InvalidOperationException(name + ": " + JsonSerializer.Serialize(look));
                records.Add(new
                {
                    name, item = slot.Item, slot.ItemKey, look, spell = ActionBar.User.Look(ActionBar.Get(0, 0)!),
                    selectedPower = ActionBar.Get(0, 2) is { } selection ? new { selection.Prop, selection.ItemPowerKey, look = ActionBar.User.Look(selection) } : null
                });
                await Wait(.15); // Let the changed slot draw before reading the viewport.
                await Shot(dir, name);
            }
            async Task RecordSpell(string name, int uses)
            {
                var doom = ActionBar.User.Look(ActionBar.Get(0, 4)!);
                if (doom.Uses != 0 || doom.Usable || doom.Icons.Length == 0)
                    throw new InvalidOperationException("Unprepared Doom: " + JsonSerializer.Serialize(doom));
                await Record(name, uses, 0);
            }
            await RecordSpell("00_spell_unprepared", 0);
            _session.Request("spell-memorize", new { @class = 2, level = 1, slot = 0, spell = 6, meta = 0 });
            await Wait(1);
            await RecordSpell("00_spell_prepared_pending_rest", 0);
            CreatureRules.RestoreSpells(world, pc);
            await Wait(1.2);
            await RecordSpell("00_spell_ready", 1);
            await Record("00_ready", 3);
            // Identical unbound bottles must neither inflate this slot nor replace it when exhausted.
            Item SeparateBottle(int stack = 1)
            {
                var bottle = world.Items.FromBlueprint("nw_it_mpotion001")!;
                world.Register(bottle);
                bottle.StackSize = stack;
                if (world.Inventory.MoveTo(bottle, pc, merge: false) == null)
                    throw new InvalidOperationException("Cannot place separate singleton bottle.");
                return bottle;
            }
            var singleton = SeparateBottle();
            var spare = SeparateBottle();
            await Wait(1.2);
            ActionBar.Set(0, 3, new BarSlot { Kind = "item", Item = singleton.Id });
            await Record("00_separate_stack", 1, 3);
            await Record("00_bound_stack_unchanged", 3);
            ActionBar.UseSlot(0, 3); ActionBar.UseSlot(0, 3);
            await Wait(7);
            await Record("00_singleton_exhausted", 0, 3);
            if (!singleton.IsDestroyed || spare.IsDestroyed || spare.StackSize != 1 || potion.StackSize != 3)
                throw new InvalidOperationException("Queued singleton use consumed a different stack.");
            void CastBless()
            {
                ActionBar.UseSlot(0, 0);
                Casting.Interaction.Targeting?.Invoke(_session.Controlled, null);
            }
            bool itemsOnly = OS.GetCmdlineUserArgs().Contains("--hotbar-items-only");
            if (!itemsOnly)
            {
                CastBless(); await Wait(4);
                if (cls.Memorized[1][0].Ready || pc.LastSpell?.SpellId != 6) throw new InvalidOperationException("First hotbar cast failed.");
                await Record("01_spell_spent", 3);
                await RecordSpell("01_spell_zero", 0);
                _session.Request("rest"); await Wait(.3);
                if (!pc.IsResting) throw new InvalidOperationException("Rest did not begin.");
                await Wait(RestAction.Duration(world, pc) + 1.5);
                if (!cls.Memorized[1][0].Ready) throw new InvalidOperationException("Rest did not restore Bless.");
                await Record("02_rested", 3);
                await RecordSpell("02_spell_restored", 1);
                pc.LastSpell = null;
                CastBless(); await Wait(4);
                if (cls.Memorized[1][0].Ready || pc.LastSpell?.SpellId != 6) throw new InvalidOperationException("Second hotbar cast failed.");
                await RecordSpell("02_spell_spent_again", 0);
                _session.Request("spell-memorize", new { @class = 2, level = 1, slot = 0, spell = -1, meta = 0 });
                await Wait(1.2);
                await RecordSpell("02_spell_unprepared_again", 0);
            }
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                ActionBar.UseSlot(0, 1);
                await Wait(5);
                await Record("0" + (5 - remaining) + "_potion_" + remaining, remaining);
            }
            var replacement = SeparateBottle(2);
            await Wait(1.5);
            await Record("06_other_stack_does_not_rebind", 0);
            ActionBar.Set(0, 1, new BarSlot { Kind = "item", Item = replacement.Id });
            await Record("06_explicit_replacement", 2);
            var owned = world.Get<Item>(ActionBar.Get(0, 1)!.Item)!;
            ActionBar.Set(0, 2, new BarSlot { Kind = "item", Item = owned.Id, Prop = 0 });
            var selectedPower = owned.Properties[0];
            owned.Identified = true;
            owned.Plot = !owned.Plot;
            owned.Properties.Insert(0, new OpenWinter.Game.Scripting.ItemProperty { Type = 1 });
            ActionBar.Book.Refresh(); await Wait(1.2);
            var selectedSlot = ActionBar.Get(0, 2)!;
            if (selectedSlot.Item != owned.Id || selectedSlot.Prop != 1 || !ActionBar.User.Look(selectedSlot).Usable)
                throw new InvalidOperationException("Existing item mutation lost its selected power.");
            await Record("07_owned_mutated", 2);
            owned.Properties.Remove(selectedPower);
            ActionBar.Book.Refresh(); await Wait(1.2);
            if (ActionBar.User.Look(selectedSlot).Usable) throw new InvalidOperationException("Removed power remained usable.");
            await Record("08_power_removed", 0, 2);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "state.json"), JsonSerializer.Serialize(new
            { character = pc.Name.Resolve(world.Resources.Talk), classes = pc.Classes.Select(c => new { c.Class, c.Level }), records }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("VISUAL|done|1");
            GD.Print("[hotbar-state] PASS: " + (itemsOnly ? "items only; " : "actual PC cast/rest/cast; ") + "separate bottles 1/0 with spare untouched; potion 3/2/1/0, no implicit replacement, explicit stack 2, owned mutation and removed power");
            GetTree().Quit(0);
        }
        catch (Exception ex) { GD.PushError("[hotbar-state] " + ex); GetTree().Quit(1); }
    }
}
