using System.Runtime.CompilerServices;
using Godot;

namespace OpenWinter.Client.Rendering;

/// <summary>
/// Keyholing, as in the original: walls, roofs and placeables between the camera and the player get a roughly round,
/// ragged-edged hole cut through them around the player, so the player and the ground around them stay in view.
/// Measured black-box against the original client (parity/visual, scenes keyhole, keyzoom, keycurve, keydepth):
/// <list type="bullet">
/// <item>The hole is centred on the point the camera looks at (the player). Its size is set per surface by that
/// surface's distance from the camera <c>z</c>: diameter D = clamp(c0 + c1 / z, min, max) in units where the radius
/// is 0.355 D of the screen height; a near wall gets a big hole, a far one a hole about the minimum. With the
/// original's defaults (min 0.4, max 0.9) c0 = 0.36 and c1 = 2.6 m (fitted; <see cref="Shape"/>).</item>
/// <item>Only what is horizontally in front of the player toward the camera and higher than min-z-offset
/// (1 m) above the player's feet is cut: floors, low furniture, walls beside or just behind the player and the
/// ground the player stands on stay. Tiles and placeables are cut (black tile caps too); creatures, items on
/// the ground, doors and effects never.</item>
/// <item>The edge is hard (no blending) but ragged, with a few broad and finer bumps of about ±10% of the radius,
/// and the cut surface darkens towards it, to about 60% at the edge, over the outer fifth of the radius.</item>
/// <item>The hole stays active with a clear sight line to the player. Per-fragment depth and height decide what
/// is cut, including geometry beside that sight line (Prelude bedroom, scene keybed).</item>
/// <item>It does not wait or animate: the same view always gives the same hole. Tile fading is unchanged by it (the
/// original fades the same parts with keyholing off); the hole cuts whatever fading leaves.</item>
/// </list>
/// It is done in the materials: world materials (tiles, placeables) are swapped per mesh instance for a copy
/// of the same shading with the cut added, all driven by a few global uniforms that <see cref="Update"/> sets once a
/// frame, so there is no per-object work while playing.
/// </summary>
public static class Keyhole
{
    /// <summary>The original's settings ([graphics.keyholing] in settings.tml), at their defaults; <see cref="Configure"/> changes them.</summary>
    public static float MinDiameter { get; private set; } = 0.4f;
    public static float MaxDiameter { get; private set; } = 0.9f;
    public static float MinZOffset { get; private set; } = 1.0f;

    /// <summary>x, y: hole centre (screen UV); z: the view depth of the point the camera looks at; w: 1 when on.</summary>
    const string ViewParam = "keyhole_view";
    /// <summary>x: c0; y: c1 (metres); z, w: min and max diameter.</summary>
    const string ShapeParam = "keyhole_shape";
    /// <summary>x: lowest world height cut; y, z, w: the camera's position (other passes, like shadows, are not cut).</summary>
    const string FloorParam = "keyhole_floor";

    static bool _ready;
    static bool _on;
    static readonly Dictionary<string, Shader> Shaders = new();
    static readonly ConditionalWeakTable<StandardMaterial3D, ShaderMaterial> Converted = new();

    /// <summary>The fitted size curve for a min and max diameter (see the class notes).</summary>
    public static (float C0, float C1) Shape(float min, float max) => (0.9f * min, max * (0.37f + 6.3f * min));

    /// <summary>Sets the original's keyholing settings (for measuring against it; the defaults are the original's).</summary>
    public static void Configure(float minDiameter, float maxDiameter, float minZOffset)
    {
        MinDiameter = minDiameter;
        MaxDiameter = maxDiameter;
        MinZOffset = minZOffset;
        _ready = false;
        Ensure();
    }

    /// <summary>The globals are declared in project.godot ([shader_globals]); this sets the shape once.</summary>
    static void Ensure()
    {
        if (_ready) return;
        _ready = true;
        var (c0, c1) = Shape(MinDiameter, MaxDiameter);
        RenderingServer.GlobalShaderParameterSet(ShapeParam, new Vector4(c0, c1, MinDiameter, MaxDiameter));
    }

    /// <summary>
    /// Sets the hole for this frame from the camera and the point it follows (the player's feet; the camera looks
    /// at <paramref name="focus"/>). Off only when <paramref name="on"/> is false or the camera leaves the tree.
    /// </summary>
    public static void Update(Camera3D camera, Vector3 feet, Vector3 focus, bool on)
    {
        if (!_ready) Ensure();
        if (!on || !camera.IsInsideTree())
        {
            if (_on) RenderingServer.GlobalShaderParameterSet(ViewParam, Vector4.Zero);
            _on = false;
            return;
        }
        _on = true;
        var size = camera.GetViewport().GetVisibleRect().Size;
        var screen = camera.IsPositionBehind(focus) ? size / 2 : camera.UnprojectPosition(focus);
        float depth = -(camera.GlobalTransform.AffineInverse() * focus).Z;
        RenderingServer.GlobalShaderParameterSet(ViewParam, new Vector4(screen.X / size.X, screen.Y / size.Y, depth, 1f));
        var eye = camera.GlobalPosition;
        RenderingServer.GlobalShaderParameterSet(FloorParam, new Vector4(feet.Y + MinZOffset, eye.X, eye.Y, eye.Z));
    }

    /// <summary>Gives every world mesh under <paramref name="root"/> keyholed copies of its materials (per instance).</summary>
    public static void Apply(Node root)
    {
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            Apply(mi);
        if (root is MeshInstance3D self) Apply(self);
    }

    static void Apply(MeshInstance3D mi)
    {
        if (mi.Mesh == null) return;
        mi.SetMeta("keyhole", true);
        for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
        {
            var current = mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i);
            if (current is StandardMaterial3D sm && Convert(sm) is { } cut) mi.SetSurfaceOverrideMaterial(i, cut);
        }
    }

    /// <summary>Whether a mesh instance was keyholed (so material swaps keep the cut).</summary>
    public static bool IsKeyholed(MeshInstance3D mi) => mi.HasMeta("keyhole");

    /// <summary>
    /// The keyholed version of a world material: a shader with the same shading as the StandardMaterial3D plus the
    /// cut. Null for materials using features this does not reproduce (they keep their own material, uncut).
    /// </summary>
    public static ShaderMaterial? Convert(StandardMaterial3D m)
    {
        if (Converted.TryGetValue(m, out var done)) return done;
        if (!Supported(m)) return null;
        Ensure();
        string key = Variant(m);
        if (!Shaders.TryGetValue(key, out var shader)) Shaders[key] = shader = new Shader { Code = Code(m) };
        var sm = new ShaderMaterial { Shader = shader, RenderPriority = m.RenderPriority };
        sm.SetMeta("keyhole_material", true);
        sm.SetMeta("keyhole_alpha_cut", m.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor ? m.AlphaScissorThreshold
            : m.Transparency is BaseMaterial3D.TransparencyEnum.Alpha or BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass ? 0.3f : 0f);
        sm.SetShaderParameter("albedo", m.AlbedoColor);
        if (m.AlbedoTexture != null) sm.SetShaderParameter("texture_albedo", m.AlbedoTexture);
        sm.SetShaderParameter("roughness", m.Roughness);
        sm.SetShaderParameter("metallic", m.Metallic);
        sm.SetShaderParameter("specular", m.MetallicSpecular);
        sm.SetShaderParameter("uv1_scale", m.Uv1Scale);
        sm.SetShaderParameter("uv1_offset", m.Uv1Offset);
        sm.SetShaderParameter("alpha_scissor_threshold", m.AlphaScissorThreshold);
        Converted.AddOrUpdate(m, sm);
        return sm;
    }

    /// <summary>Only the features world materials use (MaterialFactory); anything else is left alone.</summary>
    static bool Supported(StandardMaterial3D m) =>
        m.NextPass == null && !m.EmissionEnabled && !m.NormalEnabled && !m.RimEnabled && !m.ClearcoatEnabled && !m.AnisotropyEnabled
        && !m.AOEnabled && !m.HeightmapEnabled && !m.SubsurfScatterEnabled && !m.BacklightEnabled && !m.RefractionEnabled && !m.DetailEnabled
        && !m.VertexColorUseAsAlbedo && m.MetallicTexture == null && m.RoughnessTexture == null && !m.Uv1Triplanar
        && m.BillboardMode == BaseMaterial3D.BillboardModeEnum.Disabled && !m.ProximityFadeEnabled
        && m.DistanceFadeMode == BaseMaterial3D.DistanceFadeModeEnum.Disabled && !m.DisableReceiveShadows && !m.FixedSize
        && m.Transparency is BaseMaterial3D.TransparencyEnum.Disabled or BaseMaterial3D.TransparencyEnum.Alpha or BaseMaterial3D.TransparencyEnum.AlphaScissor or BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass
        && m.BlendMode is BaseMaterial3D.BlendModeEnum.Mix or BaseMaterial3D.BlendModeEnum.Add
        && m.ShadingMode is BaseMaterial3D.ShadingModeEnum.PerPixel or BaseMaterial3D.ShadingModeEnum.Unshaded
        && m.CullMode is BaseMaterial3D.CullModeEnum.Back or BaseMaterial3D.CullModeEnum.Disabled
        && m.DiffuseMode is BaseMaterial3D.DiffuseModeEnum.Lambert or BaseMaterial3D.DiffuseModeEnum.Burley
        && m.SpecularMode is BaseMaterial3D.SpecularModeEnum.SchlickGgx or BaseMaterial3D.SpecularModeEnum.Disabled
        && m.TextureFilter is BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic or BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;

    static string Variant(StandardMaterial3D m) =>
        $"{m.Transparency}|{m.BlendMode}|{m.ShadingMode}|{m.CullMode}|{m.DiffuseMode}|{m.SpecularMode}|{m.DepthDrawMode}|{m.TextureFilter}|{m.TextureRepeat}|{m.DisableFog}|{m.AlbedoTexture != null}";

    /// <summary>Shared cut calculation for the world surface and its highlight/mask passes.</summary>
    public const string ShaderCode = """
global uniform vec4 keyhole_view;
global uniform vec4 keyhole_shape;
global uniform vec4 keyhole_floor;

float keyhole_hash(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.71, 0.113, 0.419));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Smooth value noise in [-1, 1].
float keyhole_noise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	float n = mix(mix(mix(keyhole_hash(i), keyhole_hash(i + vec3(1, 0, 0)), f.x),
	                  mix(keyhole_hash(i + vec3(0, 1, 0)), keyhole_hash(i + vec3(1, 1, 0)), f.x), f.y),
	              mix(mix(keyhole_hash(i + vec3(0, 0, 1)), keyhole_hash(i + vec3(1, 0, 1)), f.x),
	                  mix(keyhole_hash(i + vec3(0, 1, 1)), keyhole_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
	return n * 2.0 - 1.0;
}

float keyhole_visibility(vec3 view_vertex, mat4 inverse_view, vec2 screen_uv, vec2 viewport_size, vec3 camera_position) {
	// Keyhole: cut what stands between the camera and the player (see Keyhole.cs).
	float depth = -view_vertex.z;
	if (keyhole_view.w > 0.5 && depth < keyhole_view.z && distance(camera_position, keyhole_floor.yzw) < 0.05) {
		vec3 world = (inverse_view * vec4(view_vertex, 1.0)).xyz;
		// Only horizontal separation puts scenery in front of the player. Height alone must not
		// dissolve a neighbouring plant in an overhead view (Prelude corner, keyoverhead).
		vec3 focus = keyhole_floor.yzw - inverse_view[2].xyz * keyhole_view.z;
		float ahead = dot(world.xz - focus.xz, inverse_view[2].xz);
		// Ragged edge: broad and fine bumps, the same for every surface along a line of sight (noise where the line
		// meets the player's depth), so the near and far faces of a wall line up; fainter close to the camera.
		vec3 across = keyhole_floor.yzw + (world - keyhole_floor.yzw) * (keyhole_view.z / max(depth, 0.05));
		float wobble = (0.65 * keyhole_noise(across * 1.6) + 0.35 * keyhole_noise(across * 5.0)) * clamp(depth / 6.0, 0.3, 1.0);
		// Keep the height boundary flat. Applying the ragged noise here punched holes in low furniture.
		if (world.y > keyhole_floor.x && ahead > 0.25) {
			// Surfaces nearer the camera get bigger holes. The hole closes over the last metre and a quarter of
			// horizontal separation projected along the view. Also close towards the height
			// boundary: fitted to the Prelude bed, whose mattress peaks at 1.01 m but stays whole at the default 1 m.
			float diameter = max(clamp(keyhole_shape.x + keyhole_shape.y / max(depth, 0.05), keyhole_shape.z, keyhole_shape.w)
				* clamp(ahead - 0.25, 0.0, 1.0)
				* clamp(world.y - keyhole_floor.x, 0.0, 1.0), 1e-4);
			vec2 offset = (screen_uv - keyhole_view.xy) * vec2(viewport_size.x / viewport_size.y, 1.0);
			float r = length(offset) / (0.355 * diameter) * (1.0 + 0.13 * wobble);
			if (r < 1.0) {
				return 0.0;
			}
			// The cut surface darkens towards the edge, evenly over the outer fifth of the radius.
			return mix(0.4, 1.0, clamp((r - 1.0) / 0.2, 0.0, 1.0));
		}
	}
	return 1.0;
}
""";

    /// <summary>The StandardMaterial3D shading for these features (as Godot generates it) with the cut added.</summary>
    static string Code(StandardMaterial3D m)
    {
        var modes = new List<string>
        {
            m.BlendMode == BaseMaterial3D.BlendModeEnum.Add ? "blend_add" : "blend_mix",
            m.DepthDrawMode switch { BaseMaterial3D.DepthDrawModeEnum.Always => "depth_draw_always", BaseMaterial3D.DepthDrawModeEnum.Disabled => "depth_draw_never", _ => "depth_draw_opaque" },
            m.CullMode == BaseMaterial3D.CullModeEnum.Disabled ? "cull_disabled" : "cull_back",
            m.DiffuseMode == BaseMaterial3D.DiffuseModeEnum.Lambert ? "diffuse_lambert" : "diffuse_burley",
            m.SpecularMode == BaseMaterial3D.SpecularModeEnum.Disabled ? "specular_disabled" : "specular_schlick_ggx",
        };
        if (m.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded) modes.Add("unshaded");
        if (m.Transparency == BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass) modes.Add("depth_prepass_alpha");
        if (m.DisableFog) modes.Add("fog_disabled");
        string filter = m.TextureFilter == BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic ? "filter_linear_mipmap_anisotropic" : "filter_linear_mipmap";
        string repeat = m.TextureRepeat ? "repeat_enable" : "repeat_disable";
        string alpha = m.Transparency switch
        {
            BaseMaterial3D.TransparencyEnum.Alpha or BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass => "\tALPHA *= albedo.a * albedo_tex.a;\n",
            BaseMaterial3D.TransparencyEnum.AlphaScissor => "\tALPHA *= albedo.a * albedo_tex.a;\n\tALPHA_SCISSOR_THRESHOLD = alpha_scissor_threshold;\n",
            _ => "",
        };
        return $$"""
shader_type spatial;
render_mode {{string.Join(", ", modes)}};

uniform vec4 albedo : source_color;
uniform sampler2D texture_albedo : source_color, hint_default_white, {{filter}}, {{repeat}};
uniform float alpha_scissor_threshold : hint_range(0.0, 1.0, 0.001);
uniform float roughness : hint_range(0.0, 1.0);
uniform float specular : hint_range(0.0, 1.0, 0.01);
uniform float metallic : hint_range(0.0, 1.0, 0.01);
uniform vec3 uv1_scale;
uniform vec3 uv1_offset;

{{ShaderCode}}

void vertex() {
	UV = UV * uv1_scale.xy + uv1_offset.xy;
}

void fragment() {
	vec2 base_uv = UV;
	vec4 albedo_tex = texture(texture_albedo, base_uv);
	ALBEDO = albedo.rgb * albedo_tex.rgb;
	METALLIC = metallic;
	SPECULAR = specular;
	ROUGHNESS = roughness;
{{alpha}}
	float visible = keyhole_visibility(VERTEX, INV_VIEW_MATRIX, SCREEN_UV, VIEWPORT_SIZE, CAMERA_POSITION_WORLD);
	if (visible <= 0.0) discard;
	ALBEDO *= visible;
}
""";
    }
}
