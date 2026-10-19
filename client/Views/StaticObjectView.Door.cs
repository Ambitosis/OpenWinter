using OpenWinter.Client.Rendering;
using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

public partial class StaticObjectView
{
    ModelData? _doorModel;
    Skeleton3D? _doorSkeleton;
    readonly Dictionary<int, MeshInstance3D> _doorTransitions = new();
    Transform3D[] _doorRest = [];
    bool _doorHovered;
    public bool CameraFaded { get; private set; }
    const float DoorPlaneBand = .15f;

    /// <summary>Native fades the whole door when camera and controlled actor are on opposite
    /// sides of the closed frame, even when the swung leaf is off to one side, at any distance
    /// (doorfar: 16 m to the side still fades).</summary>
    public void UpdateDoorCamera(Vector3 eye, Vector3 actor)
    {
        var inverse = GlobalTransform.AffineInverse();
        float cameraSide = (inverse * eye).Y, actorSide = (inverse * actor).Y;
        // doorfaredge: an actor up to 0.1 m onto the camera's side still fades the door, 0.2 m does not,
        // from either side; the frame counts as the far side within DoorPlaneBand.
        bool faded = cameraSide * actorSide < 0 || MathF.Abs(actorSide) < DoorPlaneBand;
        if (faded == CameraFaded) return;
        CameraFaded = faded;
        // dooralpha: native red-on-black control measures 64/255 (25%, rounded).
        SetCameraAlpha(faded ? .25f : 1f);
        if (faded) SetDoorHovered(false);
    }

    public void SetDoorHovered(bool hovered) { _doorHovered = hovered; UpdateDoorTransition(); }

    // A broken (dead) door stands open and still leads on (docs/INTER_AREA_MOVEMENT.md, "Killed doors").
    bool DoorTransitionActive => (Flags & (ObjectFlags.Open | ObjectFlags.Transition)) == (ObjectFlags.Open | ObjectFlags.Transition);

    bool IsTransitionPart(ModelPart part) => _doorModel?.Animations.GetValueOrDefault("trans")?.Tracks
        .GetValueOrDefault(part.Name) is { Alphas.Length: > 0 } track && track.Alphas.Any(a => a > 0)
        && part.Mesh is { Alpha: <= 0.01f };

    void InitDoor(ModelData model, ClientContent content)
    {
        _doorModel = model;
        _doorRest = model.RestGlobals().Select(Rendering.Convert.T).ToArray();
        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (!IsTransitionPart(part) || part.Mesh is not { } mesh) continue;
            var data = mesh.Clone();
            data.Alpha = model.Animations["trans"].Tracks[part.Name].Alphas[0];
            // The native transition overlay is unlit and uses the mesh's authored blue self illumination.
            var material = (StandardMaterial3D)content.Materials.Get(data, MaterialKind.World)!.Duplicate();
            var color = data.SelfIllumination == System.Numerics.Vector3.Zero ? data.Diffuse : data.SelfIllumination;
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            material.EmissionEnabled = false;
            material.AlbedoColor = new Color(color.X, color.Y, color.Z, data.Alpha);
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            var plane = new MeshInstance3D
            {
                Name = "fx_door_transition_" + i,
                Mesh = ModelBuilder.MeshOf(data, material),
                Transform = _doorRest[i],
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(plane);
            _doorTransitions.Add(i, plane);
        }
        UpdateDoorTransition();
    }

    void UpdateDoorTransition()
    {
        foreach (var plane in _doorTransitions.Values) plane.Visible = DoorTransitionActive && _doorHovered && !CameraFaded;
    }

    /// <summary>Pick the posed door triangles, never the skinned mesh's closed/rest bounding box.
    /// The authored trans mesh covers the opening when this is an open transition door, and stands for the whole
    /// door while it is closed.</summary>
    public float? PickDoor(Vector3 from, Vector3 direction, bool includeTransition = true)
        => PickDoor(from, direction, out _, includeTransition);

    public float? PickDoor(Vector3 from, Vector3 direction, out bool transition, bool includeTransition = true)
    {
        transition = false;
        if (_doorModel == null || CameraFaded) return null;
        float nearest = float.PositiveInfinity;
        var worldInverse = GlobalTransform.AffineInverse();
        var origin = worldInverse * from;
        var ray = worldInverse.Basis * direction;
        // A closed door is picked anywhere across its frame, between a cell door's bars too: the stock door models'
        // invisible "sam" plane covers the whole doorway (door-pick-audit: 219 of 241 door models), and it doesn't
        // move with the leaves. An open doorway stays empty space (docs/DOORS.md), so only a closed door uses it.
        bool closed = (Flags & (ObjectFlags.Open | ObjectFlags.Dead)) == 0;
        for (int i = 0; i < _doorModel.Parts.Count; i++)
        {
            var part = _doorModel.Parts[i];
            if (part.Mesh is not { Render: true, IsWalkmesh: false } mesh) continue;
            Transform3D pose;
            var plane = _doorTransitions.GetValueOrDefault(i);
            if (closed && mesh.Alpha <= 0.01f)
            {
                pose = _doorRest[i];
                plane = null; // the closed door itself, not its transition
            }
            else if (plane != null)
            {
                if (!includeTransition || !DoorTransitionActive) continue;
                pose = plane.Transform;
            }
            else
            {
                if (!ModelBuilder.Drawable(mesh) || mesh.Alpha <= 0.01f) continue;
                pose = _doorSkeleton?.GetBoneGlobalPose(i) ?? _doorRest[i];
            }
            var inverse = pose.AffineInverse();
            var localOrigin = inverse * origin;
            var localRay = inverse.Basis * ray;
            for (int f = 0; f + 2 < mesh.Indices.Length; f += 3)
            {
                var a = Rendering.Convert.V(mesh.Vertices[mesh.Indices[f]]);
                var b = Rendering.Convert.V(mesh.Vertices[mesh.Indices[f + 1]]);
                var c = Rendering.Convert.V(mesh.Vertices[mesh.Indices[f + 2]]);
                // Two-sided Moller-Trumbore. t stays in world-ray units under these transforms.
                var e1 = b - a; var e2 = c - a;
                var p = localRay.Cross(e2);
                float det = e1.Dot(p);
                if (Math.Abs(det) < 1e-7f) continue;
                var s = localOrigin - a;
                float u = s.Dot(p) / det;
                if (u < 0 || u > 1) continue;
                var q = s.Cross(e1);
                float v = localRay.Dot(q) / det;
                if (v < 0 || u + v > 1) continue;
                float t = e2.Dot(q) / det;
                if (t >= 0 && t < nearest) { nearest = t; transition = plane != null; }
            }
        }
        return float.IsFinite(nearest) ? nearest : null;
    }
}
