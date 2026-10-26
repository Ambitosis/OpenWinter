using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;

namespace OpenWinter.Game.Areas;

/// <summary>
/// Turns an area by quarter turns clockwise with everything in it except players (NWNX_Area_RotateArea), as
/// measured on nwserver (parity suite nwnx_area_objects):
/// <list type="bullet">
/// <item>A point (x, y) of a W x H metre area goes to (y, W - x) for one turn, (W - x, H - y) for two and (H - y, x)
/// for three; width and height swap on odd turns. A tile keeps its height and its orientation drops by the turns.</item>
/// <item>Facings follow the original's arithmetic: one turn leaves acos(|sin f|) and three 180 + acos(|sin f|) (both
/// folded into a quarter, so most objects don't face where they turned to); two turns add 180. A facing that comes
/// out at exactly east reads back as 360.</item>
/// </list>
/// Clients see the new tiles and walkmesh when they next load the area, as in the original.
/// </summary>
public static class AreaRotation
{
    public static void Rotate(World world, Area area, int quarterTurns)
    {
        int n = ((quarterTurns % 4) + 4) % 4;
        if (n == 0) return;
        int w = area.Width, h = area.Height;
        float wm = w * Area.TileSize, hm = h * Area.TileSize;
        int nw = n % 2 == 1 ? h : w, nh = n % 2 == 1 ? w : h;

        var tiles = new TileInfo[area.Tiles.Length];
        var map = new int[area.Tiles.Length];
        for (int i = 0; i < area.Tiles.Length && i < w * h; i++)
        {
            int x = i % w, y = i / w;
            var (tx, ty) = n switch { 1 => (y, w - 1 - x), 2 => (w - 1 - x, h - 1 - y), _ => (h - 1 - y, x) };
            int j = ty * nw + tx;
            var t = area.Tiles[i];
            t.Orientation = (byte)((t.Orientation + 4 - n) % 4);
            tiles[j] = t;
            map[i] = j;
        }
        area.Tiles = tiles;
        area.Width = nw;
        area.Height = nh;
        area.Are.SetInt("Width", nw);
        area.Are.SetInt("Height", nh);
        if (area.Environment is { } env)
            foreach (var (pc, explored) in env.Explored.ToList())
            {
                var turned = new bool[explored.Length];
                for (int i = 0; i < explored.Length && i < map.Length; i++) turned[map[i]] = explored[i];
                env.Explored[pc] = turned;
            }

        Vector3 Point(Vector3 p) => n switch
        {
            1 => new Vector3(p.Y, wm - p.X, p.Z),
            2 => new Vector3(wm - p.X, hm - p.Y, p.Z),
            _ => new Vector3(hm - p.Y, p.X, p.Z),
        };

        foreach (var o in area.Objects.ToList())
        {
            if (o is Creature { IsPC: true }) continue;
            o.Position = Point(o.Position);
            o.FacingDegrees = TurnedFacing(o.FacingDegrees, n);
            if (MathF.Abs(o.FacingDegrees % 360f) < 1e-3f) o.Facing = -1e-6f - o.BearingOffset;
            switch (o)
            {
                case Trigger t:
                    for (int i = 0; i < t.Geometry.Count; i++) t.Geometry[i] = Point(t.Geometry[i]);
                    t.UpdateBounds();
                    break;
                case Encounter e:
                    for (int i = 0; i < e.Geometry.Count; i++) e.Geometry[i] = Point(e.Geometry[i]);
                    for (int i = 0; i < e.State.SpawnPoints.Count; i++)
                    {
                        var (p, f) = e.State.SpawnPoints[i];
                        e.State.SpawnPoints[i] = (Point(p), TurnedFacing(f * 180f / MathF.PI, n) * MathF.PI / 180f);
                    }
                    break;
                case Creature c:
                    area.CreatureGrid.Update(c);
                    break;
            }
            world.MarkChanged(o);
        }
        area.TriggerCache = null;
        WalkmeshBuilder.Rebuild(world, area);
        area.ResetPathfinders();
    }

    /// <summary>The facing (degrees) an object has after <paramref name="n"/> quarter turns.</summary>
    public static float TurnedFacing(float degrees, int n)
    {
        float folded = MathF.Acos(Math.Clamp(MathF.Abs(MathF.Sin(degrees * MathF.PI / 180f)), 0f, 1f)) * 180f / MathF.PI;
        return n switch
        {
            1 => folded,
            2 => (degrees + 180f) % 360f,
            _ => 180f + folded,
        };
    }
}
