using System.Numerics;

namespace OpenWinter.Game.Areas;

/// <summary>
/// Whether two points of an area are joined by tile ground (NWNX_Area_GetPathExists): doors, placeables and
/// creatures are ignored, and the path may cross at most <c>maxDepth</c> tiles, the start tile included (a path inside
/// one tile needs a depth of 1; parity suite nwnx_area_tiles). The ground is sampled on a one-metre grid of the
/// tiles' walkmesh, steps of up to a metre joining neighbouring cells.
/// </summary>
public static class TilePathCheck
{
    const float Cell = 1f, Step = 1f;

    public static bool Exists(Area area, Vector3 start, Vector3 end, int maxDepth)
    {
        int cw = (int)(area.Width * Area.TileSize / Cell), ch = (int)(area.Height * Area.TileSize / Cell);
        if (maxDepth <= 0 || cw <= 0 || ch <= 0 || !Inside(start) || !Inside(end)) return false;
        var wm = area.Walkmesh;
        float? z0 = wm.GroundHeight(start.X, start.Y, start.Z, 1000f, tilesOnly: true);
        if (z0 == null) return false;
        int perTile = (int)(Area.TileSize / Cell);
        int startCell = CellOf(start), endCell = CellOf(end);
        var depth = new int[cw * ch];
        var height = new float[cw * ch];
        Array.Fill(depth, int.MaxValue);
        depth[startCell] = 1;
        height[startCell] = z0.Value;
        // 0-1 breadth-first: a step within a tile costs nothing, one into the next tile costs a tile.
        var queue = new LinkedList<int>();
        queue.AddFirst(startCell);
        while (queue.First is { } node)
        {
            queue.RemoveFirst();
            int c = node.Value, cx = c % cw, cy = c / cw;
            if (c == endCell) return depth[c] <= maxDepth;
            foreach (var (dx, dy) in Neighbours)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= cw || ny >= ch) continue;
                bool crosses = nx / perTile != cx / perTile || ny / perTile != cy / perTile;
                int d = depth[c] + (crosses ? 1 : 0);
                int nc = ny * cw + nx;
                if (d >= depth[nc] || d > maxDepth) continue;
                if (wm.GroundHeight((nx + 0.5f) * Cell, (ny + 0.5f) * Cell, height[c], Step, tilesOnly: true) is not { } z) continue;
                depth[nc] = d;
                height[nc] = z;
                if (crosses) queue.AddLast(nc); else queue.AddFirst(nc);
            }
        }
        return false;

        bool Inside(Vector3 p) => p.X >= 0 && p.Y >= 0 && p.X < cw * Cell && p.Y < ch * Cell;
        int CellOf(Vector3 p) => (int)(p.Y / Cell) * cw + (int)(p.X / Cell);
    }

    static readonly (int, int)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };
}
