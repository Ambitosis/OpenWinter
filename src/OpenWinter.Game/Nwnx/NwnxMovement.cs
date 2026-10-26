using System.Runtime.CompilerServices;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// NWNX_Events raised by a creature walking (nwnx_events_creature): NWNX_ON_CREATURE_TILE_CHANGE when it crosses
/// onto another tile (OLD/NEW_TILE_INDEX = y * width + x, and the grid positions), NWNX_ON_MATERIALCHANGE when the
/// surface under it changes (not on arriving in an area, only once it walks).
/// </summary>
public static class NwnxMovement
{
    sealed class Where { public uint Area; public int Tile = -1, Material = -1; }

    static readonly ConditionalWeakTable<Creature, Where> _where = new();

    public static void Moved(World world, Creature c)
    {
        if (c.Area is not { } area) return;
        bool tiles = EventsPlugin.Listening(world, "NWNX_ON_CREATURE_TILE_CHANGE"), surface = EventsPlugin.Listening(world, "NWNX_ON_MATERIALCHANGE");
        if (!tiles && !surface) return;
        var w = _where.GetValue(c, _ => new Where());
        int tx = (int)MathF.Floor(c.Position.X / 10f), ty = (int)MathF.Floor(c.Position.Y / 10f);
        int tile = tx >= 0 && ty >= 0 && tx < area.Width && ty < area.Height ? ty * area.Width + tx : -1;
        int material = surface ? area.Walkmesh.SurfaceAt(c.Position) : -1;
        if (w.Area != area.Id)
        {
            // A new area: remember where the creature is; events come once it walks on.
            (w.Area, w.Tile, w.Material) = (area.Id, tile, material);
            return;
        }
        if (tiles && tile != w.Tile && tile >= 0 && w.Tile >= 0)
        {
            var ev = NwnxEvent.Begin(world, "NWNX_ON_CREATURE_TILE_CHANGE", c)?.Add("OLD_TILE_INDEX", w.Tile)
                .Add("OLD_TILE_X", w.Tile % area.Width).Add("OLD_TILE_Y", w.Tile / area.Width)
                .Add("NEW_TILE_INDEX", tile).Add("NEW_TILE_X", tx).Add("NEW_TILE_Y", ty);
            ev?.Before();
            ev?.After();
        }
        if (tile >= 0) w.Tile = tile;
        if (surface && material != w.Material && material > 0)
        {
            if (w.Material > 0)
            {
                var ev = NwnxEvent.Begin(world, "NWNX_ON_MATERIALCHANGE", c)?.Add("MATERIAL_TYPE", material);
                ev?.Before();
                ev?.After();
            }
            w.Material = material;
        }
    }
}
