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
        _own = resources.Sources.Where(s => !ShaderSources.IsInstalled(resources, s)).ToList();
    }

    /// <summary>
    /// The module's MTR of this name (module, haks, override), or null. The game's own MTRs are left alone: meshes
    /// that name them keep the material OpenWinter has always given them until they are measured against the original.
    /// </summary>
    public MaterialData? Material(string resref) =>
        string.IsNullOrEmpty(resref) ? null : _materials.GetOrAdd(resref, r =>
        {
            var key = ResKey.Of(r, ResType.Mtr);
            foreach (var s in _own)
                if (s.Read(key) is { } b) return Mtr.Parse(b, r);
            return null;
        });

    readonly List<IResourceSource> _own;

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

    ShaderMaterial? Build(MeshData mesh, MaterialData mtr, string texture, MaterialKind kind, ShaderOptions? overrides)
    {
        string main = mtr.Textures[0].Length > 0 ? mtr.Textures[0] : texture;
        var tex = _textures.Get(main);
        var options = overrides ?? Options(mesh, mtr, tex, kind, out _);
        Options(mesh, mtr, tex, kind, out float discard);
        string vs = mtr.VertexShader.Length > 0 ? mtr.VertexShader : mtr.RenderHint.Contains("Normal", StringComparison.OrdinalIgnoreCase) ? "vslit_nm" : "vslit";
        string fs = mtr.FragmentShader.Length > 0 ? mtr.FragmentShader : mtr.RenderHint.Contains("Normal", StringComparison.OrdinalIgnoreCase) ? "fslit_nm" : "fslit";
        string key = $"{mtr.Name}|{texture}|{kind}|{mesh.Alpha}|{mesh.SelfIllumination}|{options}";
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var (shader, translation) = Compile(vs.ToLowerInvariant(), fs.ToLowerInvariant(), options, mtr.Name);
        if (shader == null) return null;

        var m = new ShaderMaterial { Shader = shader };
        m.SetMeta("ow_custom", true);
        m.SetMeta("ow_mtr", mtr.Name);
        if (translation.Keyholing) m.SetMeta("ow_keyhole", true);
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
            Set("m_texture", new Projection(new Vector4(1, 0, 0, 0), new Vector4(0, -1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 1)));
            m.SetMeta("ow_uv_flip", true);
        }
        if (tex != null && tex.Info.EnvironmentMap.Length > 0 && _textures.Get(tex.Info.EnvironmentMap) is { } env)
        {
            Set("texUnitEnv", env.Texture);
            Set("texEnvBound", 1);
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

    (Shader?, ShaderTranslation) Compile(string vs, string fs, ShaderOptions options, string material)
    {
        string key = $"{vs}|{fs}|{options.Transparent}|{options.Additive}|{options.CullDisabled}|{options.DepthDrawAlways}|{options.DepthDrawNever}|{options.FogDisabled}";
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
