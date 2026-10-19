using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>Text entry over a layout's edit box (ctl_cg_edit_fnam / _fam / _age, ctl_cg_edt_bio): white text in the caption face, the box drawn by its model.</summary>
public static class CgEdit
{
    /// <summary>A one-line edit in the box's text0..text1 area.</summary>
    public static LineEdit Over(ClassicAssets a, Control parent, GuiList box, int maxLength)
    {
        var frame = box.Frame!;
        var text = frame.Node("text0") is { } t0 && frame.Node("text1") is { } t1 ? new Rect2(t0, Vector2.Zero).Expand(t1) : new Rect2(4, 2, box.Size.X - 8, box.Size.Y - 4);
        var edit = new LineEdit
        {
            MaxLength = maxLength, Flat = true, CaretBlink = true, ContextMenuEnabled = false, SelectAllOnFocus = false,
            Position = box.Position + text.Position + new Vector2(-2, -3), Size = text.Size + new Vector2(4, 6),
        };
        Style(a, edit);
        parent.AddChild(edit);
        return edit;
    }

    /// <summary>A multi-line edit (the description) in the box's text0..text1 area.</summary>
    public static TextEdit Area(ClassicAssets a, Control parent, GuiList box)
    {
        var frame = box.Frame!;
        var text = frame.Node("text0") is { } t0 && frame.Node("text1") is { } t1 ? new Rect2(t0, Vector2.Zero).Expand(t1) : new Rect2(4, 4, box.Size.X - 24, box.Size.Y - 8);
        var edit = new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary, ContextMenuEnabled = false,
            Position = box.Position + text.Position + new Vector2(-3, -2), Size = text.Size + new Vector2(6, 4),
        };
        var font = ClassicText.For(a, ClassicText.NameFont, 16);
        edit.AddThemeFontOverride("font", font.Font);
        edit.AddThemeFontSizeOverride("font_size", font.Size);
        edit.AddThemeColorOverride("font_color", Colors.White);
        edit.AddThemeColorOverride("caret_color", Colors.White);
        edit.AddThemeConstantOverride("line_spacing", 0);
        foreach (var style in new[] { "normal", "focus", "read_only" }) edit.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        parent.AddChild(edit);
        return edit;
    }

    static void Style(ClassicAssets a, LineEdit edit)
    {
        var font = ClassicText.For(a, ClassicText.NameFont, 16);
        edit.AddThemeFontOverride("font", font.Font);
        edit.AddThemeFontSizeOverride("font_size", font.Size);
        edit.AddThemeColorOverride("font_color", Colors.White);
        edit.AddThemeColorOverride("caret_color", Colors.White);
        edit.AddThemeConstantOverride("minimum_character_width", 0);
        foreach (var style in new[] { "normal", "focus", "read_only" }) edit.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
    }
}

/// <summary>
/// Random names from the game's letter tables (.ltr), as the creation screens' Generate Name buttons give them: a
/// character's first name from racialtypes NameGenTableA / B + "m" or "f", the last name from the same + "l"
/// (c45: "Lomothrade Nulebin" for a female elf); familiars and companions from "familiar" and "animal".
/// </summary>
public static class CgNames
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ClientContent, Dictionary<string, Ltr?>> Cache = new();

    static Ltr? Table(ClientContent content, string resref)
    {
        var cache = Cache.GetValue(content, _ => new Dictionary<string, Ltr?>(StringComparer.OrdinalIgnoreCase));
        if (cache.TryGetValue(resref, out var t)) return t;
        Ltr? ltr = null;
        if (content.Resources.Read(resref, ResType.Ltr) is { } bytes)
        {
            try { ltr = Ltr.Parse(bytes); }
            catch (InvalidDataException) { ltr = null; }
        }
        return cache[resref] = ltr;
    }

    public static string Random(ClientContent content, string table) => Table(content, table)?.Generate(System.Random.Shared) ?? "";

    /// <summary>First and last name for a race's tables (A / B) and gender.</summary>
    public static (string First, string Last) Character(ClientContent content, IReadOnlyList<string> tables, int gender)
    {
        if (tables.Count == 0) tables = new[] { "human" };
        string Pick(string suffix)
        {
            var order = tables.OrderBy(_ => System.Random.Shared.Next()).Select(t => t + suffix).Append("human" + suffix);
            foreach (var t in order)
                if (Random(content, t) is { Length: > 0 } n) return n;
            return "";
        }
        return (Pick(gender == 1 ? "f" : "m"), Pick("l"));
    }
}
