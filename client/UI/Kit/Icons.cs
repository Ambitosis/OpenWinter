using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// Interface glyphs: white SVGs in Assets/Icons (from game-icons.net, CC BY 3.0; see CREDITS.md),
/// rasterised on demand at the size asked for and tinted by whoever draws them.
/// </summary>
public static class Icons
{
    static readonly Dictionary<(string, int), Texture2D?> Cache = new();

    public static Texture2D? Get(string name, int size = 64)
    {
        if (Cache.TryGetValue((name, size), out var cached)) return cached;
        Texture2D? tex = null;
        var path = "res://Assets/Icons/" + name + ".svg";
        if (Godot.FileAccess.FileExists(path))
        {
            var img = new Image();
            if (img.LoadSvgFromString(Godot.FileAccess.GetFileAsString(path), size / 512f) == Error.Ok)
            {
                img.GenerateMipmaps();
                tex = ImageTexture.CreateFromImage(img);
            }
        }
        else GD.PrintErr($"[ui] icon {name} not found");
        Cache[(name, size)] = tex;
        return tex;
    }

    /// <summary>A tinted icon control, centred in its box.</summary>
    public static TextureRect Rect(string name, int size, Color? tint = null) => new()
    {
        Texture = Get(name, Math.Max(32, size * 2)),
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SelfModulate = tint ?? Palette.TextSecondary,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}
