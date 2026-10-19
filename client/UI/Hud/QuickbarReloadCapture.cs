using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI
{
    public partial class ActionBar
    {
        internal void WriteCacheForReloadCapture(QuickbarSave<BarSlot> layout)
        {
            DirAccess.MakeDirRecursiveAbsolute(Dir);
            using var file = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(JsonSerializer.Serialize(layout));
        }

        internal QuickbarSave<BarSlot>? ReadCacheForReloadCapture() =>
            QuickbarSave<BarSlot>.Read(Godot.FileAccess.GetFileAsString(FilePath), null);
    }
}

namespace OpenWinter.Client
{
    public partial class Main
    {
        // Actual ActionBar.Load after a character/world reload, with stale disk cache and an authenticated reply.
        async Task CaptureQuickbarReload(string dir)
        {
            var checks = new List<object>();
            bool passed = false;
            Action<NotifyMessage>? observe = null;
            void Check(string name, bool ok)
            {
                checks.Add(new { name, ok });
                if (!ok) throw new InvalidOperationException(name);
            }
            try
            {
                await Wait(3);
                var server = _server!;
                var world = server.World;
                var pc = world.Get<Creature>(_session.ControlledId)!;
                Check("local-vault single-player fixture", server.Options.Vault == VaultMode.Local && server.Options.SaveGames);
                var main = pc.Equipment[EquipSlot.RightHand];
                var off = pc.Equipment[EquipSlot.LeftHand];
                var before = new BarSlot { Kind = "item", Paired = true, Item = main.Id, SecondaryItem = off.Id };
                ActionBar.Set(0, 4, before);
                ActionBar.Set(0, 0, null);
                await Wait(.5);
                var saved = Gff.Read(Gff.Write(ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true), "BIC "));
                Check("BIC retains intentional clear and saved marker", BicQuickbar.HasSavedEdits(saved)
                    && saved.GetList("QBList")[0].GetInt("QBObjectType") == 0);

                var stale = new QuickbarSave<BarSlot> { Bars = Enumerable.Range(0, 3).Select(_ => new BarSlot?[12]).ToArray() };
                stale.Bars[0][0] = new BarSlot { Kind = "command", Arg = "rest" }; // Must not resurrect the cleared slot.
                stale.Bars[0][4] = before.Clone(); // Both IDs belong to the previous world.
                ActionBar.WriteCacheForReloadCapture(stale);

                var next = new World(world.Resources);
                next.LoadModule();
                // Deliberately allocate different IDs, rather than accidentally passing with equal IDs in a fresh world.
                for (int i = 0; i < 100; i++) next.Register(new Placeable());
                var restored = next.Creatures.FromTemplate(saved);
                next.Register(restored);
                next.Inventory.Load(restored);
                CreatureRules.EnsureInit(next, restored);
                next.AddToArea(restored, next.FindArea(pc.Area!.ResRef)!, pc.Position, pc.Facing);
                var expected = QuickbarRequests.Read(next, restored, _ => { })![4]!;
                Check("both item IDs changed on BIC load", expected.Item != before.Item && expected.SecondaryItem != before.SecondaryItem);
                bool received = false, authoritative = false;
                observe = message =>
                {
                    if (message.Kind != "quickbar" || message.Subject != restored.Id) return;
                    using var json = JsonDocument.Parse(message.Json);
                    received = true;
                    authoritative = json.RootElement.GetProperty("authoritative").GetBoolean();
                };
                _session.NotifyReceived += observe;
                server.SwapWorld(next, _ => restored);
                for (int i = 0; i < 60 && (!received || _session.ControlledId != restored.Id); i++) await Wait(.1);
                await Wait(.5);
                Check("actual reload received authoritative character reply", received && authoritative && _session.ControlledId == restored.Id);
                Check("client imports both new item IDs", ActionBar.Get(0, 4) is { Paired: true } pair
                    && pair.Item == expected.Item && pair.SecondaryItem == expected.SecondaryItem);
                Check("stale local slot cannot undo saved clear", ActionBar.Get(0, 0) == null);
                Check("stale layout was not sent back as edits", restored.QuickbarEdits.Count == 0);
                var cache = ActionBar.ReadCacheForReloadCapture()!;
                Check("disk cache rewritten with new IDs and clear", cache.Bars[0][0] == null
                    && cache.Bars[0][4]!.Item == expected.Item && cache.Bars[0][4]!.SecondaryItem == expected.SecondaryItem);
                await Shot(dir, "a_reloaded_pair");

                async Task Activate(string phase, bool equipped)
                {
                    GetViewport().PushInput(new InputEventKey { Keycode = Key.F5, PhysicalKeycode = Key.F5, Pressed = true });
                    GetViewport().PushInput(new InputEventKey { Keycode = Key.F5, PhysicalKeycode = Key.F5, Pressed = false });
                    await Wait(1.1);
                    Check(phase, equipped
                        ? restored.Equipment.GetValueOrDefault(EquipSlot.RightHand)?.Id == expected.Item
                            && restored.Equipment.GetValueOrDefault(EquipSlot.LeftHand)?.Id == expected.SecondaryItem
                        : !restored.Equipment.ContainsKey(EquipSlot.RightHand) && !restored.Equipment.ContainsKey(EquipSlot.LeftHand));
                    await Shot(dir, phase);
                }
                await Activate("b_reloaded_pair_removed", false);
                await Activate("c_reloaded_pair_restored", true);
                passed = true;
                GD.Print("VISUAL|done|1");
            }
            catch (Exception error) { GD.PushError("[quickbar-reload] " + error); await Shot(dir, "failed"); }
            finally
            {
                if (observe != null) _session.NotifyReceived -= observe;
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "reload-state.json"), JsonSerializer.Serialize(new { passed, checks }, new JsonSerializerOptions { WriteIndented = true }));
                GetTree().Quit(passed ? 0 : 1);
            }
        }
    }
}
