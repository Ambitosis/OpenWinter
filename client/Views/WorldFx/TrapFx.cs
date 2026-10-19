using System.Text.Json;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// Traps the player sees (the server says which: detected, flagged, or the party's own): a trap trigger is the
/// ground inside its polygon tinted, red for a hostile trap, green for one the party set (traplook a_top: the ground
/// texture keeps its detail, as if multiplied, about x2.1 red / x0.7 green / x0.8 blue over grass; green x0.8 / x2.25 /
/// x0.9). The polygon comes with the trap-detected notify, since triggers aren't sent to clients. Trapped doors and
/// placeables get a red tint. Disarming removes the marking. <see cref="At"/> finds the trap trigger under a ground
/// point for the radial menu.
/// </summary>
public partial class TrapFx : Node
{
    public ClientSession Session = null!;
    public static readonly Color TrapColor = new(1f, 0.12f, 0.08f);
    /// <summary>
    /// The ground multipliers measured from the original's screenshots (hostile and own traps), in display space; the
    /// shader multiplies in linear light, so they go in raised to the power 2.2.
    /// </summary>
    public static readonly Vector3 HostileTint = Linear(2.1f, 0.72f, 0.8f), OwnTint = Linear(0.82f, 2.25f, 0.9f);

    static Vector3 Linear(float r, float g, float b) => new(MathF.Pow(r, 2.2f), MathF.Pow(g, 2.2f), MathF.Pow(b, 2.2f));

    /// <summary>A trap trigger being shown: its polygon (area XY) and what its radial menu needs.</summary>
    public sealed record Shown(uint Id, Vector2[] Polygon, float Z, string Name, bool Own, bool Disarmable, bool Recoverable);

    readonly Dictionary<uint, (MeshInstance3D Mesh, Shown Info)> _areas = new();
    readonly HashSet<uint> _objects = new();
    ShaderMaterial? _tint;
    static Shader? _tintShader, _groundShader;

    /// <summary>Detected traps being shown (capture checks).</summary>
    public int Count => _areas.Count + _objects.Count;

    /// <summary>The trap triggers being shown.</summary>
    public IEnumerable<Shown> Triggers => _areas.Values.Select(a => a.Info);

    public void Clear()
    {
        foreach (var a in _areas.Values) if (IsInstanceValid(a.Mesh)) a.Mesh.QueueFree();
        _areas.Clear();
        foreach (var id in _objects) if (Session.Objects.GetValueOrDefault(id) is { } v) Tint(v, false);
        _objects.Clear();
    }

    /// <summary>The shown trap trigger whose polygon holds an area point (the ground under the pointer), if any.</summary>
    public Shown? At(System.Numerics.Vector3 point)
    {
        var p = new Vector2(point.X, point.Y);
        foreach (var (_, info) in _areas.Values)
            if (Math.Abs(point.Z - info.Z) < 3f && Geometry2D.IsPointInPolygon(p, info.Polygon)) return info;
        return null;
    }

    public void Handle(string kind, uint subject, JsonElement d)
    {
        switch (kind)
        {
            case "trap-detected":
                if (J.B(d, "detected", true)) Show(subject, d);
                else Hide(subject);
                break;
            case "trap-disarmed":
                Hide(subject);
                break;
        }
    }

    void Show(uint id, JsonElement d)
    {
        if (d.TryGetProperty("geometry", out var g) && g.ValueKind == JsonValueKind.Array && g.GetArrayLength() >= 3)
        {
            var points = g.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Array && p.GetArrayLength() >= 2)
                .Select(p => new Vector3(p[0].GetSingle(), p[1].GetSingle(), p.GetArrayLength() > 2 ? p[2].GetSingle() : 0)).ToList();
            Hide(id);
            bool own = J.B(d, "own", false);
            var info = new Shown(id, points.Select(p => new Vector2(p.X, p.Y)).ToArray(), points.Average(p => p.Z),
                d.TryGetProperty("name", out var n) ? n.GetString() ?? "Trap" : "Trap", own, J.B(d, "disarmable", true), J.B(d, "recoverable", true));
            var mesh = Ground(points, own ? OwnTint : HostileTint);
            Session.Space.AddChild(mesh);
            _areas[id] = (mesh, info);
            return;
        }
        // Detection can arrive before the same-tick object spawn is replicated.
        // Retain its ID; Process applies the tint when the view/model becomes available.
        _objects.Add(id);
    }

    void Hide(uint id)
    {
        if (_areas.Remove(id, out var a) && IsInstanceValid(a.Mesh)) a.Mesh.QueueFree();
        if (_objects.Remove(id) && Session.Objects.GetValueOrDefault(id) is { } v) Tint(v, false);
    }

    public override void _Process(double delta)
    {
        // Keep trapped objects tinted (the mouse-over highlight shares the overlay slot).
        foreach (var id in _objects)
            if (Session.Objects.GetValueOrDefault(id) is { } v) Tint(v, true);
    }

    void Tint(ObjectView view, bool on)
    {
        _tintShader ??= new Shader { Code = TintShader };
        _tint ??= new ShaderMaterial { Shader = _tintShader };
        foreach (var mi in view.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (on && mi.MaterialOverlay == null) mi.MaterialOverlay = _tint;
            else if (!on && mi.MaterialOverlay == _tint) mi.MaterialOverlay = null;
        }
    }

    /// <summary>
    /// A box over the trap polygon (area space) that multiplies whatever ground lies inside it, polygon-masked: the
    /// scene depth gives each pixel's position, so slopes, grass and the tile under the trap all take the tint while
    /// a canopy above it doesn't.
    /// </summary>
    static MeshInstance3D Ground(List<Vector3> polygon, Vector3 tint)
    {
        float minX = polygon.Min(p => p.X), maxX = polygon.Max(p => p.X), minY = polygon.Min(p => p.Y), maxY = polygon.Max(p => p.Y);
        float w = Math.Max(0.5f, maxX - minX), h = Math.Max(0.5f, maxY - minY);
        float ppm = Math.Clamp(512f / Math.Max(w, h), 4f, 32f);
        int iw = Math.Clamp((int)MathF.Ceiling(w * ppm), 8, 1024), ih = Math.Clamp((int)MathF.Ceiling(h * ppm), 8, 1024);
        var image = Image.CreateEmpty(iw, ih, false, Image.Format.L8);
        var poly = polygon.Select(p => new Vector2(p.X, p.Y)).ToArray();
        for (int py = 0; py < ih; py++)
            for (int px = 0; px < iw; px++)
            {
                // Rows run from the polygon's -Y edge, matching the shader's v = local y / height + 0.5.
                var at = new Vector2(minX + (px + 0.5f) * w / iw, minY + (py + 0.5f) * h / ih);
                if (Geometry2D.IsPointInPolygon(at, poly)) image.SetPixel(px, py, Colors.White);
            }
        float z = polygon.Average(p => p.Z);
        const float below = 1.0f, above = 0.6f;
        _groundShader ??= new Shader { Code = GroundShader };
        var material = new ShaderMaterial { Shader = _groundShader };
        material.SetShaderParameter("mask", ImageTexture.CreateFromImage(image));
        material.SetShaderParameter("half_size", new Vector3(w / 2, h / 2, (below + above) / 2));
        material.SetShaderParameter("tint", tint);
        return new MeshInstance3D
        {
            Name = "TrapGround",
            Mesh = new BoxMesh { Size = new Vector3(w, h, below + above) },
            MaterialOverride = material,
            Position = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, z + (above - below) / 2),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Big enough to always draw: the box's own bounds are tiny next to what it tints.
            ExtraCullMargin = 4f,
        };
    }

    const string GroundShader = @"
shader_type spatial;
render_mode unshaded, blend_mul, depth_draw_never, depth_test_disabled, cull_front, shadows_disabled, fog_disabled;
uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D mask : filter_linear, repeat_disable;
uniform vec3 half_size;
uniform vec3 tint = vec3(2.1, 0.72, 0.8);
void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    view.xyz /= view.w;
    vec4 world = INV_VIEW_MATRIX * vec4(view.xyz, 1.0);
    vec3 local = (inverse(MODEL_MATRIX) * world).xyz;
    if (any(greaterThan(abs(local), half_size))) discard;
    if (texture(mask, local.xy / (2.0 * half_size.xy) + 0.5).r < 0.5) discard;
    ALBEDO = tint;
}
";

    const string TintShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;
void fragment() {
    float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 1.5);
    ALBEDO = vec3(0.7, 0.05, 0.02) * (0.35 + rim);
}
";
}
