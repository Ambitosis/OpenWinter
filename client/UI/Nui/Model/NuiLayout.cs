using System.Text.Json.Nodes;

namespace OpenWinter.Client.UI.Nui.Model;

/// <summary>
/// NUI default metrics at UI scale 1, measured on the original (visual scene nuifont).
/// </summary>
public static class NuiStyle
{
    public const float TitleHeight = 33;
    public const float WindowPadding = 4;
    /// <summary>Margin around elements that don't set NuiMargin: rows, columns and widgets alike, but not spacers or
    /// the window's root.</summary>
    public const float WidgetMargin = 2;
    /// <summary>Padding inside groups (and the group implied by NuiText / NuiList).</summary>
    public const float GroupPadding = 4;
    public const float ScrollbarSize = 12;
    public const float RowHeight = 25;
    public const int FontSize = 17;
    /// <summary>Scrollbar modes (NUI_SCROLLBARS_*).</summary>
    public const int ScrollNone = 0, ScrollX = 1, ScrollY = 2, ScrollBoth = 3, ScrollAuto = 4;
}

/// <summary>One cell of a NuiList row template: element, width (0 = auto) and whether it may grow.</summary>
public sealed record NuiTemplateCell(NuiNode Element, float Width, bool Variable);

/// <summary>
/// A parsed NUI element (row, col, group, widget). Layout results are written to
/// <see cref="Frame"/> (the widget's rectangle, margins excluded) in the
/// coordinate space of the parent's frame (or a list row / scroll content).
/// </summary>
public sealed class NuiNode
{
    public string Type = "";
    public string Id = "";
    public JsonObject Json = new();
    public readonly List<NuiNode> Children = new();
    public readonly List<NuiTemplateCell> Template = new();
    public float? Width, Height, Aspect;
    public float? MarginOverride, PaddingOverride;

    public NuiRect Frame;
    /// <summary>Size of the scrolled content (groups); equals the viewport when nothing scrolls.</summary>
    public NuiVec ContentSize;
    /// <summary>Viewport size inside a scrolling group (frame minus padding and visible scrollbars).</summary>
    public NuiVec Viewport;
    public bool ShowScrollX, ShowScrollY;

    public JsonNode? this[string key] => Json.TryGetPropertyValue(key, out var v) ? v : null;

    public bool IsLine => Type is "row" or "col";

    /// <summary>Group-like elements that scroll their content (group, and the group NuiText/NuiList imply).</summary>
    public bool IsGroup => Type == "group";

    /// <summary>The window's root element, which has no default margin.</summary>
    public bool IsRoot;

    public float Margin => MarginOverride ?? (IsRoot || Type == "spacer" ? 0 : NuiStyle.WidgetMargin);

    /// <summary>Whether padding insets the content (groups, lists, rows, columns); on other widgets it moves the whole
    /// widget right and down without resizing it or its neighbours.</summary>
    public bool PadsInside => IsLine || Type is "group" or "list";

    public float Padding => PaddingOverride ?? (Type is "group" or "list" ? NuiStyle.GroupPadding : 0);

    public int Scrollbars => Json["scrollbars"] is JsonNode s && NuiBinds.Number(s) is double d ? (int)d
        : Type == "list" ? NuiStyle.ScrollY : NuiStyle.ScrollAuto;

    public bool Border => Json["border"] is not JsonNode b || NuiBinds.Number(b) is not double d || d != 0;

    public static NuiNode Parse(JsonNode? json)
    {
        var n = new NuiNode();
        if (json is not JsonObject o) return n;
        n.Json = o;
        n.Type = o["type"] is JsonValue t && t.TryGetValue<string>(out var ts) ? ts : "";
        n.Id = o["id"] is JsonValue i && i.TryGetValue<string>(out var id) ? id : "";
        n.Width = Positive(o["width"]);
        n.Height = Positive(o["height"]);
        n.Aspect = Positive(o["aspect"]);
        n.MarginOverride = NonNegative(o["margin"]);
        n.PaddingOverride = NonNegative(o["padding"]);
        if (o["children"] is JsonArray children)
            foreach (var c in children)
                if (c is JsonObject) n.Children.Add(Parse(c));
        if (o["row_template"] is JsonArray template)
            foreach (var cell in template)
            {
                if (cell is not JsonArray parts || parts.Count == 0) continue;
                float w = parts.Count > 1 ? (float)(NuiBinds.Number(parts[1]) ?? 0) : 0;
                bool variable = parts.Count > 2 && (NuiBinds.Number(parts[2]) ?? 0) != 0;
                n.Template.Add(new NuiTemplateCell(Parse(parts[0]), MathF.Max(0, w), variable));
            }
        return n;
    }

    static float? Positive(JsonNode? v) => NuiBinds.Number(v) is double d && d > 0 ? (float)d : null;
    static float? NonNegative(JsonNode? v) => NuiBinds.Number(v) is double d && d >= 0 ? (float)d : null;

    public IEnumerable<NuiNode> Descendants()
    {
        foreach (var c in Children)
        {
            yield return c;
            foreach (var d in c.Descendants()) yield return d;
        }
    }

    public NuiNode? Find(string id) => id.Length == 0 ? null : Descendants().FirstOrDefault(d => d.Id == id);

    /// <summary>NuiSetGroupLayout: replaces the single child of a group.</summary>
    public void ReplaceChild(NuiNode child)
    {
        Children.Clear();
        Children.Add(child);
    }
}

/// <summary>
/// NUI layout: rows and columns place children along their axis; children with a
/// fixed size (NuiWidth / NuiHeight, or derived from NuiAspect) keep it and the
/// rest (including spacers) share what remains equally. Across the axis a child
/// takes its fixed size or the full span. Rows advise their parent of a height
/// (the tallest fixed child) and columns of a width; both advise their length
/// when every child is fixed. Groups never advise a size.
/// </summary>
public static class NuiLayout
{
    /// <summary>Preferred outer size (margins included); null on an axis means "flexible".</summary>
    public static (float? W, float? H) Preferred(NuiNode n)
    {
        float? w = n.Width, h = n.Height;
        if (n.Aspect is float a)
        {
            if (w == null && h != null) w = h * a;
            else if (h == null && w != null) h = w / a;
        }
        if (n.IsLine && n.Children.Count > 0)
        {
            bool horizontal = n.Type == "row";
            var prefs = n.Children.Select(Preferred).ToList();
            var main = prefs.Select(p => horizontal ? p.W : p.H).ToList();
            // A nested row or column's own margin counts along the line but not across it (a column in a row adds
            // its margins to the row's length, not to its height).
            var cross = prefs.Select((p, i) => (horizontal ? p.H : p.W) - (n.Children[i].IsLine ? 2 * n.Children[i].Margin : 0))
                .Where(x => x != null).Select(x => x!.Value).ToList();
            float pad = 2 * n.Padding;
            float? mainSize = main.All(x => x != null) ? main.Sum(x => x!.Value) + pad : null;
            float? crossSize = cross.Count > 0 ? cross.Max() + pad : null;
            if (horizontal) { w ??= mainSize; h ??= crossSize; }
            else { h ??= mainSize; w ??= crossSize; }
        }
        float m = 2 * n.Margin;
        return (w + m, h + m);
    }

    /// <summary>Lays out an element into its outer box (margins included), recursively.</summary>
    public static void Arrange(NuiNode n, NuiRect outer)
    {
        n.Frame = outer.Deflate(n.Margin);
        if (!n.PadsInside && n.Padding > 0)
            n.Frame = new NuiRect(n.Frame.X + n.Padding, n.Frame.Y + n.Padding, n.Frame.W, n.Frame.H);
        var content = new NuiRect(0, 0, n.Frame.W, n.Frame.H).Deflate(n.PadsInside ? n.Padding : 0);
        switch (n.Type)
        {
            case "row": ArrangeLine(n, content, true); break;
            case "col": ArrangeLine(n, content, false); break;
            case "group": ArrangeGroup(n, content); break;
            default:
                n.ContentSize = new NuiVec(content.W, content.H);
                n.Viewport = n.ContentSize;
                break;
        }
    }

    static void ArrangeLine(NuiNode n, NuiRect content, bool horizontal)
    {
        float mainAvail = horizontal ? content.W : content.H;
        float crossAvail = horizontal ? content.H : content.W;
        var sizes = new float?[n.Children.Count];
        var crosses = new float[n.Children.Count];
        float fixedSum = 0;
        int flex = 0;
        for (int i = 0; i < n.Children.Count; i++)
        {
            var c = n.Children[i];
            var (pw, ph) = Preferred(c);
            float? main = horizontal ? pw : ph;
            float? cross = horizontal ? ph : pw;
            crosses[i] = cross ?? crossAvail;
            // An aspect ratio with only the cross size known (the line's span) fixes the main size.
            if (main == null && c.Aspect is float a && c.Width == null && c.Height == null)
            {
                float inner = MathF.Max(0, crosses[i] - 2 * c.Margin);
                main = (horizontal ? inner * a : inner / a) + 2 * c.Margin;
            }
            sizes[i] = main;
            if (main is float f) fixedSum += f;
            else flex++;
        }
        float share = flex > 0 ? MathF.Max(0, (mainAvail - fixedSum) / flex) : 0;
        float pos = horizontal ? content.X : content.Y;
        for (int i = 0; i < n.Children.Count; i++)
        {
            float main = sizes[i] ?? share;
            var rect = horizontal
                ? new NuiRect(pos, content.Y, main, crosses[i])
                : new NuiRect(content.X, pos, crosses[i], main);
            Arrange(n.Children[i], rect);
            pos += main;
        }
        n.ContentSize = new NuiVec(content.W, content.H);
        n.Viewport = n.ContentSize;
    }

    /// <summary>
    /// A group holds one child in a (possibly scrolling) viewport. The child fills the
    /// viewport; where it advises a larger size and that axis may scroll, the content
    /// grows instead and a scrollbar takes room from the other axis.
    /// </summary>
    static void ArrangeGroup(NuiNode n, NuiRect content)
    {
        var child = n.Children.FirstOrDefault();
        var (pw, ph) = child != null ? Preferred(child) : (null, null);
        int mode = n.Scrollbars;
        bool canX = mode is NuiStyle.ScrollX or NuiStyle.ScrollBoth or NuiStyle.ScrollAuto;
        bool canY = mode is NuiStyle.ScrollY or NuiStyle.ScrollBoth or NuiStyle.ScrollAuto;
        bool showX = mode is NuiStyle.ScrollX or NuiStyle.ScrollBoth;
        bool showY = mode is NuiStyle.ScrollY or NuiStyle.ScrollBoth;
        float vw = content.W, vh = content.H;
        for (int pass = 0; pass < 2; pass++)
        {
            vw = content.W - (showY ? NuiStyle.ScrollbarSize : 0);
            vh = content.H - (showX ? NuiStyle.ScrollbarSize : 0);
            if (canY && ph > vh) showY = true;
            if (canX && pw > vw) showX = true;
        }
        vw = MathF.Max(0, content.W - (showY ? NuiStyle.ScrollbarSize : 0));
        vh = MathF.Max(0, content.H - (showX ? NuiStyle.ScrollbarSize : 0));
        float cw = canX && pw > vw ? pw.Value : vw;
        float ch = canY && ph > vh ? ph.Value : vh;
        n.ShowScrollX = showX;
        n.ShowScrollY = showY;
        n.Viewport = new NuiVec(vw, vh);
        n.ContentSize = new NuiVec(cw, ch);
        // Child coordinates are relative to the scroll content's origin.
        if (child != null) Arrange(child, new NuiRect(0, 0, cw, ch));
    }

    /// <summary>Vertical distance between list rows.</summary>
    public static float ListPitch(float rowHeight) => rowHeight + 2 * NuiStyle.WidgetMargin;

    /// <summary>
    /// Outer boxes of a list row's cells across <paramref name="width"/>: fixed cells
    /// keep their width, auto (0) and variable cells share the remaining space.
    /// </summary>
    public static NuiRect[] ListCells(IReadOnlyList<NuiTemplateCell> cells, float width, float rowHeight)
    {
        var result = new NuiRect[cells.Count];
        float pitch = ListPitch(rowHeight);
        float fixedSum = 0;
        int grow = 0;
        foreach (var c in cells)
        {
            if (c.Width > 0) fixedSum += c.Width + 2 * c.Element.Margin;
            if (c.Width <= 0 || c.Variable) grow++;
        }
        float extra = grow > 0 ? MathF.Max(0, (width - fixedSum) / grow) : 0;
        float x = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            float w = c.Width > 0 ? c.Width + 2 * c.Element.Margin : 0;
            if (c.Width <= 0 || c.Variable) w += extra;
            result[i] = new NuiRect(x, 0, w, pitch);
            x += w;
        }
        return result;
    }

    /// <summary>Total width of a list row when nothing grows (for horizontal scrolling).</summary>
    public static float ListMinWidth(IReadOnlyList<NuiTemplateCell> cells) =>
        cells.Sum(c => c.Width > 0 ? c.Width + 2 * c.Element.Margin : 0);
}

/// <summary>Window placement rules for the "geometry" rect and its constraints.</summary>
public static class NuiGeometry
{
    /// <summary>
    /// Screen rectangle for a requested geometry: x/y of -1 centre on the screen,
    /// -2 put the corner at the mouse, -3 centre on the mouse; size constraints
    /// (min w/h in x/y, max in w/h, 0 = none) and edge margins (left, top, right,
    /// bottom) apply afterwards.
    /// </summary>
    public static NuiRect Place(NuiRect requested, NuiVec screen, NuiVec mouse, NuiRect? sizeConstraint = null, NuiRect? edgeConstraint = null)
    {
        float w = requested.W, h = requested.H;
        if (w <= 0) w = 400;
        if (h <= 0) h = 300;
        if (sizeConstraint is { } sc)
        {
            if (sc.X > 0) w = MathF.Max(w, sc.X);
            if (sc.Y > 0) h = MathF.Max(h, sc.Y);
            if (sc.W > 0) w = MathF.Min(w, sc.W);
            if (sc.H > 0) h = MathF.Min(h, sc.H);
        }
        float x = Axis(requested.X, w, screen.X, mouse.X);
        float y = Axis(requested.Y, h, screen.Y, mouse.Y);
        if (edgeConstraint is { } ec)
        {
            if (ec.W > 0) x = MathF.Min(x, screen.X - ec.W - w);
            if (ec.H > 0) y = MathF.Min(y, screen.Y - ec.H - h);
            if (ec.X > 0) x = MathF.Max(x, ec.X);
            if (ec.Y > 0) y = MathF.Max(y, ec.Y);
        }
        return new NuiRect(MathF.Round(x), MathF.Round(y), w, h);
    }

    static float Axis(float requested, float size, float screen, float mouse) => requested switch
    {
        -1 => (screen - size) / 2,
        -2 => mouse,
        -3 => mouse - size / 2,
        _ => requested,
    };

    /// <summary>Whether a size constraint allows resizing at all (min == max on both axes locks it).</summary>
    public static bool Resizable(NuiRect? sizeConstraint, bool horizontal) =>
        sizeConstraint is not { } sc || (horizontal ? !(sc.X > 0 && sc.X == sc.W) : !(sc.Y > 0 && sc.Y == sc.H));
}
