using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Object_Serialize / Deserialize / AcquireItem (parity suite nwnx_object_serialize).</summary>
static partial class ObjectPlugin
{
    /// <summary>
    /// The object's GFF in base64, as the original writes it: the object with its state (locals, effects, actions)
    /// and plugin values (NWNX_POS), creatures as "BIC ", the rest as their blueprint type. The module and areas
    /// give "".
    /// </summary>
    [NwnxFunc(P, "Serialize")]
    static void Serialize(NwnxCall n)
    {
        var o = n.PopObject();
        n.Push(o != null && Bytes(n.World, o) is { } data ? Convert.ToBase64String(data) : "");
    }

    internal static byte[]? Bytes(World world, GameObject o)
    {
        if (!ObjectSerializer.CanStore(o) || ObjectSerializer.Serialize(world, o, saveObjectState: true) is not { } s) return null;
        NwnxPersistence.WriteField(world, o, s, always: true);
        return Gff.Write(s, o is Creature ? "BIC " : ObjectSerializer.FileType(o));
    }

    /// <summary>
    /// Creates the object in the world but in no area (items outside any inventory); scripts then place it with
    /// NWNX_Object_AddToArea, AcquireItem, a jump or CopyObject. Invalid input gives OBJECT_INVALID.
    /// </summary>
    [NwnxFunc(P, "Deserialize")]
    static void Deserialize(NwnxCall n)
    {
        var text = n.PopString();
        n.PushObject(FromText(n, text)?.Id ?? GameObject.InvalidId);
    }

    static GameObject? FromText(NwnxCall n, string text)
    {
        if (text.Length == 0) return null;
        byte[] data;
        try { data = Convert.FromBase64String(text); }
        catch (FormatException) { return null; }
        if (data.Length < 56) return null;
        GffStruct s;
        string type;
        try { s = Gff.Read(data, out type); }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidDataException or EndOfStreamException) { return null; }
        var o = ObjectSerializer.Deserialize(n.World, s, type, Location.Invalid, owner: null, loadObjectState: true, limbo: true);
        if (o != null && o.Area == null) n.Registry.TrackLimbo(o);
        return o;
    }

    /// <summary>Moves an item into a creature's, placeable's, store's or container's inventory, as acquiring it does.</summary>
    [NwnxFunc(P, "AcquireItem")]
    static void AcquireItem(NwnxCall n)
    {
        var owner = n.PopObject();
        var item = n.PopObject<Item>();
        n.Push(owner != null && item != null && Acquire(n.World, owner, item));
    }

    internal static bool Acquire(World world, GameObject owner, Item item)
    {
        if (owner == item || world.Inventory.Owner(item) != null) return false;
        if (owner is Placeable { HasInventory: false } || owner is not (Creature or Placeable or Store or Item)) return false;
        world.LiftFromArea(item);
        if (!ObjectSerializer.GiveItem(world, owner, item)) return false;
        world.Inventory.Acquired(owner, item);
        return true;
    }
}
