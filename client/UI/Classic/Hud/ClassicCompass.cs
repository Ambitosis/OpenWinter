using Godot;
using SMatrix = System.Numerics.Matrix4x4;
using SQuat = System.Numerics.Quaternion;
using SVector3 = System.Numerics.Vector3;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's compass, bottom right above the talk line: its frame (pnl_compass) and the compass model
/// (ctl_compass) at the frame's compass node. The original shows the compass as an object lying on the ground:
/// tilted back by the camera's angle from the vertical (so it flattens as the camera lowers), its rose
/// (cmp_pointer, the angle animation's node) turned with the camera so N points north, with the frame's slight
/// "default" wobble. The map opens through M or the portrait map button.
/// </summary>
public partial class ClassicCompass : Control
{
    public ClassicContext Ctx = null!;
    GuiModelView _frame = null!;
    GuiModel? _compass;
    Control _dial = null!;
    float _angle, _tilt, _time;

    /// <summary>
    /// How much of the camera's angle from the vertical the compass takes: matched to the reference capture
    /// (the original's dial there is 0.58 as tall as wide where the camera looks 69.5° from the vertical).
    /// </summary>
    const float TiltScale = 0.78f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        // This HUD region is not a map button; intercept clicks without sending world commands.
        _frame = GuiModelView.Create(Ctx.Assets, "pnl_compass");
        AddChild(_frame);
        Size = _frame.Size;
        _compass = Ctx.Assets.Model("ctl_compass");
        // Drawn over the frame (children draw over their parent).
        _dial = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = Size, ClipContents = true };
        _dial.Draw += DrawDial;
        AddChild(_dial);
    }

    /// <summary>Bottom right: the frame's bottom-left origin on the talk line's top.</summary>
    public void Place(Vector2 view, float bottom)
    {
        Position = new Vector2(view.X - Size.X, bottom - _frame.Origin.Y);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        // As the modern minimap: the angle that puts north where it is on screen; and the camera's angle from the vertical.
        if (CameraForward() is { } f)
        {
            var flat = new Vector2(f.X, f.Y);
            if (flat.LengthSquared() > 1e-6f) { flat = flat.Normalized(); _angle = -MathF.PI / 2 - MathF.Atan2(-flat.Y, flat.X); }
            _tilt = MathF.Acos(Math.Clamp(-f.Z, -1, 1)) * TiltScale;
        }
        _dial.QueueRedraw();
    }

    /// <summary>The camera's forward direction in area coordinates (z up).</summary>
    Vector3? CameraForward()
    {
        var cam = GetViewport()?.GetCamera3D();
        var space = Ctx.Session.Space;
        if (cam == null || space == null || !IsInstanceValid(space)) return null;
        return (space.GlobalTransform.Basis.Inverse() * -cam.GlobalTransform.Basis.Z).Normalized();
    }

    void DrawDial()
    {
        if (_compass == null) return;
        var at = _frame.Node("compass") ?? Size / 2;
        var hover = GetGlobalRect().HasPoint(GetGlobalMousePosition()) ? GuiDraw.Hilite : Colors.White;
        GuiDraw.Model(_dial, Ctx.Assets, Pose(), at, hover);
    }

    /// <summary>The compass model posed in 3D (rose turned, whole model tilted and wobbling) and projected flat.</summary>
    GuiModel.Pose Pose()
    {
        var data = _compass!.Data;
        var parts = data.Parts;
        var tilt = SMatrix.CreateRotationX(-Math.Clamp(_tilt, 0, 1.45f));
        if (_frame.Model?.Data.Animations.GetValueOrDefault("default") is { } wobble && wobble.Tracks.TryGetValue("compass", out var track) && track.Rotations.Length > 1)
            tilt = SMatrix.CreateFromQuaternion(Sample(track.RotationTimes, track.Rotations, wobble.Length > 0 ? _time % wobble.Length : 0)) * tilt;
        var globals = new SMatrix[parts.Count];
        var meshes = new List<GuiModel.PosedMesh>();
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var local = SMatrix.CreateFromQuaternion(p.Rotation) * SMatrix.CreateTranslation(p.Position);
            if (p.Name.Equals("cmp_pointer", StringComparison.OrdinalIgnoreCase)) local = SMatrix.CreateRotationZ(-_angle) * local;
            globals[i] = p.Parent < 0 ? local : local * globals[p.Parent];
            if (p.Mesh is not { Vertices.Length: > 0, Indices.Length: > 0 } mesh || !mesh.Render || mesh.IsWalkmesh) continue;
            var world = globals[i] * tilt;
            var points = new Vector2[mesh.Vertices.Length];
            float z = 0;
            for (int v = 0; v < points.Length; v++)
            {
                var w = SVector3.Transform(mesh.Vertices[v], world);
                points[v] = new Vector2(w.X, -w.Y) * GuiModel.PixelsPerUnit;
                z += w.Z;
            }
            var uvs = new Vector2[points.Length];
            for (int v = 0; v < uvs.Length && v < mesh.Uvs.Length; v++) uvs[v] = new Vector2(mesh.Uvs[v].X, mesh.Uvs[v].Y);
            meshes.Add(new GuiModel.PosedMesh(i, p.Name, mesh.Texture, points, uvs, mesh.Indices, z / points.Length, Math.Clamp(mesh.Alpha, 0, 1)));
        }
        return new GuiModel.Pose(meshes.OrderBy(m => m.Z).ToArray(), new Dictionary<string, Vector2>(), new Rect2());
    }

    static SQuat Sample(float[] times, SQuat[] values, float t)
    {
        int i = 0;
        while (i + 1 < times.Length && times[i + 1] <= t) i++;
        if (i + 1 >= values.Length) return values[^1];
        float span = times[i + 1] - times[i];
        return SQuat.Slerp(values[i], values[i + 1], span > 0 ? Math.Clamp((t - times[i]) / span, 0, 1) : 0);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent();
        }
    }
}
