using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Areas;

/// <summary>
/// The creatures of one area bucketed into 2 m cells, so personal space checks (placement, pushing, path
/// obstacles) only look at creatures nearby instead of the whole area. Kept current by the places that move
/// creatures (<see cref="Update"/>) and resynchronised once per world step for anything else that sets a
/// position directly.
/// </summary>
public sealed class CreatureGrid
{
    public const float CellSize = 2f;

    readonly Dictionary<long, List<Creature>> _cells = new();
    readonly Dictionary<Creature, long> _cellOf = new();
    readonly Stack<List<Creature>> _spare = new();

    public int Count => _cellOf.Count;

    static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    static int CellOf(float v) => (int)MathF.Floor(v / CellSize);
    static long KeyOf(Vector3 p) => Key(CellOf(p.X), CellOf(p.Y));

    /// <summary>Adds a creature or moves it to the cell of its current position.</summary>
    public void Update(Creature c)
    {
        long key = KeyOf(c.Position);
        if (_cellOf.TryGetValue(c, out long old))
        {
            if (old == key) return;
            RemoveFromCell(c, old);
        }
        _cellOf[c] = key;
        if (!_cells.TryGetValue(key, out var list))
        {
            list = _spare.Count > 0 ? _spare.Pop() : new List<Creature>(4);
            _cells[key] = list;
        }
        list.Add(c);
    }

    public void Remove(Creature c)
    {
        if (_cellOf.Remove(c, out long key)) RemoveFromCell(c, key);
    }

    void RemoveFromCell(Creature c, long key)
    {
        if (!_cells.TryGetValue(key, out var list)) return;
        list.Remove(c);
        if (list.Count == 0)
        {
            _cells.Remove(key);
            _spare.Push(list);
        }
    }

    /// <summary>Re-files every creature of the area whose position changed outside the tracked paths.</summary>
    public void Sync(Area area)
    {
        foreach (var o in area.Objects)
            if (o is Creature c && !c.IsDestroyed) Update(c);
        if (_cellOf.Count == 0) return;
        List<Creature>? gone = null;
        foreach (var c in _cellOf.Keys)
            if (c.Area != area || c.IsDestroyed) (gone ??= new()).Add(c);
        if (gone != null) foreach (var c in gone) Remove(c);
    }

    /// <summary>Creatures whose position may lie within <paramref name="radius"/> of a point (callers check the distance).</summary>
    public void Near(Vector3 point, float radius, List<Creature> into)
    {
        into.Clear();
        int x0 = CellOf(point.X - radius), x1 = CellOf(point.X + radius);
        int y0 = CellOf(point.Y - radius), y1 = CellOf(point.Y + radius);
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (_cells.TryGetValue(Key(x, y), out var list)) into.AddRange(list);
    }
}

public sealed partial class Area
{
    /// <summary>The area's creatures by position (personal space checks).</summary>
    public readonly CreatureGrid CreatureGrid = new();
}
