using System.Collections.Concurrent;
using OpenWinter.Aurora.Textures;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Rendering;

/// <summary>Turns decoded textures into GPU textures, cached by resref (and by palette colours for PLTs).</summary>
public sealed class TextureFactory
{
    public readonly TextureCache Source;
    readonly ConcurrentDictionary<string, Entry?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Entry
    {
        public required Texture2D Texture;
        public required TextureData? Data;
        public bool HasAlpha => Data?.HasAlpha ?? false;
        public bool FlipV => Data?.FlipV ?? false;
        public TextureInfo Info => Data?.Info ?? new TextureInfo();
        /// <summary>Palette icons (iconplt): the part of the texture the item shows (<see cref="PltIcon.Canvas"/>: a
        /// 64x128 armour part shows as 64x96); null for other textures.</summary>
        public Vector2? Canvas;
    }

    public TextureFactory(TextureCache source) { Source = source; }

    PltTinter? _iconTinter;

    public Entry? Get(string resref)
    {
        if (string.IsNullOrEmpty(resref)) return null;
        if (resref.StartsWith("iconplt:", StringComparison.Ordinal))
        {
            if (_cache.TryGetValue(resref, out var hit)) return hit;
            var parts = resref.Split(':');
            if (parts.Length != 3 || parts[2].Length != 20 || parts[2].Any(c => !Uri.IsHexDigit(c))) return null;
            // A layer whose PLT this client hasn't installed (a server-only override) stays missing: callers skip it.
            return (_iconTinter ??= new PltTinter(this)).Icon(resref, parts[1], System.Convert.FromHexString(parts[2])) ?? (_cache[resref] = null);
        }
        return _cache.GetOrAdd(resref, r =>
        {
            var data = Source.Get(r);
            if (data == null) return null;
            var image = ToImage(data);
            // The resref is kept as the resource name so materials can be matched by texture (ReplaceObjectTexture).
            return image == null ? null : new Entry { Texture = Named(ImageTexture.CreateFromImage(image), r), Data = data };
        });
    }

    public static Image? ToImage(TextureData d)
    {
        var format = d.Format switch
        {
            TextureFormat.Rgba8 => Image.Format.Rgba8,
            TextureFormat.Rgb8 => Image.Format.Rgb8,
            TextureFormat.L8 => Image.Format.L8,
            TextureFormat.Dxt1 => Image.Format.Dxt1,
            TextureFormat.Dxt3 => Image.Format.Dxt3,
            _ => Image.Format.Dxt5,
        };
        try
        {
            var image = Image.CreateFromData(d.Width, d.Height, d.MipCount > 1, format, d.Data);
            if (!image.IsCompressed() && !image.HasMipmaps()) image.GenerateMipmaps();
            else if (image.IsCompressed() && !image.HasMipmaps())
            {
                image.Decompress();
                image.GenerateMipmaps();
            }
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Registers an already-built texture under a key (palette-tinted PLTs).</summary>
    public Entry Put(string key, Image image, bool hasAlpha, Vector2? canvas = null)
    {
        var entry = new Entry { Texture = Named(ImageTexture.CreateFromImage(image), key), Data = new TextureData { HasAlpha = hasAlpha }, Canvas = canvas };
        _cache[key] = entry;
        return entry;
    }

    static Texture2D Named(Texture2D texture, string name)
    {
        texture.ResourceName = name;
        return texture;
    }

    public Entry? Cached(string key) => _cache.TryGetValue(key, out var e) ? e : null;
}
