using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.UI.Nui;
using OpenWinter.Client.Views;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original interface's art, read from the game's data at run time (and so replaced by haks the way the
/// original allows): interface models, textures, bitmap fonts, layouts (.gui) and talk-table strings.
/// One per <see cref="ClientContent"/>; nothing is shipped with OpenWinter.
/// </summary>
public sealed class ClassicAssets
{
    static readonly ConditionalWeakTable<ClientContent, ClassicAssets> All = new();

    public static ClassicAssets For(ClientContent content) => All.GetValue(content, c => new ClassicAssets(c));

    public readonly ClientContent Content;
    readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, GuiModel?> _models = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, GuiLayout?> _layouts = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, GuiFont> _fonts = new(StringComparer.OrdinalIgnoreCase);

    ClassicAssets(ClientContent content) { Content = content; }

    /// <summary>An interface texture (top-down); null when missing.</summary>
    public Texture2D? Texture(string resref)
    {
        if (string.IsNullOrEmpty(resref)) return null;
        if (_textures.TryGetValue(resref, out var t)) return t;
        if (resref.StartsWith("iconplt:", StringComparison.Ordinal)) return _textures[resref] = Content.Textures.Get(resref)?.Texture;
        return _textures[resref] = NuiSkin.LoadTexture(Content, resref.ToLowerInvariant());
    }

    /// <summary>An interface model; null when missing.</summary>
    public GuiModel? Model(string resref)
    {
        if (string.IsNullOrEmpty(resref)) return null;
        if (_models.TryGetValue(resref, out var m)) return m;
        var data = Content.Models.Get(resref.ToLowerInvariant());
        return _models[resref] = data == null ? null : new GuiModel(data);
    }

    /// <summary>A layout file (.gui); null when missing.</summary>
    public GuiLayout? Layout(string name)
    {
        if (_layouts.TryGetValue(name, out var l)) return l;
        GuiLayout? layout = null;
        try
        {
            if (Content.Resources.Read(name, ResType.Gui) is { } data) layout = GuiReader.Read(data, name);
        }
        catch (Exception e) when (e is InvalidDataException or IndexOutOfRangeException or ArgumentException)
        {
            Content.WarnOnce($"interface layout {name} could not be read ({e.Message})");
        }
        return _layouts[name] = layout;
    }

    /// <summary>A font by resref (a bitmap font with its TXI, or a TrueType font); the default font when missing.</summary>
    public GuiFont Font(string name)
    {
        if (string.IsNullOrEmpty(name)) name = GuiFont.DefaultName;
        if (_fonts.TryGetValue(name, out var f)) return f;
        f = GuiFont.Load(this, name) ?? (name.Equals(GuiFont.DefaultName, StringComparison.OrdinalIgnoreCase) ? GuiFont.Fallback() : Font(GuiFont.DefaultName));
        return _fonts[name] = f;
    }

    /// <summary>A talk-table string ("" for none).</summary>
    public string Tlk(uint strref) => strref == uint.MaxValue ? "" : Content.Resources.TlkString(strref);

    /// <summary>A caption's text: its talk-table string, else its own text.</summary>
    public string Text(GuiCaption? caption) =>
        caption == null ? "" : caption.StrRef != uint.MaxValue ? Tlk(caption.StrRef) : caption.Text;
}
