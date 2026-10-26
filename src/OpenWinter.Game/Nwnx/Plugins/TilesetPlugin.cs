using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Tileset: readings of tileset .set files, and tile overrides for areas made with CreateArea
/// (<see cref="TileOverrides"/>). Measured on nwserver with NWNX:EE (parity suite nwnx_tileset): a negative index
/// or tile id, or an override size outside 1-32, fails its argument check and returns nothing (0 or ""); an unknown
/// tileset, a missing tile or an index past the end reads as empty, a missing door as type -1. A corner's terrain
/// carries a "+" per height level ("Grass+").
/// </summary>
static class TilesetPlugin
{
    const string P = "Tileset";

    static readonly ConditionalWeakTable<World, Dictionary<string, Ini?>> _sets = new();

    /// <summary>A tileset's .set file, parsed once per world; null when there is none.</summary>
    public static Ini? SetFile(World world, string tileset)
    {
        if (tileset.Length == 0) return null;
        var cache = _sets.GetValue(world, _ => new Dictionary<string, Ini?>(StringComparer.OrdinalIgnoreCase));
        if (!cache.TryGetValue(tileset, out var ini))
            cache[tileset] = ini = world.Resources.Read(tileset, ResType.Set) is { } bytes ? Ini.Parse(bytes) : null;
        return ini;
    }

    [NwnxFunc(P, "GetTilesetData")]
    static void Data(NwnxCall n)
    {
        if (SetFile(n.World, n.PopString()) is not { } set) return;
        n.Push(set.GetInt("TILES", "Count"));
        n.Push(set.GetFloat("GENERAL", "Transition"));
        n.Push(set.GetInt("TERRAIN TYPES", "Count"));
        n.Push(set.GetInt("CROSSER TYPES", "Count"));
        n.Push(set.GetInt("GROUPS", "Count"));
        n.Push(set.Get("GENERAL", "Border"));
        n.Push(set.Get("GENERAL", "Default"));
        n.Push(set.Get("GENERAL", "Floor"));
        n.Push(set.GetInt("GENERAL", "DisplayName", -1));
        n.Push(set.Get("GENERAL", "UnlocalizedName"));
        n.Push(set.GetInt("GENERAL", "Interior") != 0);
        n.Push(set.GetInt("GENERAL", "HasHeightTransition") != 0);
    }

    static void Named(NwnxCall n, string section)
    {
        var set = SetFile(n.World, n.PopString()); int i = n.PopInt();
        if (i < 0) return;
        n.Push(set?.Get(section + i, "Name") ?? "");
    }
    [NwnxFunc(P, "GetTilesetTerrain")] static void Terrain(NwnxCall n) => Named(n, "TERRAIN");
    [NwnxFunc(P, "GetTilesetCrosser")] static void Crosser(NwnxCall n) => Named(n, "CROSSER");

    [NwnxFunc(P, "GetTilesetGroupData")]
    static void GroupData(NwnxCall n)
    {
        var set = SetFile(n.World, n.PopString()); int g = n.PopInt();
        if (g < 0) return;
        string section = "GROUP" + g;
        n.Push(set?.Get(section, "Name") ?? "");
        n.Push(set?.GetInt(section, "StrRef") ?? 0);
        n.Push(set?.GetInt(section, "Rows") ?? 0);
        n.Push(set?.GetInt(section, "Columns") ?? 0);
    }

    [NwnxFunc(P, "GetTilesetGroupTile")]
    static void GroupTile(NwnxCall n)
    {
        var set = SetFile(n.World, n.PopString()); int g = n.PopInt(), i = n.PopInt();
        if (g < 0 || i < 0) return;
        n.Push(set?.GetInt("GROUP" + g, "Tile" + i) ?? 0);
    }

    /// <summary>The tile's section, or null (with nothing returned) for a negative id.</summary>
    static (Ini? Set, string Section)? Tile(NwnxCall n)
    {
        var set = SetFile(n.World, n.PopString()); int id = n.PopInt();
        return id < 0 ? null : (set, "TILE" + id);
    }

    [NwnxFunc(P, "GetTileModel")]
    static void Model(NwnxCall n) { if (Tile(n) is var (set, s)) n.Push(set?.Get(s, "Model") ?? ""); }

    [NwnxFunc(P, "GetTileMinimapTexture")]
    static void Minimap(NwnxCall n) { if (Tile(n) is var (set, s)) n.Push(set?.Get(s, "ImageMap2D") ?? ""); }

    [NwnxFunc(P, "GetTileNumDoors")]
    static void NumDoors(NwnxCall n) { if (Tile(n) is var (set, s)) n.Push(set?.GetInt(s, "Doors") ?? 0); }

    [NwnxFunc(P, "GetTileEdgesAndCorners")]
    static void Edges(NwnxCall n)
    {
        if (Tile(n) is not var (set, s)) return;
        string Corner(string key)
        {
            var terrain = set?.Get(s, key) ?? "";
            return terrain.Length == 0 ? "" : terrain + new string('+', Math.Max(0, set!.GetInt(s, key + "Height")));
        }
        n.Push(Corner("TopLeft"));
        n.Push(set?.Get(s, "Top") ?? "");
        n.Push(Corner("TopRight"));
        n.Push(set?.Get(s, "Right") ?? "");
        n.Push(Corner("BottomRight"));
        n.Push(set?.Get(s, "Bottom") ?? "");
        n.Push(Corner("BottomLeft"));
        n.Push(set?.Get(s, "Left") ?? "");
    }

    [NwnxFunc(P, "GetTileDoorData")]
    static void DoorData(NwnxCall n)
    {
        if (Tile(n) is not var (set, s)) return;
        int i = n.PopInt();
        string door = s + "DOOR" + i;
        bool found = i >= 0 && set != null && set.HasSection(door);
        n.Push(found ? set!.GetInt(door, "Type") : -1);
        n.Push(found ? set!.GetFloat(door, "X") : 0f);
        n.Push(found ? set!.GetFloat(door, "Y") : 0f);
        n.Push(found ? set!.GetFloat(door, "Z") : 0f);
        n.Push(found ? set!.GetFloat(door, "Orientation") : 0f);
    }

    // ---- overrides ----

    static TileOverrides Overrides(NwnxCall n) => n.World.System<TileOverrides>();

    [NwnxFunc(P, "SetAreaTileOverride")]
    static void SetAreaOverride(NwnxCall n)
    {
        var area = n.PopString(); var name = n.PopString();
        Overrides(n).SetAreaOverride(area, name);
    }

    [NwnxFunc(P, "CreateTileOverride")]
    static void CreateOverride(NwnxCall n)
    {
        var name = n.PopString(); var tileset = n.PopString(); int w = n.PopInt(), h = n.PopInt();
        if (name.Length == 0 || w is < 1 or > 32 || h is < 1 or > 32) return;
        Overrides(n).Create(name, tileset, w, h);
    }

    [NwnxFunc(P, "DeleteTileOverride")] static void DeleteOverride(NwnxCall n) => Overrides(n).Delete(n.PopString());

    [NwnxFunc(P, "SetOverrideTileData")]
    static void SetTileData(NwnxCall n)
    {
        var name = n.PopString(); int index = n.PopInt();
        int id = n.PopInt(), orientation = n.PopInt(), height = n.PopInt();
        int main1 = n.PopInt(), main2 = n.PopInt(), source1 = n.PopInt(), source2 = n.PopInt();
        bool loop1 = n.PopInt() != 0, loop2 = n.PopInt() != 0, loop3 = n.PopInt() != 0;
        if (index < 0 || Overrides(n).Get(name) is not { } o) return;
        o.Tiles[index] = new TileOverrides.CustomTile(id, orientation, height, main1, main2, source1, source2, loop1, loop2, loop3);
    }

    [NwnxFunc(P, "DeleteOverrideTileData")]
    static void DeleteTileData(NwnxCall n)
    {
        var name = n.PopString(); int index = n.PopInt();
        if (Overrides(n).Get(name) is not { } o) return;
        if (index == -1) o.Tiles.Clear();
        else o.Tiles.Remove(index);
    }
}
