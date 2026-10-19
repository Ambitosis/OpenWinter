using System.Numerics;

namespace OpenWinter.Content;

/// <summary>
/// Rules for the "tilefade" geometry of area tiles. Tile models mark the parts above wall
/// height so the renderer can cut walls down where they would hide the player:
/// <list type="bullet">
/// <item><see cref="Fade"/> (1): upper walls, arches and their black tops; hidden on faded tiles.</item>
/// <item><see cref="Base"/> (2): black caps over the cut wall tops; drawn only on faded tiles.</item>
/// <item><see cref="Neighbour"/> (4): black plugs on a tile edge that close this tile's upper wall
/// where it meets a faded neighbour; drawn only while this tile is whole and that neighbour is faded.</item>
/// </list>
/// Coordinates are area space (Z up, tiles 10 units square).
/// </summary>
public static class TileFade
{
    public const int Fade = 1, Base = 2, Neighbour = 4;
    public const float TileSize = 10f;

    /// <summary>The role of a node's tilefade value (unknown values count as plain fading geometry).</summary>
    public static int Role(int mode) => mode == 0 ? 0 : (mode & Fade) != 0 ? Fade : (mode & Base) != 0 ? Base : (mode & Neighbour) != 0 ? Neighbour : Fade;

    /// <summary>Edges, counter-clockwise from east: 0 = +X, 1 = +Y, 2 = -X, 3 = -Y; -1 = not on an edge.</summary>
    public static int EdgeOf(Vector3 min, Vector3 max, float tolerance = 0.35f)
    {
        float half = TileSize / 2;
        if (min.X > half - tolerance) return 0;
        if (min.Y > half - tolerance) return 1;
        if (max.X < -half + tolerance) return 2;
        if (max.Y < -half + tolerance) return 3;
        return -1;
    }

    /// <summary>A model-space edge after the tile is turned <paramref name="orientation"/> quarter turns counter-clockwise.</summary>
    public static int RotateEdge(int edge, int orientation) => edge < 0 ? -1 : (edge + orientation) & 3;

    public static (int Dx, int Dy) Offset(int edge) => edge switch
    {
        0 => (1, 0),
        1 => (0, 1),
        2 => (-1, 0),
        3 => (0, -1),
        _ => (0, 0),
    };

    /// <summary>Where a tile's fading geometry stands, in area space: its ground footprint and top.</summary>
    public readonly record struct Footprint(Vector2 Min, Vector2 Max, float Top, float Bottom = float.NegativeInfinity);

    /// <summary>
    /// Tiles whose fading geometry would come between the camera and its focus: those whose
    /// footprint (grown by <paramref name="margin"/>, for the width of a creature) the line of
    /// sight crosses while it is still below the geometry's top.
    /// </summary>
    public static HashSet<int> TilesToFade(int width, int height, Vector3 focus, Vector3 camera, IReadOnlyList<Footprint?> footprints, float margin = 1.5f)
    {
        var result = new HashSet<int>();
        TilesToFade(width, height, focus, camera, footprints, result, margin);
        return result;
    }

    /// <summary>As above, filling <paramref name="result"/> (cleared first) so a caller can reuse it every frame.</summary>
    public static void TilesToFade(int width, int height, Vector3 focus, Vector3 camera, IReadOnlyList<Footprint?> footprints, HashSet<int> result, float margin = 1.5f)
    {
        result.Clear();
        if (width <= 0 || height <= 0) return;
        var from = new Vector2(focus.X, focus.Y);
        var to = new Vector2(camera.X, camera.Y);
        var lo = Vector2.Min(from, to) - new Vector2(margin);
        var hi = Vector2.Max(from, to) + new Vector2(margin);
        int x0 = Math.Max(0, (int)MathF.Floor(lo.X / TileSize)), x1 = Math.Min(width - 1, (int)MathF.Floor(hi.X / TileSize));
        int y0 = Math.Max(0, (int)MathF.Floor(lo.Y / TileSize)), y1 = Math.Min(height - 1, (int)MathF.Floor(hi.Y / TileSize));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int tile = y * width + x;
                if (tile >= footprints.Count || footprints[tile] is not { } f) continue;
                if (Blocks(f, focus, camera, margin)) result.Add(tile);
            }
    }

    /// <summary>
    /// Does fading geometry standing on <paramref name="f"/> come between the camera and its focus: does the
    /// line of sight cross the footprint (grown by <paramref name="margin"/>) while still below its top?
    /// </summary>
    public static bool Blocks(Footprint f, Vector3 focus, Vector3 camera, float margin)
    {
        var from = new Vector2(focus.X, focus.Y);
        var to = new Vector2(camera.X, camera.Y);
        float rise = camera.Z - focus.Z;
        // Only the stretch of the line of sight at the geometry's height can be blocked by it: past the point
        // where it climbs over the top, or before it reaches the bottom (a tree's crown), it passes clear.
        float t0 = 0, t1 = 1;
        if (rise > 1e-3f)
        {
            t1 = Math.Clamp((f.Top - focus.Z) / rise, 0f, 1f);
            if (!float.IsNegativeInfinity(f.Bottom)) t0 = Math.Clamp((f.Bottom - margin - focus.Z) / rise, 0f, 1f);
        }
        if (t1 <= 0 || t0 >= t1) return false;
        return SegmentHitsBox(from + (to - from) * t0, from + (to - from) * t1, f.Min - new Vector2(margin), f.Max + new Vector2(margin));
    }

    /// <summary>Slab test: does the segment a-b touch the axis-aligned box?</summary>
    public static bool SegmentHitsBox(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
    {
        float t0 = 0, t1 = 1;
        var d = b - a;
        for (int axis = 0; axis < 2; axis++)
        {
            float start = axis == 0 ? a.X : a.Y, dir = axis == 0 ? d.X : d.Y;
            float lo = axis == 0 ? min.X : min.Y, hi = axis == 0 ? max.X : max.Y;
            if (MathF.Abs(dir) < 1e-6f)
            {
                if (start < lo || start > hi) return false;
                continue;
            }
            float ta = (lo - start) / dir, tb = (hi - start) / dir;
            if (ta > tb) (ta, tb) = (tb, ta);
            t0 = Math.Max(t0, ta);
            t1 = Math.Min(t1, tb);
            if (t0 > t1) return false;
        }
        return true;
    }
}
