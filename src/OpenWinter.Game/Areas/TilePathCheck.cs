using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenWinter.Game.Areas;

/// <summary>
/// Whether two points of an area are joined by tile ground (NWNX_Area_GetPathExists): doors, placeables and
/// creatures are ignored, and the path may cross at most <c>maxDepth</c> tiles, the start tile included (a path inside
/// one tile needs a depth of 1; parity suite nwnx_area_tiles). The tiles' walkmesh is sampled once per area (until
/// its geometry changes) on a one-metre grid, steps of up to a metre joining neighbouring cells; each tile's ground
/// falls into connected pieces, and a query walks the graph of pieces.
/// </summary>
public static class TilePathCheck
{
    const float Cell = 1f, Step = 1f;
    const int PerTile = (int)(Area.TileSize / Cell);

    sealed class Graph
    {
        public int GeometryVersion, Width, Height, Cw, Ch;
        /// <summary>Per cell, the ground piece it belongs to (-1: no ground).</summary>
        public int[] Piece = [];
        /// <summary>Neighbouring pieces (in other tiles) of each piece.</summary>
        public int[][] Links = [];
        /// <summary>Query scratch: per piece the depth reached, and the queue.</summary>
        public int[] Depth = [], Queue = [];
    }

    static readonly ConditionalWeakTable<Area, Graph> Cache = new();

    public static bool Exists(Area area, Vector3 start, Vector3 end, int maxDepth)
    {
        if (maxDepth <= 0) return false;
        var g = GraphOf(area);
        int a = PieceAt(g, start), b = PieceAt(g, end);
        if (a < 0 || b < 0) return false;
        Array.Fill(g.Depth, int.MaxValue);
        int head = 0, tail = 0;
        g.Depth[a] = 1;
        g.Queue[tail++] = a;
        while (head < tail)
        {
            int p = g.Queue[head++];
            if (p == b) return true;
            int d = g.Depth[p] + 1;
            if (d > maxDepth) continue;
            foreach (int q in g.Links[p])
            {
                if (g.Depth[q] <= d) continue;
                g.Depth[q] = d;
                g.Queue[tail++] = q;
            }
        }
        return false;
    }

    static int PieceAt(Graph g, Vector3 p)
    {
        if (p.X < 0 || p.Y < 0 || p.X >= g.Cw * Cell || p.Y >= g.Ch * Cell) return -1;
        return g.Piece[(int)(p.Y / Cell) * g.Cw + (int)(p.X / Cell)];
    }

    static Graph GraphOf(Area area)
    {
        var wm = area.Walkmesh;
        if (Cache.TryGetValue(area, out var g) && g.GeometryVersion == wm.GeometryVersion && g.Width == area.Width && g.Height == area.Height) return g;
        g = Build(area);
        Cache.AddOrUpdate(area, g);
        return g;
    }

    static Graph Build(Area area)
    {
        var wm = area.Walkmesh;
        int cw = area.Width * PerTile, ch = area.Height * PerTile;
        var piece = new int[cw * ch];
        var height = new float[cw * ch];
        Array.Fill(piece, -1);
        var stack = new Stack<int>();
        int pieces = 0;
        // Flood each tile's ground into pieces, staying inside the tile.
        for (int c0 = 0; c0 < piece.Length; c0++)
        {
            if (piece[c0] >= 0) continue;
            int x0 = c0 % cw, y0 = c0 / cw;
            if (wm.GroundHeight((x0 + 0.5f) * Cell, (y0 + 0.5f) * Cell, 1000f, 2000f, tilesOnly: true) is not { } z0) continue;
            int id = pieces++;
            piece[c0] = id;
            height[c0] = z0;
            stack.Push(c0);
            while (stack.Count > 0)
            {
                int c = stack.Pop(), cx = c % cw, cy = c / cw;
                foreach (var (dx, dy) in Neighbours)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= cw || ny >= ch || nx / PerTile != cx / PerTile || ny / PerTile != cy / PerTile) continue;
                    int nc = ny * cw + nx;
                    if (piece[nc] >= 0) continue;
                    if (wm.GroundHeight((nx + 0.5f) * Cell, (ny + 0.5f) * Cell, height[c], Step, tilesOnly: true) is not { } z) continue;
                    piece[nc] = id;
                    height[nc] = z;
                    stack.Push(nc);
                }
            }
        }
        // Pieces of neighbouring tiles touch where cells across the tile edge are within a step of each other.
        var links = new HashSet<int>[pieces];
        for (int i = 0; i < pieces; i++) links[i] = new HashSet<int>();
        for (int c = 0; c < piece.Length; c++)
        {
            if (piece[c] < 0) continue;
            int cx = c % cw, cy = c / cw;
            if ((cx + 1) % PerTile == 0 && cx + 1 < cw) Link(c, c + 1);
            if ((cy + 1) % PerTile == 0 && cy + 1 < ch) Link(c, c + cw);
        }
        return new Graph
        {
            GeometryVersion = wm.GeometryVersion, Width = area.Width, Height = area.Height, Cw = cw, Ch = ch, Piece = piece,
            Links = links.Select(l => l.ToArray()).ToArray(), Depth = new int[pieces], Queue = new int[pieces],
        };

        void Link(int c, int n)
        {
            if (piece[n] < 0 || MathF.Abs(height[n] - height[c]) > Step) return;
            links[piece[c]].Add(piece[n]);
            links[piece[n]].Add(piece[c]);
        }
    }

    static readonly (int, int)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };
}
