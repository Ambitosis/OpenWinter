namespace OpenWinter.Content;

public enum TextureFormat { Rgba8, Rgb8, L8, Dxt1, Dxt3, Dxt5 }

/// <summary>Decoded texture pixels, rows top to bottom, with an optional mip chain.</summary>
public sealed class TextureData
{
    public int Width;
    public int Height;
    public TextureFormat Format;
    /// <summary>Mip levels including the base level; 1 if there is no chain.</summary>
    public int MipCount = 1;
    public byte[] Data = Array.Empty<byte>();
    public bool HasAlpha;
    /// <summary>Rows are stored bottom-up; renderers should flip V.</summary>
    public bool FlipV;
    public TextureInfo Info = new();
}

/// <summary>Per-texture render hints (Aurora TXI files).</summary>
public sealed class TextureInfo
{
    /// <summary>"default", "additive", "punchthrough".</summary>
    public string Blending = "default";
    public bool Decal;
    public string EnvironmentMap = "";
    public string BumpMap = "";
    public int ProcedureType;
    public int NumX = 1;
    public int NumY = 1;
    public float Fps;
    public bool HasInfo;
    /// <summary>TrueType glyph coverage and outline controls in font TXIs.</summary>
    public float TtfContrast = 1, TtfOutlineOpacity = 0.35f, TtfOutlineBrightness;
    /// <summary>A font's TXI spacingR: extra advance after each character (fnt_default_hr 0.003: docs/CLASSIC_UI.md "Party members").</summary>
    public float FontSpacingR;
}

/// <summary>Palette-indexed texture: per pixel a shade and a palette layer.</summary>
public sealed class PaletteTextureData
{
    public int Width;
    public int Height;
    /// <summary>Rows top to bottom.</summary>
    public byte[] Shades = Array.Empty<byte>();
    public byte[] Layers = Array.Empty<byte>();
}
