using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// NuiList: rows built from a template of cells, row count from a bind (an int,
/// or an array whose length counts). Only visible rows exist as controls; each
/// row's elements resolve array binds at their row index and send that index
/// with their events.
/// </summary>
public partial class NuiListView : NuiElement
{
    NuiScrollArea _scroll = null!;
    readonly Dictionary<int, Control> _rows = new();
    NuiRect[] _cells = Array.Empty<NuiRect>();
    int _count;
    float _rowHeight = NuiStyle.RowHeight;
    float _contentWidth;

    protected override bool TakesMouse => true;

    protected override IEnumerable<string> OwnBinds() => NuiBinds.BindNames(Node.Json, "row_template");

    protected override void Build()
    {
        _rowHeight = (float)(NuiBinds.Number(Node["row_height"]) is double rh && rh > 0 ? rh : NuiStyle.RowHeight);
        _scroll = new NuiScrollArea { Skin = Ctx.Skin };
        _scroll.Scrolled += SyncRows;
        AddChild(_scroll);
    }

    int RowCount()
    {
        var v = Ctx.Binds.Resolve(Node["row_count"], Row);
        return v is JsonArray a ? a.Count : Math.Max(0, (int)(NuiBinds.Number(v) ?? 0));
    }

    public override void Refresh()
    {
        base.Refresh();
        int count = RowCount();
        if (count != _count)
        {
            _count = count;
            Relayout();
        }
    }

    public override void ApplyLayout()
    {
        base.ApplyLayout();
        Relayout();
    }

    void Relayout()
    {
        if (_scroll == null || Size.X <= 0) return;
        float pad = Node.Border ? NuiStyle.GroupPadding : 0;
        _scroll.Position = new Vector2(pad, pad);
        _scroll.Size = new Vector2(Math.Max(0, Size.X - 2 * pad), Math.Max(0, Size.Y - 2 * pad));
        int mode = Node.Scrollbars;
        float pitch = NuiLayout.ListPitch(_rowHeight);
        float height = _count * pitch;
        bool canY = mode is NuiStyle.ScrollY or NuiStyle.ScrollBoth or NuiStyle.ScrollAuto;
        bool canX = mode is NuiStyle.ScrollX or NuiStyle.ScrollBoth or NuiStyle.ScrollAuto;
        bool showY = mode is NuiStyle.ScrollY or NuiStyle.ScrollBoth || (canY && height > _scroll.Size.Y);
        bool showX = mode is NuiStyle.ScrollX or NuiStyle.ScrollBoth;
        float viewW = Math.Max(0, _scroll.Size.X - (showY ? NuiStyle.ScrollbarSize : 0));
        float minW = NuiLayout.ListMinWidth(Node.Template);
        _contentWidth = canX ? Math.Max(viewW, minW) : viewW;
        _cells = NuiLayout.ListCells(Node.Template, _contentWidth, _rowHeight);
        for (int i = 0; i < Node.Template.Count; i++) NuiLayout.Arrange(Node.Template[i].Element, _cells[i]);
        _scroll.SetContent(new Vector2(_contentWidth, Math.Max(height, 0)), showX, showY);
        foreach (var r in _rows.Values) r.QueueFree();
        _rows.Clear();
        SyncRows();
    }

    /// <summary>Creates the rows scrolled into view and frees the rest.</summary>
    void SyncRows()
    {
        float pitch = NuiLayout.ListPitch(_rowHeight);
        float top = _scroll.ScrollOffset.Y, view = _scroll.ViewportSize.Y;
        int first = Math.Max(0, (int)(top / pitch)), last = Math.Min(_count - 1, (int)((top + view) / pitch));
        foreach (var i in _rows.Keys.Where(i => i < first || i > last).ToList())
        {
            _rows[i].QueueFree();
            _rows.Remove(i);
        }
        for (int i = first; i <= last; i++)
        {
            if (_rows.ContainsKey(i)) continue;
            var row = new Control { MouseFilter = MouseFilterEnum.Pass, Position = new Vector2(0, i * pitch), Size = new Vector2(_contentWidth, pitch) };
            _scroll.Content.AddChild(row);
            _rows[i] = row;
            foreach (var cell in Node.Template)
            {
                var e = Create(cell.Element, Ctx, i);
                row.AddChild(e);
                e.ApplyLayout();
            }
        }
    }

    /// <summary>Scripted access for capture runs: the element with this id in a visible row.</summary>
    public NuiElement? RowElement(int row, string id) =>
        _rows.TryGetValue(row, out var r) ? r.GetChildren().OfType<NuiElement>().SelectMany(Flatten).FirstOrDefault(e => e.Node.Id == id) : null;

    static IEnumerable<NuiElement> Flatten(NuiElement e) =>
        new[] { e }.Concat(e.GetChildren().OfType<NuiElement>().SelectMany(Flatten));

    protected override bool OnWheel(float dx, float dy) => _scroll.ScrollBy(dx, dy);

    public override void _Draw()
    {
        if (Node.Border) DrawStyleBox(Ctx.Skin.Group, new Rect2(Vector2.Zero, Size));
        DrawEncouraged();
    }
}
