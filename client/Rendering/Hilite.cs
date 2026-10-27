using Godot;

namespace OpenWinter.Client.Rendering;

/// <summary>
/// The original's object highlight (mouse-over and the Tab key), measured on the original client
/// (parity/visual/scenes/hilite). It has two parts:
/// <list type="bullet">
/// <item>The model is lit by an extra light of the highlight colour. Its own colours are multiplied by it,
/// so skin turns violet under blue and wood turns teal under cyan, with a faint flat glow on top.</item>
/// <item>A soft glow of the same colour surrounds the silhouette, strongest at the edge and fading
/// outwards. Its reach and strength are kept deliberately subtle.</item>
/// </list>
/// The light is a pass chained after a copy of each surface's material, sampling the surface's own texture
/// (copies cached per material and colour; the originals are put back afterwards). The glow is drawn in
/// screen space by <see cref="HiliteGlow"/>: each highlighted mesh gets a flat-coloured twin that only the
/// glow's mask camera sees, and the mask is blurred and added over the view.
/// </summary>
public static class Hilite
{
    /// <summary>How strongly the highlight lights the model's own colours (linear light, measured).</summary>
    const float Light = 0.8f;
    /// <summary>The flat glow over the whole model.</summary>
    const float Glow = 0.02f;
    /// <summary>Texture brightness for the PC's white, even light (selflight native captures).</summary>
    const float SelfLight = 1.2f;
    /// <summary>The render layer only the glow's mask camera draws.</summary>
    public const uint MaskLayer = 1u << 19;
    /// <summary>The render layer only the glow's depth camera draws (the highlighted models' distances, for occlusion).</summary>
    // Layer 19 belongs to CreatureLight's shadow casters. Sharing it renders the creatures' ordinary
    // colours into this distance texture, so the occlusion test rejects their halos.
    public const uint DepthLayer = 1u << 17;
    /// <summary>Distances are stored divided by this (metres).</summary>
    public const float DepthRange = 512f;

    static Shader? _lightShader, _maskShader, _depthShader;
    static readonly Dictionary<(ulong Material, Color Colour, bool FlatLight), Material> Copies = new();
    static readonly HashSet<ulong> OurCopies = new();
    static readonly HashSet<ulong> DynamicCopies = new();
    static readonly Dictionary<(SurfaceSettings Surface, Color Colour), ShaderMaterial> Masks = new();

    sealed class Applied
    {
        public Color Colour;
        public bool FlatLight;
        public bool Dynamic;
        public readonly List<ShaderMaterial> Colours = new();
        public readonly List<ulong> Copies = new();
        public readonly List<(MeshInstance3D Mesh, int Surface, Material? Previous)> Slots = new();
        public readonly Dictionary<ulong, MeshInstance3D> Twins = new();
        public readonly Dictionary<ulong, MeshInstance3D> DepthTwins = new();
    }

    static readonly Dictionary<ulong, Applied> Objects = new();
    static readonly Dictionary<ulong, (Color Colour, bool FlatLight)> PointerLooks = new();
    static readonly Dictionary<ulong, (Color Colour, bool Dynamic)> EffectLooks = new();

    /// <summary>Whether anything is highlighted (the glow only renders then). Forgets objects that were freed.</summary>
    public static bool Any
    {
        get
        {
            foreach (var id in Objects.Keys.Where(id => GodotObject.InstanceFromId(id) == null).ToList())
            {
                RemoveLook(id);
                PointerLooks.Remove(id);
                EffectLooks.Remove(id);
            }
            return Objects.Count > 0;
        }
    }

    /// <summary>Puts the highlight on every mesh under <paramref name="root"/> (cheap to repeat every frame).</summary>
    public static void Apply(Node3D root, Color colour, bool flatLight = false)
    {
        PointerLooks[root.GetInstanceId()] = (colour, flatLight);
        Refresh(root);
    }

    /// <summary>A duration effect owns its glow independently of temporary pointer/Tab highlighting.</summary>
    public static void SetEffectGlow(Node3D root, Color? colour, bool dynamic = false)
    {
        if (colour is { } value) EffectLooks[root.GetInstanceId()] = (value, dynamic);
        else if (!EffectLooks.Remove(root.GetInstanceId())) return;
        Refresh(root);
    }

    static void Refresh(Node3D root)
    {
        // Preserve interaction feedback while highlighted, then restore the duration
        // effect immediately when the pointer or Tab highlight is removed.
        if (PointerLooks.TryGetValue(root.GetInstanceId(), out var pointer))
            ApplyLook(root, pointer.Colour, pointer.FlatLight);
        else if (EffectLooks.TryGetValue(root.GetInstanceId(), out var effect))
            ApplyLook(root, effect.Colour, false, effect.Dynamic);
        else RemoveLook(root);
    }

    static void ApplyLook(Node3D root, Color colour, bool flatLight, bool dynamic = false)
    {
        if (Copies.Count > 4000) { Copies.Clear(); OurCopies.Clear(); }
        var id = root.GetInstanceId();
        if (Objects.TryGetValue(id, out var previous) && (previous.FlatLight != flatLight || previous.Dynamic != dynamic || !dynamic && previous.Colour != colour)) RemoveLook(root);
        if (!Objects.TryGetValue(id, out var applied)) Objects[id] = applied = new Applied { Colour = colour, FlatLight = flatLight, Dynamic = dynamic };
        if (dynamic && applied.Colour != colour)
        {
            // Each animated effect owns its passes. Change uniforms without
            // rebuilding mesh twins or caching a new material for every frame.
            foreach (var material in applied.Colours) material.SetShaderParameter("colour", colour);
            applied.Colour = colour;
        }
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Mesh == null || mi.Layers == MaskLayer || mi.Layers == DepthLayer || mi.CastShadow == GeometryInstance3D.ShadowCastingSetting.ShadowsOnly
                || mi.Name.ToString().StartsWith("fx_")) continue;
            bool needsTwins = !applied.Twins.ContainsKey(mi.GetInstanceId());
            var surfaces = needsTwins ? new List<Material?>() : null;
            if (mi.MaterialOverride is { } over)
            {
                surfaces?.Add(over);
                if (!IsCopy(over))
                {
                    applied.Slots.Add((mi, -1, over));
                    mi.MaterialOverride = Copy(over, colour, flatLight, applied);
                }
            }
            else
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var current = mi.GetSurfaceOverrideMaterial(s);
                    var baseMaterial = current ?? mi.Mesh.SurfaceGetMaterial(s);
                    surfaces?.Add(baseMaterial);
                    if (baseMaterial == null || current != null && IsCopy(current)) continue;
                    applied.Slots.Add((mi, s, current));
                    mi.SetSurfaceOverrideMaterial(s, Copy(baseMaterial, colour, flatLight, applied));
                }
            // The glow's twin: the same mesh (and skeleton), flat colour, seen only by the mask camera.
            if (surfaces != null && mi.GetParent() is Node parent)
            {
                var twin = new MeshInstance3D
                {
                    Name = "HiliteTwin", Mesh = mi.Mesh, Skin = mi.Skin, Transform = mi.Transform, Layers = MaskLayer,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Transparency = mi.Transparency, Visible = mi.IsVisibleInTree(),
                };
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                    twin.SetSurfaceOverrideMaterial(s, Mask(surfaces[Math.Min(s, surfaces.Count - 1)], flatLight ? colour * 0.7f : colour, applied));
                parent.AddChild(twin);
                if (!mi.Skeleton.IsEmpty) twin.Skeleton = twin.GetPathTo(mi.GetNode(mi.Skeleton));
                applied.Twins[mi.GetInstanceId()] = twin;
                // Its distance twin: how far each pixel of the model is from the camera (walls in front hide the glow).
                var depth = new MeshInstance3D
                {
                    Name = "HiliteDepthTwin", Mesh = mi.Mesh, Skin = mi.Skin, Transform = mi.Transform, Layers = DepthLayer,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Transparency = mi.Transparency, Visible = mi.IsVisibleInTree(),
                };
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                    depth.SetSurfaceOverrideMaterial(s, DepthMaterial(surfaces[Math.Min(s, surfaces.Count - 1)]));
                parent.AddChild(depth);
                if (!mi.Skeleton.IsEmpty) depth.Skeleton = depth.GetPathTo(mi.GetNode(mi.Skeleton));
                applied.DepthTwins[mi.GetInstanceId()] = depth;
            }
            else if (applied.Twins.TryGetValue(mi.GetInstanceId(), out var t) && GodotObject.IsInstanceValid(t))
            {
                t.Transform = mi.Transform;
                t.Visible = mi.IsVisibleInTree();
                t.Transparency = mi.Transparency;
                if (applied.DepthTwins.TryGetValue(mi.GetInstanceId(), out var d) && GodotObject.IsInstanceValid(d))
                {
                    d.Transform = t.Transform;
                    d.Visible = t.Visible;
                    d.Transparency = t.Transparency;
                }
            }
        }
    }

    /// <summary>Takes the highlight off and restores the materials it replaced.</summary>
    public static void Remove(Node3D root)
    {
        PointerLooks.Remove(root.GetInstanceId());
        Refresh(root);
    }

    static bool IsCopy(Material material) => OurCopies.Contains(material.GetInstanceId()) || DynamicCopies.Contains(material.GetInstanceId());

    static void RemoveLook(Node3D root) => RemoveLook(root.GetInstanceId());

    static void RemoveLook(ulong id)
    {
        if (!Objects.Remove(id, out var applied)) return;
        foreach (var (mi, s, previous) in applied.Slots)
        {
            if (!GodotObject.IsInstanceValid(mi)) continue;
            if (s < 0) { if (mi.MaterialOverride is { } m && IsCopy(m)) mi.MaterialOverride = previous; }
            else if (s < mi.Mesh?.GetSurfaceCount() && mi.GetSurfaceOverrideMaterial(s) is { } m && IsCopy(m))
                mi.SetSurfaceOverrideMaterial(s, previous);
        }
        foreach (var twin in applied.Twins.Values.Concat(applied.DepthTwins.Values))
            if (GodotObject.IsInstanceValid(twin)) twin.QueueFree();
        foreach (var copy in applied.Copies) DynamicCopies.Remove(copy);
    }

    static Material Copy(Material baseMaterial, Color colour, bool flatLight, Applied applied)
    {
        var key = (baseMaterial.GetInstanceId(), colour, flatLight);
        if (!applied.Dynamic && Copies.TryGetValue(key, out var cached) && GodotObject.IsInstanceValid(cached)) return cached;
        Material copy;
        copy = (Material)baseMaterial.Duplicate();
        if (flatLight && copy is StandardMaterial3D surface)
        {
            // Native selflight front/back and dark/lit captures: the PC's white highlight
            // reveals its texture evenly, without the area's directional lighting or a white wash.
            // Keep UVs, tint, transparency and any effect pass on the original material.
            surface.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            var tint = surface.AlbedoColor;
            surface.AlbedoColor = new Color(tint.R * SelfLight, tint.G * SelfLight, tint.B * SelfLight, tint.A);
        }
        else
        {
            var light = LightPass(baseMaterial, colour);
            if (applied.Dynamic) applied.Colours.Add(light);
            // Keep any pass the material already had (effects that draw over the model) after ours.
            light.NextPass = baseMaterial.NextPass;
            copy.NextPass = light;
        }
        if (applied.Dynamic)
        {
            applied.Copies.Add(copy.GetInstanceId());
            DynamicCopies.Add(copy.GetInstanceId());
        }
        else { Copies[key] = copy; OurCopies.Add(copy.GetInstanceId()); }
        return copy;
    }

    // Highlight passes must sample exactly the same atlas region as the original surface.
    // Installed DDS materials flip V; omitting that transform draws unrelated atlas pieces
    // over the armoire doors and also misaligns alpha-cut foliage in the glow/depth masks.
    readonly record struct SurfaceSettings(Texture2D? Texture, float Cut, Vector3 Scale, Vector3 Offset, Color Tint, bool Keyholed);

    static SurfaceSettings Surface(Material? material)
    {
        if (material is BaseMaterial3D b)
            return new(b.AlbedoTexture, b.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor ? b.AlphaScissorThreshold
                : b.Transparency != BaseMaterial3D.TransparencyEnum.Disabled ? 0.3f : 0, b.Uv1Scale, b.Uv1Offset, b.AlbedoColor, false);
        if (material is ShaderMaterial { } custom && custom.HasMeta("ow_custom"))
        {
            // A module shader: its main texture, alpha test and texture flip.
            var cut = custom.GetShaderParameter("fAlphaDiscardValue");
            bool flip = custom.HasMeta("ow_uv_flip");
            return new(custom.GetShaderParameter("texUnit0").As<Texture2D>(), cut.VariantType == Variant.Type.Float ? Math.Max(0f, cut.AsSingle()) : 0,
                flip ? new Vector3(1, -1, 1) : Vector3.One, flip ? new Vector3(0, 1, 0) : Vector3.Zero, Colors.White, custom.HasMeta("ow_keyhole"));
        }
        if (material is ShaderMaterial sm)
        {
            var threshold = sm.HasMeta("keyhole_alpha_cut") ? sm.GetMeta("keyhole_alpha_cut") : sm.GetShaderParameter("alpha_scissor_threshold");
            var scale = sm.GetShaderParameter("uv1_scale");
            var offset = sm.GetShaderParameter("uv1_offset");
            var tint = sm.GetShaderParameter("albedo");
            return new(sm.GetShaderParameter("texture_albedo").As<Texture2D>(), threshold.VariantType == Variant.Type.Float ? threshold.AsSingle() : 0,
                scale.VariantType == Variant.Type.Vector3 ? scale.AsVector3() : Vector3.One,
                offset.VariantType == Variant.Type.Vector3 ? offset.AsVector3() : Vector3.Zero,
                tint.VariantType == Variant.Type.Color ? tint.AsColor() : Colors.White, sm.HasMeta("keyhole_material"));
        }
        return new(null, 0, Vector3.One, Vector3.Zero, Colors.White, false);
    }

    static void SetSurface(ShaderMaterial m, SurfaceSettings surface)
    {
        if (surface.Texture != null) m.SetShaderParameter("tex", surface.Texture);
        m.SetShaderParameter("alpha_cut", surface.Cut);
        m.SetShaderParameter("uv_scale", surface.Scale);
        m.SetShaderParameter("uv_offset", surface.Offset);
        m.SetShaderParameter("tint", surface.Tint);
        m.SetShaderParameter("keyholed", surface.Keyholed);
    }

    /// <summary>The coloured light on the model's own texture, plus the flat glow.</summary>
    static ShaderMaterial LightPass(Material baseMaterial, Color colour)
    {
        _lightShader ??= new Shader { Code = LightCode };
        var surface = Surface(baseMaterial);
        var m = new ShaderMaterial { Shader = _lightShader };
        m.SetShaderParameter("colour", colour);
        SetSurface(m, surface);
        m.SetShaderParameter("light", Light);
        m.SetShaderParameter("glow", Glow);
        return m;
    }

    /// <summary>The twin's flat colour for the glow mask (cut out where the surface is).</summary>
    static ShaderMaterial Mask(Material? surface, Color colour, Applied applied)
    {
        var settings = Surface(surface);
        var key = (settings, colour);
        if (!applied.Dynamic && Masks.TryGetValue(key, out var cached)) return cached;
        _maskShader ??= new Shader { Code = MaskCode };
        var m = new ShaderMaterial { Shader = _maskShader };
        m.SetShaderParameter("colour", colour);
        SetSurface(m, settings);
        if (applied.Dynamic) { applied.Colours.Add(m); return m; }
        return Masks[key] = m;
    }

    static readonly Dictionary<SurfaceSettings, ShaderMaterial> Depths = new();

    /// <summary>The distance twin's material: distance from the camera over <see cref="DepthRange"/> (cut out where the surface is).</summary>
    static ShaderMaterial DepthMaterial(Material? surface)
    {
        var settings = Surface(surface);
        var key = settings;
        if (Depths.TryGetValue(key, out var m)) return m;
        _depthShader ??= new Shader { Code = DepthCode };
        m = new ShaderMaterial { Shader = _depthShader };
        SetSurface(m, settings);
        m.SetShaderParameter("range", DepthRange);
        return Depths[key] = m;
    }

    static readonly string DepthCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D tex : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float alpha_cut = 0.0;
uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform bool keyholed = false;
" + Keyhole.ShaderCode + @"
void vertex() { UV = UV * uv_scale.xy + uv_offset.xy; }
uniform float range = 512.0;
void fragment() {
    if (keyholed && keyhole_visibility(VERTEX, INV_VIEW_MATRIX, SCREEN_UV, VIEWPORT_SIZE, CAMERA_POSITION_WORLD) <= 0.0) discard;
    if (alpha_cut > 0.0 && texture(tex, UV).a * tint.a < alpha_cut) discard;
    ALBEDO = vec3(clamp(-VERTEX.z / range, 0.0, 1.0));
}
";

    static readonly string LightCode = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;
uniform vec4 colour : source_color;
uniform sampler2D tex : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float light = 0.4;
uniform float glow = 0.04;
uniform float alpha_cut = 0.0;
uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform bool keyholed = false;
" + Keyhole.ShaderCode + @"
void vertex() { UV = UV * uv_scale.xy + uv_offset.xy; }
void fragment() {
    if (keyholed && keyhole_visibility(VERTEX, INV_VIEW_MATRIX, SCREEN_UV, VIEWPORT_SIZE, CAMERA_POSITION_WORLD) <= 0.0) discard;
    vec4 t = texture(tex, UV) * tint;
    if (alpha_cut > 0.0 && t.a < alpha_cut) discard;
    ALBEDO = colour.rgb * (t.rgb * light + glow);
}
";

    static readonly string MaskCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;
uniform vec4 colour : source_color;
uniform sampler2D tex : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float alpha_cut = 0.0;
uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform bool keyholed = false;
" + Keyhole.ShaderCode + @"
void vertex() { UV = UV * uv_scale.xy + uv_offset.xy; }
void fragment() {
    if (keyholed && keyhole_visibility(VERTEX, INV_VIEW_MATRIX, SCREEN_UV, VIEWPORT_SIZE, CAMERA_POSITION_WORLD) <= 0.0) discard;
    if (alpha_cut > 0.0 && texture(tex, UV).a * tint.a < alpha_cut) discard;
    ALBEDO = colour.rgb;
}
";
}

/// <summary>
/// The screen-space half of <see cref="Hilite"/>: a viewport whose camera follows the main one and
/// draws only the highlighted meshes' flat-coloured twins, blurred and added over the view by a full-screen
/// quad in front of the main camera. Renders nothing while nothing is highlighted.
/// </summary>
public partial class HiliteGlow : Node
{
    public Camera3D Camera = null!;
    /// <summary>A faint halo behind the existing surface highlight (linear light).</summary>
    const float Strength = 0.35f;
    /// <summary>The halo fades out over 9 pixels at 720p, scaling with the view's height.</summary>
    const float Reach = 9f / 720f;

    SubViewport _mask = null!, _depth = null!;
    Camera3D _maskCamera = null!, _depthCamera = null!;
    MeshInstance3D _quad = null!;
    ShaderMaterial _composite = null!;

    public override void _Ready()
    {
        _mask = new SubViewport
        {
            TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa2X,
            PositionalShadowAtlasSize = 0, Name = "HiliteMask",
        };
        // Its own environment: flat colours, no fog, tone mapping or bloom from the area's.
        _maskCamera = new Camera3D
        {
            CullMask = Hilite.MaskLayer, Current = true,
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor, TonemapMode = Godot.Environment.ToneMapper.Linear },
        };
        _mask.AddChild(_maskCamera);
        AddChild(_mask);
        // The highlighted models' distances (a float target, so nothing is rounded or tone-mapped away).
        _depth = new SubViewport
        {
            TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Disabled,
            PositionalShadowAtlasSize = 0, UseHdr2D = true, Name = "HiliteDepth",
        };
        _depthCamera = new Camera3D
        {
            CullMask = Hilite.DepthLayer, Current = true,
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor, TonemapMode = Godot.Environment.ToneMapper.Linear },
        };
        _depth.AddChild(_depthCamera);
        AddChild(_depth);
        _composite = new ShaderMaterial { Shader = new Shader { Code = CompositeCode } };
        _composite.SetShaderParameter("mask", _mask.GetTexture());
        _composite.SetShaderParameter("twin_depth", _depth.GetTexture());
        _composite.SetShaderParameter("range", Hilite.DepthRange);
        _composite.SetShaderParameter("strength", Strength);
        _quad = new MeshInstance3D
        {
            Name = "HiliteGlow", Mesh = new QuadMesh { Size = new Vector2(2, 2) }, MaterialOverride = _composite,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384, Visible = false,
            Layers = 1,
        };
        Camera.AddChild(_quad);
        // The twins are for the mask camera only.
        Camera.CullMask &= ~(Hilite.MaskLayer | Hilite.DepthLayer);
    }

    public override void _Process(double delta)
    {
        bool on = Hilite.Any;
        _quad.Visible = on;
        _mask.RenderTargetUpdateMode = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _depth.RenderTargetUpdateMode = _mask.RenderTargetUpdateMode;
        if (!on) return;
        var size = GetViewport().GetVisibleRect().Size;
        // Keep silhouette and distance samples aligned with the scene, especially at thin/diagonal edges.
        var want = new Vector2I(Math.Max(1, (int)size.X), Math.Max(1, (int)size.Y));
        if (_mask.Size != want) _mask.Size = want;
        if (_depth.Size != want) _depth.Size = want;
        foreach (var c in new[] { _maskCamera, _depthCamera })
        {
            c.GlobalTransform = Camera.GlobalTransform;
            c.Projection = Camera.Projection;
            c.KeepAspect = Camera.KeepAspect;
            c.Fov = Camera.Fov;
            c.Size = Camera.Size;
            c.FrustumOffset = Camera.FrustumOffset;
            c.HOffset = Camera.HOffset;
            c.VOffset = Camera.VOffset;
            c.Near = Camera.Near;
            c.Far = Camera.Far;
        }
        _composite.SetShaderParameter("reach", Reach);
        _composite.SetShaderParameter("aspect", size.X / Math.Max(1f, size.Y));
    }

    // A dense Gaussian kernel: sparse rings leave recognisable copies of the silhouette
    // (cyan stair steps around chests). Test visibility before blurring so hidden objects cast no halo.
    const string CompositeCode = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_test_disabled, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D mask : source_color, filter_linear, repeat_disable;
uniform sampler2D twin_depth : filter_nearest, repeat_disable;
uniform sampler2D scene_depth : hint_depth_texture, filter_nearest, repeat_disable;
uniform float range = 512.0;
uniform float strength = 1.0;
uniform float reach = 0.018;
uniform float aspect = 1.7778;
void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}
// The mask where the highlighted model is in sight: its distance against the scene's at that point (a wall in
// front hides it, and with it the glow, as in the original).
vec4 seen(vec2 uv, mat4 inv_projection) {
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return vec4(0.0);
    vec4 m = texture(mask, uv);
    if (m.a <= 0.0) return vec4(0.0);
    vec4 t = texture(twin_depth, uv);
    if (t.a < 0.5) return vec4(0.0);
    float z = texture(scene_depth, uv).r;
    vec4 v = inv_projection * vec4(uv * 2.0 - 1.0, z, 1.0);
    float scene = -v.z / v.w;
    float model = t.r * range;
    return model <= scene + 0.15 + scene * 0.01 ? m : vec4(0.0);
}

void fragment() {
    vec2 r = vec2(reach / aspect, reach);
    // Closely spaced samples blend into a continuous halo instead of separate offset silhouettes.
    vec4 centre = seen(SCREEN_UV, INV_PROJECTION_MATRIX);
    vec3 sum = vec3(0.0);
    float total = 0.0;
    for (int y = -6; y <= 6; y++) {
        for (int x = -6; x <= 6; x++) {
            vec2 d = vec2(float(x), float(y)) / 6.0;
            float w = exp(-4.0 * dot(d, d));
            vec4 m = seen(SCREEN_UV + d * r, INV_PROJECTION_MATRIX);
            sum += m.rgb * w;
            total += w;
        }
    }
    // Leave the model's existing textured highlight alone; the halo sits behind its silhouette.
    ALBEDO = sum / total * strength * (1.0 - centre.a);
}
";
}
