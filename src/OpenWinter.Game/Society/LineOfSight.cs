using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Society;

/// <summary>
/// Line of sight against an area's walkmesh. A sight line is blocked when it passes through a triangle
/// whose surface blocks sight (surfacemat.2da LineOfSight = 1: walls, obscuring and non-walkable faces,
/// and the ground itself when a hill is in the way) or through a closed door's footprint. Tile walls
/// are vertical obscuring faces, so the test is a true 3D segment/triangle test.
/// </summary>
public static class LineOfSight
{
    /// <summary>Sight lines run this far above the given positions (creature eye level, roughly).</summary>
    public const float EyeHeight = 1.5f;
    const float CellSize = 2.5f;

    sealed class Grid
    {
        public int GeometryVersion;
        public readonly Dictionary<long, List<int>> Cells = new();
        /// <summary>Per-triangle "tested in query N" marks, so a query needs no set allocation.</summary>
        public int[] Stamp = [];
        public int Query;
    }

    static readonly ConditionalWeakTable<Walkmesh, Grid> _grids = new();
    static readonly ConditionalWeakTable<World, bool[]> _surfaceBlocks = new();
    static readonly ConditionalWeakTable<World, DoorSight> _doorSight = new();

    /// <summary>A world's door BlockSight values by appearance row, read from its own 2DAs once per row.</summary>
    sealed class DoorSight
    {
        public readonly Dictionary<int, bool> DoorTypes = new(), GenericDoors = new();
    }

    /// <summary>Whether the straight line between two objects in the same area is unobstructed.</summary>
    public static bool Between(World world, GameObject a, GameObject b)
    {
        if (a.Area == null || a.Area != b.Area) return false;
        if (a == b) return true;
        return Clear(world, a.Area, a.Position, b.Position, Margin(a), Margin(b));
    }

    /// <summary>Ignore geometry this close to an endpoint (an object's own footprint mustn't hide it).</summary>
    static float Margin(GameObject o) => o switch
    {
        Creature => 0.3f,
        Placeable or Door => 1.5f,
        _ => 0.3f,
    };

    /// <summary>Whether the segment between two points (raised to eye level) is unobstructed.</summary>
    public static bool Clear(World world, Area area, Vector3 from, Vector3 to, float marginFrom = 0.3f, float marginTo = 0.3f)
    {
        var p0 = from + new Vector3(0, 0, EyeHeight);
        var p1 = to + new Vector3(0, 0, EyeHeight);
        var d = p1 - p0;
        float len = d.Length();
        if (len < 1e-4f) return true;
        float tMin = Math.Clamp(marginFrom / len, 0, 1), tMax = Math.Clamp(1 - marginTo / len, 0, 1);
        if (tMin >= tMax) return true;

        var wm = area.Walkmesh;
        var blocks = SurfaceBlocks(world);
        var doorSight = _doorSight.GetValue(world, static _ => new DoorSight());
        var grid = GridFor(wm, blocks);
        var tris = wm.Triangles;
        if (grid.Stamp.Length != tris.Count) grid.Stamp = new int[tris.Count];
        int query = ++grid.Query;
        if (query == int.MaxValue) { Array.Clear(grid.Stamp); grid.Query = query = 1; }
        foreach (long key in CellsAlong(new Vector2(p0.X, p0.Y), new Vector2(p1.X, p1.Y)))
        {
            if (!grid.Cells.TryGetValue(key, out var list)) continue;
            foreach (int i in list)
            {
                if (grid.Stamp[i] == query) continue;
                grid.Stamp[i] = query;
                var t = tris[i];
                if (!Blocks(world, t, blocks, doorSight)) continue;
                if (Intersect(p0, d, t.A, t.B, t.C, out float hit) && hit > tMin && hit < tMax) return false;
            }
        }
        return true;
    }

    static bool Blocks(World world, in Walkmesh.Triangle t, bool[] surfaceBlocks, DoorSight doorSight)
    {
        if (t.Owner != 0)
        {
            // Door footprints: nothing while open; walls of the closed door otherwise.
            if (world.Get((uint)t.Owner) is Door door) return !door.IsOpen && !door.Broken && DoorBlocksSight(world, door, doorSight) && (!t.Walkable || SurfaceBlocks(surfaceBlocks, t.Surface));
            return false;
        }
        return SurfaceBlocks(surfaceBlocks, t.Surface);
    }

    /// <summary>Whether a closed door of this appearance blocks sight (doortypes.2da or genericdoors.2da BlockSight).</summary>
    public static bool DoorBlocksSight(World world, Door door) => DoorBlocksSight(world, door, _doorSight.GetValue(world, static _ => new DoorSight()));

    static bool DoorBlocksSight(World world, Door door, DoorSight cache)
    {
        bool typed = door.Appearance > 0;
        int row = typed ? door.Appearance : door.GenericType;
        var rows = typed ? cache.DoorTypes : cache.GenericDoors;
        if (rows.TryGetValue(row, out bool blocks)) return blocks;
        blocks = world.Resources.TwoDA(typed ? "doortypes" : "genericdoors").GetInt(row, "BlockSight", 1) != 0;
        rows[row] = blocks;
        return blocks;
    }

    static bool SurfaceBlocks(bool[] table, int surface) => surface >= 0 && surface < table.Length && table[surface];

    static bool[] SurfaceBlocks(World world)
    {
        if (_surfaceBlocks.TryGetValue(world, out var table)) return table;
        var mat = world.Resources.TwoDA("surfacemat");
        table = new bool[Math.Max(mat.RowCount, 1)];
        for (int i = 0; i < mat.RowCount; i++) table[i] = mat.GetInt(i, "LineOfSight") != 0;
        if (mat.RowCount == 0)
        {
            // No table: treat everything except "transparent", "door" and "trigger" style rows as blocking.
            table = Enumerable.Range(0, 31).Select(i => i is not (0 or 8 or 18 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30)).ToArray();
        }
        _surfaceBlocks.AddOrUpdate(world, table);
        return table;
    }

    /// <summary>
    /// Grid of the triangles that can ever block sight (sight-blocking surfaces and door footprints);
    /// walkable ground that never blocks is left out, which is most of an area.
    /// </summary>
    static Grid GridFor(Walkmesh wm, bool[] surfaceBlocks)
    {
        if (_grids.TryGetValue(wm, out var g) && g.GeometryVersion == wm.GeometryVersion) return g;
        g = new Grid { GeometryVersion = wm.GeometryVersion };
        var tris = wm.Triangles;
        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i];
            if (t.Owner == 0 && !SurfaceBlocks(surfaceBlocks, t.Surface)) continue;
            float minX = MathF.Min(t.A.X, MathF.Min(t.B.X, t.C.X)), maxX = MathF.Max(t.A.X, MathF.Max(t.B.X, t.C.X));
            float minY = MathF.Min(t.A.Y, MathF.Min(t.B.Y, t.C.Y)), maxY = MathF.Max(t.A.Y, MathF.Max(t.B.Y, t.C.Y));
            for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
                for (int cy = Cell(minY); cy <= Cell(maxY); cy++)
                {
                    long key = Key(cx, cy);
                    if (!g.Cells.TryGetValue(key, out var list)) g.Cells[key] = list = new List<int>();
                    list.Add(i);
                }
        }
        _grids.AddOrUpdate(wm, g);
        return g;
    }

    static int Cell(float v) => (int)MathF.Floor(v / CellSize);
    static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>Grid cells a 2D segment passes through (Amanatides-Woo traversal).</summary>
    static IEnumerable<long> CellsAlong(Vector2 a, Vector2 b)
    {
        int x = Cell(a.X), y = Cell(a.Y);
        int ex = Cell(b.X), ey = Cell(b.Y);
        var d = b - a;
        int sx = Math.Sign(d.X), sy = Math.Sign(d.Y);
        float tdx = d.X != 0 ? MathF.Abs(CellSize / d.X) : float.PositiveInfinity;
        float tdy = d.Y != 0 ? MathF.Abs(CellSize / d.Y) : float.PositiveInfinity;
        float nextX = d.X > 0 ? (x + 1) * CellSize : x * CellSize;
        float nextY = d.Y > 0 ? (y + 1) * CellSize : y * CellSize;
        float tx = d.X != 0 ? (nextX - a.X) / d.X : float.PositiveInfinity;
        float ty = d.Y != 0 ? (nextY - a.Y) / d.Y : float.PositiveInfinity;
        yield return Key(x, y);
        for (int guard = 0; guard < 4096 && (x != ex || y != ey); guard++)
        {
            if (tx < ty) { x += sx; tx += tdx; }
            else { y += sy; ty += tdy; }
            yield return Key(x, y);
        }
    }

    /// <summary>Segment p0 + t·d against a triangle (two-sided Möller–Trumbore); <paramref name="t"/> in [0, 1] on hit.</summary>
    static bool Intersect(Vector3 p0, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-9f) return false;
        float inv = 1f / det;
        var s = p0 - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, q) * inv;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t >= 0 && t <= 1;
    }
}
