using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;

namespace OpenWinter.Game.DungeonMaster;

/// <summary>One node of a blueprint palette: a category (with children) or a blueprint (with a resref).</summary>
public sealed class PaletteNode
{
    public string Name = "";
    /// <summary>Blueprint resref; empty for categories.</summary>
    public string ResRef = "";
    /// <summary>Category id (blueprints' PaletteID points here), or -1.</summary>
    public int Id = -1;
    /// <summary>Creatures: challenge rating; -1 when not given.</summary>
    public float CR = -1;
    public string Faction = "";
    public List<PaletteNode> Children = new();
    public bool IsBlueprint => ResRef.Length > 0;

    public int CountBlueprints() => IsBlueprint ? 1 : Children.Sum(c => c.CountBlueprints());
}

/// <summary>A blueprint kind the DM creator offers, with its palette name and NWScript object type.</summary>
public sealed record PaletteKind(string Key, string Palette, int ObjectType, ushort ResType, string Title);

/// <summary>
/// The toolset's blueprint palettes (<c>*.itp</c> GFF trees): the base game's standard palette
/// (<c>creaturepalstd</c>, ...) and the module's custom one (<c>creaturepalcus</c>, ...).
/// Each MAIN/LIST node has a STRREF or NAME, category nodes an ID, and blueprint entries a
/// RESREF (creatures add CR and FACTION). The DM creator browses these.
/// </summary>
public sealed class Palettes
{
    public static readonly PaletteKind[] Kinds =
    {
        new("creature", "creaturepal", 1, ResType.Utc, "Creatures"),
        new("item", "itempal", 2, ResType.Uti, "Items"),
        new("placeable", "placeablepal", 64, ResType.Utp, "Placeables"),
        new("encounter", "encounterpal", 256, ResType.Ute, "Encounters"),
        new("waypoint", "waypointpal", 32, ResType.Utw, "Waypoints"),
        new("trigger", "triggerpal", 4, ResType.Utt, "Triggers"),
        new("sound", "soundpal", 0x4000, ResType.Uts, "Sounds"),
        new("store", "storepal", 128, ResType.Utm, "Stores"),
    };

    readonly World _world;
    readonly Dictionary<string, List<PaletteNode>> _trees = new(StringComparer.OrdinalIgnoreCase);

    public Palettes(World world) { _world = world; }

    public static PaletteKind? Kind(string key) => Kinds.FirstOrDefault(k => k.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The palette for a kind: "Custom" (the module's) then "Standard" (the game's) as root categories.</summary>
    public List<PaletteNode> Tree(PaletteKind kind)
    {
        if (_trees.TryGetValue(kind.Key, out var cached)) return cached;
        var roots = new List<PaletteNode>();
        foreach (var (suffix, title) in new[] { ("cus", "Custom"), ("std", "Standard") })
        {
            var gff = _world.Resources.ReadGff(kind.Palette + suffix, ResType.Itp);
            if (gff == null) continue;
            var root = new PaletteNode { Name = title, Children = ReadList(gff.GetList("MAIN")) };
            Prune(root);
            if (root.Children.Count > 0) roots.Add(root);
        }
        return _trees[kind.Key] = roots;
    }

    /// <summary>Every blueprint of a kind (custom first), flattened.</summary>
    public IEnumerable<PaletteNode> Blueprints(PaletteKind kind) => Tree(kind).SelectMany(Flatten).Where(n => n.IsBlueprint);

    static IEnumerable<PaletteNode> Flatten(PaletteNode n) => new[] { n }.Concat(n.Children.SelectMany(Flatten));

    List<PaletteNode> ReadList(List<GffStruct> list)
    {
        var result = new List<PaletteNode>(list.Count);
        foreach (var s in list)
        {
            var node = new PaletteNode
            {
                Name = NodeName(s),
                ResRef = s.GetString("RESREF"),
                Id = s.Has("ID") ? (int)s.GetInt("ID") : -1,
                CR = s.Has("CR") ? s.GetFloat("CR", s.GetInt("CR")) : -1,
                Faction = s.GetString("FACTION"),
            };
            if (node.Name.Length == 0) node.Name = node.ResRef;
            node.Children = ReadList(s.GetList("LIST"));
            result.Add(node);
        }
        return result;
    }

    string NodeName(GffStruct s)
    {
        var name = s.GetString("NAME");
        if (name.Length > 0) return name;
        if (s.Has("STRREF")) return _world.Resources.TlkString(unchecked((uint)s.GetInt("STRREF")));
        return "";
    }

    /// <summary>Drops empty categories so the browser only shows branches with something to place.</summary>
    static void Prune(PaletteNode n)
    {
        foreach (var c in n.Children) Prune(c);
        n.Children.RemoveAll(c => !c.IsBlueprint && c.Children.Count == 0);
    }
}
