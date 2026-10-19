using Godot;

namespace OpenWinter.Client.Gameplay;

public partial class Interaction
{
    double _leftDownAt;
    bool _mouseDriving;
    bool _leftReleased;
    Vector3? _leftGround;
    public Vector3? MouseDriveTarget { get; private set; }
    public bool MouseDriveWalk { get; private set; }
    public int? DriveCursor { get; private set; }

    void UpdateMouseDriving()
    {
        MouseDriveTarget = null;
        DriveCursor = null;
        if (_leftReleased) { _leftReleased = false; _pressed = _mouseDriving = false; }
        if (Targeting != null || RadialOpen?.Invoke() == true) { _mouseDriving = false; return; }
        if (!_pressed || Suspended || PlayerInput.Blocked || UI.DM.ChatInput.Typing
            || Session.Controlled is not { } pc || (pc.Flags & (OpenWinter.Net.ObjectFlags.Dead | OpenWinter.Net.ObjectFlags.Dying)) != 0)
        { _pressed = _mouseDriving = false; return; }
        bool held = _clock - _leftDownAt >= MouseDriving.HoldSeconds;
        // A quick click replaces the destination on release. Entering driving before
        // the hold threshold would cancel that route and switch animation states.
        if (!held) return;
        _mouseDriving = true;
        var at = PointerOverride ?? GetViewport().GetMousePosition();
        if (GetViewport().GuiGetHoveredControl() != null && PointerOverride == null) return;
        if (!GroundWalkable || GroundPosition is not { } target) { DriveCursor = UI.HudParts.Cursors.Original.NoWalk; return; }
        var offset = at - Camera.UnprojectPosition(pc.GlobalPosition);
        MouseDriveWalk = PlayerInput.WalkHeld;
        bool shortArrow = new Vector2(target.X - pc.Position.X, target.Y - pc.Position.Y).Length() < MouseDriving.LongArrowDistance;
        DriveCursor = MouseDriving.Cursor(offset.X, offset.Y, shortArrow);
        MouseDriveTarget = target;
    }

    // A release over a UI control must stop steering even when that control consumes the event.
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _leftReleased = true;
            // Physics can run before the next _Process; a stale target would resend driving and clear the click's move.
            MouseDriveTarget = null;
            if (_mouseDriving)
            {
                _pressed = _mouseDriving = false;
                MouseDriveTarget = null;
                DriveCursor = null;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMWindowFocusOut) return;
        _pressed = _mouseDriving = false;
        MouseDriveTarget = null;
        DriveCursor = null;
    }
}
