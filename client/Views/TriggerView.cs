using System.Text.Json;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// An area transition trigger: an invisible polygon on the ground that lights up while the pointer is over it, as in
/// the original (a translucent blue-violet fill laid over the walkable ground, ramps included).
/// </summary>
public partial class TriggerView : ObjectView
{
    /// <summary>The polygon's corners relative to the trigger's position, in area (Z-up) space.</summary>
    Vector2[] _points = [];
    System.Numerics.Vector2[] _flat = [];
    float[] _heights = [];
    MeshInstance3D? _fill;
    bool _hovered;

    /// <summary>Half way to blue-violet, blended in linear space: the original's fill over sandstone tiles.</summary>
    static readonly Color FillColor = new(0.27f, 0.26f, 0.86f, 0.5f);
    /// <summary>Longest edge of the draped fill's triangles, in metres.</summary>
    const float Step = 0.75f;
    const float Lift = 0.04f;

    public static TriggerView Create(SpawnMessage spawn, IReadOnlyList<Vector3>? walkTriangles)
    {
        var view = new TriggerView();
        view.Init(spawn);
        view.Rotation = Vector3.Zero;
        if (spawn.Extra.Length > 0)
        {
            using var doc = JsonDocument.Parse(spawn.Extra);
            if (doc.RootElement.TryGetProperty("points", out var points))
            {
                var p = points.EnumerateArray().Select(e => e.EnumerateArray().Select(n => n.GetSingle()).ToArray()).ToArray();
                view._points = p.Select(v => new Vector2(v[0], v[1])).ToArray();
                view._flat = p.Select(v => new System.Numerics.Vector2(v[0], v[1])).ToArray();
                view._heights = p.Select(v => v.Length > 2 ? v[2] : 0).ToArray();
            }
        }
        view.BuildFill(walkTriangles);
        return view;
    }

    public void SetHovered(bool hovered)
    {
        _hovered = hovered;
        if (_fill != null) _fill.Visible = hovered;
    }

    public bool Hovered => _hovered;

    /// <summary>Whether a point in area space lies inside the polygon (seen from above).</summary>
    public bool Contains(Vector3 areaPoint) => _points.Length >= 3
        && Gameplay.TriggerPolygon.Contains(_flat, new System.Numerics.Vector2(areaPoint.X - Position.X, areaPoint.Y - Position.Y));

    void BuildFill(IReadOnlyList<Vector3>? walk)
    {
        if (_points.Length < 3) return;
        float baseZ = Position.Z + _heights.Average();
        // The walkable triangles under the polygon, in its local space.
        var min = _points.Aggregate((a, b) => a.Min(b)) + new Vector2(Position.X, Position.Y) - Vector2.One;
        var max = _points.Aggregate((a, b) => a.Max(b)) + new Vector2(Position.X, Position.Y) + Vector2.One;
        var ground = new List<(Vector3 A, Vector3 B, Vector3 C)>();
        if (walk != null)
            for (int i = 0; i + 2 < walk.Count; i += 3)
            {
                Vector3 a = walk[i], b = walk[i + 1], c = walk[i + 2];
                if (Math.Max(a.X, Math.Max(b.X, c.X)) < min.X || Math.Min(a.X, Math.Min(b.X, c.X)) > max.X
                    || Math.Max(a.Y, Math.Max(b.Y, c.Y)) < min.Y || Math.Min(a.Y, Math.Min(b.Y, c.Y)) > max.Y) continue;
                ground.Add((a - Position, b - Position, c - Position));
            }
        float local = baseZ - Position.Z;
        // The walkable height under a point nearest the polygon's own; the polygon's height off the walkmesh.
        float Height(Vector2 q)
        {
            float best = local, bestGap = float.MaxValue;
            foreach (var (a, b, c) in ground)
            {
                var v0 = new Vector2(b.X - a.X, b.Y - a.Y);
                var v1 = new Vector2(c.X - a.X, c.Y - a.Y);
                var v2 = new Vector2(q.X - a.X, q.Y - a.Y);
                float d = v0.X * v1.Y - v1.X * v0.Y;
                if (Math.Abs(d) < 1e-8f) continue;
                float u = (v2.X * v1.Y - v1.X * v2.Y) / d, w = (v0.X * v2.Y - v2.X * v0.Y) / d;
                if (u < -1e-4f || w < -1e-4f || u + w > 1 + 1e-4f) continue;
                float z = a.Z + u * (b.Z - a.Z) + w * (c.Z - a.Z);
                if (Math.Abs(z - local) < bestGap) { bestGap = Math.Abs(z - local); best = z; }
            }
            return best + Lift;
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        static Vector2 G(System.Numerics.Vector2 v) => new(v.X, v.Y);
        foreach (var (a, b, c) in Gameplay.TriggerPolygon.Triangulate(_flat))
            Subdivide(st, G(a), G(b), G(c), Height, 0);
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = FillColor,
            NoDepthTest = false,
        };
        _fill = new MeshInstance3D
        {
            Name = "fx_trigger_fill",
            Mesh = st.Commit(),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(_fill);
    }

    static void Subdivide(SurfaceTool st, Vector2 a, Vector2 b, Vector2 c, Func<Vector2, float> height, int depth)
    {
        if (depth < 6 && Math.Max(a.DistanceTo(b), Math.Max(b.DistanceTo(c), c.DistanceTo(a))) > Step)
        {
            Vector2 ab = (a + b) / 2, bc = (b + c) / 2, ca = (c + a) / 2;
            Subdivide(st, a, ab, ca, height, depth + 1);
            Subdivide(st, ab, b, bc, height, depth + 1);
            Subdivide(st, ca, bc, c, height, depth + 1);
            Subdivide(st, ab, bc, ca, height, depth + 1);
            return;
        }
        foreach (var p in new[] { a, b, c }) st.AddVertex(new Vector3(p.X, p.Y, height(p)));
    }
}
