using Godot;

namespace OpenWinter.Client.UI;

public partial class MapPanel
{
    /// <summary>Sandbox capture of actual map controls and authenticated personal-pin requests.</summary>
    public async Task CapturePins(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        int checks = 0;
        void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException("[modern-map-flow] FAIL " + label);
            checks++;
            GD.Print("[modern-map-flow] PASS " + label);
        }
        TextEdit Text() => _pinEditor!.Body.GetChildren().OfType<TextEdit>().Single();
        void Button(string label) => _pinEditor!.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            await wait(2);
            if (!Visible) Toggle();
            await wait(.6);
            int before = _map.Pins.Count;
            _addPin.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_addingPin && _view.MouseDefaultCursorShape == CursorShape.Cross, "placement-cursor");
            OnMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = _view.Size / 2 });
            Check(!_addingPin && _pinEditor?.Visible == true, "point-opens-editor");
            Text().Text = "Personal map note";
            await wait(.3);
            await shot(dir, "01_editor");
            Button("Save");
            await wait(.5);
            var pin = _map.Pins.Single(p => p.Personal && p.Note == "Personal map note");
            Check(_map.Pins.Count == before + 1, "add-acknowledged");
            await shot(dir, "02_saved");
            EditPin(pin);
            Text().Text = "Edited personal note";
            Button("Save");
            await wait(.5);
            pin = _map.Pins.Single(p => p.Id == pin.Id);
            Check(pin.Note == "Edited personal note", "edit-acknowledged");
            EditPin(pin);
            Text().Text = "Discard this change";
            Button("Cancel");
            await wait(.3);
            Check(_map.Pins.Single(p => p.Id == pin.Id).Note == "Edited personal note", "cancel-keeps-note");
            EditPin(pin);
            await wait(.3);
            await shot(dir, "03_edit_delete");
            Button("Delete");
            await wait(.5);
            Check(_map.Pins.Count == before, "delete-acknowledged");
            _addPin.EmitSignal(BaseButton.SignalName.Pressed);
            OnMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            Check(!_addingPin, "right-click-cancels-placement");
            ShowPinEditor(_map.Extent / 2, null);
            Close();
            Check(_pinEditor == null && !_addingPin, "close-cleans-editor");
            Toggle();
            await wait(.3);
            await shot(dir, "04_complete");
            GD.Print($"[modern-map-flow] COMPLETE {checks} checks");
            GD.Print("VISUAL|done|1");
        }
        catch (Exception e) { GD.PushError(e.ToString()); }
        finally { quit(); }
    }
}
