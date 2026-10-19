using System.Collections.Concurrent;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Textures;

/// <summary>
/// Texture lookup by resref. The highest-priority source that has any form of
/// the texture wins (so a hak TGA overrides a base-game DDS); within one
/// source DDS beats TGA beats PLT. Thread-safe.
/// </summary>
public sealed class TextureCache
{
    readonly ResMan _rm;
    readonly ConcurrentDictionary<string, TextureData?> _textures = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, PaletteTextureData?> _palettes = new(StringComparer.OrdinalIgnoreCase);

    public TextureCache(ResMan rm) { _rm = rm; }

    public enum Kind { Missing, Image, Palette }

    public Kind KindOf(string resref)
    {
        if (string.IsNullOrEmpty(resref)) return Kind.Missing;
        foreach (var s in _rm.Sources)
        {
            if (s.Contains(ResKey.Of(resref, ResType.Dds)) || s.Contains(ResKey.Of(resref, ResType.Tga))) return Kind.Image;
            if (s.Contains(ResKey.Of(resref, ResType.Plt))) return Kind.Palette;
        }
        return Kind.Missing;
    }

    public TextureData? Get(string resref) =>
        string.IsNullOrEmpty(resref) ? null : _textures.GetOrAdd(resref, Load);

    public PaletteTextureData? GetPalette(string resref) =>
        string.IsNullOrEmpty(resref) ? null : _palettes.GetOrAdd(resref, r => _rm.Read(r, ResType.Plt) is { } b ? TextureDecoder.DecodePlt(b) : null);

    TextureData? Load(string resref)
    {
        TextureData? tex = null;
        foreach (var s in _rm.Sources)
        {
            if (s.Read(ResKey.Of(resref, ResType.Dds)) is { } dds) { tex = TextureDecoder.DecodeDds(dds); if (tex != null) break; }
            if (s.Read(ResKey.Of(resref, ResType.Tga)) is { } tga) { tex = TextureDecoder.DecodeTga(tga); if (tex != null) break; }
            if (s.Contains(ResKey.Of(resref, ResType.Plt))) return null;
        }
        if (tex == null) return null;
        if (_rm.Read(resref, ResType.Txi) is { } txi) tex.Info = TextureDecoder.ParseTxi(txi);
        return tex;
    }

    public TextureInfo Info(string resref) =>
        Get(resref)?.Info ?? (_rm.Read(resref, ResType.Txi) is { } txi ? TextureDecoder.ParseTxi(txi) : new TextureInfo());
}
