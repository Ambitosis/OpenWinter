using OpenWinter.Client.Preferences;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// Third-person camera orbiting a target, with the original's three modes (<see cref="CameraMode"/>, toggled with
/// Num *): top down stays where the player turns it; chase swings round behind the player as they move; driving
/// stays right behind them at a fixed distance. Middle-drag and the arrow keys turn
/// it; wheel, Up / Down and +/- zoom; Page Up / Down tilt; Home / End zoom fully in / out; Insert / Delete tilt fully
/// up / down. Speed and invert-Y come from Options, Gameplay.
/// </summary>
public partial class OrbitCamera : Camera3D
{
    public float Distance = 9f, MinDistance = 2.5f, MaxDistance = 30f;
    public float Pitch = Mathf.DegToRad(-50f);
    public float Yaw;
    public float HeightOffset = DefaultHeight;
    /// <summary>
    /// Measured against the original client (parity/visual, scene calib): a 45 degree vertical field of view
    /// (wider screens see more to the sides, not less above and below), looking at a point about a metre
    /// above the player's feet.
    /// </summary>
    public const float DefaultFov = 45f, DefaultHeight = 1.0f;

    public OrbitCamera() { Fov = DefaultFov; }
    public Node3D? Target;
    /// <summary>LockCameraPitch / LockCameraDistance / LockCameraDirection: the player can't change these.</summary>
    public bool LockPitch, LockDistance, LockYaw;
    /// <summary>The camera mode: the player's setting, or a script's (SetCameraMode) until the setting next changes.</summary>
    public CameraMode Mode = CameraMode.TopDown;
    /// <summary>Pitch limits (radians below the horizon): highest overhead and lowest.</summary>
    public static readonly float HighestPitch = -Mathf.Pi / 2, LowestPitch = Mathf.DegToRad(-5);
    /// <summary>The driving camera stays above this pitch (the original allows 62° from straight down).</summary>
    static readonly float DrivingLowestPitch = Mathf.DegToRad(-28);
    bool _dragging;
    /// <summary>Seconds since the player last turned the camera by hand; the chase cameras wait a moment after.</summary>
    double _sinceManualTurn = 10;
    Vector3 _lastTargetPos;
    float _armDistance;

    public override void _Ready()
    {
        Mode = CameraModes.FromInt(Settings.Current.Gameplay.CameraMode);
        Settings.Changed += OnSettings;
    }

    public override void _ExitTree() => Settings.Changed -= OnSettings;

    void OnSettings(GameSettings s) => Mode = CameraModes.FromInt(s.Gameplay.CameraMode);

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            // Middle-button drag orbits; right-button drag selects a radial command.
            if (mb.ButtonIndex == MouseButton.Middle) _dragging = mb.Pressed;
            else if (LockDistance || Mode == CameraMode.Driving) { }
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) Distance = Math.Max(MinDistance, Distance * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) Distance = Math.Min(MaxDistance, Distance * 1.1f);
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            // The release can land on a window and never reach here: stop once neither button is down.
            if (!Input.IsMouseButtonPressed(MouseButton.Middle)) { _dragging = false; return; }
            var g = Settings.Current.Gameplay;
            if (!LockYaw && mm.Relative.X != 0) { Yaw -= mm.Relative.X * 0.006f * g.CameraSensitivity; _sinceManualTurn = 0; }
            if (!LockPitch) Pitch = Math.Clamp(Pitch - mm.Relative.Y * 0.004f * g.CameraSensitivity * (g.InvertY ? -1 : 1), HighestPitch, LowestPitch);
        }
        else if (e is InputEventKey { Pressed: true, Echo: false } && !UI.DM.ChatInput.Typing && !PlayerInput.Blocked)
        {
            bool P(string action) => InputBindings.Pressed(e, action);
            if (P(ActionCatalog.ZoomInFull)) { if (!LockDistance) Distance = MinDistance; }
            else if (P(ActionCatalog.ZoomOutFull)) { if (!LockDistance) Distance = MaxDistance; }
            else if (P(ActionCatalog.CameraUpFull)) { if (!LockPitch) Pitch = HighestPitch; }
            else if (P(ActionCatalog.CameraDownFull)) { if (!LockPitch) Pitch = LowestPitch; }
            else return;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _sinceManualTurn += delta;
        if (!UI.DM.ChatInput.Typing && !PlayerInput.Blocked)
        {
            float speed = Settings.Current.Gameplay.CameraKeySpeed * (float)delta;
            float turn = (Input.IsActionPressed(ActionCatalog.CameraLeft) ? 1 : 0)
                - (Input.IsActionPressed(ActionCatalog.CameraRight) ? 1 : 0);
            if (!LockYaw && turn != 0) { Yaw += 1.8f * speed * turn; _sinceManualTurn = 0; }
            if (!LockPitch && Input.IsActionPressed(ActionCatalog.CameraUp)) Pitch = Math.Max(HighestPitch, Pitch - 1.0f * speed);
            if (!LockPitch && Input.IsActionPressed(ActionCatalog.CameraDown)) Pitch = Math.Min(LowestPitch, Pitch + 1.0f * speed);
            if (!LockDistance && Input.IsActionPressed(ActionCatalog.ZoomIn)) Distance = Math.Max(MinDistance, Distance * (1 - 1.5f * speed));
            if (!LockDistance && Input.IsActionPressed(ActionCatalog.ZoomOut)) Distance = Math.Min(MaxDistance, Distance * (1 + 1.5f * speed));
        }
        if (Target == null || !IsInstanceValid(Target)) { Rendering.Keyhole.Update(this, default, default, false); return; }
        Follow((float)delta);
        var focus = Target.GlobalPosition + Vector3.Up * HeightOffset;
        var wanted = focus + Offset();
        var desired = Settings.Current.Gameplay.CameraCollision ? Unblocked(focus, wanted) : wanted;
        // Ease only the arm length. Interpolating world positions also eased the orbit and cut
        // across its circle, so turning lagged behind input and kept drifting after release.
        float distance = desired.DistanceTo(focus);
        _armDistance = distance < _armDistance ? distance
            : Mathf.Lerp(_armDistance, distance, Math.Clamp(8f * (float)delta, 0, 1));
        GlobalPosition = focus + (wanted - focus).Normalized() * _armDistance;
        GlobalBasis = OrbitBasis();
        Rendering.Keyhole.Update(this, Target.GlobalPosition, focus, Settings.Current.Graphics.Keyholing);
    }

    /// <summary>Where the camera sits relative to the focus; the driving camera keeps its own distance and a higher pitch.</summary>
    Vector3 Offset()
    {
        bool driving = Mode == CameraMode.Driving;
        float distance = driving && !LockDistance ? CameraModes.DrivingDistance : Distance;
        return OrbitBasis() * new Vector3(0, 0, distance);
    }

    // Keep yaw defined even at the pole, where LookAt's world-up vector is parallel to the view.
    Basis OrbitBasis() => Basis.FromEuler(new Vector3(Mode == CameraMode.Driving ? Math.Min(Pitch, DrivingLowestPitch) : Pitch, Yaw, 0));

    /// <summary>
    /// The chase cameras swing round behind the player while they move forward (by clicking, or the forward key).
    /// Not while they back up or sidestep with the keys, nor just after the player turned the camera by hand.
    /// </summary>
    void Follow(float delta)
    {
        var pos = Target!.GlobalPosition;
        bool moving = delta > 0 && pos.DistanceTo(_lastTargetPos) / delta > 0.5f;
        _lastTargetPos = pos;
        if (Mode == CameraMode.TopDown || LockYaw || !moving || _sinceManualTurn < 0.35) return;
        var keys = PlayerInput.KeyInput;
        if (keys.X != 0 || keys.Y < 0) return;
        // Models face their local +Y.
        var forward = Target.GlobalBasis * Vector3.Up;
        forward.Y = 0;
        if (forward.LengthSquared() < 1e-4f) return;
        forward = forward.Normalized();
        float behind = MathF.Atan2(-forward.X, -forward.Z);
        float gap = Math.Abs(CameraModes.Wrap(behind - Yaw));
        float rate = Mode == CameraMode.Driving ? 10f : 2.5f;
        Yaw = CameraModes.TurnToward(Yaw, behind, Math.Max(0.4f * delta, gap * Math.Min(1, rate * delta)));
    }

    /// <summary>Spring-arm behaviour: stop short of static geometry between the focus and the camera.</summary>
    Vector3 Unblocked(Vector3 focus, Vector3 wanted)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return wanted;
        var query = PhysicsRayQueryParameters3D.Create(focus, wanted, Views.AreaView.CameraLayer);
        // Only surfaces facing the focus block the camera: the black caps over the tops of walls face
        // up, so a camera rising out of a room passes them from below (as it does through the upper
        // walls that fade away) instead of being pulled down to the player's back.
        query.HitBackFaces = false;
        var hit = space.IntersectRay(query);
        if (hit.Count == 0) return wanted;
        var at = hit["position"].AsVector3();
        var dir = (wanted - focus).Normalized();
        return focus + dir * Math.Max(0.6f, focus.DistanceTo(at) - 0.35f);
    }

    public void Snap()
    {
        if (Target == null) return;
        var focus = Target.GlobalPosition + Vector3.Up * HeightOffset;
        var offset = Offset();
        _armDistance = offset.Length();
        GlobalPosition = focus + offset;
        _lastTargetPos = Target.GlobalPosition;
        GlobalBasis = OrbitBasis();
        Rendering.Keyhole.Update(this, Target.GlobalPosition, focus, Settings.Current.Graphics.Keyholing);
    }
}
