using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Drawing shared by the action bar, the radial menu and the abilities window: icons, badges, tooltips.</summary>
public static class SlotArt
{
    /// <summary>
    /// Draws a slot's icon into <paramref name="rect"/>: the game's icon layers when the module has them,
    /// else the kit glyph tinted brass (commands, emotes, modes without art).
    /// </summary>
    public static void DrawIcon(CanvasItem ci, ClientContent? content, SlotLook look, Rect2 rect, Color modulate, Color? glyphTint = null)
    {
        if (look.SecondaryIcons.Length > 0 && content != null)
        {
            DrawPair(ci, content, look, rect, modulate);
            return;
        }
        bool any = false;
        if (content != null && IconCanvas(content, look.Icons) is { } canvas)
        {
            // Palette icons (armour, helmets, cloaks) keep their shape: the shown canvas fitted and centred.
            float fit = Math.Min(rect.Size.X / canvas.X, rect.Size.Y / canvas.Y);
            var size = (canvas * fit).Round();
            var at = new Rect2((rect.Position + (rect.Size - size) / 2).Round(), size);
            foreach (var layer in look.Icons)
                if (layer.Length > 0 && content.Textures.Get(layer) is { } tex)
                    ci.DrawTextureRectRegion(tex.Texture, at, new Rect2(Vector2.Zero, canvas), modulate);
            return;
        }
        if (content != null)
            foreach (var layer in look.Icons)
            {
                if (layer.Length == 0 || content.Textures.Get(layer) is not { } tex) continue;
                any = true;
                if (tex.FlipV)
                {
                    // Bottom-up images: mirror vertically about the rect.
                    ci.DrawSetTransform(rect.Position + new Vector2(0, rect.Size.Y), 0, new Vector2(1, -1));
                    ci.DrawTextureRect(tex.Texture, new Rect2(Vector2.Zero, rect.Size), false, modulate);
                    ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                }
                else ci.DrawTextureRect(tex.Texture, rect, false, modulate);
            }
        if (any) return;
        var glyph = Icons.Get(look.Glyph, 96);
        if (glyph == null) return;
        var inset = rect.Grow(-rect.Size.X * 0.16f);
        ci.DrawTextureRect(glyph, inset, false, (glyphTint ?? Palette.AccentBright) * modulate);
    }

    /// <summary>The shown canvas of palette icon layers (the largest layer's), or null when none is a palette icon.</summary>
    public static Vector2? IconCanvas(ClientContent content, string[] icons)
    {
        Vector2? canvas = null;
        foreach (var layer in icons)
            if (layer.Length > 0 && content.Textures.Get(layer) is { Canvas: { } c })
                canvas = canvas is { } prev ? prev.Max(c) : c;
        return canvas;
    }

    /// <summary>Native paired slot: the main-hand icon lies across the slot behind the upright off-hand icon.</summary>
    public static void DrawPair(CanvasItem ci, ClientContent content, SlotLook look, Rect2 box, Color modulate)
    {
        void Layers(string[] icons, bool rotate)
        {
            foreach (var icon in icons)
            {
                if (content.Textures.Get(icon) is not { } texture) continue;
                var size = texture.Texture.GetSize();
                var rotated = rotate ? new Vector2(size.Y, size.X) : size;
                float fit = Math.Min(box.Size.X / rotated.X, box.Size.Y / rotated.Y);
                var drawn = size * fit;
                ci.DrawSetTransform(box.GetCenter(), rotate ? -Mathf.Pi / 2 : 0, new Vector2(1, texture.FlipV ? -1 : 1));
                ci.DrawTextureRect(texture.Texture, new Rect2(-drawn / 2, drawn), false, modulate);
                ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }
        }
        Layers(look.Icons, true);
        Layers(look.SecondaryIcons, false);
    }

    /// <summary>A number in the bottom-right corner (uses left, stack), outlined so it reads over any icon.</summary>
    public static void DrawBadge(CanvasItem ci, string text, Rect2 rect, Color? color = null, int size = 12)
    {
        var font = Fonts.UiBold;
        var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var pos = rect.End - new Vector2(ts.X + 3, 3);
        ci.DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, 0.9f));
        ci.DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color ?? Palette.Text);
    }

    /// <summary>The kit's slot tooltip: name, kind and detail, then key and hints.</summary>
    public static Control Tooltip(SlotLook look, string? key = null, params string[] hints)
    {
        var box = Ui.VBox(3);
        box.CustomMinimumSize = new Vector2(180, 0);
        var title = Ui.HBox(8);
        var name = Ui.Text(look.Name, Ui.TextStyle.Strong, Palette.AccentBright, Palette.FontBody);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddChild(name);
        if (!string.IsNullOrEmpty(key)) title.AddChild(KeyCap.Create(key));
        box.AddChild(title);
        if (look.Kind.Length > 0) box.AddChild(Ui.Text(look.Kind, Ui.TextStyle.Caption, Palette.TextSecondary));
        if (look.Detail.Length > 0)
            box.AddChild(Ui.Text(look.Detail, Ui.TextStyle.Small, look.Usable ? (look.Toggled ? Palette.Success : Palette.Text) : Palette.Danger));
        foreach (var h in hints)
            if (h.Length > 0) box.AddChild(Ui.Text(h, Ui.TextStyle.Caption, Palette.TextMuted));
        return box;
    }

    /// <summary>A dark pie over the icon for the part of a cooldown still to run (0..1), clockwise from the top.</summary>
    public static void DrawSweep(CanvasItem ci, Rect2 rect, float remaining, Color color)
    {
        if (remaining <= 0) return;
        var c = rect.GetCenter();
        float r = rect.Size.Length() / 2;
        var pts = new List<Vector2> { c };
        float start = -MathF.PI / 2 + (1 - remaining) * MathF.Tau, end = -MathF.PI / 2 + MathF.Tau;
        int steps = Math.Max(2, (int)(remaining * 40));
        for (int i = 0; i <= steps; i++)
        {
            float a = start + (end - start) * i / steps;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            pts.Add(new Vector2(Math.Clamp(p.X, rect.Position.X, rect.End.X), Math.Clamp(p.Y, rect.Position.Y, rect.End.Y)));
        }
        ci.DrawColoredPolygon(pts.ToArray(), color);
    }
}
