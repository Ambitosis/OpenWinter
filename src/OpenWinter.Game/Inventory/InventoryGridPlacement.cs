namespace OpenWinter.Game.Inventory;

/// <summary>Geometry shared by authoritative placement and client drop previews.</summary>
public static class InventoryGridPlacement
{
    /// <summary>Keep the complete footprint inside the page beneath the pointer.</summary>
    public static (int X, int Y)? ClampToPage(GridShape shape, int w, int h, int x, int y, int page)
    {
        if (w <= 0 || h <= 0 || w > shape.Width || h > shape.PageHeight || page < 0
            || shape.Limited && page >= shape.Pages) return null;
        int first = page * shape.PageHeight;
        return (Math.Clamp(x, 0, shape.Width - w), Math.Clamp(y, first, first + shape.PageHeight - h));
    }

    public static HashSet<(int X, int Y)> Occupied(IEnumerable<(int X, int Y, int W, int H)> items)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var item in items)
            if (item.X >= 0 && item.Y >= 0)
                for (int x = 0; x < item.W; x++)
                    for (int y = 0; y < item.H; y++) cells.Add((item.X + x, item.Y + y));
        return cells;
    }

    /// <summary>First free footprint in repository row order, opening pages for unlimited holders.</summary>
    public static (int X, int Y)? FirstFit(GridShape shape, int w, int h, IReadOnlySet<(int X, int Y)> occupied)
    {
        if (w <= 0 || h <= 0 || w > shape.Width) return null;
        int lastRow = occupied.Count == 0 ? 0 : occupied.Max(c => c.Y) + 1;
        int rows = shape.Limited ? shape.Rows : lastRow + h + shape.PageHeight;
        for (int y = 0; y + h <= rows; y++)
            for (int x = 0; x + w <= shape.Width; x++)
            {
                if (!shape.Contains(x, y, w, h)) continue;
                bool free = true;
                for (int i = 0; i < w && free; i++)
                    for (int j = 0; j < h; j++)
                        if (occupied.Contains((x + i, y + j))) { free = false; break; }
                if (free) return (x, y);
            }
        return null;
    }
}
