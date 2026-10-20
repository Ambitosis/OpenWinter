using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Interior discovery follows the current eight-step tile visibility graph, retaining previously
/// discovered rooms after their doors close. Exterior maps retain the existing radius rule.
/// GetTileExplored, saves and the client world/map presentation share this per-player state.
/// </summary>
public sealed class MapExploration : IWorldSystem
{
    /// <summary>Distance (metres) around a player that counts as seen.</summary>
    public const float RevealRadius = 15f;
    const float Interval = 0.5f;

    readonly World _world;
    float _timer;

    public MapExploration(World world) { _world = world; }

    public void Step(float dt)
    {
        _timer -= dt;
        if (_timer > 0) return;
        _timer = Interval;
        foreach (var pc in _world.ConnectedPCs)
            if (pc.ExploresMinimap && pc.Area is { } area) Reveal(area, pc);
    }

    public void Sync(Creature pc)
    {
        if (pc.Area is not { } area) return;
        if (pc.ExploresMinimap) Reveal(area, pc);
        Send(_world, pc);
    }

    /// <summary>Explores what the player can see now (native refills it straight after ExploreAreaForPlayer FALSE).</summary>
    public void RevealNow(Creature pc)
    {
        if (pc.ExploresMinimap && pc.Area is { } area) Reveal(area, pc);
    }

    void Reveal(Area area, Creature pc)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        var env = AreaSettings.Of(_world, area);
        if (!env.Explored.TryGetValue(pc.Id, out var tiles)) env.Explored[pc.Id] = tiles = new bool[area.Width * area.Height];
        if (RoomVisibility.For(_world, area) is { } rooms)
        {
            foreach (int i in rooms.TilesIn(area, pc.Position))
            {
                if (tiles[i]) continue;
                tiles[i] = true;
                _world.Output?.Notify("tile-explored", area, new { area = area.Id, x = i % area.Width, y = i / area.Width, explored = true }, pc);
            }
            return;
        }
        int x0 = (int)MathF.Floor((pc.Position.X - RevealRadius) / Area.TileSize), x1 = (int)MathF.Floor((pc.Position.X + RevealRadius) / Area.TileSize);
        int y0 = (int)MathF.Floor((pc.Position.Y - RevealRadius) / Area.TileSize), y1 = (int)MathF.Floor((pc.Position.Y + RevealRadius) / Area.TileSize);
        for (int y = Math.Max(0, y0); y <= Math.Min(area.Height - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(area.Width - 1, x1); x++)
            {
                int i = y * area.Width + x;
                if (tiles[i]) continue;
                // Tile centre within reach of the radius (plus half a tile so the tile under the player always counts).
                float cx = (x + 0.5f) * Area.TileSize - pc.Position.X, cy = (y + 0.5f) * Area.TileSize - pc.Position.Y;
                if (cx * cx + cy * cy > (RevealRadius + Area.TileSize * 0.71f) * (RevealRadius + Area.TileSize * 0.71f)) continue;
                tiles[i] = true;
                _world.Output?.Notify("tile-explored", area, new { area = area.Id, x, y, explored = true }, pc);
            }
    }

    /// <summary>The map window: map notes and what this player has explored of their area.</summary>
    [ClientRequest("area-map")]
    static void AreaMap(Request r) => Send(r.World, r.Player);

    public static void Send(World world, Creature player)
    {
        if (player.Area is not { } area) return;
        var env = AreaSettings.Of(world, area);
        env.Explored.TryGetValue(player.Id, out var tiles);
        var talk = world.Resources.Talk;
        world.Output?.Notify("area-map", area, new
        {
            area = area.Id,
            explored = tiles == null ? "" : new string(tiles.Select(t => t ? '1' : '0').ToArray()),
            pins = area.Objects.OfType<Waypoint>().Where(w => w.HasMapNote && w.MapNoteEnabled)
                .Select(w => new { id = w.Id.ToString(), note = w.MapNote.Resolve(talk), x = w.Position.X, y = w.Position.Y, personal = false })
                .Concat(PersonalMapPins.Read(world, area, player).Select(p => new { id = p.Id, note = p.Note, x = p.X, y = p.Y, personal = true })).ToArray(),
        }, player);
    }
}
