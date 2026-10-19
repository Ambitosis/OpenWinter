using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// Paints an element's draw list (NuiDrawList): polylines, curves, circles, arcs,
/// text, images, lines and rects, anchored at the element's top-left, before or
/// after the element itself, optionally scissored to its bounds, filtered by the
/// mouse render conditions, with array binds expanding to one primitive per index.
/// </summary>
public partial class NuiDrawLayer : Control
{
    public NuiElement Element = null!;
    public JsonArray Items = null!;
    /// <summary>-1 = NUI_DRAW_LIST_ITEM_ORDER_BEFORE, 1 = AFTER.</summary>
    public int Order;
    public bool Scissor;
    readonly Control _painter = new() { MouseFilter = MouseFilterEnum.Ignore };
    bool _conditional;
    int _state = -1;

    const int TypePolyline = 0, TypeCurve = 1, TypeCircle = 2, TypeArc = 3, TypeText = 4, TypeImage = 5, TypeLine = 6, TypeRect = 7;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _painter.Draw += Paint;
        AddChild(_painter);
        foreach (var item in Items.OfType<JsonObject>())
            if (OrderOf(item) == Order && NuiBinds.Number(item["render"]) is double r && r != 0) _conditional = true;
        SetProcess(_conditional);
        Fit();
    }

    static int OrderOf(JsonObject item) => NuiBinds.Number(item["order"]) is double d && d < 0 ? -1 : 1;

    public void Fit()
    {
        Position = Vector2.Zero;
        Size = Element.Size;
        ClipContents = Scissor;
        _painter.Size = Element.Size;
    }

    public void Refresh()
    {
        ClipContents = Scissor;
        _painter.QueueRedraw();
    }

    public new void QueueRedraw() => _painter.QueueRedraw();

    /// <summary>Mouse state bits: hover, left, right, middle.</summary>
    int MouseState()
    {
        var local = GetLocalMousePosition();
        bool hover = new Rect2(Vector2.Zero, Element.Size).HasPoint(local) && Element.IsVisibleInTree();
        int s = hover ? 1 : 0;
        if (hover && Input.IsMouseButtonPressed(MouseButton.Left)) s |= 2;
        if (hover && Input.IsMouseButtonPressed(MouseButton.Right)) s |= 4;
        if (hover && Input.IsMouseButtonPressed(MouseButton.Middle)) s |= 8;
        return s;
    }

    public override void _Process(double delta)
    {
        int s = MouseState();
        if (s == _state) return;
        _state = s;
        _painter.QueueRedraw();
    }

    static bool RenderNow(int mode, int state) => mode switch
    {
        1 => (state & 1) == 0,
        2 => (state & 1) != 0,
        3 => (state & 2) != 0,
        4 => (state & 4) != 0,
        5 => (state & 8) != 0,
        _ => true,
    };

    void Paint()
    {
        var binds = Element.Ctx.Binds;
        int row = Element.Row;
        int state = _conditional ? MouseState() : 1;
        foreach (var item in Items.OfType<JsonObject>())
        {
            if (OrderOf(item) != Order) continue;
            if (!RenderNow((int)(NuiBinds.Number(item["render"]) ?? 0), state)) continue;
            int type = (int)(NuiBinds.Number(item["type"]) ?? -1);
            bool arrays = NuiBinds.Number(item["arrayBinds"]) is double ab && ab != 0;
            int count = 1;
            if (arrays)
            {
                count = 0;
                foreach (var kv in item)
                    if (NuiBinds.IsBind(kv.Value, out _) && binds.Resolve(kv.Value, row) is JsonArray a) count = Math.Max(count, a.Count);
            }
            for (int i = 0; i < count; i++)
            {
                JsonNode? P(string key)
                {
                    var v = binds.Resolve(item[key], row);
                    return arrays && NuiBinds.IsBind(item[key], out _) && v is JsonArray a ? (i < a.Count ? a[i] : null) : v;
                }
                if (P("enabled") is JsonNode en && NuiBinds.Number(en) is double e && e == 0) continue;
                DrawItem(type, P);
            }
        }
    }

    void DrawItem(int type, Func<string, JsonNode?> p)
    {
        var skin = Element.Ctx.Skin;
        var color = NuiBinds.ToColor(p("color")) is { } c ? NuiSkin.ToColor(c) : Colors.White;
        bool fill = NuiBinds.Number(p("fill")) is double f && f != 0;
        float thick = (float)(NuiBinds.Number(p("line_thickness")) ?? 1);
        if (thick <= 0) thick = 1;
        switch (type)
        {
            case TypePolyline:
            {
                var pts = NuiBinds.Floats(p("points"));
                var v = new Vector2[pts.Length / 2];
                for (int k = 0; k < v.Length; k++) v[k] = new Vector2(pts[2 * k], pts[2 * k + 1]);
                if (v.Length < 2) break;
                if (fill && v.Length >= 3) TryFill(v, color);
                else _painter.DrawPolyline(v, color, thick, true);
                break;
            }
            case TypeCurve:
            {
                if (NuiBinds.ToVec(p("a")) is not { } a || NuiBinds.ToVec(p("b")) is not { } b) break;
                var c0 = NuiBinds.ToVec(p("ctrl0")) ?? a;
                var c1 = NuiBinds.ToVec(p("ctrl1")) ?? b;
                var v = new Vector2[25];
                for (int k = 0; k < v.Length; k++)
                {
                    float t = k / (float)(v.Length - 1), u = 1 - t;
                    float x = u * u * u * a.X + 3 * u * u * t * c0.X + 3 * u * t * t * c1.X + t * t * t * b.X;
                    float y = u * u * u * a.Y + 3 * u * u * t * c0.Y + 3 * u * t * t * c1.Y + t * t * t * b.Y;
                    v[k] = new Vector2(x, y);
                }
                _painter.DrawPolyline(v, color, thick, true);
                break;
            }
            case TypeCircle:
            {
                if (NuiBinds.ToRect(p("rect")) is not { } r) break;
                var center = new Vector2(r.X + r.W / 2, r.Y + r.H / 2);
                var v = Ellipse(center, r.W / 2, r.H / 2, 0, MathF.Tau, 48);
                if (fill) TryFill(v, color);
                else _painter.DrawPolyline(Close(v), color, thick, true);
                break;
            }
            case TypeArc:
            {
                var center = NuiBinds.ToVec(p("c")) is { } cv ? new Vector2(cv.X, cv.Y)
                    : NuiBinds.ToRect(p("c")) is { } cr ? new Vector2(cr.X, cr.Y) : Vector2.Zero;
                float radius = (float)(NuiBinds.Number(p("radius")) ?? 0);
                float amin = (float)(NuiBinds.Number(p("amin")) ?? 0), amax = (float)(NuiBinds.Number(p("amax")) ?? 0);
                if (radius <= 0) break;
                int segs = Math.Clamp((int)(MathF.Abs(amax - amin) / MathF.Tau * 48) + 2, 3, 64);
                var arc = Ellipse(center, radius, radius, amin, amax, segs);
                if (fill)
                {
                    var pie = new Vector2[arc.Length + 1];
                    pie[0] = center;
                    arc.CopyTo(pie, 1);
                    TryFill(pie, color);
                }
                else _painter.DrawPolyline(arc, color, thick, true);
                break;
            }
            case TypeText:
            {
                if (NuiBinds.ToRect(p("rect")) is not { } r) break;
                string text = Element.Ctx.Binds.Text(p("text"));
                if (text.Length == 0) break;
                _painter.DrawMultilineString(skin.Font, new Vector2(r.X, r.Y + skin.Font.GetAscent(skin.FontSize)), text,
                    HorizontalAlignment.Left, r.W > 0 ? r.W : -1, skin.FontSize, Math.Max(1, (int)(r.H / skin.Font.GetHeight(skin.FontSize))), color);
                break;
            }
            case TypeImage:
            {
                if (NuiBinds.ToRect(p("rect")) is not { } r) break;
                var resref = p("image") is JsonValue iv && iv.TryGetValue<string>(out var s) ? s : "";
                if (skin.Texture(resref) is not { } tex) break;
                NuiImageView.DrawImage(_painter, tex, new Rect2(r.X, r.Y, r.W, r.H),
                    (int)(NuiBinds.Number(p("image_aspect")) ?? 0), (int)(NuiBinds.Number(p("image_halign")) ?? 0),
                    (int)(NuiBinds.Number(p("image_valign")) ?? 0), NuiBinds.ToRect(p("image_region")), Colors.White);
                break;
            }
            case TypeLine:
            {
                if (NuiBinds.ToVec(p("a")) is not { } a || NuiBinds.ToVec(p("b")) is not { } b) break;
                _painter.DrawLine(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), color, thick, true);
                break;
            }
            case TypeRect:
            {
                if (NuiBinds.ToRect(p("rect")) is not { } r) break;
                var rect = new Rect2(r.X, r.Y, r.W, r.H);
                if (fill) _painter.DrawRect(rect, color);
                else _painter.DrawRect(rect, color, false, thick);
                break;
            }
        }
    }

    void TryFill(Vector2[] v, Color color)
    {
        // Godot rejects self-intersecting or degenerate polygons; triangulate defensively.
        var idx = Geometry2D.TriangulatePolygon(v);
        if (idx.Length == 0) return;
        var colors = new Color[v.Length];
        Array.Fill(colors, color);
        RenderingServer.CanvasItemAddTriangleArray(_painter.GetCanvasItem(), idx, v, colors);
    }

    static Vector2[] Ellipse(Vector2 c, float rx, float ry, float a0, float a1, int segments)
    {
        var v = new Vector2[segments + 1];
        for (int k = 0; k <= segments; k++)
        {
            float a = a0 + (a1 - a0) * k / segments;
            v[k] = c + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
        }
        return v;
    }

    static Vector2[] Close(Vector2[] v)
    {
        var r = new Vector2[v.Length + 1];
        v.CopyTo(r, 0);
        r[^1] = v[0];
        return r;
    }
}
