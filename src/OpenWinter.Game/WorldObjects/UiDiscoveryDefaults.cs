using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Default UI discovery masks (NWNX_Area_SetDefaultObjectUiDiscoveryMask) per area and for all areas, by object type.
/// An object arriving in an area takes the area's mask for its type, else the global one. Measured on nwserver
/// (parity suite nwnx_area_objects): objects already there keep theirs unless the update is forced, an area mask
/// wins over the global one in its area, and setting a mask back to -1 removes it. A creature moving to another area
/// takes that area's default even over a mask its script set. Items dropped and players arriving take it the same
/// way here; those two are unmeasured.
/// </summary>
public sealed class UiDiscoveryDefaults : IWorldSystem
{
    const int TypeCreature = 1, TypeItem = 2, TypeDoor = 8, TypePlaceable = 64;
    /// <summary>OBJECT_UI_DISCOVERY_HILITE_MOUSEOVER | _HILITE_TAB: the only bits doors take.</summary>
    const int HiliteBits = 3;

    readonly World _world;
    readonly Dictionary<(uint Area, int Type), int> _masks = new();

    public UiDiscoveryDefaults(World world)
    {
        _world = world;
        world.ObjectAdded += o => { if (_masks.Count > 0 && o.Area is { } a && Mask(a, o) is { } m) Apply(o, m); };
        world.ObjectRemoved += o => { if (o is Area { IsDestroyed: true } a) foreach (var k in _masks.Keys.Where(k => k.Area == a.Id).ToList()) _masks.Remove(k); };
    }

    public void Step(float dt) { }

    /// <summary>The object type bit a mask applies to, or 0 for objects that take none (unusable placeables, waypoints...).</summary>
    static int TypeOf(GameObject o) => o switch
    {
        Creature => TypeCreature,
        Item => TypeItem,
        Door => TypeDoor,
        Placeable { Useable: true } => TypePlaceable,
        _ => 0,
    };

    /// <summary>The default mask for <paramref name="o"/> in <paramref name="area"/>, or null if none is set.</summary>
    public int? Mask(Area area, GameObject o)
    {
        int type = TypeOf(o);
        if (type == 0) return null;
        if (_masks.TryGetValue((area.Id, type), out int m)) return m;
        return _masks.TryGetValue((GameObject.InvalidId, type), out int g) ? g : null;
    }

    /// <summary>Sets (or with -1 removes) the mask of every type in <paramref name="types"/>, for one area or (InvalidId) all.</summary>
    public void Set(uint area, int types, int mask, bool forceUpdate)
    {
        foreach (int type in new[] { TypeCreature, TypeItem, TypeDoor, TypePlaceable })
        {
            if ((types & type) == 0) continue;
            if (mask == -1) _masks.Remove((area, type));
            else _masks[(area, type)] = mask;
        }
        if (!forceUpdate) return;
        List<Area> areas = area == GameObject.InvalidId ? _world.Areas.ToList() : _world.Get(area) is Area one ? [one] : [];
        foreach (var a in areas)
            foreach (var o in a.Objects.ToList())
                if ((types & TypeOf(o)) != 0 && Mask(a, o) is { } m) Apply(o, m);
    }

    void Apply(GameObject o, int mask)
    {
        o.UiDiscoveryMask = o is Door ? mask & HiliteBits : mask;
        _world.Output?.Notify("ui-discovery", o, new { mask = o.UiDiscoveryMask });
    }
}
