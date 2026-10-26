using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Tile overrides for areas made with CreateArea (NWNX_Tileset): a named override holds a tileset, a size and data
/// for some of its tiles; an area resref mapped to one is created with that tileset, size and tiles instead of its
/// own. Measured on nwserver (parity suite nwnx_tileset): tiles without data are tile 0 with everything off, data
/// for an index outside the size is ignored, and an override that no longer exists leaves the area as it is.
/// Module areas already loaded keep their tiles.
/// </summary>
public sealed class TileOverrides : IWorldSystem
{
    public sealed record CustomTile(int TileId, int Orientation, int Height, int MainLight1, int MainLight2,
        int SourceLight1, int SourceLight2, bool AnimLoop1, bool AnimLoop2, bool AnimLoop3);

    public sealed class Override
    {
        public required string Tileset;
        public required int Width, Height;
        public readonly Dictionary<int, CustomTile> Tiles = new();
    }

    readonly Dictionary<string, Override> _overrides = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _areas = new(StringComparer.OrdinalIgnoreCase);

    public TileOverrides(World world) { }
    public void Step(float dt) { }

    public void Create(string name, string tileset, int width, int height) =>
        _overrides[name] = new Override { Tileset = tileset, Width = width, Height = height };

    public void Delete(string name) => _overrides.Remove(name);

    public Override? Get(string name) => _overrides.GetValueOrDefault(name);

    /// <summary>Maps an area resref to an override, or removes the mapping when <paramref name="name"/> is "".</summary>
    public void SetAreaOverride(string areaResRef, string name)
    {
        if (name.Length == 0) _areas.Remove(areaResRef);
        else _areas[areaResRef] = name;
    }

    /// <summary>Rewrites an area's ARE (Tileset, Width, Height, Tile_List) from the override its resref maps to, if any.</summary>
    public void Apply(string areaResRef, GffStruct are)
    {
        if (!_areas.TryGetValue(areaResRef, out var name) || !_overrides.TryGetValue(name, out var o)) return;
        are.SetResRef("Tileset", o.Tileset);
        are.SetInt("Width", o.Width);
        are.SetInt("Height", o.Height);
        var list = new List<GffStruct>(o.Width * o.Height);
        for (int i = 0; i < o.Width * o.Height; i++)
        {
            var t = o.Tiles.GetValueOrDefault(i) ?? new CustomTile(0, 0, 0, 0, 0, 0, 0, false, false, false);
            list.Add(new GffStruct(1).SetInt("Tile_ID", t.TileId).SetInt("Tile_Orientation", t.Orientation).SetInt("Tile_Height", t.Height)
                .SetByte("Tile_MainLight1", t.MainLight1).SetByte("Tile_MainLight2", t.MainLight2)
                .SetByte("Tile_SrcLight1", t.SourceLight1).SetByte("Tile_SrcLight2", t.SourceLight2)
                .SetByte("Tile_AnimLoop1", t.AnimLoop1 ? 1 : 0).SetByte("Tile_AnimLoop2", t.AnimLoop2 ? 1 : 0).SetByte("Tile_AnimLoop3", t.AnimLoop3 ? 1 : 0));
        }
        are.SetList("Tile_List", list);
    }
}
