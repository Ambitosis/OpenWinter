using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Creature portraits from the module's textures. Portrait files come in sizes h/l/m/s/t; the
/// picture fills the top of a taller texture (e.g. 128×200 of 128×256), so it is cropped to that.
/// </summary>
public static class Portraits
{
    static readonly Dictionary<string, Texture2D?> Cache = new();

    /// <summary>Forgets cached portraits when a new module's content replaces the old (resrefs can change meaning).</summary>
    public static void Clear() => Cache.Clear();

    /// <summary>
    /// The best portrait texture for a display height in pixels, cropped to an aspect (width / height):
    /// 0.64 is the whole portrait, 1 a head-and-shoulders square.
    /// </summary>
    public static Texture2D? Get(ClientContent? content, string resref, int height, float aspect = 0.64f)
    {
        if (content == null || resref.Length == 0) return null;
        string[] order = height > 110 ? ["h", "l", "m"] : height > 55 ? ["l", "m", "h"] : ["m", "l", "s"];
        string key = resref + "|" + order[0] + "|" + aspect.ToString("0.00");
        if (Cache.TryGetValue(key, out var cached)) return cached;
        Texture2D? result = null;
        foreach (var suffix in order)
        {
            if (content.Textures.Get(resref + suffix) is not { } tex) continue;
            // Crop (and turn bottom-up images upright) once, into a plain texture shaders can sample 0..1.
            var img = tex.Texture.GetImage();
            if (img == null) continue;
            if (img.IsCompressed()) img.Decompress();
            if (tex.FlipV) img.FlipY();
            int full = Math.Min(img.GetHeight(), (int)(img.GetWidth() * 1.5625f));
            int h = Math.Min(full, (int)(img.GetWidth() / aspect));
            int top = Math.Min(full - h, (int)(full * 0.05f));
            var region = img.GetRegion(new Rect2I(0, Math.Max(0, top), img.GetWidth(), h));
            region.GenerateMipmaps();
            result = ImageTexture.CreateFromImage(region);
            break;
        }
        Cache[key] = result;
        return result;
    }

    static Shader? _rounded;

    /// <summary>A portrait clipped to a rounded rectangle (or a circle), with an optional desaturated look for the dead.</summary>
    public static TextureRect View(Vector2 size, float radius)
    {
        _rounded ??= new Shader
        {
            Code = """
            shader_type canvas_item;
            uniform vec2 size = vec2(64.0, 96.0);
            uniform float radius = 8.0;
            uniform float grey = 0.0;
            void fragment() {
                vec4 c = texture(TEXTURE, UV);
                vec2 p = UV * size;
                vec2 q = abs(p - size * 0.5) - (size * 0.5 - vec2(radius));
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float l = dot(c.rgb, vec3(0.3, 0.59, 0.11));
                c.rgb = mix(c.rgb, vec3(l) * 0.7, grey);
                c.a *= clamp(0.5 - d, 0.0, 1.0);
                COLOR = c;
            }
            """,
        };
        var rect = new TextureRect
        {
            CustomMinimumSize = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var mat = new ShaderMaterial { Shader = _rounded };
        mat.SetShaderParameter("size", size);
        mat.SetShaderParameter("radius", radius);
        rect.Material = mat;
        return rect;
    }

    public static void Show(TextureRect view, ClientContent? content, string resref, bool dead = false)
    {
        var size = view.CustomMinimumSize;
        var tex = Get(content, resref, (int)size.Y, size.X / size.Y);
        view.Texture = tex ?? Icons.Get("character", 128);
        if (view.Material is ShaderMaterial m)
        {
            m.SetShaderParameter("grey", dead ? 1f : 0f);
        }
        view.SelfModulate = tex == null ? Palette.TextMuted : Colors.White;
    }
}
