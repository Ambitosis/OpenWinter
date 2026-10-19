using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Net;

namespace OpenWinter.Game.Objects;

/// <summary>Inventory slot bits (Equip_ItemList struct ids, INVENTORY_SLOT_* as bit positions).</summary>
public static class EquipSlot
{
    public const int Head = 0x1, Chest = 0x2, Boots = 0x4, Arms = 0x8, RightHand = 0x10, LeftHand = 0x20, Cloak = 0x40,
        LeftRing = 0x80, RightRing = 0x100, Neck = 0x200, Belt = 0x400, Arrows = 0x800, Bullets = 0x1000, Bolts = 0x2000,
        CreatureLeft = 0x4000, CreatureRight = 0x8000, CreatureBite = 0x10000, CreatureArmour = 0x20000;

    /// <summary>NWScript INVENTORY_SLOT_* index for a slot bit.</summary>
    public static int Index(int bit) => System.Numerics.BitOperations.TrailingZeroCount(bit);
    public static int Bit(int index) => 1 << index;
}

/// <summary>Builds items from UTI-shaped GFF data.</summary>
public sealed class ItemFactory
{
    /// <summary>Armour part field names in ItemVisual.ArmorParts order (body part order, then robe).</summary>
    public static readonly string[] ArmorPartFields =
    {
        "", "ArmorPart_Neck", "ArmorPart_Torso", "ArmorPart_Pelvis", "ArmorPart_Belt",
        "ArmorPart_RShoul", "ArmorPart_LShoul", "ArmorPart_RBicep", "ArmorPart_LBicep", "ArmorPart_RFArm",
        "ArmorPart_LFArm", "ArmorPart_RHand", "ArmorPart_LHand", "ArmorPart_RThigh", "ArmorPart_LThigh",
        "ArmorPart_RShin", "ArmorPart_LShin", "ArmorPart_RFoot", "ArmorPart_LFoot", "ArmorPart_Robe",
    };

    /// <summary>ItemVisual.Colors order.</summary>
    public static readonly string[] ColorFields = { "Cloth1Color", "Cloth2Color", "Leather1Color", "Leather2Color", "Metal1Color", "Metal2Color" };

    readonly World _world;
    public ItemFactory(World world) { _world = world; }

    public Item FromTemplate(GffStruct s)
    {
        var item = new Item
        {
            Template = s,
            Tag = s.GetString("Tag"),
            ResRef = s.GetString("TemplateResRef"),
            BaseItem = (int)s.GetInt("BaseItem"),
            StackSize = (int)Math.Max(1, s.GetInt("StackSize", 1)),
            Name = s.GetLocString("LocalizedName"),
            Description = s.GetLocString("DescIdentified").FirstString,
            Plot = s.GetInt("Plot") != 0,
        };
        item.Locals.LoadFrom(s);
        item.Identified = s.GetInt("Identified", 1) != 0;
        item.Stolen = s.GetInt("Stolen") != 0;
        item.Cursed = s.GetInt("Cursed") != 0;
        item.Charges = (int)s.GetInt("Charges");
        item.Cost = (int)s.GetInt("Cost");
        item.AddCost = (int)s.GetInt("AddCost");
        // An item that doesn't say is neither: the toolset writes Dropable/Pickpocketable only when they are ticked, and
        // the original reads 0 for blueprint and area-instance items without them and for an item made on the ground
        // (nwserver corpse_drops; its own save games write both fields on every item). CreateItemOnObject, CopyItem
        // and a player taking an item set them (InventorySystem).
        item.Droppable = s.GetInt("Dropable") != 0;
        item.Pickpocketable = s.GetInt("Pickpocketable") != 0;
        foreach (var p in s.GetList("PropertiesList"))
            item.Properties.Add(new Scripting.ItemProperty
            {
                Type = (int)p.GetInt("PropertyName"),
                SubType = (int)p.GetInt("Subtype"),
                CostTable = (int)p.GetInt("CostTable"),
                CostTableValue = (int)p.GetInt("CostValue"),
                Param1 = (int)p.GetInt("Param1"),
                Param1Value = (int)p.GetInt("Param1Value"),
                ChanceToAppear = (int)p.GetInt("ChanceAppear", 100),
                // Without a saved count a per-day property has all its uses.
                UsesPerDay = (int)p.GetInt("UsesPerDay", Inventory.InventorySystem.MaxUsesPerDay((int)p.GetInt("CostTable"), (int)p.GetInt("CostValue")) is var max and > 0 ? max : 255),
                Useable = p.GetInt("Useable", 1) != 0,
                Tag = p.GetString("Tag"),
                DurationType = 2, // permanent
            });
        // An item without properties has nothing to identify.
        if (item.Properties.Count == 0) item.Identified = true;
        foreach (var entry in s.GetList("ItemList"))
            if (FromListEntry(entry) is { } inner) { _world.Register(inner); inner.Possessor = item.Id; item.Inventory.Add(inner); }
        return item;
    }

    public Item? FromBlueprint(string resref)
    {
        var uti = _world.Resources.ReadGff(resref, ResType.Uti);
        if (uti == null) return null;
        var item = FromTemplate(uti);
        if (item.ResRef.Length == 0) item.ResRef = resref;
        return item;
    }

    /// <summary>An equipment list entry is either an embedded UTI or a reference to a blueprint.</summary>
    public Item? FromListEntry(GffStruct entry)
    {
        Item? item = null;
        foreach (var field in new[] { "EquippedRes", "InventoryRes" })
            if (entry.GetString(field) is { Length: > 0 } res) { item = FromBlueprint(res); break; }
        item ??= entry.Has("BaseItem") ? FromTemplate(entry) : null;
        // Where it sits in the inventory grid (every ItemList entry carries it; equipment lists don't).
        if (item != null && entry.Has("Repos_PosX"))
        {
            item.InvX = (int)entry.GetInt("Repos_PosX");
            item.InvY = (int)entry.GetInt("Repos_Posy");
        }
        // Creature and placeable inventories mark per entry whether the item drops on death / can be stolen (the
        // toolset's Droppable and Pickpocketable ticks; an entry without them is neither).
        if (item != null && !entry.Has("BaseItem"))
        {
            if (entry.Has("Dropable")) item.Droppable = entry.GetInt("Dropable") != 0;
            if (entry.Has("Pickpocketable")) item.Pickpocketable = entry.GetInt("Pickpocketable") != 0;
        }
        return item;
    }

    /// <summary>itempropdef.2da row of the Light property (ITEM_PROPERTY_LIGHT).</summary>
    public const int PropertyLight = 44;

    public static ItemVisual Visual(int slot, Item item)
    {
        var t = item.Template;
        // A Light property lights the creature that has the item equipped (the torch: yellow, 20 m).
        var light = item.Properties.FirstOrDefault(p => p.Type == PropertyLight && p.Param1Value is >= 0 and < ItemVisual.NoLight);
        return new ItemVisual
        {
            LightColor = light != null ? (byte)light.Param1Value : ItemVisual.NoLight,
            LightLevel = light != null ? (byte)Math.Clamp(light.CostTableValue, 0, 254) : (byte)0,
            Slot = slot,
            BaseItem = item.BaseItem,
            ModelParts = new[] { (ushort)CreatureFactory.Wide(t, "ModelPart1"), (ushort)CreatureFactory.Wide(t, "ModelPart2"), (ushort)CreatureFactory.Wide(t, "ModelPart3") },
            Colors = ColorFields.Select(f => (byte)t.GetInt(f)).ToArray(),
            ArmorParts = ArmorPartFields.Select(f => f.Length == 0 ? (ushort)0 : (ushort)CreatureFactory.Wide(t, f)).ToArray(),
        };
    }
}
