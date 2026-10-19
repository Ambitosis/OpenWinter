using OpenWinter.Client.UI.Map;
using Godot;
using System.IO;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>Sandbox-only client UI/actions proof; does not claim native pin-key semantics.</summary>
public static class ClassicMapFlowCapture
{
    public static async Task Run(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        Directory.CreateDirectory(dir);
        int checks = 0;
        void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException("[classic-map-flow] FAIL " + name);
            checks++;
            GD.Print("[classic-map-flow] PASS " + name);
        }
        void Button(GuiPanel panel, string name) => panel.Get<BaseButton>(name)!.EmitSignal(BaseButton.SignalName.Pressed);
        try
        {
            await wait(2);
            var ctx = ClassicGame.Current ?? throw new InvalidOperationException("Run with --ui classic");
            var inventory = ClassicInventory.Current!.Inventory;
            var character = ClassicSheets.Character!;
            var journal = ClassicSheets.Journal!;
            foreach (var w in ClassicWindow.OpenWindows.ToArray()) w.Close();
            inventory.Open(); await wait(.4);
            character.Open(); await wait(.4);
            journal.Open(); await wait(.4);
            float right = ctx.Hud.GetViewport().GetVisibleRect().Size.X - 80 - 360;
            Check(inventory.Position.X == 0 && character.Position.X == right && journal.Position.X == right / 2, "left-right-middle");
            await shot(dir, "01_three_panels");
            character.Close();
            Check(journal.Position.X == right && inventory.Position.X == 0, "middle-promotes-right");
            character.Open(); inventory.Close();
            Check(character.Position.X == 0 && journal.Position.X == right, "middle-promotes-left");
            await shot(dir, "02_reflow");
            character.Close(); journal.Close();

            var mini = ClassicSheets.MiniMap!;
            var map = AreaMap.Of(ctx.Session);
            ctx.Input.ToggleMap?.Invoke(); await wait(.8);
            Check(mini.IsOpen, "M-route-opens");
            var canvas = mini.Panel.GetNode<MapCanvas>("MapCanvas");
            var expected = new Vector2(Math.Max(74, map.Width * 16 + 10), map.Height * 16 + 30);
            Check(mini.Panel.Size == expected, "dimension-derived-default");
            Check(canvas.WorldToScreen(map.Extent / 2) == (canvas.Size / 2).Round(), "whole-area-center");
            await shot(dir, "03_map_default");
            Button(mini.Panel, "ZoomIn");
            Check(mini.Panel.Size == new Vector2(Math.Max(74, map.Width * 32 + 10), map.Height * 32 + 30), "zoom-resizes");
            await shot(dir, "04_map_zoom");
            Button(mini.Panel, "ZoomOut");
            Check(mini.Panel.Size == expected, "zoom-out-restores");
            ctx.Hud.OpenMap?.Invoke();
            Check(!mini.IsOpen, "portrait-map-route-toggles");
            ctx.Hud.OpenMap?.Invoke(); await wait(.3);

            var at = map.Extent / 2;
            int before = map.Pins.Count(p => p.Personal);
            void ClickMap(Vector2 world) => canvas._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = canvas.WorldToScreen(world) });
            ClassicPopup Editor() => ClassicPopup.Top ?? throw new InvalidOperationException("pin editor missing");
            void Text(string text)
            {
                var edit = Editor().Panel.GetChildren().OfType<TextEdit>().Single();
                edit.Text = text;
                // Programmatic assignment does not emit the signal produced by user edits.
                edit.EmitSignal(TextEdit.SignalName.TextChanged);
            }
            Button(mini.Panel, "AddPin");
            Check(HudParts.Cursors.InterfaceCursor == 45, "pin-cursor");
            ClickMap(at);
            Check(HudParts.Cursors.InterfaceCursor != 45 && Editor().Panel.GetChildren().OfType<TextEdit>().Any(), "point-opens-editor");
            Text(new string('x', 4097));
            Check(Editor().Panel.Get<GuiButton>("OkButton")!.Disabled, "overlength-disables-save");
            var longEditor = Editor();
            longEditor._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Check(ClassicPopup.Top == longEditor && !longEditor.IsQueuedForDeletion(), "overlength-enter-keeps-editor");
            Text("Capture pin"); await shot(dir, "05_pin_editor");
            Button(Editor().Panel, "OkButton"); await wait(.8);
            var pin = map.Pins.Single(p => p.Personal && p.Note == "Capture pin");
            Check(map.Pins.Count(p => p.Personal) == before + 1, "authoritative-add");
            Check(canvas.PinAt(canvas.WorldToScreen(pin.Position))?.Note == "Capture pin", "pin-hover-hit");
            await shot(dir, "06_pin_added");
            // Hovering the pin: its note at once, in the hover box over the map (two frames after the pointer arrives).
            var pinAt = canvas.GetGlobalTransformWithCanvas() * canvas.WorldToScreen(pin.Position);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = pinAt, GlobalPosition = pinAt });
            await ctx.Hud.ToSignal(ctx.Hud.GetTree(), SceneTree.SignalName.ProcessFrame);
            await ctx.Hud.ToSignal(ctx.Hud.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(ClassicHud.Current?.HoverShown is ["Capture pin"], "pin-hover-instant");
            await shot(dir, "06b_pin_hover");
            var away = new Vector2(900, 600);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = away, GlobalPosition = away });
            ClickMap(pin.Position); Text("Edited capture pin");
            Button(Editor().Panel, "OkButton"); await wait(.8);
            Check(map.Pins.Any(p => p.Id == pin.Id && p.Note == "Edited capture pin"), "authoritative-edit");
            ClickMap(pin.Position); Text("Canceled edit"); Editor().Cancel(); await wait(.3);
            Check(map.Pins.Any(p => p.Id == pin.Id && p.Note == "Edited capture pin"), "cancel-preserves-note");
            ClickMap(pin.Position); Button(Editor().Panel, "DeleteButton"); await wait(.8);
            Check(!map.Pins.Any(p => p.Id == pin.Id), "authoritative-delete");
            Button(mini.Panel, "AddPin"); ClickMap(at); Text("Must not survive close");
            mini.Close(); await wait(.3);
            Check(ClassicPopup.Top == null && HudParts.Cursors.InterfaceCursor != 45, "map-close-clears-editor");
            Check(map.Pins.Count(p => p.Personal) == before, "cancel-no-new-pin");
            // Authenticated stale-area request control; real area-transition UI capture remains separate.
            map.AddPin(map.AreaId + 100000, at, "Wrong area"); await wait(.3);
            Check(!map.Pins.Any(p => p.Note == "Wrong area"), "stale-area-request-rejected");
            mini.Open(); await wait(.3); await shot(dir, "07_after_delete"); mini.Close();
            GD.Print($"[classic-map-flow] COMPLETE checks={checks}");
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); }
        finally { quit(); }
    }
}
