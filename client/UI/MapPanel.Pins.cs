using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.UI.Map;
using Godot;

namespace OpenWinter.Client.UI;

public partial class MapPanel
{
    bool _addingPin;
    Button _addPin = null!;
    UiTheme.Window? _pinEditor;

    void SetAddingPin(bool active)
    {
        if (active) CancelPinEdit();
        _addingPin = active;
        _dragging = false;
        _addPin.Text = active ? "Cancel pin placement" : "Add map pin";
        _view.MouseDefaultCursorShape = active ? CursorShape.Cross : CursorShape.Arrow;
    }

    bool HandlePinInput(InputEvent e)
    {
        if (!_addingPin) return false;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            SetAddingPin(false);
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } click)
        {
            var at = _view.ScreenToWorld(click.Position);
            if (at.X >= 0 && at.Y >= 0 && at.X < _map.Extent.X && at.Y < _map.Extent.Y)
            {
                SetAddingPin(false);
                ShowPinEditor(at, null);
            }
        }
        return e is InputEventMouseButton or InputEventMouseMotion;
    }

    void EditPin(MapPin pin) => ShowPinEditor(pin.Position, pin);

    void ShowPinEditor(Vector2 at, MapPin? pin)
    {
        CancelPinEdit();
        uint area = _map.AreaId;
        var editor = UiTheme.Window.Create<UiTheme.Window>("Edit Pin Text", new Vector2(440, 280));
        _pinEditor = editor;
        var text = new TextEdit
        {
            Text = pin?.Note ?? "", WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 128), SizeFlagsVertical = SizeFlags.ExpandFill,
            TooltipText = "Text shown when you hover over this map pin."
        };
        editor.Body.AddChild(text);
        var buttons = Ui.HBox(Palette.Space2);
        editor.Body.AddChild(buttons);
        bool Current() => Visible && _map.AreaId == area;
        Button Button(string label, string hint, Action action, bool primary = false)
        {
            var button = new Button { Text = label, TooltipText = hint, ThemeTypeVariation = primary ? "PrimaryButton" : "Button" };
            button.Pressed += action;
            buttons.AddChild(button);
            return button;
        }
        var save = Button("Save", "Save this personal map pin.", () =>
        {
            if (!Current() || text.Text.Length > 4096) return;
            if (pin is { } existing) _map.EditPin(area, existing.Id, text.Text);
            else _map.AddPin(area, at, text.Text);
            editor.Close();
        }, primary: true);
        text.TextChanged += () => { save.Disabled = text.Text.Length > 4096; save.TooltipText = save.Disabled ? "Shorten this note to 4096 characters or fewer." : "Save this personal map pin."; };
        Button("Cancel", "Discard changes.", editor.Close);
        if (pin is { } removable)
            Button("Delete", "Delete this personal map pin.", () =>
            {
                if (Current()) _map.RemovePin(area, removable.Id);
                editor.Close();
            });
        editor.Closed += () => { if (_pinEditor == editor) _pinEditor = null; editor.QueueFree(); };
        GetParent().AddChild(editor);
        editor.Position = (GetViewportRect().Size - editor.Size) / 2;
        editor.Visible = true;
        text.GrabFocus();
    }

    void CancelPinEdit()
    {
        if (GodotObject.IsInstanceValid(_pinEditor)) _pinEditor!.Close();
        _pinEditor = null;
    }

    void OnMapAreaLoaded()
    {
        _recentre = true;
        SetAddingPin(false);
        CancelPinEdit();
    }

    public override void Close()
    {
        SetAddingPin(false);
        CancelPinEdit();
        base.Close();
    }
}
