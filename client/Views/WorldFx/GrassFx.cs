using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views.WorldFx.Logic;
using Godot;
using N = System.Numerics;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// Tileset grass: billboard tufts scattered over walkmesh faces of the grass surface material
/// (surfacemat.2da row 3) when the tileset's [GRASS] section enables it, with its density,
/// height and colours. SetAreaGrassOverride adds or replaces grass per surface material,
/// SetAreaDefaultGrassDisabled removes the tileset's own, ReloadAreaGrass rebuilds it, and
/// SetAreaWind makes it sway.
/// </summary>
public partial class GrassFx : Node
{
    public ClientSession Session = null!;
    public const int GrassMaterial = 3;
    const int ChunkTiles = 3;

    sealed record Spec(string Texture, float Density, float Height, Color Ambient, Color Diffuse);

    readonly Dictionary<int, Spec> _overrides = new();
    bool _defaultDisabled;
    AreaView? _area;
    Node3D? _root;
    bool _dirty;
    Vector3 _windDir = new(1, 1, 0);
    float _windMagnitude;
    readonly List<ShaderMaterial> _materials = new();
    static Shader? _shader;

    /// <summary>Tufts placed by the last build, per surface material (capture checks).</summary>
    public readonly Dictionary<int, int> Counts = new();

    public void Build(AreaView area)
    {
        if (area != _area)
        {
            _area = area;
            _overrides.Clear();
            _defaultDisabled = false;
            _windMagnitude = 0;
        }
        _dirty = true;
    }

    public void ApplyOverride(JsonElement d, AreaView area)
    {
        if (area != _area) Build(area);
        if (J.Has(d, "defaultDisabled")) _defaultDisabled = J.B(d, "defaultDisabled");
        else if (J.B(d, "removed")) _overrides.Remove(J.I(d, "material", -1));
        else if (J.S(d, "texture") is { Length: > 0 } texture)
        {
            var amb = J.V(d, "ambient") ?? Vector3.One;
            var dif = J.V(d, "diffuse") ?? Vector3.One;
            _overrides[J.I(d, "material", GrassMaterial)] = new Spec(texture, J.F(d, "density", 1), J.F(d, "height", 1),
                new Color(amb.X, amb.Y, amb.Z), new Color(dif.X, dif.Y, dif.Z));
        }
        _dirty = true;
    }

    public void SetWind(Vector3 direction, float magnitude, float yaw, float pitch)
    {
        _windDir = direction.LengthSquared() > 1e-6f ? direction : new Vector3(1, 1, 0);
        _windMagnitude = magnitude;
        ApplyWind();
    }

    void ApplyWind()
    {
        // Area (x, y, z) is Godot (x, z, -y); wind blows horizontally.
        var dir = new Vector3(_windDir.X, 0, -_windDir.Y);
        dir = dir.LengthSquared() > 1e-6f ? dir.Normalized() : Vector3.Right;
        foreach (var m in _materials)
        {
            m.SetShaderParameter("wind_dir", dir);
            m.SetShaderParameter("wind_strength", 0.004f + 0.012f * Math.Clamp(_windMagnitude, 0f, 3f));
        }
    }

    public override void _Process(double delta)
    {
        if (!_dirty) return;
        _dirty = false;
        Rebuild();
    }

    Dictionary<int, Spec> Specs()
    {
        var specs = new Dictionary<int, Spec>();
        var content = Session.Content;
        if (_area?.TileSet is { } set && content != null && !_defaultDisabled && set.GetInt("GRASS", "Grass") != 0)
        {
            // The tileset names no texture: "<tileset>_grass" when the game has one, else the shared "grass".
            var own = _area.Info.Tileset + "_grass";
            var texture = content.Textures.Get(own) != null ? own : "grass";
            specs[GrassMaterial] = new Spec(texture, set.GetFloat("GRASS", "Density", 1), set.GetFloat("GRASS", "Height", 1),
                new Color(set.GetFloat("GRASS", "AmbientRed", 1), set.GetFloat("GRASS", "AmbientGreen", 1), set.GetFloat("GRASS", "AmbientBlue", 1)),
                new Color(set.GetFloat("GRASS", "DiffuseRed", 1), set.GetFloat("GRASS", "DiffuseGreen", 1), set.GetFloat("GRASS", "DiffuseBlue", 1)));
        }
        foreach (var (material, spec) in _overrides) specs[material] = spec;
        return specs;
    }

    void Rebuild()
    {
        _root?.QueueFree();
        _root = null;
        _materials.Clear();
        Counts.Clear();
        var area = _area;
        var content = Session.Content;
        if (area == null || !IsInstanceValid(area) || content == null) return;
        var specs = Specs();
        if (specs.Count == 0) return;
        _root = new Node3D { Name = "Grass" };
        area.AddChild(_root);
        var info = area.Info;
        int chunksX = (info.Width + ChunkTiles - 1) / ChunkTiles, chunksY = (info.Height + ChunkTiles - 1) / ChunkTiles;
        foreach (var (material, spec) in specs)
        {
            var mesh = TuftMesh(spec, content);
            if (mesh == null) continue;
            // Triangles of this surface material, grouped by chunk of tiles.
            var chunks = new List<(Vector3 P, float Yaw, float Scale)>[chunksX * chunksY];
            for (int i = 0; i < info.Tiles.Length; i++)
            {
                if (area.TileModel(i) is not { } model) continue;
                var tris = new List<N.Vector3>();
                var tileXf = area.TileTransform(i);
                var globals = model.RestGlobals();
                for (int p = 0; p < model.Parts.Count; p++)
                {
                    if (model.Parts[p].Mesh is not { IsWalkmesh: true } wm) continue;
                    var xf = tileXf * Rendering.Convert.T(globals[p]);
                    for (int f = 0; f * 3 + 2 < wm.Indices.Length; f++)
                    {
                        int surface = f < wm.FaceSurfaces.Length ? wm.FaceSurfaces[f] : 0;
                        if (surface != material) continue;
                        for (int k = 0; k < 3; k++) tris.Add(Rendering.Convert.N(xf * Rendering.Convert.V(wm.Vertices[wm.Indices[f * 3 + k]])));
                    }
                }
                if (tris.Count == 0) continue;
                int seed = HashCode.Combine(i, info.Tiles[i].Id, material);
                int chunk = (i / info.Width / ChunkTiles) * chunksX + (i % info.Width / ChunkTiles);
                chunks[chunk] ??= new();
                foreach (var (pos, yaw, scale) in FxMath.ScatterGrass(tris, Density(spec.Density), seed))
                    chunks[chunk].Add((Rendering.Convert.V(pos), yaw, scale));
            }
            int total = 0;
            for (int c = 0; c < chunks.Length; c++)
            {
                if (chunks[c] is not { Count: > 0 } list) continue;
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = list.Count };
                for (int k = 0; k < list.Count; k++)
                {
                    var (p, yaw, s) = list[k];
                    mm.SetInstanceTransform(k, new Transform3D(new Basis(Vector3.Back, yaw).Scaled(Vector3.One * s), p));
                    // Grass textures hold four tufts in a 2x2 grid; each tuft picks one.
                    int cell = (int)(yaw * 1000) & 3;
                    mm.SetInstanceCustomData(k, new Color(cell & 1, cell >> 1, 0, 0));
                }
                _root.AddChild(new MultiMeshInstance3D
                {
                    Name = $"Grass{material}_{c}", Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    VisibilityRangeEnd = 55, VisibilityRangeEndMargin = 8, VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
                });
                total += list.Count;
            }
            Counts[material] = total;
        }
        ApplyWind();
        if (WorldFxPlayer.Verbose) GD.Print($"[grass] {string.Join(", ", Counts.Select(kv => $"material {kv.Key}: {kv.Value} tufts"))}");
    }

    /// <summary>Tufts per square metre from a .set / SetAreaGrassOverride density.</summary>
    static float Density(float density) => Math.Clamp(density, 0f, 20f) * 0.6f;

    /// <summary>One tuft: three crossed upright quads (area space, Z up) with the grass texture.</summary>
    ArrayMesh? TuftMesh(Spec spec, ClientContent content)
    {
        var tex = content.Textures.Get(spec.Texture);
        if (tex == null) return null;
        float h = 0.55f * Math.Clamp(spec.Height, 0.1f, 5f), w = h * 1.4f;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var idx = new List<int>();
        float v0 = tex.FlipV ? 1 : 0, v1 = tex.FlipV ? 0 : 1;
        for (int q = 0; q < 3; q++)
        {
            float a = q * MathF.PI / 3f;
            var dir = new Vector3(MathF.Cos(a), MathF.Sin(a), 0) * (w / 2);
            int b = verts.Count;
            verts.AddRange(new[] { -dir, dir, dir + new Vector3(0, 0, h), -dir + new Vector3(0, 0, h) });
            uvs.AddRange(new[] { new Vector2(0, v1), new Vector2(1, v1), new Vector2(1, v0), new Vector2(0, v0) });
            colors.AddRange(new[] { new Color(0, 0, 0), new Color(0, 0, 0), new Color(1, 0, 0), new Color(1, 0, 0) });
            for (int k = 0; k < 4; k++) normals.Add(Vector3.Back);
            idx.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _shader ??= new Shader { Code = ShaderCode };
        var mat = new ShaderMaterial { Shader = _shader };
        mat.SetShaderParameter("albedo_tex", tex.Texture);
        // The tileset's grass colours tint the texture: diffuse for the lit part, ambient for the floor.
        var tint = new Color(Math.Min(1, spec.Diffuse.R * 0.7f + spec.Ambient.R * 0.6f), Math.Min(1, spec.Diffuse.G * 0.7f + spec.Ambient.G * 0.6f), Math.Min(1, spec.Diffuse.B * 0.7f + spec.Ambient.B * 0.6f));
        mat.SetShaderParameter("tint", tint);
        mesh.SurfaceSetMaterial(0, mat);
        _materials.Add(mat);
        return mesh;
    }

    const string ShaderCode = @"
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform vec4 tint : source_color = vec4(1.0);
uniform vec3 wind_dir = vec3(1.0, 0.0, 0.0);
uniform float wind_strength = 0.02;
varying float top;
varying vec2 cell;
void vertex() {
    top = COLOR.r;
    cell = INSTANCE_CUSTOM.xy;
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float phase = world.x * 0.37 + world.z * 0.29;
    float sway = (sin(TIME * 1.6 + phase) * 0.7 + sin(TIME * 3.3 + phase * 1.9) * 0.3 + 0.4) * wind_strength * 10.0 * COLOR.r;
    VERTEX += (inverse(MODEL_MATRIX) * vec4(wind_dir * sway, 0.0)).xyz;
}
void fragment() {
    vec4 c = texture(albedo_tex, (UV + cell) * 0.5);
    // Lit as the ground it grows from, whichever side of the quad faces the camera (a double-sided
    // quad's back face would otherwise get a downward normal and render near black).
    NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
    ALBEDO = c.rgb * tint.rgb * mix(0.65, 1.0, top);
    ALPHA = c.a;
    ALPHA_SCISSOR_THRESHOLD = 0.45;
    ROUGHNESS = 0.95;
    SPECULAR = 0.1;
}
";
}
