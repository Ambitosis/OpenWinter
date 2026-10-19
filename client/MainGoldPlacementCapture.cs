using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    async Task CaptureGoldPlacement(string dir)
    {
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok)
        {
            checks.Add(new { name, ok });
            GD.Print($"[gold-placement] {name}: {ok}");
            if (!ok) throw new InvalidOperationException(name);
        }
        try
        {
            await Wait(3);
            var world = _server!.World;
            var pc = world.Get<Creature>(_session.ControlledId)!;
            pc.Gold = 198;
            _input.ToggleInventory?.Invoke();
            await Wait(1);
            var classic = ClassicInventory.Current;
            Control goldButton = classic != null ? classic.Inventory.Panel.Get<GuiButton>("GoldButton")!
                : Descendants<Control>(Inventory).First(c => c.TooltipText == "Drop gold");
            var terrain = classic != null ? new Vector2(1050, 370) : new Vector2(150, 300);
            var destination = _interaction.PickGround(terrain)!.Value;
            Check("terrain point outside inventory and beyond reach", System.Numerics.Vector3.Distance(destination, pc.Position) > 2);
            async Task Carry()
            {
                Click(goldButton, MouseButton.Left);
                await Wait(0.2);
                if (classic != null)
                {
                    Click(classic.Split.Panel.Get<GuiButton>("AllButton")!, MouseButton.Left);
                    await Wait(0.1);
                    Click(classic.Split.Panel.Get<GuiButton>("OkButton")!, MouseButton.Left);
                }
                else
                {
                    var dialog = Descendants<ConfirmOverlay>(Inventory).Single();
                    Click(Descendants<Button>(dialog).First(b => b.Text == "Drop"), MouseButton.Left);
                }
                await Wait(0.2);
                Check("confirmed gold follows pointer without spending", GetViewport().GuiIsDragging() && pc.Gold == 198);
                await MovePushed(Center(goldButton), terrain, false, 3);
            }
            await Carry();
            VisualKey(27, true); VisualKey(27, false);
            await Wait(0.2);
            Check("escape cancels carry without action or spending", !GetViewport().GuiIsDragging() && pc.Gold == 198 && !pc.Actions.OfType<DropGoldAction>().Any());
            await Shot(dir, "a_cancelled");
            await Carry();
            Button(terrain, MouseButton.Left, true);
            await Wait(0.05);
            Button(terrain, MouseButton.Left, false);
            await Wait(0.2);
            var action = pc.Actions.OfType<DropGoldAction>().FirstOrDefault();
            Check("terrain release queues selected point", action?.Destination is { } target && System.Numerics.Vector3.Distance(target.Position, destination) < 0.02f);
            Check("approach runs without spending", pc.Gold == 198 && !pc.WalkRequested);
            await Shot(dir, "b_approaching");
            await Wait(5);
            var pile = pc.Area!.Objects.OfType<Item>().SingleOrDefault(i => i.BaseItem == 76);
            Check("completed drop spends exact amount", pc.Gold == 0 && pile?.StackSize == 198);
            Check("pile uses native zero facing", pile?.FacingDegrees == 0);
            Check("pile stays at selected terrain point", pile != null && System.Numerics.Vector3.Distance(pile.Position, destination) < 0.02f);
            Check("player stops one metre from pile", Math.Abs(System.Numerics.Vector3.Distance(pc.Position, destination) - 1) < 0.02f);
            Check("open inventory gold updates", (classic?.Data.Gold ?? Inventory.Gold) == 0);
            await Shot(dir, "c_finished");
            passed = true;
            GD.Print("VISUAL|done");
        }
        catch (Exception e) { GD.PrintErr(e); }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "gold-placement.json"),
                System.Text.Json.JsonSerializer.Serialize(checks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
