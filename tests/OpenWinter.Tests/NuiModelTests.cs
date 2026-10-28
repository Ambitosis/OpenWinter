using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>The Godot-free part of the client's NUI renderer: layout, binds and window placement.</summary>
public class NuiModelTests
{
    static JsonObject El(string type, float? w = null, float? h = null, params JsonObject[] children)
    {
        var o = new JsonObject { ["type"] = type, ["label"] = null, ["value"] = null };
        if (w != null) o["width"] = w;
        if (h != null) o["height"] = h;
        if (children.Length > 0) o["children"] = new JsonArray(children.Select(c => (JsonNode)c).ToArray());
        return o;
    }

    static JsonObject Bind(string name, int numberFlags = 0, int precision = 0, int textFlags = 0) =>
        new() { ["bind"] = name, ["number_flags"] = numberFlags, ["number_precision"] = precision, ["text_flags"] = textFlags };

    const float M = NuiStyle.WidgetMargin;

    /// <summary>An element as a window's root (no default margin of its own).</summary>
    static NuiNode Root(JsonObject json)
    {
        var n = NuiNode.Parse(json);
        n.IsRoot = true;
        return n;
    }

    [Fact]
    public void SpacersCentreAFixedButtonInARow()
    {
        var row = Root(El("row", null, null, El("spacer"), El("button", 100, 30), El("spacer")));
        NuiLayout.Arrange(row, new NuiRect(0, 0, 400, 50));
        var button = row.Children[1];
        Assert.Equal(new NuiRect((400 - 100) / 2f, M, 100, 30), button.Frame);
        Assert.Equal(row.Children[0].Frame.W, row.Children[2].Frame.W);
    }

    [Fact]
    public void FixedChildrenWithoutSpacersPackFromTheStart()
    {
        var row = NuiNode.Parse(El("row", null, null, El("button", 50, 20), El("button", 60, 20)));
        NuiLayout.Arrange(row, new NuiRect(0, 0, 400, 40));
        Assert.Equal(M, row.Children[0].Frame.X);
        Assert.Equal(50 + 2 * M + M, row.Children[1].Frame.X);
    }

    [Fact]
    public void RowsAdviseTheirHeightAndFlexibleSiblingsShareTheRest()
    {
        var col = Root(El("col", null, null,
            El("row", null, null, El("spacer"), El("button", 80, 35)),
            El("label"),
            El("text")));
        // The button's margins and the row's own.
        var (_, h) = NuiLayout.Preferred(col.Children[0]);
        Assert.Equal(35 + 4 * M, h);
        NuiLayout.Arrange(col, new NuiRect(0, 0, 300, 300));
        Assert.Equal(35 + 2 * M, col.Children[0].Frame.H);
        float rest = (300 - (35 + 4 * M)) / 2;
        Assert.Equal(rest - 2 * M, col.Children[1].Frame.H);
        Assert.Equal(col.Children[0].Frame.Bottom + 2 * M, col.Children[1].Frame.Y);
        // Flexible widgets span the column's width.
        Assert.Equal(300 - 2 * M, col.Children[1].Frame.W);
    }

    [Fact]
    public void ColumnsAdviseWidthToTheirRow()
    {
        var row = Root(El("row", null, null,
            El("col", null, null, El("button", 120, 30), El("label", null, 20)),
            El("group")));
        NuiLayout.Arrange(row, new NuiRect(0, 0, 500, 200));
        Assert.Equal(124, row.Children[0].Frame.W);
        Assert.Equal(500 - 128 - 2 * M, row.Children[1].Frame.W);
        // The label in the column takes the column's width.
        Assert.Equal(120, row.Children[0].Children[1].Frame.W);
    }

    [Fact]
    public void SpacersHaveNoMarginAndPaddingMovesAWidgetWithoutResizingIt()
    {
        // As measured on the original (visual scene nuifont): boxes in a row abut, a padded label shifts by its
        // padding and keeps its width, and the next cell stays where it was.
        static JsonObject Zero(JsonObject o)
        {
            o["margin"] = 0.0;
            return o;
        }
        var padded = Zero(El("label", 75, 26));
        padded["padding"] = 5.0;
        var row = Root(El("row", null, null, El("spacer", 60, 20), padded, Zero(El("label", 115, 26))));
        NuiLayout.Arrange(row, new NuiRect(0, 0, 400, 40));
        Assert.Equal(new NuiRect(0, 0, 60, 20), row.Children[0].Frame);
        Assert.Equal(new NuiRect(65, 5, 75, 26), row.Children[1].Frame);
        Assert.Equal(135, row.Children[2].Frame.X);
    }

    [Fact]
    public void AColumnInARowAddsItsMarginsToTheRowsLengthButNotItsHeight()
    {
        static JsonObject Box() => new() { ["type"] = "spacer", ["width"] = 60.0, ["height"] = 20.0 };
        var col = Root(El("col", null, null, El("row", null, null, El("col", null, null, Box(), Box()), Box())));
        var (w, h) = NuiLayout.Preferred(col.Children[0]);
        Assert.Equal(60 + 2 * M + 60 + 2 * M, w);
        Assert.Equal(40 + 2 * M, h);
    }

    [Fact]
    public void AspectDerivesTheMissingDimension()
    {
        var img = El("image", null, 40);
        img["aspect"] = 2.0;
        var row = NuiNode.Parse(El("row", null, null, img, El("spacer")));
        NuiLayout.Arrange(row, new NuiRect(0, 0, 300, 60));
        Assert.Equal(80, row.Children[0].Frame.W);
        Assert.Equal(40, row.Children[0].Frame.H);
    }

    [Fact]
    public void ExplicitMarginAndPaddingOverrideDefaults()
    {
        var b = El("button", 100, 20);
        b["margin"] = 10.0;
        var row = El("row", null, null, b);
        row["padding"] = 5.0;
        var n = NuiNode.Parse(row);
        NuiLayout.Arrange(n, new NuiRect(0, 0, 300, 100));
        Assert.Equal(new NuiRect(15, 15, 100, 20), n.Children[0].Frame);
    }

    [Fact]
    public void GroupScrollsWhenItsContentAdvisesMoreThanFits()
    {
        var buttons = Enumerable.Range(0, 5).Select(_ => El("button", null, 30)).ToArray();
        var group = El("group", null, null, El("col", null, null, buttons));
        group["scrollbars"] = NuiStyle.ScrollAuto;
        var n = NuiNode.Parse(group);
        NuiLayout.Arrange(n, new NuiRect(0, 0, 200, 100));
        float inner = 200 - 2 * M - 2 * NuiStyle.GroupPadding;
        Assert.True(n.ShowScrollY);
        Assert.False(n.ShowScrollX);
        // The buttons and the column each add their margins.
        Assert.Equal(5 * (30 + 2 * M) + 2 * M, n.ContentSize.Y);
        Assert.Equal(inner - NuiStyle.ScrollbarSize, n.Viewport.X);
        Assert.Equal(inner - NuiStyle.ScrollbarSize - 4 * M, n.Children[0].Children[0].Frame.W);
    }

    [Fact]
    public void GroupWithoutScrollbarsClipsInstead()
    {
        var buttons = Enumerable.Range(0, 5).Select(_ => El("button", null, 30)).ToArray();
        var group = El("group", null, null, El("col", null, null, buttons));
        group["scrollbars"] = NuiStyle.ScrollNone;
        var n = NuiNode.Parse(group);
        NuiLayout.Arrange(n, new NuiRect(0, 0, 200, 100));
        Assert.False(n.ShowScrollY);
        Assert.Equal(n.Viewport.Y, n.ContentSize.Y);
    }

    [Fact]
    public void ListTemplateCellsShareSpaceAmongAutoAndVariableCells()
    {
        var list = new JsonObject
        {
            ["type"] = "list",
            ["row_template"] = new JsonArray(
                new JsonArray(El("image"), 30.0, false),
                new JsonArray(El("label"), 0.0, true),
                new JsonArray(El("button"), 60.0, true)),
            ["row_count"] = Bind("names"),
            ["row_height"] = 25.0,
        };
        var n = NuiNode.Parse(list);
        Assert.Equal(3, n.Template.Count);
        var cells = NuiLayout.ListCells(n.Template, 400, 25);
        float fixedW = 30 + 2 * M + 60 + 2 * M;
        float extra = (400 - fixedW) / 2;
        Assert.Equal(30 + 2 * M, cells[0].W);
        Assert.Equal(extra, cells[1].W, 3);
        Assert.Equal(60 + 2 * M + extra, cells[2].W, 3);
        Assert.Equal(400, cells[2].Right, 3);
        Assert.Equal(25 + 2 * M, cells[0].H);
    }

    [Fact]
    public void BindsResolvePerListRowAndFormatNumbers()
    {
        var b = new NuiBinds();
        b.Set("names", new JsonArray("a", "b", "c"));
        b.Set("level", 7);
        b.Set("ratio", 0.756);
        b.Set("flags", 255);
        Assert.Equal("b", b.Text(Bind("names"), 1));
        Assert.Equal("", b.Text(Bind("names"), 9));
        Assert.Equal("7", b.Text(Bind("level"), 2)); // scalars apply to every row
        Assert.Equal("0.76", b.Text(Bind("ratio", 0, 2)));
        Assert.Equal("ff", b.Text(Bind("flags", NuiBinds.NumberFlagHex)));
        Assert.Equal("B", b.Text(Bind("names", 0, 0, NuiBinds.TextFlagUpper), 1));
        Assert.Equal("static", b.Text(JsonValue.Create("static")));
        b.StrRef = s => s == 12 ? "Twelve" : "";
        Assert.Equal("Twelve", b.Text(new JsonObject { ["strref"] = 12 }));
    }

    [Fact]
    public void BindsReadTypedValues()
    {
        var b = new NuiBinds();
        b.Set("c", new NuiColor(10, 20, 30).ToJson());
        b.Set("r", new NuiRect(-1, -1, 300, 200).ToJson());
        b.Set("on", true);
        b.Set("count", 3.0);
        Assert.Equal(new NuiColor(10, 20, 30, 255), b.Color(Bind("c")));
        Assert.Equal(new NuiRect(-1, -1, 300, 200), b.Rect(Bind("r")));
        Assert.True(b.Bool(Bind("on")));
        Assert.True(b.Bool(JsonValue.Create(1)));
        Assert.False(b.Bool(Bind("missing"), -1, false));
        Assert.Equal(3, b.Int(Bind("count")));
        Assert.Equal(0.5, b.Float(JsonValue.Create(0.5)));
    }

    [Fact]
    public void UserEditsInAListRowReplaceOnlyThatElement()
    {
        var b = new NuiBinds();
        b.Set("done", new JsonArray(false, false, true));
        var full = b.WithValue("done", 1, JsonValue.Create(true));
        Assert.Equal("[false,true,true]", full!.ToJsonString());
        Assert.Equal("[false,false,true]", b.Get("done")!.ToJsonString());
        Assert.Equal("5", b.WithValue("x", -1, JsonValue.Create(5))!.ToJsonString());
    }

    [Fact]
    public void ChangedFiresForEverySet()
    {
        var b = new NuiBinds();
        var seen = new List<string>();
        b.Changed += seen.Add;
        b.Set("a", 1);
        b.Set("b", null);
        Assert.Equal(new[] { "a", "b" }, seen);
    }

    [Fact]
    public void BindNamesSkipChildElements()
    {
        var el = El("row", null, null, El("label"));
        el["enabled"] = Bind("en");
        ((JsonObject)el["children"]![0]!)["value"] = Bind("inner");
        Assert.Equal(new[] { "en" }, NuiBinds.BindNames(el, "children").ToArray());
        Assert.Equal(new[] { "en", "inner" }, NuiBinds.BindNames(el).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void ParsesIdsSizesAndChildren()
    {
        var b = El("button", 100, 30);
        b["id"] = "ok";
        var n = NuiNode.Parse(El("col", null, null, El("row", null, null, b)));
        Assert.Equal("ok", n.Find("ok")!.Id);
        Assert.Equal(100f, n.Find("ok")!.Width);
        Assert.Null(n.Find("missing"));
    }

    [Fact]
    public void GeometryCentresAndFollowsTheMouse()
    {
        var screen = new NuiVec(1600, 900);
        var mouse = new NuiVec(200, 100);
        Assert.Equal(new NuiRect(500, 250, 600, 400), NuiGeometry.Place(new NuiRect(-1, -1, 600, 400), screen, mouse));
        Assert.Equal(new NuiRect(200, 100, 600, 400), NuiGeometry.Place(new NuiRect(-2, -2, 600, 400), screen, mouse));
        Assert.Equal(new NuiRect(-100, -100, 600, 400), NuiGeometry.Place(new NuiRect(-3, -3, 600, 400), screen, mouse));
        Assert.Equal(new NuiRect(10, 250, 600, 400), NuiGeometry.Place(new NuiRect(10, -1, 600, 400), screen, mouse));
    }

    [Fact]
    public void GeometryAppliesSizeAndEdgeConstraints()
    {
        var screen = new NuiVec(1600, 900);
        var r = NuiGeometry.Place(new NuiRect(0, 0, 100, 1000), screen, default, sizeConstraint: new NuiRect(200, 0, 0, 500));
        Assert.Equal(200, r.W);
        Assert.Equal(500, r.H);
        r = NuiGeometry.Place(new NuiRect(1500, 0, 300, 200), screen, default, edgeConstraint: new NuiRect(20, 30, 40, 0));
        Assert.Equal(1600 - 40 - 300, r.X);
        Assert.Equal(30, r.Y);
        Assert.False(NuiGeometry.Resizable(new NuiRect(300, 0, 300, 0), true));
        Assert.True(NuiGeometry.Resizable(new NuiRect(300, 0, 300, 0), false));
    }
}
