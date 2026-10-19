using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Client.UI;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Society;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    async Task CaptureQuickbarPossession(string dir)
    {
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            if (!ok) throw new InvalidOperationException(name);
        }
        string Layout() => JsonSerializer.Serialize(Enumerable.Range(0, 36).Select(i => ActionBar.Get(i / 12, i % 12)));
        string Cache() => JsonSerializer.Serialize(ActionBar.ReadCacheForReloadCapture());
        async Task Until(Func<bool> ready)
        {
            for (int i = 0; i < 60 && !ready(); i++) await Wait(.1);
            Check("control and abilities settled", ready());
            await Wait(.3);
        }
        try
        {
            await Wait(4);
            var world = _server!.World;
            var master = world.Get<Creature>(_session.ControlledId)!;
            Check("original bar loaded", ActionBar.Book.LoadedFor == master.Id && ActionBar.Get(0, 4) is { Paired: true });
            ActionBar.Set(0, 0, null);
            ActionBar.Set(1, 3, new BarSlot { Kind = "command", Arg = "rest" });
            ActionBar.Set(2, 11, new BarSlot { Kind = "mode", Id = 0 });
            await Wait(.3);
            string layout = Layout(), cache = Cache();
            string saved = JsonSerializer.Serialize(BicQuickbar.Read(ObjectSerializer.CreatureGff(world, master, uint.MaxValue, true)));
            await Shot(dir, "a_owner_bar");
            var familiar = world.Creatures.FromBlueprint("nw_fm_bat01")!;
            familiar.Scripts.Clear();
            // Deliberate collision: a possessed creature must not read or write the character's named cache.
            familiar.Name = master.Name;
            world.AddToArea(familiar, master.Area!, master.Position + new System.Numerics.Vector3(1, 0, 0), 0);
            AssociateService.Add(world, master, familiar, 3);
            ActionBar.User.Aim(2, null, "old owner target", (_, _) => throw new InvalidOperationException("stale target used"), false);
            _session.Request("possess-familiar", new { target = familiar.Id });
            await Until(() => _session.ControlledId == familiar.Id && ActionBar.Book.LoadedFor == familiar.Id && ActionBar.Book.SpellbookLoadedFor == familiar.Id);
            Check("native empty familiar bar", Enumerable.Range(0, 36).All(i => ActionBar.Get(i / 12, i % 12) == null));
            Check("old pending target cancelled", ActionBar.User.TargetingFor == null);
            Check("same-name familiar leaves owner disk cache intact", Cache() == cache);
            Check("old item metadata discarded", ActionBar.Book.ItemInfo.Count == 0);
            await Shot(dir, "b_familiar_empty");
            ActionBar.Set(0, 0, new BarSlot { Kind = "mode", Id = 0 });
            await Wait(.2);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, PhysicalKeycode = Key.F1, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, PhysicalKeycode = Key.F1, Pressed = false });
            await Wait(.3);
            Check("F1 acts on familiar only", familiar.DetectMode && !master.DetectMode);
            Check("familiar edit stored on familiar", familiar.QuickbarEdits.GetValueOrDefault(0)?.Kind == "mode");
            Check("familiar edit does not rewrite cache", Cache() == cache);
            Check("owner BIC unchanged during possession", saved == JsonSerializer.Serialize(BicQuickbar.Read(ObjectSerializer.CreatureGff(world, master, uint.MaxValue, true))));
            await Shot(dir, "c_familiar_mode");
            _session.Request("unpossess-familiar");
            await Until(() => _session.ControlledId == master.Id && ActionBar.Book.LoadedFor == master.Id && ActionBar.Book.SpellbookLoadedFor == master.Id);
            Check("all original slots restored including pair and clear", Layout() == layout);
            Check("original character cache restored intact", Cache() == cache);
            Check("owner abilities rebound", ActionBar.Book.Items.All(i => world.Get<Item>(i.Item)?.Possessor == master.Id));
            await Shot(dir, "d_owner_restored");
            passed = true;
            GD.Print("[quickbar-possession] PASS");
            GD.Print("VISUAL|done|1");
        }
        catch (Exception error) { GD.PushError("[quickbar-possession] " + error); await Shot(dir, "failed"); }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "possession-state.json"), JsonSerializer.Serialize(new { passed, checks }, new JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
