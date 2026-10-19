using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// The three typefaces (all SIL Open Font License, in Assets/Fonts): Cinzel for titles, Inter for
/// the interface, EB Garamond for things meant to be read (dialogue, journal, descriptions).
/// Variable fonts, so each weight is a <see cref="FontVariation"/> of one file.
/// </summary>
public static class Fonts
{
    static readonly Dictionary<string, FontFile?> Files = new();
    static readonly Dictionary<(string, int, float), Font> Variants = new();

    public static Font Display => Get("Cinzel.ttf", 600, 1.2f);
    public static Font DisplayBold => Get("Cinzel.ttf", 700, 1.5f);
    public static Font Ui => Get("Inter.ttf", 440);
    public static Font UiMedium => Get("Inter.ttf", 520);
    public static Font UiBold => Get("Inter.ttf", 650);
    public static Font Reading => Get("EBGaramond.ttf", 430);
    public static Font ReadingBold => Get("EBGaramond.ttf", 620);
    public static Font ReadingItalic => Get("EBGaramond-Italic.ttf", 430);

    static Font Get(string file, int weight, float tracking = 0)
    {
        if (Variants.TryGetValue((file, weight, tracking), out var cached)) return cached;
        Font result;
        if (LoadFile(file) is { } baseFont)
        {
            var v = new FontVariation { BaseFont = baseFont };
            var ts = TextServerManager.GetPrimaryInterface();
            v.VariationOpentype = new Godot.Collections.Dictionary { { ts.NameToTag("wght"), weight } };
            if (tracking != 0) v.SpacingGlyph = (int)Math.Round(tracking);
            result = v;
        }
        else result = ThemeDB.FallbackFont;
        Variants[(file, weight, tracking)] = result;
        return result;
    }

    static FontFile? LoadFile(string file)
    {
        if (Files.TryGetValue(file, out var f)) return f;
        string res = "res://Assets/Fonts/" + file;
        FontFile? font = null;
        if (ResourceLoader.Exists(res)) font = ResourceLoader.Load<FontFile>(res);
        else
        {
            var path = ProjectSettings.GlobalizePath(res);
            if (System.IO.File.Exists(path))
            {
                font = new FontFile();
                if (font.LoadDynamicFont(path) != Error.Ok) font = null;
            }
        }
        if (font != null)
        {
            font.Antialiasing = TextServer.FontAntialiasing.Gray;
            font.Hinting = TextServer.Hinting.Light;
            font.SubpixelPositioning = TextServer.SubpixelPositioning.Auto;
            font.GenerateMipmaps = true;
            // Symbols the text faces lack (arrows, check marks, other scripts) come from the system.
            font.Fallbacks = new Godot.Collections.Array<Font> { ThemeDB.FallbackFont };
        }
        else GD.PrintErr($"[ui] font {file} not found; using the default");
        Files[file] = font;
        return font;
    }
}
