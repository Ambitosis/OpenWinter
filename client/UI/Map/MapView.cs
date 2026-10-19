using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Map;

/// <summary>
/// Draws an <see cref="AreaMap"/>: the explored tiles under a soft fog of war, map notes, the
/// party and the player's arrow with the camera's view cone. The view is a centre (area metres),
/// a zoom (pixels per metre) and an angle (the minimap turns with the camera). Used by the map
/// window (with pan and zoom) and the HUD minimap.
/// </summary>
public partial class MapView : Control
{
    public ClientSession Session = null!;
    public AreaMap Map = null!;
    /// <summary>Area metres at the middle of the control.</summary>
    public Vector2 Center;
    /// <summary>Screen pixels per area metre.</summary>
    public float Zoom = 4;
    /// <summary>Screen rotation of the map (radians, clockwise); 0 = north up.</summary>
    public float Angle;
    /// <summary>Small markers for the minimap.</summary>
    public bool Compact;
    /// <summary>Pins hovered by the pointer show their note as a tooltip.</summary>
    public bool PinTooltips = true;
    /// <summary>A pin highlighted from outside (the map window's note list).</summary>
    public int HighlightPin = -1;

    static Shader? _shader;
    ShaderMaterial _material = null!;
    Control _tiles = null!;
    Overlay _overlay = null!;
    double _time;

    public override void _Ready()
    {
        ClipContents = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
        // The tiles draw in their own layer: the fog shader applies to them only.
        _tiles = new Control { Material = _material, MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        _tiles.Draw += DrawTiles;
        AddChild(_tiles);
        _overlay = new Overlay { View = this, MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_overlay);
        Map.Changed += OnMapChanged;
        TreeExiting += () => Map.Changed -= OnMapChanged;
    }

    void OnMapChanged() => _tiles.QueueRedraw();

    public override void _Process(double delta)
    {
        _time += delta;
        if (IsVisibleInTree()) { _tiles.QueueRedraw(); _overlay.QueueRedraw(); }
    }

    /// <summary>The player's position in area metres (or the map centre when there is no player).</summary>
    public Vector2 PlayerPosition => Session.Controlled is { } me ? new Vector2(me.Position.X, me.Position.Y) : Map.Extent / 2;

    public Vector2 WorldToScreen(Vector2 world)
    {
        var d = new Vector2(world.X - Center.X, Center.Y - world.Y) * Zoom;
        return Size / 2 + d.Rotated(Angle);
    }

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        var d = (screen - Size / 2).Rotated(-Angle) / Zoom;
        return new Vector2(Center.X + d.X, Center.Y - d.Y);
    }

    /// <summary>Zoom that fits the whole area in the control with a margin.</summary>
    public float FitZoom(float margin = 24)
    {
        if (Map.Width <= 0 || Size.X <= 0) return Zoom;
        return Math.Min((Size.X - margin * 2) / Map.Extent.X, (Size.Y - margin * 2) / Map.Extent.Y);
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), FogColor);

    void DrawTiles()
    {
        if (Map.Tiles is not { } tiles || Map.Fog is not { } fog) return;
        _material.SetShaderParameter("fog", fog);
        _material.SetShaderParameter("fog_color", FogColor);
        _material.SetShaderParameter("time", (float)_time);
        // The area as one rotated, scaled rectangle.
        var topLeft = new Vector2(-Center.X, Center.Y - Map.Extent.Y) * Zoom;
        _tiles.DrawSetTransform(Size / 2, Angle, Vector2.One);
        // Drawn with a wide margin past the area's edges so the fog's haze fills the whole view.
        float margin = Size.Length() / Math.Max(0.01f, Zoom);
        float ppm = Map.PixelsPerTile / AreaMap.TileSize;
        var dest = new Rect2(topLeft - new Vector2(margin, margin) * Zoom, (Map.Extent + new Vector2(margin, margin) * 2) * Zoom);
        var src = new Rect2(-new Vector2(margin, margin) * ppm, (Map.Extent + new Vector2(margin, margin) * 2) * ppm);
        _tiles.DrawTextureRectRegion(tiles, dest, src);
        _tiles.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    static readonly Color FogColor = new("0b0c10");

    /// <summary>Index of the visible pin under a point, or -1.</summary>
    public int PinAt(Vector2 screen)
    {
        float best = Compact ? 9 : 14;
        int found = -1;
        for (int i = 0; i < Map.Pins.Count; i++)
        {
            var p = Map.Pins[i];
            if (!Map.IsExplored(p.Position)) continue;
            var at = WorldToScreen(p.Position) + (Compact ? Vector2.Zero : new Vector2(0, -10));
            float d = at.DistanceTo(screen);
            if (d < best) { best = d; found = i; }
        }
        return found;
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (!PinTooltips) return TooltipText;
        int pin = PinAt(atPosition);
        return pin >= 0 ? Map.Pins[pin].Note : TooltipText;
    }

    /// <summary>Camera look direction on the map (area x/y), from the active 3D camera.</summary>
    public Vector2? CameraDirection()
    {
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null || !IsInstanceValid(Session.Space)) return null;
        var fwd = Session.Space.GlobalTransform.Basis.Inverse() * -cam.GlobalTransform.Basis.Z;
        var flat = new Vector2(fwd.X, fwd.Y);
        return flat.LengthSquared() < 1e-6f ? null : flat.Normalized();
    }

    /// <summary>A map direction (area x/y) as a screen direction.</summary>
    public Vector2 ScreenDirection(Vector2 world) => new Vector2(world.X, -world.Y).Rotated(Angle);

    /// <summary>Markers drawn above the (shaded) map.</summary>
    partial class Overlay : Control
    {
        public MapView View = null!;

        public override void _Draw()
        {
            var v = View;
            var map = v.Map;
            if (map.Width <= 0) return;
            float s = v.Compact ? 0.8f : 1f;

            // Map notes.
            for (int i = 0; i < map.Pins.Count; i++)
            {
                var p = map.Pins[i];
                if (!map.IsExplored(p.Position)) continue;
                var at = v.WorldToScreen(p.Position);
                bool hot = i == v.HighlightPin;
                if (v.Compact) DrawDiamond(at, hot ? 6 : 4.5f);
                else MapGlyphs.Pin(this, at, hot ? 1.25f : 1f, hot);
            }

            // Party members (in this area).
            foreach (var id in map.Party)
            {
                if (!v.Session.Objects.TryGetValue(id, out var o) || o == v.Session.Controlled) continue;
                var at = v.WorldToScreen(new Vector2(o.Position.X, o.Position.Y));
                DrawCircle(at, 6.5f * s, new Color(0, 0, 0, 0.55f));
                DrawCircle(at, 4.5f * s, Palette.Friendly);
                DrawArc(at, 4.5f * s, 0, MathF.Tau, 20, Palette.Friendly.Lightened(0.4f), 1.2f, true);
            }

            // The player: view cone, then the arrow.
            if (v.Session.Controlled is { } me)
            {
                var at = v.WorldToScreen(new Vector2(me.Position.X, me.Position.Y));
                if (v.CameraDirection() is { } look) MapGlyphs.ViewCone(this, at, v.ScreenDirection(look), v.Compact ? 46 : Math.Clamp(v.Zoom * 9, 70, 150));
                // ObjectView rotation is facing - 90°.
                float facing = me.Rotation.Z + MathF.PI / 2;
                var dir = v.ScreenDirection(new Vector2(MathF.Cos(facing), MathF.Sin(facing)));
                MapGlyphs.PlayerArrow(this, at, dir, v.Compact ? 0.85f : Math.Clamp(v.Zoom / 8f, 1.1f, 1.8f));
            }
        }

        void DrawDiamond(Vector2 at, float r)
        {
            DrawColoredPolygon([at + new Vector2(0, -r - 1.5f), at + new Vector2(r + 1.5f, 0), at + new Vector2(0, r + 1.5f), at + new Vector2(-r - 1.5f, 0)], new Color(0, 0, 0, 0.6f));
            DrawColoredPolygon([at + new Vector2(0, -r), at + new Vector2(r, 0), at + new Vector2(0, r), at + new Vector2(-r, 0)], Palette.AccentBright);
        }
    }

    const string ShaderCode = """
        shader_type canvas_item;
        uniform sampler2D fog : filter_linear, repeat_disable;
        uniform vec4 fog_color : source_color;
        uniform float time;

        float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float noise(vec2 p) {
            vec2 i = floor(p), f = fract(p);
            f = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
        }

        float lum(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

        void fragment() {
            bool inside = UV.x >= 0.0 && UV.y >= 0.0 && UV.x <= 1.0 && UV.y <= 1.0;
            vec4 tile = inside ? texture(TEXTURE, UV) : vec4(0.0);
            float seen = inside ? smoothstep(0.08, 0.72, texture(fog, UV).r) : 0.0;
            // The tileset's minimap art is light floors on black: map it onto ink and warm stone,
            // keep strongly coloured marks (doors, water), and edge the walls with a brass line.
            float l = lum(tile.rgb);
            float sat = max(tile.r, max(tile.g, tile.b)) - min(tile.r, min(tile.g, tile.b));
            vec3 ink = vec3(0.07, 0.068, 0.078);
            vec3 stone = vec3(0.74, 0.67, 0.54);
            // Threshold the (smoothly upscaled) art into crisp floor shapes, keeping its shading.
            float w = fwidth(l) * 0.8 + 0.012;
            float floor_mask = smoothstep(0.3 - w, 0.3 + w, l);
            vec3 graded = mix(ink, stone * mix(0.78, 1.04, smoothstep(0.3, 0.95, l)), floor_mask);
            graded = mix(graded, tile.rgb * 0.95, smoothstep(0.2, 0.45, sat));
            // A brass line along the walls.
            float line = 1.0 - smoothstep(0.0, 1.0, abs(l - 0.3) / (w * 2.4));
            graded = mix(graded, vec3(0.86, 0.68, 0.36), line * 0.7 * (1.0 - smoothstep(0.2, 0.45, sat)));
            graded *= mix(0.55, 1.0, seen);
            // Drifting haze over the unexplored part.
            vec2 p = UV / TEXTURE_PIXEL_SIZE / 40.0;
            float n = noise(p + vec2(time * 0.03, time * 0.02)) * 0.6 + noise(p * 2.3 - vec2(time * 0.02, 0.0)) * 0.4;
            vec3 haze = fog_color.rgb + vec3(0.035, 0.037, 0.045) * n;
            // A faint brass edge where the explored part meets the fog.
            float edge = smoothstep(0.05, 0.35, seen) * (1.0 - smoothstep(0.35, 0.75, seen));
            vec3 c = mix(haze, graded, seen);
            c = mix(c, vec3(0.83, 0.65, 0.34) * 0.3, edge * 0.18);
            COLOR = vec4(c, COLOR.a);
        }
        """;
}

/// <summary>Vector markers shared by the map window and minimap.</summary>
public static class MapGlyphs
{
    /// <summary>The player: a white-edged cyan arrowhead with a dark outline and glow.</summary>
    public static void PlayerArrow(CanvasItem c, Vector2 at, Vector2 dir, float scale = 1)
    {
        dir = dir.Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        float s = 9 * scale;
        Vector2[] Shape(float k) => [at + dir * s * 1.25f * k, at - dir * s * 0.85f * k + side * s * 0.95f * k, at - dir * s * 0.35f * k, at - dir * s * 0.85f * k - side * s * 0.95f * k];
        c.DrawCircle(at, s * 1.7f, new Color(0.45f, 0.85f, 1f, 0.12f));
        c.DrawColoredPolygon(Shape(1.35f), new Color(0, 0, 0, 0.7f));
        c.DrawColoredPolygon(Shape(1.1f), new Color(0.95f, 0.98f, 1f));
        c.DrawColoredPolygon(Shape(0.8f), new Color("4fc3f7"));
    }

    /// <summary>The camera's view: a soft wedge fading out from the player.</summary>
    public static void ViewCone(CanvasItem c, Vector2 at, Vector2 dir, float length, float halfAngle = 0.55f)
    {
        dir = dir.Normalized();
        const int steps = 12;
        var points = new Vector2[steps + 2];
        var colors = new Color[steps + 2];
        points[0] = at;
        colors[0] = new Color(1, 1, 1, 0.22f);
        for (int i = 0; i <= steps; i++)
        {
            float a = -halfAngle + 2 * halfAngle * i / steps;
            points[i + 1] = at + dir.Rotated(a) * length;
            colors[i + 1] = new Color(1, 1, 1, 0);
        }
        c.DrawPolygon(points, colors);
    }

    /// <summary>A map-note pin: brass teardrop with a dark core, its point at <paramref name="at"/>.</summary>
    public static void Pin(CanvasItem c, Vector2 at, float scale = 1, bool hot = false)
    {
        float r = 6.5f * scale;
        var head = at + new Vector2(0, -r * 2.1f);
        // Shadow on the ground.
        c.DrawSetTransform(at, 0, new Vector2(1, 0.4f));
        c.DrawCircle(Vector2.Zero, r * 0.9f, new Color(0, 0, 0, 0.45f));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var body = Palette.Accent;
        var outline = new Color(0.1f, 0.07f, 0.03f, 0.9f);
        Vector2[] Tail(float k) => [head + new Vector2(-r * 0.78f * k, r * 0.55f), at, head + new Vector2(r * 0.78f * k, r * 0.55f)];
        c.DrawColoredPolygon(Tail(1.25f), outline);
        c.DrawCircle(head, r + 1.6f, outline);
        c.DrawColoredPolygon(Tail(1), body);
        c.DrawCircle(head, r, hot ? Palette.AccentBright : body);
        c.DrawCircle(head + new Vector2(-r * 0.25f, -r * 0.3f), r * 0.45f, new Color(1, 1, 1, 0.18f));
        c.DrawCircle(head, r * 0.38f, outline);
        if (hot) c.DrawArc(head, r + 4, 0, MathF.Tau, 28, Palette.AccentBright with { A = 0.6f }, 1.5f, true);
    }
    /// <summary>A compass rose: dark disc, brass ring, four points with north highlighted, turned by <paramref name="angle"/>.</summary>
    public static void Compass(CanvasItem c, Vector2 at, float radius, float angle)
    {
        c.DrawCircle(at, radius, new Color(0.04f, 0.045f, 0.06f, 0.8f));
        c.DrawArc(at, radius, 0, MathF.Tau, 48, Palette.AccentDim, 1.2f, true);
        c.DrawArc(at, radius * 0.82f, 0, MathF.Tau, 48, Palette.Line, 1, true);
        for (int i = 0; i < 4; i++)
        {
            var d = Vector2.Up.Rotated(angle + i * MathF.PI / 2);
            var side = new Vector2(-d.Y, d.X);
            var tip = at + d * radius * (i == 0 ? 0.62f : 0.5f);
            c.DrawColoredPolygon([tip, at + side * radius * 0.15f, at - side * radius * 0.15f], i == 0 ? Palette.AccentBright : Palette.TextMuted);
        }
        c.DrawCircle(at, radius * 0.1f, Palette.Bg0);
        var font = Fonts.DisplayBold;
        int size = (int)Math.Max(9, radius * 0.45f);
        var n = at + Vector2.Up.Rotated(angle) * radius * 0.86f;
        var ts = font.GetStringSize("N", HorizontalAlignment.Left, -1, size);
        c.DrawCircle(n, size * 0.72f, new Color(0.04f, 0.045f, 0.06f, 0.95f));
        c.DrawString(font, n + new Vector2(-ts.X / 2, font.GetAscent(size) / 2 - 1), "N", HorizontalAlignment.Left, -1, size, Palette.AccentBright);
    }

    public enum Kind { Plus, Minus, Locate, Fit }

    /// <summary>A small square button with a drawn glyph (zoom in/out, locate, fit).</summary>
    public static Button Button(Kind glyph, string tooltip, Action pressed, int size = 32, bool round = false)
    {
        var b = new Button { CustomMinimumSize = new Vector2(size, size), TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None, ThemeTypeVariation = "GhostButton" };
        int radius = round ? size / 2 : Palette.RadiusSmall;
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
            b.AddThemeStyleboxOverride(state, Styles.Box(state switch
            {
                "hover" => round ? Palette.Bg3 : new Color(1, 1, 1, 0.08f),
                "pressed" or "hover_pressed" => round ? Palette.Bg2 : Palette.Accent with { A = 0.18f },
                _ => round ? Palette.Bg1 with { A = 0.95f } : Colors.Transparent,
            }, radius, round ? (state == "normal" ? Palette.AccentDim : Palette.Accent) : null, 1, 0, 0, shadow: round ? 4 : 0));
        b.Pressed += pressed;
        b.Draw += () =>
        {
            var c = b.Size / 2;
            var col = b.IsHovered() ? Palette.AccentBright : round ? Palette.Accent : Palette.TextSecondary;
            float r = size * 0.22f;
            float w = round ? 1.8f : 2f;
            switch (glyph)
            {
                case Kind.Plus:
                    b.DrawLine(c - new Vector2(r, 0), c + new Vector2(r, 0), col, w, true);
                    b.DrawLine(c - new Vector2(0, r), c + new Vector2(0, r), col, w, true);
                    break;
                case Kind.Minus:
                    b.DrawLine(c - new Vector2(r, 0), c + new Vector2(r, 0), col, w, true);
                    break;
                case Kind.Locate:
                    b.DrawArc(c, r * 0.8f, 0, MathF.Tau, 24, col, 1.6f, true);
                    b.DrawCircle(c, r * 0.3f, col);
                    foreach (var d in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
                        b.DrawLine(c + d * r * 0.8f, c + d * r * 1.35f, col, 1.6f, true);
                    break;
                case Kind.Fit:
                    float k = r * 1.1f, t = r * 0.55f;
                    foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        var corner = c + new Vector2(sx * k, sy * k);
                        b.DrawLine(corner, corner - new Vector2(sx * t, 0), col, 1.6f, true);
                        b.DrawLine(corner, corner - new Vector2(0, sy * t), col, 1.6f, true);
                    }
                    break;
            }
        };
        b.MouseEntered += b.QueueRedraw;
        b.MouseExited += b.QueueRedraw;
        return b;
    }
}
