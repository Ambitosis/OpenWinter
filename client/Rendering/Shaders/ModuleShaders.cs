using System.Collections.Concurrent;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Shaders;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Rendering.Shaders;

/// <summary>
/// Materials for meshes whose bitmap names an MTR with custom shaders: the module's shaders translated
/// (<see cref="ShaderTranslator"/>) into a Godot shader, with the MTR's textures and parameters. A shader that
/// doesn't translate or compile leaves the mesh its normal material and is logged once, with the reason.
/// </summary>
public sealed class ModuleShaders
{
    /// <summary>Off: every mesh keeps its normal material (--module-shaders off).</summary>
    public static bool Enabled = !OS.GetCmdlineUserArgs().SkipWhile(a => a != "--module-shaders").Skip(1).Take(1).Contains("off");

    readonly ResMan _resources;
    readonly TextureFactory _textures;
    readonly ShaderTranslator _translator;
    readonly ConcurrentDictionary<string, MaterialData?> _materials = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (Shader? Shader, ShaderTranslation Translation)> _shaders = new();
    readonly Dictionary<string, ShaderMaterial> _cache = new();
    readonly HashSet<string> _logged = new();
    /// <summary>What each material was made from, to build its sky variant.</summary>
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<ShaderMaterial, Source> _sources = new();

    sealed record Source(MaterialData Material, MeshData Mesh, string Texture, MaterialKind Kind);

    /// <summary>Programs translated and compiled, and those that fell back, by "vs + fs" (capture reports).</summary>
    public readonly Dictionary<string, string> Programs = new();

    public ModuleShaders(ResMan resources, TextureFactory textures)
    {
        _resources = resources;
        _textures = textures;
        _translator = new ShaderTranslator { ModuleSource = ShaderSources.Module(resources), InstalledSource = ShaderSources.Installed(resources) };
    }

    /// <summary>
    /// The MTR of this name: the module's (module, haks, override), else the game's own (the stock ice and old heads:
    /// with theirs the ice's walls lose the streaks the plain material shows and the original doesn't, scene
    /// stockmtr), or null.
    /// </summary>
    public MaterialData? Material(string resref) =>
        string.IsNullOrEmpty(resref) ? null : _materials.GetOrAdd(resref, r =>
            _resources.Read(r, ResType.Mtr) is { } b ? Mtr.Parse(b, r) : null);

    /// <summary>The shader material for a mesh drawn with this MTR, or null to keep the normal material.</summary>
    public ShaderMaterial? For(MeshData mesh, MaterialData mtr, string texture, MaterialKind kind)
    {
        if (!Enabled || !mtr.HasCustomShader) return null;
        try
        {
            return Build(mesh, mtr, texture, kind, null);
        }
        catch (Exception e)
        {
            Log(mtr.Name, $"{mtr.Name}: not used ({e.GetType().Name}: {e.Message}); the mesh keeps its normal material");
            return null;
        }
    }

    /// <summary>Meta of a module shader material: the mesh's bitmap (or its tinted palette texture) it was made for.</summary>
    public const string BitmapMeta = "ow_bitmap";

    /// <summary>
    /// ReplaceObjectTexture on a mesh drawn with a module shader, as the original does (scene shadertex): a replacement
    /// that names an MTR with shaders draws the mesh with that material (its texture 0 still defaults to the mesh's
    /// own bitmap); any other replacement keeps the material and becomes its texture 0. Null when it can't be made.
    /// </summary>
    public ShaderMaterial? Retextured(ShaderMaterial material, string replacement)
    {
        if (!_sources.TryGetValue(material, out var s)) return null;
        if (Material(PltTinter.Resref(replacement) ?? replacement) is { HasCustomShader: true } other)
        {
            var made = For(s.Mesh, other, s.Texture, s.Kind);
            // The mesh was built without the per-vertex data its new shader reads: vPos and m_mv are the model's.
            if (made != null && made.HasMeta(MeshSpaceMeta) && !material.HasMeta(MeshSpaceMeta))
                Log("retexture:" + other.Name, $"{other.Name}: put on a mesh by ReplaceObjectTexture, reads vPos in the model's space");
            return made;
        }
        var tex = _textures.Get(PaletteAlpha?.Invoke(replacement) ?? replacement);
        if (tex == null) return null;
        var copy = (ShaderMaterial)material.Duplicate();
        copy.SetShaderParameter("texUnit0", tex.Texture);
        copy.SetShaderParameter("texture0Bound", 1);
        copy.SetShaderParameter("m_texture", tex.FlipV ? FlipV : Projection.Identity);
        if (tex.FlipV) copy.SetMeta("ow_uv_flip", true);
        else if (copy.HasMeta("ow_uv_flip")) copy.RemoveMeta("ow_uv_flip");
        return copy;
    }

    /// <summary>The texture matrix of a texture stored bottom-up.</summary>
    static readonly Projection FlipV = new(new Vector4(1, 0, 0, 0), new Vector4(0, -1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 1));

    (ShaderMaterial? Material, bool Done) _post;

    /// <summary>
    /// The module's replacement for the post-process pass (fsfbpostpr, with vsfbpostpr from the module or the
    /// installed game), drawn over the finished 3D view; null when the module has none (the view is shown as it is)
    /// or it can't be used.
    /// </summary>
    public ShaderMaterial? PostProcess()
    {
        if (!Enabled) return null;
        if (_post.Done) return _post.Material;
        _post.Done = true;
        if (_translator.ModuleSource("fsfbpostpr") == null && _translator.ModuleSource("vsfbpostpr") == null) return null;
        try
        {
            var (shader, translation) = Compile("vsfbpostpr", "fsfbpostpr", new ShaderOptions { PostProcess = true }, "the post-process pass");
            if (shader == null) return null;
            var m = new ShaderMaterial { Shader = shader };
            m.SetMeta("ow_reads_depth", translation.ReadsDepth);
            _post.Material = m;
        }
        catch (Exception e)
        {
            Log("fsfbpostpr", $"the post-process pass: not used ({e.GetType().Name}: {e.Message})");
        }
        return _post.Material;
    }

    bool? _water, _postDepth;

    /// <summary>
    /// Whether the module's post-process pass reads depth: translucent world surfaces then write depth only where
    /// they are solid, as in the original (scene postfx: depth over a tree's clear leaf areas is what is behind).
    /// </summary>
    public bool PostProcessReadsDepth => _postDepth ??= Enabled && (_translator.ModuleSource("fsfbpostpr") != null || _translator.ModuleSource("vsfbpostpr") != null)
        && _translator.Translate("vsfbpostpr", "fsfbpostpr", new ShaderOptions { PostProcess = true }) is { Ok: true, ReadsDepth: true };

    /// <summary>Whether the module replaces the water program (fswater or vswater).</summary>
    public bool ModuleWater => _water ??= _translator.ModuleSource("fswater") != null || _translator.ModuleSource("vswater") != null;

    /// <summary>
    /// The water material for a mesh whose texture is the original's water (TXI proceduretype arturo), when the
    /// module replaces the water program: vswater and fswater, the module's or the installed game's. Null keeps the
    /// mesh's normal material (no replacement, or it can't be used).
    /// </summary>
    public ShaderMaterial? Water(MeshData mesh, string texture, MaterialKind kind)
    {
        if (!Enabled || !ModuleWater || texture.Length == 0) return null;
        var tex = _textures.Get(texture);
        if (tex == null || tex.Info.Procedure != "arturo") return null;
        var mtr = new MaterialData { Name = "water:" + texture, VertexShader = "vswater", FragmentShader = "fswater" };
        mtr.Textures[0] = texture;
        try
        {
            return Build(mesh, mtr, texture, kind, null);
        }
        catch (Exception e)
        {
            Log("water", $"water ({texture}): not used ({e.GetType().Name}: {e.Message}); the mesh keeps its normal material");
            return null;
        }
    }

    /// <summary>The sky's version of a module shader material: unfogged, behind everything, both sides.</summary>
    public ShaderMaterial? Sky(ShaderMaterial material)
    {
        if (!_sources.TryGetValue(material, out var s)) return null;
        ShaderMaterial? sky;
        try
        {
            sky = Build(s.Mesh, s.Material, s.Texture, s.Kind, new ShaderOptions { FogDisabled = true, DepthDrawNever = true, CullDisabled = true });
        }
        catch (Exception)
        {
            return null;
        }
        if (sky != null) sky.RenderPriority = -100;
        return sky;
    }

    /// <summary>A tinted palette texture's key with the palette's alpha kept (PltTinter.WithAlpha), or null.</summary>
    public Func<string, string?>? PaletteAlpha { get; set; }

    ShaderMaterial? Build(MeshData mesh, MaterialData mtr, string texture, MaterialKind kind, ShaderOptions? overrides)
    {
        string main = mtr.Textures[0].Length > 0 ? mtr.Textures[0] : PaletteAlpha?.Invoke(texture) ?? texture;
        var tex = _textures.Get(main);
        var options = overrides ?? Options(mesh, mtr, tex, kind, out _);
        Options(mesh, mtr, tex, kind, out float discard);
        // Water builds its colour from the view behind it (refraction, reflection): that colour is already lit.
        // The original's water writes its surface's depth (scene waterfx: a post-process pass reads the surface).
        if (mtr.FragmentShader == "fswater") options = options with { Emissive = true, Transparent = true, DepthDrawAlways = true };
        string vs = mtr.VertexShader.Length > 0 ? mtr.VertexShader : mtr.RenderHint.Contains("Normal", StringComparison.OrdinalIgnoreCase) ? "vslit_nm" : "vslit";
        string fs = mtr.FragmentShader.Length > 0 ? mtr.FragmentShader : mtr.RenderHint.Contains("Normal", StringComparison.OrdinalIgnoreCase) ? "fslit_nm" : "fslit";
        var (shader, translation) = Compile(vs.ToLowerInvariant(), fs.ToLowerInvariant(), options, mtr.Name);
        if (shader == null) return null;
        // A shader that reads its mesh's bounding box gets a material per mesh.
        var bounds = translation.Bounds ? Bounds(mesh) : default;
        string key = $"{mtr.Name}|{texture}|{kind}|{mesh.Alpha}|{mesh.SelfIllumination}|{options}" + (translation.Bounds ? $"|{bounds}" : "");
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var m = new ShaderMaterial { Shader = shader };
        m.SetMeta("ow_custom", true);
        m.SetMeta("ow_mtr", mtr.Name);
        m.SetMeta(BitmapMeta, texture);
        if (translation.Keyholing) m.SetMeta("ow_keyhole", true);
        if (translation.MeshSpace) m.SetMeta(MeshSpaceMeta, translation.PlacesVertex ? 2 : 1);
        var parameters = translation.Parameters.ToDictionary(p => p.Name, StringComparer.Ordinal);
        void Set(string name, Variant value)
        {
            if (parameters.ContainsKey(name)) m.SetShaderParameter(name, value);
        }
        for (int i = 0; i < MaterialData.TextureSlots; i++)
        {
            var slot = i == 0 ? tex : mtr.Textures[i].Length > 0 ? _textures.Get(mtr.Textures[i]) : null;
            if (slot == null) continue;
            Set($"texUnit{i}", slot.Texture);
            Set($"texture{i}Bound", 1);
        }
        if (tex is { FlipV: true })
        {
            // Rows stored bottom-up: the texture matrix flips V, as the plain material's UV transform does.
            Set("m_texture", FlipV);
            m.SetMeta("ow_uv_flip", true);
        }
        // Water's environment map is its texture's bumpyshinytexture; other materials keep only envmaptexture.
        string bumpyShiny = mtr.FragmentShader == "fswater" ? tex?.Info.BumpyShiny ?? "" : "";
        if (tex != null && (tex.Info.EnvironmentMap.Length > 0 ? tex.Info.EnvironmentMap : bumpyShiny) is { Length: > 0 } envName
            && _textures.Get(envName) is { } env)
        {
            Set("texUnitEnv", env.Texture);
            Set("texEnvBound", 1);
        }
        if (translation.Bounds)
        {
            Set("bboxMin", bounds.Min);
            Set("bboxMax", bounds.Max);
        }
        // The noise texture the engine gives shaders (water waves): the game's own solid_noise.
        if (parameters.ContainsKey("texUnitNoise") && _textures.Get(NoiseTexture) is { } noise)
        {
            Set("texUnitNoise", noise.Texture);
            Set("texNoiseBound", 1);
        }
        Set("materialFrontDiffuse", new Vector4(1, 1, 1, Math.Clamp(mesh.Alpha, 0f, 1f)));
        Set("fAlphaDiscardValue", discard);
        var illum = mesh.SelfIllumination;
        m.SetShaderParameter("ow_self_illumination", new Vector3(MathF.Pow(Math.Max(illum.X, 0), 2.2f), MathF.Pow(Math.Max(illum.Y, 0), 2.2f), MathF.Pow(Math.Max(illum.Z, 0), 2.2f)));
        foreach (var p in mtr.Parameters)
            if (parameters.TryGetValue(ShaderTranslator.UniformName(p.Name), out var declared))
                m.SetShaderParameter(declared.Name, Value(p, declared.Type));
        _cache[key] = m;
        _sources.AddOrUpdate(m, new Source(mtr, mesh, texture, kind));
        return m;
    }

    /// <summary>Meta of materials whose meshes must give each vertex its mesh-space data (ModelBuilder).</summary>
    public const string MeshSpaceMeta = "ow_mesh_space";

    /// <summary>A mesh's bounding box in its own space.</summary>
    static (Vector3 Min, Vector3 Max) Bounds(MeshData mesh)
    {
        if (mesh.Vertices.Length == 0) return (Vector3.Zero, Vector3.Zero);
        var min = new System.Numerics.Vector3(float.MaxValue);
        var max = new System.Numerics.Vector3(float.MinValue);
        foreach (var v in mesh.Vertices)
        {
            min = System.Numerics.Vector3.Min(min, v);
            max = System.Numerics.Vector3.Max(max, v);
        }
        return (Convert.V(min), Convert.V(max));
    }

    /// <summary>An MTR parameter as the type the shader declares it with.</summary>
    public static Variant Value(MaterialParameter p, string type)
    {
        float V(int i) => i < p.Values.Length ? p.Values[i] : 0f;
        return type switch
        {
            "int" => p.IntValue,
            "bool" => p.IntValue != 0,
            "vec2" => new Vector2(V(0), V(1)),
            "vec3" => new Vector3(V(0), V(1), V(2)),
            "vec4" => new Vector4(V(0), V(1), V(2), V(3)),
            _ => V(0),
        };
    }

    /// <summary>
    /// How the material is drawn: the same alpha rules as the plain material (blended, alpha-tested, opaque with an
    /// environment-map mask) and the MTR's transparency.
    /// </summary>
    static ShaderOptions Options(MeshData mesh, MaterialData mtr, TextureFactory.Entry? tex, MaterialKind kind, out float discard)
    {
        discard = -1f;
        bool blended = mesh.Alpha < 0.99f || mtr.Transparency == true, additive = false, cull = blended;
        if (tex != null)
        {
            var info = tex.Info;
            if (info.Blending == "additive") additive = true;
            else if (tex.HasAlpha)
            {
                bool envMask = info.EnvironmentMap.Length > 0 || kind == MaterialKind.Creature;
                if (!(envMask && !info.Decal && info.Blending != "punchthrough"))
                {
                    if (info.Blending == "default" && info.HasInfo && !info.Decal) { blended = true; cull = true; }
                    else { discard = 0.5f; cull = true; }
                }
            }
        }
        if (blended && discard < 0) discard = 0f;
        return new ShaderOptions
        {
            Transparent = blended,
            Additive = additive,
            CullDisabled = cull,
            DepthDrawAlways = blended && kind == MaterialKind.World,
            Declarations = KeyholeDeclarations,
            FragmentEnd = KeyholeCut,
        };
    }

    static readonly string KeyholeDeclarations = Keyhole.ShaderCode + "\ninstance uniform float ow_keyhole = 0.0;\n";
    const string KeyholeCut = """
	if (ow_keyhole > 0.5) {
		float ow_visible = keyhole_visibility(VERTEX, INV_VIEW_MATRIX, SCREEN_UV, VIEWPORT_SIZE, CAMERA_POSITION_WORLD);
		if (ow_visible <= 0.0) discard;
		ALBEDO *= ow_visible;
	}
""";

    /// <summary>The game texture bound as texUnitNoise.</summary>
    public const string NoiseTexture = "solid_noise";

    (Shader?, ShaderTranslation) Compile(string vs, string fs, ShaderOptions options, string material)
    {
        string key = $"{vs}|{fs}|{options.Transparent}|{options.Additive}|{options.CullDisabled}|{options.DepthDrawAlways}|{options.DepthDrawNever}|{options.FogDisabled}|{options.PostProcess}|{options.Emissive}";
        if (_shaders.TryGetValue(key, out var done)) return done;
        string program = $"{vs} + {fs}";
        var translation = new ShaderTranslation { VertexShader = vs, FragmentShader = fs };
        Shader? shader = null;
        try
        {
            translation = _translator.Translate(vs, fs, options);
            if (translation.Ok)
            {
                ShaderGlobals.Register();
                var candidate = new Shader { Code = translation.Code };
                // A shader that fails to compile has no parameters at all; every translation declares ow_ok.
                if (candidate.GetShaderUniformList().Any(u => u.AsGodotDictionary()["name"].AsString() == "ow_ok")) shader = candidate;
                else Log(program, $"{material}: {program} did not compile (the shader error is above); the mesh keeps its normal material");
            }
            else Log(program, $"{material}: {program} not used ({translation.Reason}); the mesh keeps its normal material");
        }
        catch (Exception e)
        {
            // Never let a module's shader stop an area loading: the mesh keeps its normal material.
            translation.Problems.Add(e.GetType().Name);
            shader = null;
            Log(program, $"{material}: {program} not used ({e.GetType().Name}: {e.Message}); the mesh keeps its normal material");
        }
        if (shader != null && !Programs.ContainsKey(program))
        {
            Programs[program] = "ok";
            GD.Print($"[shaders] {program}: translated for {material} and later materials");
        }
        else if (shader == null) Programs[program] = translation.Ok ? "did not compile" : translation.Reason;
        return _shaders[key] = (shader, translation);
    }

    void Log(string program, string message)
    {
        if (_logged.Add(program)) GD.PushWarning("[shaders] " + message);
    }
}
