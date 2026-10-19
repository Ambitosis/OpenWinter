using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The radial menu in the original's look, on the modern radial menu's logic (the same rings, actions, keys,
/// paging and drag onto the quickbar): eight positions at gui_ra_menu's hook nodes (100 px out, clockwise from
/// the top), each a dark disc (gui_ra_circle56) with the option's icon, the hovered one ringed (gui_ra_clearring),
/// directional arrows from gui_ra_more and the pointer band from gui_radialband. Other creatures have a root
/// portrait; submenus show their parent icon, and a menu of its own entries (the chat window's) its root icon on the
/// large centre disc (gui_ra_circle112, chatradial). Hover labels and miniature child previews sit around the hovered
/// option; a label stays on screen (chatmenuheld: the chat menu's bottom option labels just above the screen edge).
/// </summary>
public sealed class ClassicRadialSkin : RadialMenu.ISkin
{
    readonly ClassicContext _ctx;
    GuiModel? _menu, _disc, _bigDisc, _ring, _more, _portrait, _band;
    public int Slots => 8;

    ClassicRadialSkin(ClassicContext ctx) { _ctx = ctx; }

    public static ClassicRadialSkin Install(ClassicContext ctx)
    {
        var skin = new ClassicRadialSkin(ctx);
        foreach (var radial in ctx.Hud.GetChildren().OfType<RadialMenu>()) radial.Skin = skin;
        return skin;
    }

    public void Selected() => ClassicSounds.Play(_ctx.Assets, "gui_radial_slect");

    /// <summary>The quickbar's text macro prompts: the original's util_editpopup ("Enter Label", "Input Command").</summary>
    public bool Prompt(string message, string text, Action<string> ok)
    {
        // OK sounds gui_quick_add (and no button click), Cancel the button's own (quickbarsound).
        var popup = ClassicPopup.Edit(_ctx.Hud, _ctx.Assets, message, text, line => { ClassicSounds.Play(_ctx.Assets, "gui_quick_add"); ok(line); },
            maxLength: ClassicQuickbarText.PromptLength);
        if (popup == null) return false;
        if (popup.Panel.Get<GuiButton>("OkButton") is { } okButton) okButton.ClickSound = "";
        // The original's box takes characters only while they fit its width (quickbarsound: 41 digits of a 320-digit line).
        if (popup.Panel.GetChildren().OfType<LineEdit>().FirstOrDefault() is { } edit) ClassicQuickbarText.FitToWidth(edit);
        return true;
    }

    /// <summary>Examine on a spell, feat or skill slot: the original's description window.</summary>
    public bool Describe(BarSlot slot, SlotLook look) => Sheets.ClassicDescription.Show(_ctx, slot, look);


    void Load()
    {
        if (_menu != null) return;
        var a = _ctx.Assets;
        _menu = a.Model("gui_ra_menu");
        _disc = a.Model("gui_ra_circle56");
        _bigDisc = a.Model("gui_ra_circle112");
        _ring = a.Model("gui_ra_clearring");
        _more = a.Model("gui_ra_more");
        _portrait = a.Model("gui_ra_portrait");
        _band = a.Model("gui_radialband");
    }

    Vector2 Hook(int i)
    {
        if (_menu?.Node($"{i}hook") is { } p) return p;
        float a = i * MathF.Tau / Slots;
        return new Vector2(MathF.Sin(a), -MathF.Cos(a)) * 100;
    }

    public void Draw(Control canvas, RadialMenu menu)
    {
        Load();
        var assets = _ctx.Assets;
        var content = _ctx.Session.Content;
        var font = assets.Font(ClassicChatStyle.FontName);
        var centre = menu.Centre.Round();

        // Native items show their layered inventory art at its own pixel size, without a portrait panel.
        if (menu.Path.Count == 1 && menu.TargetItemArt is { } item)
        {
            var size = ClassicItemArt.IconSize(assets, item);
            var area = new Rect2((centre - size / 2).Round(), size);
            ClassicItemArt.DrawIcon(canvas, assets, item, area, Colors.White);
            ClassicItemArt.DrawCount(canvas, assets, ClassicItemArt.CountOf(item), area);
        }
        // Other targets keep the measured portrait behavior.
        else if (menu.Path.Count == 1 && !menu.IsSelf && menu.TargetPortrait is { Length: > 0 } portrait && assets.Texture(portrait + "m") is { } tex)
        {
            var size = _portrait?.Bounds.Size ?? new Vector2(64, 100);
            var src = new Rect2(0, 0, tex.GetWidth(), tex.GetHeight() * 0.781f);
            var rect = new Rect2(centre - size / 2, size);
            canvas.DrawRect(rect.Grow(1), Colors.Black);
            canvas.DrawTextureRectRegion(tex, rect, src, menu.Hovered == -2 ? GuiDraw.Hilite : Colors.White);
        }

        // A custom menu's root icon sits on the large disc (the chat menu's ir_chat); a submenu's parent icon stands alone.
        // A hidden root (a trap trigger's menu) leaves the centre empty, as the original's does.
        if (menu.Path.Count == 1 && menu.ParentEntry is { Hidden: false } && _bigDisc != null)
            GuiDraw.Model(canvas, assets, _bigDisc.Rest, centre, Colors.White);
        DrawBand(canvas, centre, menu.Pointer);
        if (menu.ParentEntry is { Hidden: false } parent) DrawEntry(canvas, parent, centre, 32, false);

        var shown = menu.Shown;
        for (int i = 0; i < shown.Count && i < Slots; i++)
        {
            var e = shown[i];
            var at = centre + Hook(i);
            bool hover = i == menu.Hovered;
            if (e.Label == "More" && e.Open == null && e.Look == null && _more != null)
            {
                GuiDraw.Model(canvas, assets, _more.Rest, at, hover ? GuiDraw.Hilite : Colors.White);
                continue;
            }
            if (e.Hidden) continue;
            DrawEntry(canvas, e, at, 32, hover);
            if (e.Open != null && _more != null)
            {
                canvas.DrawSetTransform(centre + (_menu?.Node($"{i}submenu") ?? Hook(i) * 1.55f), i * MathF.Tau / Slots);
                GuiDraw.Model(canvas, assets, _more.Rest, Vector2.Zero, Colors.White);
                canvas.DrawSetTransform(Vector2.Zero);
            }
            if (hover && menu.Preview.Count > 0)
            {
                for (int j = 0; j < Math.Min(8, menu.Preview.Count); j++)
                {
                    var child = menu.Preview[j];
                    if (child.Hidden) continue;
                    var point = at + Hook(j) * 0.30f;
                    canvas.DrawArc(point, 10.5f, 0, MathF.Tau, 24, Colors.White, 1, true);
                    DrawEntry(canvas, child, point, 18, false, false);
                }
            }
        }

        // Native labels sit outside the hovered icon; the centre remains clear.
        if (menu.Hovered >= 0 && menu.Hovered < shown.Count && !shown[menu.Hovered].Hidden)
        {
            var title = shown[menu.Hovered].Label;
            var direction = Hook(menu.Hovered).Normalized();
            var at = centre + Hook(menu.Hovered) * 1.65f;
            at.X -= direction.X < -0.2f ? font.Measure(title) : direction.X > 0.2f ? 0 : font.Measure(title) / 2;
            at.Y -= font.Height / 2;
            // Kept on screen: the chat menu's bottom option sits 100 px above a centre clamped 154 px from the bottom.
            var view = canvas.GetViewportRect().Size;
            at.X = Math.Clamp(at.X, 2, Math.Max(2, view.X - font.Measure(title) - 3));
            at.Y = Math.Clamp(at.Y, 2, Math.Max(2, view.Y - font.Height - 3));
            Shadowed(canvas, font, at, title, Colors.Yellow);
        }
    }

    void DrawEntry(Control canvas, RadialEntry e, Vector2 at, float size, bool hover, bool disc = true)
    {
        var content = _ctx.Session.Content;
        if (disc && _disc != null) GuiDraw.Model(canvas, _ctx.Assets, _disc.Rest, at, Colors.White);
        // A text macro has no icon: the original's ring shows a black square on its centre disc (quickbargaps i_ring).
        if (e.Slot?.Kind == "macro") { canvas.DrawRect(new Rect2((at - Vector2.One * 16).Round(), Vector2.One * 32), Colors.Black); return; }
        var look = e.Look ?? new SlotLook(e.Label, [], e.Glyph, -1, e.Active, !e.Disabled, "", "");
        var icons = ClassicIcons.For(content, look, e.Slot);
        if (icons.Length == 0 && ClassicIcons.ForGlyph(content, e.Glyph) is { } ir) icons = [ir];
        var mod = e.Disabled ? new Color(0.4f, 0.4f, 0.4f) : hover ? GuiDraw.Hilite : Colors.White;
        ClassicIcons.Draw(canvas, content, look, icons, new Rect2(at - Vector2.One * size / 2, Vector2.One * size), mod);
        if (disc && (hover || e.Active) && _ring != null)
            GuiDraw.Model(canvas, _ctx.Assets, _ring.Rest, at, hover ? Colors.White : new Color(0.5f, 1f, 0.5f));
    }

    void DrawBand(Control canvas, Vector2 centre, Vector2 pointer)
    {
        var delta = pointer - centre;
        if (delta.LengthSquared() < 4 || _band == null) return;
        var direction = delta.Normalized();
        var side = direction.Orthogonal();
        // The installed band mesh has coincident skinned endpoints. Stretch its own atlas UVs
        // between the ring centre and pointer, keeping the authored five-pixel thickness.
        foreach (var mesh in _band.Rest.Meshes)
        {
            if (_ctx.Assets.Texture(mesh.Texture) is not { } texture) continue;
            Vector2[] points;
            if (mesh.Node.Equals("RubberBand", StringComparison.OrdinalIgnoreCase))
            {
                float u0 = mesh.Uvs.Min(p => p.X), u1 = mesh.Uvs.Max(p => p.X);
                points = mesh.Points.Select((p, i) => centre + delta * ((mesh.Uvs[i].X - u0) / (u1 - u0)) + side * p.Y).ToArray();
            }
            else points = mesh.Points.Select(p => pointer + direction * p.X + side * p.Y).ToArray();
            RenderingServer.CanvasItemAddTriangleArray(canvas.GetCanvasItem(), mesh.Indices, points,
                Enumerable.Repeat(Colors.White, points.Length).ToArray(), mesh.Uvs, texture: texture.GetRid());
        }
    }

    static void Shadowed(CanvasItem ci, GuiFont font, Vector2 at, string text, Color color)
    {
        at = at.Round();
        font.Draw(ci, at + new Vector2(1, 1), text, Colors.Black);
        font.Draw(ci, at, text, color);
    }
}
