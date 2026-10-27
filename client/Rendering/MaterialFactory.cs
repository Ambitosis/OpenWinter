using System.Collections.Concurrent;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Rendering;

public enum MaterialKind { World, Creature }

/// <summary>Builds and caches materials for meshes.</summary>
public sealed class MaterialFactory
{
    readonly TextureFactory _textures;
    readonly ConcurrentDictionary<string, Material> _cache = new();

    public MaterialFactory(TextureFactory textures) { _textures = textures; }

    public TextureFactory Textures => _textures;

    /// <summary>Model lookup for emitters that draw another model's mesh as their particles (chunks).</summary>
    public Func<string, ModelData?>? Models { get; set; }

    /// <summary>Materials (MTR) and module shaders, for bitmaps that name a material.</summary>
    public Shaders.ModuleShaders? Shaders { get; set; }

    /// <param name="moduleShaders">False for overlays that restyle the plain material (they need a StandardMaterial3D).</param>
    public Material? Get(MeshData mesh, MaterialKind kind, string? textureOverride = null, bool moduleShaders = true)
    {
        string texName = textureOverride ?? mesh.Texture;
        // A bitmap that names an MTR: its custom shaders, or else the texture it names.
        if (Shaders != null && texName.Length is > 0 and <= 16 && !texName.Contains(':') && Shaders.Material(texName) is { } mtr)
        {
            if (moduleShaders && Shaders.For(mesh, mtr, texName, kind) is { } custom) return custom;
            if (mtr.Textures[0].Length > 0) texName = mtr.Textures[0];
        }
        // The original's water, when the module replaces the water program.
        if (moduleShaders && Shaders is { ModuleWater: true } && Shaders.Water(mesh, texName, kind) is { } water) return water;
        bool translucent = mesh.Alpha < 0.99f;
        // Surfaces get only the highlights their model asks for (its specular colour), as in the original: the
        // renderer's default dielectric specular brightened sunlit ground by about a seventh (parity/visual, scene
        // lightcal) and made black wall caps glint in the tile lights.
        float specular = Math.Clamp(Math.Max(mesh.Specular.X, Math.Max(mesh.Specular.Y, mesh.Specular.Z)), 0f, 1f) * 0.5f;
        string key = $"{texName}|{kind}|{mesh.Diffuse}|{mesh.TransparencyHint}|{(translucent ? mesh.Alpha : 1f)}|{mesh.Beaming}|{specular}|{mesh.SelfIllumination}";
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var m = new StandardMaterial3D
        {
            // Plain N.L diffuse, as the original lights (Godot's default Burley term darkens grazing light).
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
            Roughness = 0.92f,
            MetallicSpecular = specular,
            // No specular at all, not even the Fresnel sheen at grazing angles.
            SpecularMode = specular > 0 ? BaseMaterial3D.SpecularModeEnum.SchlickGgx : BaseMaterial3D.SpecularModeEnum.Disabled,
            AlbedoColor = new Color(1, 1, 1, mesh.Alpha),
            EmissionEnabled = mesh.SelfIllumination != System.Numerics.Vector3.Zero,
            Emission = EmissionColor(mesh.SelfIllumination),
            EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Add,
        };
        var tex = _textures.Get(texName);
        if (tex != null)
        {
            m.AlbedoTexture = tex.Texture;
            m.EmissionTexture = tex.Texture;
            m.EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Multiply;
            m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
            if (tex.FlipV)
            {
                m.Uv1Scale = new Vector3(1, -1, 1);
                m.Uv1Offset = new Vector3(0, 1, 0);
            }
            ApplyAlpha(m, tex, mesh, kind);
        }
        else if (texName.Length > 0 && texName.Contains("black", StringComparison.OrdinalIgnoreCase))
        {
            // Tilesets fill the void above and between walls with "..._black" textured caps; when a
            // hak forgets to ship that texture the cap must still read as void, not as grey stone.
            m.AlbedoColor = new Color(0, 0, 0, mesh.Alpha);
        }
        else if (texName.Length > 0)
        {
            m.AlbedoColor = new Color(0.55f, 0.55f, 0.55f, mesh.Alpha);
        }
        if (translucent)
        {
            m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        }
        // Overlapping coplanar translucent surfaces (water planes from neighbouring tiles)
        // must not blend twice: write depth so the second one fails the depth test.
        if (m.Transparency == BaseMaterial3D.TransparencyEnum.Alpha && kind == MaterialKind.World && m.BlendMode == BaseMaterial3D.BlendModeEnum.Mix)
        {
            // Solid texels must also enter the opaque depth texture used by the highlight
            // compositor. A late alpha-pass depth write alone lets heard-only halos cross walls.
            m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass;
            m.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always;
            // A module's post-process pass that reads depth sees what is behind a translucent surface's clear parts
            // (tree leaves), as in the original (scene postfx): only the solid texels' prepass depth is kept. Water
            // keeps writing its surface's depth (scene waterfx).
            if (Shaders is { PostProcessReadsDepth: true } && tex?.Info.Procedure != "arturo")
                m.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly;
        }
        _cache[key] = m;
        return m;
    }

    // Native meshillum controls use authored display levels for self illumination.
    // StandardMaterial3D decodes its color as sRGB; encode the desired gamma-2.2
    // linear value at that API boundary. This is independent of the user's output
    // gamma, which remains GraphicsApply's final display adjustment.
    static Color EmissionColor(System.Numerics.Vector3 value) => new Color(
        MathF.Pow(Math.Max(value.X, 0f), 2.2f),
        MathF.Pow(Math.Max(value.Y, 0f), 2.2f),
        MathF.Pow(Math.Max(value.Z, 0f), 2.2f)).LinearToSrgb();

    /// <summary>
    /// Texture alpha means different things: cut-out (foliage, fences),
    /// blended (water, glass), additive (glows), or an environment-map mask
    /// (shiny armour, creature skin) that must render opaque.
    /// </summary>
    static void ApplyAlpha(StandardMaterial3D m, TextureFactory.Entry tex, MeshData mesh, MaterialKind kind)
    {
        var info = tex.Info;
        if (info.Blending == "additive")
        {
            m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
            m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            // Authored front/back controls in meshfaces show that native additive
            // meshes keep back-face culling, just like ordinary opaque meshes.
            return;
        }
        if (!tex.HasAlpha) return;
        bool envMask = info.EnvironmentMap.Length > 0 || kind == MaterialKind.Creature;
        if (envMask && !info.Decal && info.Blending != "punchthrough") return;
        if (info.Blending == "default" && info.HasInfo && !info.Decal)
        {
            // Blended surfaces like water: depth is written so overlapping planes blend once.
            m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            m.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always;
            m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            return;
        }
        m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
        m.AlphaScissorThreshold = 0.5f;
        m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
    }
}
