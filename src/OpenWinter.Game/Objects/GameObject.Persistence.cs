using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Persistence;

namespace OpenWinter.Game.Objects;

public abstract partial class GameObject
{
    /// <summary>GetObjectUUID value; assigned lazily (or restored from a serialized object). Managed by <see cref="PersistenceSystem"/>.</summary>
    public string? Uuid { get; internal set; }
}

public sealed partial class Creature
{
    /// <summary>Slot edits from the client; a null value explicitly clears a slot. Included in world snapshots.</summary>
    public readonly Dictionary<int, OpenWinter.Content.QuickbarSlot?> QuickbarEdits = new();
    /// <summary>Items referenced by original or edited quickbar slots, retained across moves and snapshots.</summary>
    public readonly Dictionary<int, Item> QuickbarItems = new();
    public readonly Dictionary<int, Item> QuickbarSecondaryItems = new();
    /// <summary>File name (without .bic) this character is exported to in its vault folder; chosen on first export.</summary>
    public string VaultFile = "";
    /// <summary>The character's SQLite database (SqlPrepareQueryObject), opened on first use; saved into the BIC on export.</summary>
    internal SqlDb? Database;
    /// <summary>The BIC carried a database OpenWinter cannot read yet (zstd); it is written back unchanged on export.</summary>
    internal bool DatabaseUnreadable;
}

public sealed partial class Module
{
    /// <summary>The module's SQLite database (SqlPrepareQueryObject(GetModule())), opened on first use.</summary>
    internal SqlDb? Database;
}

static class TemplateAccess
{
    /// <summary>The GFF an object was built from, for types that keep one.</summary>
    public static GffStruct? TemplateOf(GameObject o) => o switch
    {
        Creature c => c.Template,
        Item i => i.Template,
        Placeable p => p.Template,
        Door d => d.Template,
        Store s => s.Template,
        Encounter e => e.Template,
        _ => null,
    };
}
