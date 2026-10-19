using OpenWinter.Aurora.Textures;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Rendering;

/// <summary>
/// Colours palette textures (PLT). Each PLT pixel has a shade (0-255) and a
/// layer; the layer picks a palette image and a colour row in it, and the
/// shade picks the column.
/// </summary>
public sealed class PltTinter
{
    /// <summary>Layer order: skin, hair, metal 1, metal 2, cloth 1, cloth 2, leather 1, leather 2, tattoo 1, tattoo 2.</summary>
    public const int Layers = 10;
    static readonly string[] Palettes =
    {
        "pal_skin01", "pal_hair01", "pal_armor01", "pal_armor02", "pal_cloth01",
        "pal_cloth01", "pal_leath01", "pal_leath01", "pal_tattoo01", "pal_tattoo01",
    };

    readonly TextureFactory _textures;
    readonly Dictionary<string, (byte[] Rgb, int W, int H)?> _palettes = new(StringComparer.OrdinalIgnoreCase);

    public PltTinter(TextureFactory textures) { _textures = textures; }

    public bool IsPalette(string resref) => resref.Length > 0 && _textures.Source.KindOf(resref) == TextureCache.Kind.Palette;

    /// <summary>Returns a texture key registered with the texture factory.</summary>
    public string? Tint(string resref, byte[] layerColors)
    {
        string key = $"plt:{resref}:{System.Convert.ToHexString(layerColors)}";
        if (_textures.Cached(key) != null) return key;
        var plt = _textures.Source.GetPalette(resref);
        if (plt == null) return null;
        var rgb = new byte[plt.Width * plt.Height * 3];
        for (int i = 0; i < plt.Width * plt.Height; i++)
        {
            int layer = Math.Min(plt.Layers[i], (byte)(Layers - 1));
            var pal = Palette(Palettes[layer]);
            if (pal is not { } p) { rgb[i * 3] = rgb[i * 3 + 1] = rgb[i * 3 + 2] = plt.Shades[i]; continue; }
            int row = Math.Clamp(layerColors[layer], 0, p.H - 1);
            int col = plt.Shades[i] * p.W / 256;
            int src = (row * p.W + col) * 3;
            rgb[i * 3] = p.Rgb[src]; rgb[i * 3 + 1] = p.Rgb[src + 1]; rgb[i * 3 + 2] = p.Rgb[src + 2];
        }
        var image = Image.CreateFromData(plt.Width, plt.Height, false, Image.Format.Rgb8, rgb);
        image.GenerateMipmaps();
        _textures.Put(key, image, hasAlpha: false);
        return key;
    }

    /// <summary>An inventory icon PLT layer (<see cref="PltIcon"/>: transparent canvas, palette alpha, icon palettes).</summary>
    public TextureFactory.Entry? Icon(string key, string resref, byte[] colors)
    {
        var plt = _textures.Source.GetPalette(resref);
        if (plt == null || colors.Length != Layers) return null;
        var rgba = PltIcon.Paint(plt, colors, IconPalette);
        var (w, h) = PltIcon.Canvas(plt.Width, plt.Height);
        return _textures.Put(key, Image.CreateFromData(plt.Width, plt.Height, false, Image.Format.Rgba8, rgba), true, new Vector2(w, h));
    }

    readonly Dictionary<string, PltIcon.Palette?> _iconPalettes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A palette with its alpha, for icons.</summary>
    PltIcon.Palette? IconPalette(string name)
    {
        if (_iconPalettes.TryGetValue(name, out var cached)) return cached;
        PltIcon.Palette? result = null;
        if (_textures.Source.Get(name) is { } data && TextureFactory.ToImage(data) is { } image)
        {
            if (image.IsCompressed()) image.Decompress();
            image.ClearMipmaps();
            if (data.FlipV) image.FlipY();
            image.Convert(Image.Format.Rgba8);
            result = new PltIcon.Palette(image.GetData(), image.GetWidth(), image.GetHeight());
        }
        return _iconPalettes[name] = result;
    }

    (byte[] Rgb, int W, int H)? Palette(string name)
    {
        if (_palettes.TryGetValue(name, out var cached)) return cached;
        (byte[], int, int)? result = null;
        if (_textures.Source.Get(name) is { } data && TextureFactory.ToImage(data) is { } image)
        {
            if (image.IsCompressed()) image.Decompress();
            image.ClearMipmaps();
            if (data.FlipV) image.FlipY();
            image.Convert(Image.Format.Rgb8);
            result = (image.GetData(), image.GetWidth(), image.GetHeight());
        }
        _palettes[name] = result;
        return result;
    }

    /// <summary>A copy of the model whose PLT-textured meshes use tinted textures.</summary>
    public ModelData TintModel(ModelData model, byte[] layerColors)
    {
        bool any = model.Parts.Any(p => p.Mesh != null && IsPalette(p.Mesh.Texture));
        if (!any) return model;
        var copy = ShallowCopy(model);
        foreach (var part in copy.Parts)
            if (part.Mesh != null && IsPalette(part.Mesh.Texture) && Tint(part.Mesh.Texture, layerColors) is { } key)
            {
                part.Mesh = part.Mesh.Clone();
                part.Mesh.Texture = key;
            }
        return copy;
    }

    public static ModelData ShallowCopy(ModelData model)
    {
        var copy = new ModelData { Name = model.Name, Supermodel = model.Supermodel, AnimationScale = model.AnimationScale, Classification = model.Classification };
        foreach (var p in model.Parts)
            copy.Parts.Add(new ModelPart { Name = p.Name, Parent = p.Parent, Position = p.Position, Rotation = p.Rotation, Mesh = p.Mesh, Light = p.Light, Emitter = p.Emitter, TileFadeMode = p.TileFadeMode });
        foreach (var kv in model.Animations) copy.Animations[kv.Key] = kv.Value;
        return copy;
    }
}
