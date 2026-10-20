using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Areas;

/// <summary>Interior tile connectivity through walkable openings. Closed doors seal openings;
/// an isolated slab on open ground does not create a room. Derived state is rebuilt after loading.</summary>
public sealed class RoomVisibility
{
    static readonly ConditionalWeakTable<Area, RoomVisibility> Cache = new();
    readonly List<(int A, int B, Vector3 From, Vector3 To)> _openings = new();
    int _geometry = -1, _version = -1;
    // The area's door list as last seen (Area.Doors hands out a new array when doors come or go) and each door's state.
    Door[] _doorList = [];
    DoorState[] _doorStates = [];
    readonly record struct DoorState(bool Open, Vector3 Position, float Facing, int Appearance, int GenericType)
    {
        public static DoorState Of(Door d) => new(d.IsOpen, d.Position, d.Facing, d.Appearance, d.GenericType);
    }
    List<int>[] _links = [];
    readonly Dictionary<int, bool[]> _visible = new();
    public const int PortalDepth = 8;
    public static RoomVisibility? For(World world, Area area) => area.IsInterior ? Rooms(world, area) : null;

    /// <summary>Rooms for hearing: interior and underground areas alike (PerceptionSystem's sound path).</summary>
    public static RoomVisibility? ForSound(World world, Area area) => area.IsInterior || area.IsUnderground ? Rooms(world, area) : null;

    static RoomVisibility? Rooms(World world, Area area)
    {
        if (area.Width <= 0 || area.Height <= 0 || area.Tiles.Length != area.Width * area.Height) return null;
        var rooms = Cache.GetValue(area, _ => new());
        rooms.Update(world, area);
        return rooms;
    }
    public bool AllowsPerception(Area area, Vector3 a, Vector3 b)
    {
        int x = (int)MathF.Floor(b.X / 10), y = (int)MathF.Floor(b.Y / 10);
        return x >= 0 && y >= 0 && x < area.Width && y < area.Height && VisibleTiles(area, a)[y * area.Width + x];
    }
    public IReadOnlyList<bool> VisibleTiles(Area area, Vector3 point)
    {
        int x = (int)MathF.Floor(point.X / 10), y = (int)MathF.Floor(point.Y / 10);
        int start = x >= 0 && y >= 0 && x < area.Width && y < area.Height ? y * area.Width + x : -1;
        if (_visible.TryGetValue(start, out var known)) return known;
        var mask = new bool[_links.Length];
        if (start >= 0)
        {
            var queue = new Queue<(int Tile, int Depth)>(); queue.Enqueue((start, 0)); mask[start] = true;
            while (queue.TryDequeue(out var step))
            {
                if (step.Depth == PortalDepth) continue;
                foreach (int next in _links[step.Tile])
                    if (!mask[next]) { mask[next] = true; queue.Enqueue((next, step.Depth + 1)); }
            }
        }
        _visible[start] = mask;
        return mask;
    }
    public IEnumerable<int> TilesIn(Area area, Vector3 point)
    {
        var mask = VisibleTiles(area, point);
        for (int i = 0; i < mask.Count; i++) if (mask[i]) yield return i;
    }
    void Update(World world, Area area)
    {
        bool doorsChanged = false;
        var doors = area.Doors;
        if (!ReferenceEquals(doors, _doorList))
        {
            // A door was added or removed. Open state, placement and appearance are plain fields that restored or
            // script state may write without a notification, so those are compared on every update below.
            doorsChanged = doors.Length != _doorList.Length || !doors.SequenceEqual(_doorList);
            if (doorsChanged)
            {
                _doorStates = new DoorState[doors.Length];
                for (int i = 0; i < doors.Length; i++) _doorStates[i] = DoorState.Of(doors[i]);
            }
            _doorList = doors;
        }
        for (int i = 0; i < _doorList.Length; i++)
        {
            var state = DoorState.Of(_doorList[i]);
            if (_doorStates[i] == state) continue;
            _doorStates[i] = state; doorsChanged = true;
        }
        if (_geometry != area.Walkmesh.GeometryVersion || _links.Length != area.Tiles.Length)
        {
            Build(world, area);
            _geometry = area.Walkmesh.GeometryVersion;
            _version = -1;
        }
        if (_version == area.Walkmesh.Version && !doorsChanged) return;
        _version = area.Walkmesh.Version;
        _links = Enumerable.Range(0, area.Tiles.Length).Select(_ => new List<int>()).ToArray();
        _visible.Clear();
        var linked = new HashSet<(int, int)>();
        foreach (var (a, b, from, to) in _openings)
        {
            if (!linked.Contains((a, b)) && LineOfSight.Clear(world, area, from, to, 0, 0))
            {
                linked.Add((a, b)); _links[a].Add(b); _links[b].Add(a);
            }
        }
    }
    /// <summary>
    /// Surfaces that count as floor for openings: every surfacemat row with WalkCheck = 1. That is all walkable
    /// ground plus floor that is not walkable (Nonwalk, Transparent, pits, deep water, lava), and leaves out
    /// Obscuring and undefined faces, the tops of walls. The original reveals the crate-filled half of the
    /// Academy storeroom (tic01_c07_01, its floor all Nonwalk) with the walkable half (scene storeroom).
    /// </summary>
    static bool[] FloorSurfaces(World world)
    {
        var table = world.Resources.TwoDA("surfacemat");
        var floor = new bool[Math.Max(table.RowCount, 1)];
        for (int i = 0; i < table.RowCount; i++) floor[i] = table.GetInt(i, "WalkCheck") != 0;
        return floor;
    }

    void Build(World world, Area area)
    {
        _openings.Clear();
        var floorSurfaces = FloorSurfaces(world);
        bool Floor(int surface) => surface >= 0 && surface < floorSurfaces.Length && floorSurfaces[surface];
        // Sample actual floor on both sides of each tile seam; terrain labels alone cannot
        // distinguish a solid wall, a doorway, a fence or a custom narrow opening.
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                for (int axis = 0; axis < 2; axis++)
                {
                    int nx = x + (axis == 0 ? 1 : 0), ny = y + (axis == 1 ? 1 : 0);
                    if (nx >= area.Width || ny >= area.Height) continue;
                    int a = y * area.Width + x, b = ny * area.Width + nx;
                    for (float along = 0.125f; along < 10; along += 0.25f)
                    {
                        var seam = axis == 0 ? new Vector3((x + 1) * 10, y * 10 + along, 0) : new Vector3(x * 10 + along, (y + 1) * 10, 0);
                        var offset = axis == 0 ? new Vector3(.4f, 0, 0) : new Vector3(0, .4f, 0);
                        var from = seam - offset; var to = seam + offset;
                        float? za = area.Walkmesh.TileFloorHeight(from.X, from.Y, area.Tiles[a].Height * area.TileHeightStep, 1, Floor);
                        float? zb = area.Walkmesh.TileFloorHeight(to.X, to.Y, area.Tiles[b].Height * area.TileHeightStep, 1, Floor);
                        if (za == null || zb == null || Math.Abs(za.Value - zb.Value) > .5f) continue;
                        from.Z = za.Value; to.Z = zb.Value;
                        _openings.Add((a, b, from, to));
                    }
                }
    }
}
