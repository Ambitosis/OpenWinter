using System.Numerics;

namespace OpenWinter.Game.Areas;

/// <summary>
/// An area's ground: walkable and blocking triangles (Z-up) bucketed into a
/// grid for fast point queries. Built from tile walkmeshes plus placeable and
/// door footprints.
/// </summary>
public sealed class Walkmesh
{
    public readonly struct Triangle
    {
        public readonly Vector3 A, B, C;
        public readonly int Surface;
        public readonly bool Walkable;
        public readonly int Owner;

        public Triangle(Vector3 a, Vector3 b, Vector3 c, int surface, bool walkable, int owner)
        {
            A = a; B = b; C = c; Surface = surface; Walkable = walkable; Owner = owner;
        }

        /// <summary>Height of the triangle's plane at (x, y) if the point lies inside it in XY.</summary>
        public bool TryHeightAt(float x, float y, out float z)
        {
            z = 0;
            float d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
            if (MathF.Abs(d) < 1e-9f) return false;
            float u = ((B.Y - C.Y) * (x - C.X) + (C.X - B.X) * (y - C.Y)) / d;
            float v = ((C.Y - A.Y) * (x - C.X) + (A.X - C.X) * (y - C.Y)) / d;
            float w = 1 - u - v;
            const float eps = -1e-4f;
            if (u < eps || v < eps || w < eps) return false;
            z = u * A.Z + v * B.Z + w * C.Z;
            return true;
        }
    }

    const float CellSize = 2.5f;
    readonly List<Triangle> _triangles = new();
    readonly Dictionary<long, List<int>> _cells = new();
    readonly HashSet<int> _disabledOwners = new();
    /// <summary>Owners whose footprints are placeables: flat, blocking at ground level, never switched off.</summary>
    readonly HashSet<int> _placeableOwners = new();
    readonly HashSet<int> _closedDoorOwners = new();
    public void SetClosedDoorOwner(int owner) => _closedDoorOwners.Add(owner);
    /// <summary>The ground each owner covers (XY bounds), so switching one only invalidates that much.</summary>
    readonly Dictionary<int, (float MinX, float MinY, float MaxX, float MaxY)> _ownerBounds = new();
    /// <summary>Recent changes: the version they made and the XY box they touched (null: anywhere).</summary>
    readonly List<(int Version, (float MinX, float MinY, float MaxX, float MaxY)? Box)> _changes = new();

    public int TriangleCount => _triangles.Count;
    /// <summary>Changes whenever walkability changes (doors opening, footprints added), so caches can rebuild.</summary>
    public int Version { get; private set; }
    /// <summary>Changes only when triangle geometry changes, not when a door footprint is enabled.</summary>
    public int GeometryVersion { get; private set; }
    public IReadOnlyList<Triangle> Triangles => _triangles;

    /// <summary>Adds a triangle. <paramref name="owner"/> lets door footprints be switched off when opened (0 = permanent).</summary>
    public void Add(Vector3 a, Vector3 b, Vector3 c, int surface, bool walkable, int owner = 0)
    {
        int index = _triangles.Count;
        GeometryVersion++;
        Changed(null);
        _triangles.Add(new Triangle(a, b, c, surface, walkable, owner));
        float minX = MathF.Min(a.X, MathF.Min(b.X, c.X)), maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
        float minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        if (owner != 0)
            _ownerBounds[owner] = _ownerBounds.TryGetValue(owner, out var ob)
                ? (MathF.Min(ob.MinX, minX), MathF.Min(ob.MinY, minY), MathF.Max(ob.MaxX, maxX), MathF.Max(ob.MaxY, maxY))
                : (minX, minY, maxX, maxY);
        for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
            for (int cy = Cell(minY); cy <= Cell(maxY); cy++)
            {
                long key = Key(cx, cy);
                if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<int>();
                list.Add(index);
            }
    }

    /// <summary>Removes every triangle (SetTile rebuilds the walkmesh). Disabled owners stay disabled.</summary>
    public void Clear()
    {
        GeometryVersion++;
        Changed(null);
        _triangles.Clear();
        _cells.Clear();
        _ownerBounds.Clear();
    }

    /// <summary>Marks an owner's triangles as a placeable's footprint (see <see cref="GroundHeight"/>).</summary>
    public void SetPlaceableOwner(int owner) => _placeableOwners.Add(owner);

    public void SetOwnerEnabled(int owner, bool enabled)
    {
        bool changed = enabled ? _disabledOwners.Remove(owner) : _disabledOwners.Add(owner);
        if (changed) Changed(_ownerBounds.TryGetValue(owner, out var box) ? box : null);
    }

    void Changed((float, float, float, float)? box)
    {
        Version++;
        // Additions come in thousands while an area loads: one "anywhere" entry covers them.
        if (box == null && _changes.Count > 0 && _changes[^1].Box == null) _changes[^1] = (Version, null);
        else _changes.Add((Version, box));
        if (_changes.Count > 256) _changes.RemoveRange(0, 128);
    }

    /// <summary>
    /// The XY boxes whose walkability changed after <paramref name="version"/>; false when that isn't known
    /// (triangles added or removed, or too long ago), so a cache must start over.
    /// </summary>
    public bool ChangedSince(int version, List<(float MinX, float MinY, float MaxX, float MaxY)> boxes)
    {
        boxes.Clear();
        if (version == Version) return true;
        if (_changes.Count == 0 || _changes[0].Version > version + 1) return false;
        foreach (var (v, box) in _changes)
        {
            if (v <= version) continue;
            if (box == null) return false;
            boxes.Add(box.Value);
        }
        return true;
    }

    static int Cell(float v) => (int)MathF.Floor(v / CellSize);
    static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>
    /// Ground height under (x, y) nearest to <paramref name="nearZ"/> within
    /// <paramref name="maxStep"/>, or null if there is no walkable ground in
    /// reach or a blocking footprint covers it at that height.
    /// </summary>
    /// <param name="tilesOnly">Use only authored tile geometry when deriving room openings; ignore all object footprints.</param>
    /// <param name="noPlaceables">Leave placeable footprints out (where a creature may stand partly over one).</param>
    public float? GroundHeight(float x, float y, float nearZ, float maxStep = 1.0f, bool noPlaceables = false, bool ignoreClosedDoors = false, bool tilesOnly = false)
    {
        if (!_cells.TryGetValue(Key(Cell(x), Cell(y)), out var list)) return null;
        float? best = null;
        float bestDist = float.MaxValue;
        foreach (int i in list)
        {
            var t = _triangles[i];
            if (tilesOnly && t.Owner != 0 || !t.Walkable || (t.Owner != 0 && _disabledOwners.Contains(t.Owner))) continue;
            if (!t.TryHeightAt(x, y, out float z)) continue;
            float dist = MathF.Abs(z - nearZ);
            if (dist <= maxStep && dist < bestDist) { best = z; bestDist = dist; }
        }
        if (best == null) return null;
        foreach (int i in list)
        {
            var t = _triangles[i];
            if (tilesOnly && t.Owner != 0 || t.Walkable || (t.Owner != 0 && _disabledOwners.Contains(t.Owner))) continue;
            bool placeable = t.Owner != 0 && _placeableOwners.Contains(t.Owner);
            if (placeable && noPlaceables || ignoreClosedDoors && _closedDoorOwners.Contains(t.Owner)) continue;
            // Flat footprints (placeables) block at ground level; door footprints are boxes,
            // so any face of theirs up to head height blocks.
            if (t.TryHeightAt(x, y, out float z) && z > best.Value - maxStep && z < best.Value + (t.Owner != 0 && !placeable ? 3.5f : maxStep)) return null;
        }
        return best;
    }

    /// <summary>
    /// Authored tile floor under (x, y) nearest to <paramref name="nearZ"/> within <paramref name="maxStep"/>, for
    /// room openings. Unlike <see cref="GroundHeight"/> it also counts floor nobody may walk on (a storeroom's
    /// crate-filled half is all "Nonwalk"), as decided by <paramref name="floor"/> per surface; any other tile face
    /// (a wall's obscuring top) covering the point at that height hides it. Object footprints are ignored.
    /// </summary>
    public float? TileFloorHeight(float x, float y, float nearZ, float maxStep, Func<int, bool> floor)
    {
        if (!_cells.TryGetValue(Key(Cell(x), Cell(y)), out var list)) return null;
        float? best = null;
        float bestDist = float.MaxValue;
        foreach (int i in list)
        {
            var t = _triangles[i];
            if (t.Owner != 0 || !(t.Walkable || floor(t.Surface)) || !t.TryHeightAt(x, y, out float z)) continue;
            float dist = MathF.Abs(z - nearZ);
            if (dist <= maxStep && dist < bestDist) { best = z; bestDist = dist; }
        }
        if (best == null) return null;
        foreach (int i in list)
        {
            var t = _triangles[i];
            if (t.Owner != 0 || t.Walkable || floor(t.Surface)) continue;
            if (t.TryHeightAt(x, y, out float z) && z > best.Value - maxStep && z < best.Value + maxStep) return null;
        }
        return best;
    }

    /// <summary>Enabled closed-door slab at a point with otherwise walkable ground; open leaves are excluded.</summary>
    public int ClosedDoorAt(Vector3 point, float maxStep)
    {
        if (_closedDoorOwners.Count == 0 || !_cells.TryGetValue(Key(Cell(point.X), Cell(point.Y)), out var list)) return 0;
        foreach (int index in list)
        {
            var t = _triangles[index];
            if (t.Walkable || !_closedDoorOwners.Contains(t.Owner) || _disabledOwners.Contains(t.Owner)
                || !t.TryHeightAt(point.X, point.Y, out float z)) continue;
            if (GroundHeight(point.X, point.Y, point.Z, maxStep, ignoreClosedDoors: true) is not { } ground) return 0;
            if (z > ground - maxStep && z < ground + 3.5f) return t.Owner;
        }
        return 0;
    }

    readonly List<float> _doorTraceTs = new();

    /// <summary>First closed slab crossed by a step, including steps longer than the authored slab thickness.</summary>
    public int ClosedDoorBetween(Vector3 from, Vector3 to, float maxStep)
    {
        if (_closedDoorOwners.Count == 0) return 0;
        var ts = _doorTraceTs;
        ts.Clear(); ts.Add(0); ts.Add(1);
        var a = new Vector2(from.X, from.Y);
        var d = new Vector2(to.X - from.X, to.Y - from.Y);
        for (int x = Cell(MathF.Min(from.X, to.X)); x <= Cell(MathF.Max(from.X, to.X)); x++)
            for (int y = Cell(MathF.Min(from.Y, to.Y)); y <= Cell(MathF.Max(from.Y, to.Y)); y++)
                if (_cells.TryGetValue(Key(x, y), out var list))
                    foreach (int index in list)
                    {
                        var t = _triangles[index];
                        if (t.Walkable || !_closedDoorOwners.Contains(t.Owner) || _disabledOwners.Contains(t.Owner)) continue;
                        Cross(a, d, t.A, t.B, ts); Cross(a, d, t.B, t.C, ts); Cross(a, d, t.C, t.A, ts);
                    }
        ts.Sort();
        for (int i = 1; i < ts.Count; i++)
        {
            if (ts[i] - ts[i - 1] < 1e-6f) continue;
            int owner = ClosedDoorAt(Vector3.Lerp(from, to, (ts[i] + ts[i - 1]) / 2), maxStep);
            if (owner != 0) return owner;
        }
        return ClosedDoorAt(to, maxStep);
    }

    /// <summary>
    /// The walkable point nearest to <paramref name="p"/> in XY (within <paramref name="radius"/>), with its ground
    /// height; where several floors overlap, the one nearest in height. Null when nothing walkable is in reach.
    /// The original moves a player who starts on unwalkable ground there (parity/visual: a module entry point
    /// inside a building's footprint).
    /// </summary>
    public Vector3? NearestWalkable(Vector3 p, float radius = 10f)
    {
        Vector3? best = null;
        float bestD = float.MaxValue, bestDz = float.MaxValue;
        var seen = new HashSet<int>();
        for (int cx = Cell(p.X - radius); cx <= Cell(p.X + radius); cx++)
            for (int cy = Cell(p.Y - radius); cy <= Cell(p.Y + radius); cy++)
            {
                if (!_cells.TryGetValue(Key(cx, cy), out var list)) continue;
                foreach (int i in list)
                {
                    if (!seen.Add(i)) continue;
                    var t = _triangles[i];
                    if (!t.Walkable || (t.Owner != 0 && _disabledOwners.Contains(t.Owner))) continue;
                    var q = ClosestInXY(t, new Vector2(p.X, p.Y));
                    if (!t.TryHeightAt(q.X, q.Y, out float z)) z = (t.A.Z + t.B.Z + t.C.Z) / 3;
                    float d = Vector2.Distance(q, new Vector2(p.X, p.Y)), dz = MathF.Abs(z - p.Z);
                    if (d > radius) continue;
                    if (d < bestD - 1e-3f || (d < bestD + 1e-3f && dz < bestDz)) { best = new Vector3(q, z); bestD = d; bestDz = dz; }
                }
            }
        return best;
    }

    /// <summary>The point of a triangle (seen from above) nearest to <paramref name="p"/>, nudged a little inside.</summary>
    static Vector2 ClosestInXY(Triangle t, Vector2 p)
    {
        Vector2 a = new(t.A.X, t.A.Y), b = new(t.B.X, t.B.Y), c = new(t.C.X, t.C.Y);
        if (t.TryHeightAt(p.X, p.Y, out _)) return p;
        static Vector2 OnSegment(Vector2 p, Vector2 u, Vector2 v)
        {
            var d = v - u;
            float len = d.LengthSquared();
            float k = len < 1e-9f ? 0 : Math.Clamp(Vector2.Dot(p - u, d) / len, 0f, 1f);
            return u + d * k;
        }
        var best = OnSegment(p, a, b);
        foreach (var q in new[] { OnSegment(p, b, c), OnSegment(p, c, a) })
            if (Vector2.DistanceSquared(q, p) < Vector2.DistanceSquared(best, p)) best = q;
        // A few centimetres towards the middle, so the point is inside rather than on the edge.
        var centre = (a + b + c) / 3;
        var toward = centre - best;
        return toward.LengthSquared() > 1e-6f ? best + Vector2.Normalize(toward) * MathF.Min(0.05f, toward.Length()) : best;
    }

    /// <summary>Highest walkable ground at (x, y), for placing objects whose height is unknown.</summary>
    public float? HighestGround(float x, float y)
    {
        if (!_cells.TryGetValue(Key(Cell(x), Cell(y)), out var list)) return null;
        float? best = null;
        foreach (int i in list)
        {
            var t = _triangles[i];
            if (t.Walkable && t.TryHeightAt(x, y, out float z) && (best == null || z > best)) best = z;
        }
        return best;
    }

    /// <summary>Surface id of the walkable triangle under a point (footstep sounds, GetSurfaceMaterial).</summary>
    public int SurfaceAt(Vector3 p)
    {
        if (!_cells.TryGetValue(Key(Cell(p.X), Cell(p.Y)), out var list)) return 0;
        int surface = 0;
        float bestDist = float.MaxValue;
        foreach (int i in list)
        {
            var t = _triangles[i];
            if (t.Walkable && t.TryHeightAt(p.X, p.Y, out float z) && MathF.Abs(z - p.Z) < bestDist)
            {
                bestDist = MathF.Abs(z - p.Z);
                surface = t.Surface;
            }
        }
        return surface;
    }

    /// <summary>
    /// Walks a straight segment; returns how far along (0..1) it can go. Exact, so that it agrees with stepping along
    /// the segment in any step size (Movement checks each step's end with <see cref="GroundHeight"/>): what is under
    /// the segment only changes where it crosses a triangle edge, so it is checked once between each two crossings,
    /// besides every quarter metre to follow the ground's height.
    /// </summary>
    public float Trace(Vector3 from, Vector3 to, float maxStep = 0.6f, bool ignoreClosedDoors = false)
    {
        var delta = to - from;
        float length = new Vector2(delta.X, delta.Y).Length();
        if (length < 1e-5f) return 1f;
        var ts = _traceTs;
        ts.Clear();
        int steps = Math.Max(1, (int)MathF.Ceiling(length / 0.25f));
        for (int s = 1; s <= steps; s++) ts.Add((float)s / steps);
        // Edge crossings of every triangle near the segment.
        if (_traceStamp.Length < _triangles.Count) _traceStamp = new int[_triangles.Count];
        if (++_traceSearch == int.MaxValue) { Array.Clear(_traceStamp); _traceSearch = 1; }
        int cx0 = Cell(MathF.Min(from.X, to.X)), cx1 = Cell(MathF.Max(from.X, to.X));
        int cy0 = Cell(MathF.Min(from.Y, to.Y)), cy1 = Cell(MathF.Max(from.Y, to.Y));
        var a = new Vector2(from.X, from.Y);
        var d = new Vector2(delta.X, delta.Y);
        for (int cx = cx0; cx <= cx1; cx++)
            for (int cy = cy0; cy <= cy1; cy++)
            {
                if (!_cells.TryGetValue(Key(cx, cy), out var list)) continue;
                foreach (int i in list)
                {
                    if (_traceStamp[i] == _traceSearch) continue;
                    _traceStamp[i] = _traceSearch;
                    var t = _triangles[i];
                    Cross(a, d, t.A, t.B, ts);
                    Cross(a, d, t.B, t.C, ts);
                    Cross(a, d, t.C, t.A, ts);
                }
            }
        ts.Sort();
        float z = from.Z, prev = 0;
        foreach (float f in ts)
        {
            if (f - prev < 1e-6f) continue;
            // Between two crossings, and at the next one.
            float mid = (prev + f) / 2;
            var hm = GroundHeight(from.X + delta.X * mid, from.Y + delta.Y * mid, z, maxStep, ignoreClosedDoors: ignoreClosedDoors);
            if (hm == null) return prev;
            z = hm.Value;
            var h = GroundHeight(from.X + delta.X * f, from.Y + delta.Y * f, z, maxStep, ignoreClosedDoors: ignoreClosedDoors);
            if (h == null) return mid;
            z = h.Value;
            prev = f;
        }
        return 1f;
    }

    readonly List<float> _traceTs = new();
    int[] _traceStamp = [];
    int _traceSearch;

    /// <summary>Where (0..1) the segment a + d·t crosses the edge p-q, added to the list.</summary>
    static void Cross(Vector2 a, Vector2 d, Vector3 p3, Vector3 q3, List<float> ts)
    {
        var p = new Vector2(p3.X, p3.Y);
        var e = new Vector2(q3.X, q3.Y) - p;
        float den = d.X * e.Y - d.Y * e.X;
        if (MathF.Abs(den) < 1e-12f) return;
        var ap = p - a;
        float t = (ap.X * e.Y - ap.Y * e.X) / den;
        float u = (ap.X * d.Y - ap.Y * d.X) / den;
        if (t > 0 && t < 1 && u >= -1e-4f && u <= 1 + 1e-4f) ts.Add(t);
    }
}
