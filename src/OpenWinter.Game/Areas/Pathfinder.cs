using System.Numerics;

namespace OpenWinter.Game.Areas;

/// <summary>
/// Grid pathfinding over an area's walkmesh. The grid (0.5 m cells) samples
/// ground heights once, which folds in placeable and door footprints; A* finds
/// a route and string-pulling straightens it using walkmesh traces. Like the original, paths keep a creature's
/// PERSPACE clear of walls (cells it doesn't fit in are avoided, straightened legs are traced at its sides too).
/// </summary>
public sealed class Pathfinder
{
    public const float Cell = 0.5f;
    const float MaxStep = 0.9f;

    readonly Area _area;
    readonly bool _passClosedDoors;
    readonly int _w, _h;
    readonly float[] _height; // NaN = not walkable
    int _version = -1;

    public Pathfinder(Area area, bool passClosedDoors = false)
    {
        _area = area;
        _passClosedDoors = passClosedDoors;
        _w = (int)MathF.Ceiling(area.Width * Area.TileSize / Cell);
        _h = (int)MathF.Ceiling(area.Height * Area.TileSize / Cell);
        _height = new float[_w * _h];
    }

    readonly List<(float MinX, float MinY, float MaxX, float MaxY)> _changed = new();

    void EnsureBuilt()
    {
        var wm = _area.Walkmesh;
        if (_version == wm.Version) return;
        // A door opening or closing only re-samples the cells under it; anything else, the whole area.
        if (_version >= 0 && wm.ChangedSince(_version, _changed))
            foreach (var (minX, minY, maxX, maxY) in _changed)
                Sample((int)MathF.Floor(minX / Cell) - 1, (int)MathF.Floor(minY / Cell) - 1, (int)MathF.Floor(maxX / Cell) + 1, (int)MathF.Floor(maxY / Cell) + 1);
        else Sample(0, 0, _w - 1, _h - 1);
        _version = wm.Version;
    }

    // Per cell, two bits per radius class (0.1 m steps up to 0.7 m): worked out, and fits.
    ushort[] _fits = [];
    // Per cell, two bits per neighbour direction (index (dy+1)*3+dx+1, skipping the middle): worked out, and walkable.
    uint[] _edges = [];

    bool Edge(int from, int to, int dir)
    {
        int bit = (dir > 4 ? dir - 1 : dir) * 2;
        uint bits = _edges[from];
        if ((bits & (1u << bit)) != 0) return (bits & (2u << bit)) != 0;
        var a = new Vector3((from % _w + 0.5f) * Cell, (from / _w + 0.5f) * Cell, _height[from]);
        var b = new Vector3((to % _w + 0.5f) * Cell, (to / _w + 0.5f) * Cell, _height[to]);
        bool ok = _area.Walkmesh.Trace(a, b, MaxStep, ignoreClosedDoors: _passClosedDoors) >= 0.999f;
        _edges[from] = bits | (1u << bit) | (ok ? 2u << bit : 0);
        return ok;
    }

    /// <summary>A creature of radius <paramref name="r"/> fits at the cell's centre: walkable ground r away all round.</summary>
    bool Fits(int idx, float r)
    {
        int cls = Math.Clamp((int)MathF.Round(r * 10), 1, 7) - 1;
        ushort bits = _fits[idx];
        if ((bits & (1 << (cls * 2))) != 0) return (bits & (2 << (cls * 2))) != 0;
        float cx = (idx % _w + 0.5f) * Cell, cy = (idx / _w + 0.5f) * Cell, z = _height[idx];
        float rr = (cls + 1) * 0.1f;
        bool ok = true;
        for (int i = 0; i < 8 && ok; i++)
        {
            float a = i * MathF.PI / 4;
            ok = _area.Walkmesh.GroundHeight(cx + MathF.Cos(a) * rr, cy + MathF.Sin(a) * rr, z, MaxStep, ignoreClosedDoors: _passClosedDoors) != null;
        }
        _fits[idx] = (ushort)(bits | (1 << (cls * 2)) | (ok ? 2 << (cls * 2) : 0));
        return ok;
    }

    void Sample(int x0, int y0, int x1, int y1)
    {
        if (_edges.Length == _height.Length)
            for (int y = Math.Max(0, y0 - 1); y <= Math.Min(_h - 1, y1 + 1); y++)
                for (int x = Math.Max(0, x0 - 1); x <= Math.Min(_w - 1, x1 + 1); x++) _edges[y * _w + x] = 0;
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(_w - 1, x1); y1 = Math.Min(_h - 1, y1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float wx = (x + 0.5f) * Cell, wy = (y + 0.5f) * Cell;
                var top = _area.Walkmesh.HighestGround(wx, wy);
                float? z = top is { } t ? _area.Walkmesh.GroundHeight(wx, wy, t, MaxStep, ignoreClosedDoors: _passClosedDoors) : null;
                _height[y * _w + x] = z ?? float.NaN;
                if (_fits.Length == _height.Length) _fits[y * _w + x] = 0;
            }
    }

    bool Walkable(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && !float.IsNaN(_height[y * _w + x]);

    (int, int) CellOf(Vector3 p) => ((int)MathF.Floor(p.X / Cell), (int)MathF.Floor(p.Y / Cell));

    /// <summary>Nearest walkable cell to a point (searches outward a few metres).</summary>
    (int, int)? Snap(Vector3 p)
    {
        var (cx, cy) = CellOf(p);
        if (Walkable(cx, cy)) return (cx, cy);
        for (int r = 1; r <= 12; r++)
        {
            (int, int)? best = null;
            float bestD = float.MaxValue;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    if (!Walkable(cx + dx, cy + dy)) continue;
                    float d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = (cx + dx, cy + dy); }
                }
            if (best != null) return best;
        }
        return null;
    }

    /// <summary>A shrunken obstacle smaller than this (the start or goal is on the creature itself) is left out.</summary>
    const float MinObstacle = 0.05f;

    /// <summary>How close (metres) a route must get for an unreachable goal to count as reached: doors and furniture block their own footprint.</summary>
    public const float NearEnough = 3f;

    /// <summary>
    /// Waypoints from start to goal (excluding start), or null if unreachable. When the goal itself can't be reached but
    /// the search got within <see cref="NearEnough"/> of it (a door whose nearest open ground is on its far side), the
    /// route to that closest point. <paramref name="obstacles"/> are circles
    /// to go round (busy creatures, <see cref="PersonalSpace.Obstacles"/>); one holding the start or the goal shrinks to
    /// just inside it.
    /// </summary>
    public List<Vector3>? FindPath(Vector3 start, Vector3 goal, int maxNodes = 60000, List<(Vector2 Centre, float Radius)>? obstacles = null,
        float radius = 0)
    {
        _radius = radius;
        _obstacles.Clear();
        if (obstacles != null)
            foreach (var (centre, reach) in obstacles)
            {
                // A creature whose circle holds the start or the goal still stands in the way: shrink the circle to
                // just inside them rather than dropping it, so the route still goes round its body (a door's use
                // point a metre from Bim must not send the walker straight through him).
                float r = MathF.Min(reach, MathF.Min(Vector2.Distance(centre, new(start.X, start.Y)), Vector2.Distance(centre, new(goal.X, goal.Y))) - 0.0005f);
                if (r >= MinObstacle) _obstacles.Add((centre, r));
            }
        // Straight line first: most moves need no search.
        if (Clear(start, goal)) return new List<Vector3> { goal };
        // Only creatures in the way: step round them, as the original does (out to personal space, along, back in).
        if (_obstacles.Count > 0 && Walkable(start, goal) && Detour(start, goal) is { } detour) return detour;
        EnsureBuilt();
        if (_fits.Length != _height.Length) { _fits = new ushort[_height.Length]; _edges = new uint[_height.Length]; }
        var s = Snap(start);
        var g = Snap(goal);
        if (s == null || g == null) return null;
        var (sx, sy) = s.Value;
        var (gx, gy) = g.Value;
        int startIdx = sy * _w + sx, goalIdx = gy * _w + gx;
        // Search state lives in reusable arrays stamped per search (no per-search allocation).
        int cells = _height.Length;
        if (_g.Length != cells) { _g = new float[cells]; _came = new int[cells]; _seen = new int[cells]; _closed = new int[cells]; _blocked = new int[cells]; _search = 0; }
        int search = ++_search;
        if (search == int.MaxValue) { Array.Clear(_seen); Array.Clear(_closed); Array.Clear(_blocked); _search = search = 1; }
        // Cells within reach of an obstacle (grown by half a cell's diagonal, so cell-to-cell steps stay clear).
        foreach (var (centre, reach) in _obstacles)
        {
            float grown = reach + Cell * 0.71f;
            int x0 = Math.Max(0, (int)MathF.Floor((centre.X - grown) / Cell)), x1 = Math.Min(_w - 1, (int)MathF.Floor((centre.X + grown) / Cell));
            int y0 = Math.Max(0, (int)MathF.Floor((centre.Y - grown) / Cell)), y1 = Math.Min(_h - 1, (int)MathF.Floor((centre.Y + grown) / Cell));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float cx = (x + 0.5f) * Cell - centre.X, cy = (y + 0.5f) * Cell - centre.Y;
                    if (cx * cx + cy * cy < grown * grown) _blocked[y * _w + x] = search;
                }
        }
        _blocked[startIdx] = _blocked[goalIdx] = 0;
        _seen[startIdx] = search;
        _g[startIdx] = 0;
        _came[startIdx] = -1;
        _open.Clear();
        _open.Enqueue(startIdx, Heuristic(sx, sy, gx, gy));
        int expanded = 0;
        int closest = startIdx;
        float closestD = float.MaxValue;
        while (_open.TryDequeue(out int cur, out _))
        {
            if (cur == goalIdx)
            {
                var cellPath = Reconstruct(cur);
                var smooth = Smooth(start, cellPath, goal, out var picked);
                return smooth == null ? null : Tighten(start, Corners(start, smooth, cellPath, picked));
            }
            if (_closed[cur] == search) continue;
            _closed[cur] = search;
            float d = Vector2.Distance(new Vector2((cur % _w + 0.5f) * Cell, (cur / _w + 0.5f) * Cell), new Vector2(goal.X, goal.Y));
            if (d < closestD) { closestD = d; closest = cur; }
            if (++expanded > maxNodes) break;
            int cx = cur % _w, cy = cur / _w;
            float cz = _height[cur];
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (!Walkable(nx, ny)) continue;
                    // No corner cutting past blocked cells.
                    if (dx != 0 && dy != 0 && (!Walkable(cx + dx, cy) || !Walkable(cx, cy + dy))) continue;
                    int ni = ny * _w + nx;
                    if (_blocked[ni] == search) continue;
                    if (_radius > 0.05f && ni != goalIdx && !Fits(ni, _radius)) continue;
                    if (MathF.Abs(_height[ni] - cz) > MaxStep * 0.6f) continue;
                    // The step between the two cells must be walkable too (a footprint or wall thinner than a cell).
                    if (!Edge(cur, ni, (dy + 1) * 3 + dx + 1)) continue;
                    float cost = _g[cur] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (_seen[ni] == search && _g[ni] <= cost) continue;
                    _seen[ni] = search;
                    _g[ni] = cost;
                    _came[ni] = cur;
                    _open.Enqueue(ni, cost + Heuristic(nx, ny, gx, gy));
                }
        }
        // The goal's cell is cut off (a door at the area's edge snaps to the strip behind it): get as close as we can.
        if (closestD <= NearEnough && closest != startIdx)
        {
            var cellPath = Reconstruct(closest);
            var near = cellPath[^1];
            var smooth = Smooth(start, cellPath, near, out var picked);
            return smooth == null ? null : Tighten(start, Corners(start, smooth, cellPath, picked));
        }
        return null;
    }

    static float Heuristic(int x, int y, int gx, int gy)
    {
        int dx = Math.Abs(x - gx), dy = Math.Abs(y - gy);
        return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
    }

    float[] _g = [];
    int[] _came = [], _seen = [], _closed = [], _blocked = [];
    readonly List<(Vector2 Centre, float Radius)> _obstacles = new();

    /// <summary>
    /// A path round the obstacles on a straight line: for each in turn (nearest first), out to its radius on the side
    /// away from its centre (left when it is right on the line) a little before it and back a little after it.
    /// Null when that runs into something else.
    /// </summary>
    List<Vector3>? Detour(Vector3 start, Vector3 goal)
    {
        var a = new Vector2(start.X, start.Y);
        var ab = new Vector2(goal.X, goal.Y) - a;
        float len = ab.Length();
        if (len < 1e-4f) return null;
        var dir = ab / len;
        var left = new Vector2(-dir.Y, dir.X);
        var hits = new List<(float Along, float Lateral, float Radius)>();
        foreach (var (centre, radius) in _obstacles)
        {
            var rel = centre - a;
            float along = Vector2.Dot(rel, dir), lateral = Vector2.Dot(rel, left);
            if (along > 0 && along < len && MathF.Abs(lateral) < radius) hits.Add((along, lateral, radius));
        }
        if (hits.Count == 0) return null;
        hits.Sort((x, y) => x.Along.CompareTo(y.Along));
        var path = new List<Vector3>();
        if (hits.Count == 1)
            Around(a, new Vector2(goal.X, goal.Y), a + dir * hits[0].Along + left * hits[0].Lateral, hits[0].Radius, left, path, start.Z);
        else foreach (var (along, lateral, radius) in hits)
        {
            float side = lateral > 0 ? -1 : 1;
            float off = lateral + side * radius;
            // The corners of an octagon round the circle.
            float half = radius * 0.4142f;
            foreach (float d in new[] { along - half, along + half })
            {
                var p = a + dir * d + left * off;
                path.Add(new Vector3(p.X, p.Y, start.Z));
            }
        }
        path.Add(goal);
        var from = start;
        for (int i = 0; i < path.Count; i++)
        {
            float? z = _area.Walkmesh.GroundHeight(path[i].X, path[i].Y, from.Z, MaxStep, ignoreClosedDoors: _passClosedDoors);
            if (z == null) return null;
            if (i < path.Count - 1) path[i] = path[i] with { Z = z.Value };
            if (!ClearOfOthers(from, path[i])) return null;
            from = path[i];
        }
        return path;
    }

    /// <summary>
    /// The shortest way from a to b round one circle: along the tangent from a, round the arc (as a polygon whose
    /// sides touch the circle) and along the tangent to b. It passes on the side away from the centre, on the left
    /// when the centre is right on the line (the shorter way when it isn't).
    /// </summary>
    static void Around(Vector2 a, Vector2 b, Vector2 centre, float radius, Vector2 left, List<Vector3> path, float z)
    {
        float da = Vector2.Distance(a, centre), db = Vector2.Distance(b, centre);
        float ta = MathF.Atan2(a.Y - centre.Y, a.X - centre.X), tb = MathF.Atan2(b.Y - centre.Y, b.X - centre.X);
        float alphaA = MathF.Acos(Math.Clamp(radius / MathF.Max(da, radius), -1, 1)), alphaB = MathF.Acos(Math.Clamp(radius / MathF.Max(db, radius), -1, 1));
        float bestLength = float.MaxValue, bestFrom = 0, bestSweep = 0; int bestSide = 0;
        // The shorter way round; a tie (the centre right on the line) goes left.
        foreach (int side in new[] { -1, 1 })
        {
            float from = ta + side * alphaA, to = tb - side * alphaB;
            float sweep = (to - from) * side;
            sweep %= MathF.Tau;
            if (sweep < 0) sweep += MathF.Tau;
            float length = radius * sweep;
            // The walker's side: the arc's middle point relative to the straight line.
            var mid = centre + new Vector2(MathF.Cos(from + side * sweep / 2), MathF.Sin(from + side * sweep / 2));
            bool onLeft = Vector2.Dot(mid - centre, left) > 0;
            if (length < bestLength - 1e-3f || MathF.Abs(length - bestLength) <= 1e-3f && onLeft)
            {
                bestLength = length; bestFrom = from; bestSweep = sweep; bestSide = side;
            }
        }
        int n = Math.Max(1, (int)MathF.Ceiling(bestSweep / (MathF.PI / 8)));
        float step = bestSweep / n;
        float outer = radius / MathF.Cos(step / 2);
        for (int i = 0; i <= n; i++)
        {
            float angle = bestFrom + bestSide * step * i;
            // Corners just outside the circle so that the sides touch it (the ends, straight out from the tangent
            // points, keep the tangents clear too).
            var p = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * outer;
            path.Add(new Vector3(p.X, p.Y, z));
        }
    }

    /// <summary>A straight walk the walkmesh allows that passes no obstacle (touching one's edge is fine).</summary>
    bool ClearOfOthers(Vector3 from, Vector3 to)
    {
        if (!Walkable(from, to)) return false;
        var a = new Vector2(from.X, from.Y);
        var ab = new Vector2(to.X, to.Y) - a;
        float len2 = ab.LengthSquared();
        foreach (var (centre, radius) in _obstacles)
        {
            float t = len2 > 1e-8f ? Math.Clamp(Vector2.Dot(centre - a, ab) / len2, 0, 1) : 0;
            float r = radius - 0.001f;
            if (Vector2.DistanceSquared(a + ab * t, centre) < r * r) return false;
        }
        return true;
    }

    float _radius;

    /// <summary>
    /// The walkmesh allows a straight walk, also along both sides at the creature's radius (away from the ends,
    /// which may lie near a wall: a door, a chair).
    /// </summary>
    bool Walkable(Vector3 from, Vector3 to)
    {
        if (_area.Walkmesh.Trace(from, to, MaxStep, ignoreClosedDoors: _passClosedDoors) < 0.999f) return false;
        var d = new Vector2(to.X - from.X, to.Y - from.Y);
        float len = d.Length();
        if (_radius <= 0.05f || len <= 2 * _radius) return true;
        d /= len;
        var side = new Vector3(-d.Y * _radius, d.X * _radius, 0);
        var a = from + new Vector3(d.X, d.Y, 0) * _radius;
        var b = to - new Vector3(d.X, d.Y, 0) * _radius;
        a.Z = _area.Walkmesh.GroundHeight(a.X, a.Y, from.Z, MaxStep, ignoreClosedDoors: _passClosedDoors) ?? from.Z;
        b.Z = _area.Walkmesh.GroundHeight(b.X, b.Y, to.Z, MaxStep, ignoreClosedDoors: _passClosedDoors) ?? to.Z;
        // Half way out too: a door's leaf is thinner than a creature.
        return Side(a + side, b + side) && Side(a - side, b - side) && Side(a + side / 2, b + side / 2) && Side(a - side / 2, b - side / 2);
    }

    bool Side(Vector3 a, Vector3 b)
    {
        if (_area.Walkmesh.GroundHeight(a.X, a.Y, a.Z, MaxStep, ignoreClosedDoors: _passClosedDoors) is not { } z) return false;
        return _area.Walkmesh.Trace(new Vector3(a.X, a.Y, z), b, MaxStep, ignoreClosedDoors: _passClosedDoors) >= 0.999f;
    }

    /// <summary>A straight walk: the walkmesh allows it and it passes no obstacle.</summary>
    bool Clear(Vector3 from, Vector3 to)
    {
        if (!Walkable(from, to)) return false;
        if (_obstacles.Count == 0) return true;
        var a = new Vector2(from.X, from.Y);
        var ab = new Vector2(to.X, to.Y) - a;
        float len2 = ab.LengthSquared();
        foreach (var (centre, radius) in _obstacles)
        {
            float t = len2 > 1e-8f ? Math.Clamp(Vector2.Dot(centre - a, ab) / len2, 0, 1) : 0;
            if (Vector2.DistanceSquared(a + ab * t, centre) < radius * radius) return false;
        }
        return true;
    }
    int _search;
    readonly PriorityQueue<int, float> _open = new();

    List<Vector3> Reconstruct(int cur)
    {
        var cells = new List<Vector3>();
        while (cur >= 0)
        {
            cells.Add(new Vector3((cur % _w + 0.5f) * Cell, (cur / _w + 0.5f) * Cell, _height[cur]));
            cur = _came[cur];
        }
        cells.Reverse();
        return cells;
    }

    /// <summary>
    /// Pulls each corner of a straightened path toward the line between its neighbours as far as the walk stays clear,
    /// so paths round corners hug them at the creature's radius rather than at the grid's cell centres.
    /// </summary>
    List<Vector3> Tighten(Vector3 start, List<Vector3> path)
    {
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i + 1 < path.Count; i++)
            {
                var prev = i == 0 ? start : path[i - 1];
                var next = path[i + 1];
                var v = path[i];
                var ab = new Vector2(next.X - prev.X, next.Y - prev.Y);
                float len2 = ab.LengthSquared();
                if (len2 < 1e-6f) continue;
                float t = Math.Clamp(Vector2.Dot(new Vector2(v.X - prev.X, v.Y - prev.Y), ab) / len2, 0, 1);
                var target = new Vector2(prev.X, prev.Y) + ab * t;
                foreach (float f in new[] { 0.9f, 0.75f, 0.6f, 0.45f, 0.3f, 0.15f })
                {
                    var p = Vector2.Lerp(new Vector2(v.X, v.Y), target, f);
                    if (_area.Walkmesh.GroundHeight(p.X, p.Y, v.Z, MaxStep, ignoreClosedDoors: _passClosedDoors) is not { } z) continue;
                    var moved = new Vector3(p.X, p.Y, z);
                    if (Clear(prev, moved) && Clear(moved, next)) { path[i] = moved; break; }
                }
            }
        return path;
    }

    /// <summary>Drops intermediate cells while the walkmesh allows a straight walk.</summary>
    /// <remarks>
    /// Every leg it hands out is walkable (the walk from the start into its cell and from the last cell to the goal
    /// included): null when one isn't, so that movement never gets a leg its step check refuses.
    /// </remarks>
    List<Vector3>? Smooth(Vector3 start, List<Vector3> cells, Vector3 goal, out List<int> picked)
    {
        cells[^1] = goal;
        var result = new List<Vector3>();
        picked = new List<int>();
        var anchor = start;
        int i = 0;
        while (i < cells.Count)
        {
            int furthest = -1;
            for (int j = cells.Count - 1; j >= i; j--)
                if (Clear(anchor, cells[j])) { furthest = j; break; }
            // Too tight for the creature's sides: at least the centre line must be walkable.
            if (furthest < 0)
            {
                if (_area.Walkmesh.Trace(anchor, cells[i], MaxStep, ignoreClosedDoors: _passClosedDoors) < 0.999f) return null;
                furthest = i;
            }
            result.Add(cells[furthest]);
            picked.Add(furthest);
            anchor = cells[furthest];
            i = furthest + 1;
        }
        return result;
    }

    /// <summary>
    /// Straightening keeps the furthest cell in sight, which overshoots a corner when the walk could turn back
    /// sooner: each corner moves to the cell of the grid route between its neighbours that makes the shortest
    /// clear walk.
    /// </summary>
    List<Vector3> Corners(Vector3 start, List<Vector3> path, List<Vector3> cells, List<int> picked)
    {
        for (int k = 0; k + 1 < path.Count; k++)
        {
            var prev = k == 0 ? start : path[k - 1];
            var next = path[k + 1];
            int from = k == 0 ? 0 : picked[k - 1] + 1, to = picked[k + 1] - 1;
            float best = Vector3.Distance(prev, path[k]) + Vector3.Distance(path[k], next);
            for (int m = from; m <= to; m++)
            {
                var c = cells[m];
                float length = Vector3.Distance(prev, c) + Vector3.Distance(c, next);
                if (length < best - 0.01f && Clear(prev, c) && Clear(c, next)) { best = length; path[k] = c; picked[k] = m; }
            }
        }
        return path;
    }
}

public sealed partial class Area
{
    Pathfinder? _pathfinder, _movementPathfinder;
    public Pathfinder Pathfinder => _pathfinder ??= new Pathfinder(this);
    /// <summary>Scripted movement plans through closed doors; collision raises OnBlocked so scripts may open them.</summary>
    public Pathfinder MovementPathfinder => _movementPathfinder ??= new Pathfinder(this, passClosedDoors: true);
}
