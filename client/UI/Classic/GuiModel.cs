using OpenWinter.Content;
using Godot;
using SVector3 = System.Numerics.Vector3;
using SMatrix = System.Numerics.Matrix4x4;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// An interface model of the original game (pnl_*, ctl_*, pb_*, gui_* ...) prepared for 2D drawing. These models
/// are flat textured meshes in interface units (1 unit = 100 px at 1x), y up; dummy nodes mark anchor points
/// (text0/text1 text boxes, icon, scrollmin/scrollmax, slots ...). Animations (up, down, hilite, disabled,
/// checkedup ...) move nodes and switch mesh faces on and off through their alpha.
/// Pixel space here is y down with the model's origin at (0, 0).
/// </summary>
public sealed class GuiModel
{
    public const float PixelsPerUnit = 100f;

    public readonly string Name;
    public readonly ModelData Data;
    readonly SMatrix[] _rest;
    readonly Dictionary<string, Pose> _poses = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Union of the rendered meshes at rest, in pixels.</summary>
    public Rect2 Bounds { get; }

    public GuiModel(ModelData data)
    {
        Name = data.Name;
        Data = data;
        _rest = data.RestGlobals();
        Bounds = Rest.Bounds;
    }

    /// <summary>The pose at rest (no animation).</summary>
    public Pose Rest => _rest_ ??= Build(null, 0);
    Pose? _rest_;

    /// <summary>The pose an animation gives at a time (seconds, looped); the rest pose when the model has no such animation.</summary>
    public Pose PoseOf(string? animation, float time = 0)
    {
        if (string.IsNullOrEmpty(animation) || !Data.Animations.TryGetValue(animation, out var anim)) return Rest;
        // States are held poses: cache their first frame; animated (pulsing) states sample by time in 1/20 s steps.
        float t = anim.Length > 0 && time > 0 ? time % anim.Length : 0;
        int step = (int)(t * 20);
        string key = $"{animation}|{step}";
        if (_poses.TryGetValue(key, out var p)) return p;
        return _poses[key] = Build(anim, step / 20f);
    }

    public bool HasAnimation(string name) => Data.Animations.ContainsKey(name);

    /// <summary>A node's position in pixels at rest (null when the model has no such node).</summary>
    public Vector2? Node(string name) => Rest.Node(name);

    Pose Build(ModelAnimation? anim, float time)
    {
        var parts = Data.Parts;
        var globals = new SMatrix[parts.Count];
        var alpha = new float[parts.Count];
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var pos = p.Position;
            var rot = p.Rotation;
            alpha[i] = p.Mesh?.Alpha ?? 1;
            if (anim != null && anim.Tracks.TryGetValue(p.Name, out var track))
            {
                if (track.Positions.Length > 0) pos = Sample(track.PositionTimes, track.Positions, time);
                if (track.Rotations.Length > 0) rot = track.Rotations[Index(track.RotationTimes, time)];
                if (track.Alphas.Length > 0) alpha[i] = SampleF(track.AlphaTimes, track.Alphas, time);
            }
            var local = SMatrix.CreateFromQuaternion(rot) * SMatrix.CreateTranslation(pos);
            globals[i] = p.Parent < 0 ? local : local * globals[p.Parent];
            // A hidden parent hides its children's faces too.
            if (p.Parent >= 0 && alpha[p.Parent] <= 0 && parts[p.Parent].Mesh != null) alpha[i] = 0;
        }
        var meshes = new List<PosedMesh>();
        var nodes = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        Rect2? bounds = null;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var origin = SVector3.Transform(SVector3.Zero, globals[i]);
            nodes.TryAdd(p.Name, ToPixels(origin));
            if (p.Mesh is not { Vertices.Length: > 0, Indices.Length: > 0 } mesh || !mesh.Render || mesh.IsWalkmesh) continue;
            var points = new Vector2[mesh.Vertices.Length];
            float z = 0;
            for (int v = 0; v < points.Length; v++)
            {
                var w = SVector3.Transform(mesh.Vertices[v], globals[i]);
                points[v] = ToPixels(w);
                z += w.Z;
            }
            z /= points.Length;
            var uvs = new Vector2[points.Length];
            for (int v = 0; v < uvs.Length && v < mesh.Uvs.Length; v++) uvs[v] = new Vector2(mesh.Uvs[v].X, mesh.Uvs[v].Y); // as the 3D renderer: v = 0 is the top of the decoded image
            var r = new Rect2(points[0], Vector2.Zero);
            foreach (var pt in points) r = r.Expand(pt);
            if (alpha[i] > 0) bounds = bounds is { } b ? b.Merge(r) : r;
            meshes.Add(new PosedMesh(i, p.Name, mesh.Texture, points, uvs, mesh.Indices, z, Math.Clamp(alpha[i], 0, 1)));
        }
        // Painter's order: lower z first; ties keep model order.
        var ordered = meshes.Select((m, idx) => (m, idx)).OrderBy(t => t.m.Z).ThenBy(t => t.idx).Select(t => t.m).ToArray();
        return new Pose(ordered, nodes, bounds ?? new Rect2());
    }

    static Vector2 ToPixels(SVector3 v) => new(v.X * PixelsPerUnit, -v.Y * PixelsPerUnit);

    static int Index(float[] times, float t)
    {
        int i = 0;
        while (i + 1 < times.Length && times[i + 1] <= t) i++;
        return i;
    }

    static SVector3 Sample(float[] times, SVector3[] values, float t)
    {
        int i = Index(times, t);
        if (i + 1 >= values.Length || times.Length <= i + 1) return values[Math.Min(i, values.Length - 1)];
        float span = times[i + 1] - times[i];
        float f = span > 0 ? Math.Clamp((t - times[i]) / span, 0, 1) : 0;
        return SVector3.Lerp(values[i], values[i + 1], f);
    }

    static float SampleF(float[] times, float[] values, float t)
    {
        int i = Index(times, t);
        if (i + 1 >= values.Length || times.Length <= i + 1) return values[Math.Min(i, values.Length - 1)];
        float span = times[i + 1] - times[i];
        float f = span > 0 ? Math.Clamp((t - times[i]) / span, 0, 1) : 0;
        return values[i] + (values[i + 1] - values[i]) * f;
    }

    /// <summary>The model's geometry in one state: meshes in drawing order and node positions, in pixels.</summary>
    public sealed class Pose(PosedMesh[] meshes, Dictionary<string, Vector2> nodes, Rect2 bounds)
    {
        public readonly PosedMesh[] Meshes = meshes;
        public readonly Rect2 Bounds = bounds;
        public Vector2? Node(string name) => nodes.TryGetValue(name, out var p) ? p : null;
        public IReadOnlyDictionary<string, Vector2> Nodes => nodes;
    }

    public sealed record PosedMesh(int Part, string Node, string Texture, Vector2[] Points, Vector2[] Uvs, int[] Indices, float Z, float Alpha);
}
