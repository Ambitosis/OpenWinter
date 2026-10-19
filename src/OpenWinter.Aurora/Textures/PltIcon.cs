using OpenWinter.Content;

namespace OpenWinter.Aurora.Textures;

/// <summary>
/// Paints an inventory icon PLT (armour parts, helmets, cloaks) the way the original's icons look (visual scenes
/// armouricons*, docs/CLASSIC_UI.md "Armour icons"): shade 255 is the transparent canvas; any other pixel takes the
/// colour and the alpha of its layer's palette at (shade, row), so stacked layers blend where the palette is
/// translucent. Both metal layers read pal_armor02 in icons.
/// </summary>
public static class PltIcon
{
    public const int Layers = 10;

    /// <summary>Palette per PLT layer: skin, hair, metal 1, metal 2, cloth 1, cloth 2, leather 1, leather 2, tattoo 1, tattoo 2.</summary>
    public static readonly string[] Palettes =
    {
        "pal_skin01", "pal_hair01", "pal_armor02", "pal_armor02", "pal_cloth01",
        "pal_cloth01", "pal_leath01", "pal_leath01", "pal_tattoo01", "pal_tattoo01",
    };

    /// <summary>A palette image as RGBA, rows top to bottom (row = colour, column = shade).</summary>
    public sealed record Palette(byte[] Rgba, int Width, int Height);

    /// <summary>
    /// The part of a palette icon texture an item shows: the items with palette icons are 2x3 cells (armour, cloaks:
    /// the top 64x96 of a 64x128 texture, as the inventory, examine window and quickbar show them) or 2x2 (helmets, 64x64).
    /// </summary>
    public static (int Width, int Height) Canvas(int width, int height) => (width, Math.Min(height, width * 3 / 2));

    /// <summary>RGBA pixels (rows top to bottom) of <paramref name="plt"/> with one palette row per layer.</summary>
    public static byte[] Paint(PaletteTextureData plt, byte[] rows, Func<string, Palette?> palette)
    {
        var rgba = new byte[plt.Width * plt.Height * 4];
        for (int i = 0; i < plt.Shades.Length; i++)
        {
            int layer = plt.Layers[i];
            if (plt.Shades[i] == 255 || layer >= Layers || palette(Palettes[layer]) is not { } pal || pal.Width <= 0 || pal.Height <= 0) continue;
            int row = Math.Min(layer < rows.Length ? rows[layer] : 0, pal.Height - 1);
            int src = (row * pal.Width + plt.Shades[i] * pal.Width / 256) * 4;
            Buffer.BlockCopy(pal.Rgba, src, rgba, i * 4, 4);
        }
        return rgba;
    }
}
