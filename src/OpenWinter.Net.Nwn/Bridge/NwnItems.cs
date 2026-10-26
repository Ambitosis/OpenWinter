using System.Globalization;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Resources;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// An NWN item as the client's inventory, container and store windows describe it: its name, icon layers and the
/// tooltip details ("info"), worked out from the item's appearance and properties and the base item's 2DA rows.
/// </summary>
public static class NwnItems
{
    const int BaseArmor = 16, BaseSpellScroll = 75, PropCastSpell = 15;
    const int PropUseLimitAlignmentGroup = 62, PropUseLimitTerrain = 66;
    const int PropThievesTools = 55, PropTrap = 70, PropHealersKit = 80;
    const int SlotHead = 0x1, SlotBoots = 0x4, SlotArms = 0x8, SlotRightHand = 0x10, SlotCloak = 0x40,
        SlotRings = 0x180, SlotNeck = 0x200, SlotBelt = 0x400;

    public static string Name(INwnBridgeData data, ItemAppearanceAndProps item) => item.Name?.Value switch
    {
        OriginalItemName o => o.Name is { IsStrRef: true } s ? data.Text(s.StrRef, s.Gender) : o.Name.Text,
        DisplayItemName d => d.Name,
        _ => "",
    };

    /// <summary>The client's JSON for an item: id, name, base item, stack, icon, identified and info.</summary>
    public static JsonObject Entry(INwnBridgeData data, uint id, ItemAppearanceAndProps item, bool female)
    {
        var icon = new JsonArray();
        foreach (var layer in Icon(data, item, female)) icon.Add(layer);
        return new JsonObject
        {
            ["id"] = id,
            ["name"] = Name(data, item),
            ["baseItem"] = (int)item.BaseItemId,
            ["stack"] = (int)Math.Max(1, item.StackSize),
            ["icon"] = icon,
            ["identified"] = item.Identified,
            ["info"] = Info(data, item),
        };
    }

    static string Cell(INwnBridgeData data, string table, int row, string column)
    {
        try { return row >= 0 && row < data.TwoDARowCount(table) ? data.TwoDA(table, row, column) : ""; }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException or IndexOutOfRangeException) { return ""; }
    }

    static int Int(INwnBridgeData data, string table, int row, string column, int fallback = 0)
    {
        var s = Cell(data, table, row, column);
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : fallback;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    static string Tlk(INwnBridgeData data, int strRef) => strRef >= 0 ? data.Text((uint)strRef, 0) : "";

    /// <summary>baseitems EquipableSlots: the INVENTORY_SLOT_* bits the item goes in.</summary>
    public static int EquipableSlots(INwnBridgeData data, int baseItem) => Int(data, "baseitems", baseItem, "EquipableSlots");

    /// <summary>Store panel (0 armour, 1 misc, 2 potions, 3 jewellery, 4 weapons): baseitems StorePanel numbers weapons 1 and
    /// miscellaneous 4.</summary>
    public static int StorePanel(INwnBridgeData data, int baseItem) =>
        Int(data, "baseitems", baseItem, "StorePanel", 4) switch { 0 => 0, 1 => 4, 2 => 2, 3 => 3, _ => 1 };

    // -- icons ----------------------------------------------------------------

    /// <summary>
    /// Icon layers, bottom first: one i&lt;class&gt;_&lt;model&gt; for simple items, tinted for layered ones, bottom, middle and
    /// top part icons for composite weapons, the armour's part icons, and a spell scroll's spell icon.
    /// </summary>
    public static string[] Icon(INwnBridgeData data, ItemAppearanceAndProps item, bool female)
    {
        int baseItem = (int)item.BaseItemId;
        if (baseItem == BaseSpellScroll && item.Properties.FirstOrDefault(p => p.Name == PropCastSpell) is { } cast
            && Cell(data, "iprp_spells", cast.Subtype, "Icon").ToLowerInvariant() is { Length: > 0 } spell && data.HasResource(spell, ResType.Tga))
            return [spell];
        string cls = Cell(data, "baseitems", baseItem, "ItemClass").ToLowerInvariant();
        string fallback = Cell(data, "baseitems", baseItem, "DefaultIcon").ToLowerInvariant();
        if (cls.Length == 0) return fallback.Length > 0 ? [fallback] : [];
        switch (item.ModelData?.Value)
        {
            case CompositeModel c:
                return [$"i{cls}_b_{c.Part1:000}", $"i{cls}_m_{c.Part2:000}", $"i{cls}_t_{c.Part3:000}"];
            case ArmorModel a:
            {
                int Colour(int index) => index < 6 ? At(a.Palette, index)
                    : (index - 6) / 6 < a.PalettePerPart.Count ? At(a.PalettePerPart[(index - 6) / 6], (index - 6) % 6) : 255;
                int robe = At(a.Parts, ArmourIcon.Robe);
                var layers = ArmourIcon.Layers(female, part => At(a.Parts, part), Colour, r => data.HasResource(r, ResType.Plt),
                    column => Int(data, "parts_robe", robe, column) == 1);
                return layers.Length > 0 ? layers : [fallback];
            }
            case LayeredModel l:
                return Layered(data, $"i{cls}_{l.Part1:000}", l.Palette);
            case SimpleModel s:
                return [$"i{cls}_{s.Part1:000}"];
            default:
                return fallback.Length > 0 ? [fallback] : [];
        }
    }

    static int At<T>(IReadOnlyList<T> list, int index) where T : IConvertible =>
        index >= 0 && index < list.Count ? list[index].ToInt32(CultureInfo.InvariantCulture) : 0;

    /// <summary>Helmets and cloaks: tinted with the item's colours when the icon is a PLT (cloaks are installed as
    /// male icons only).</summary>
    static string[] Layered(INwnBridgeData data, string resref, IReadOnlyList<byte> palette)
    {
        bool Any(string r) => data.HasResource(r, ResType.Plt) || data.HasResource(r, ResType.Tga) || data.HasResource(r, ResType.Dds);
        if (!Any(resref) && resref.LastIndexOf('_') is var cut and > 0 && resref.Insert(cut + 1, "m_") is var male && data.HasResource(male, ResType.Plt))
            resref = male;
        if (!data.HasResource(resref, ResType.Plt)) return [resref];
        var colours = new byte[10];
        int[] channels = [6, 7, 4, 5, 2, 3];
        for (int channel = 0; channel < channels.Length; channel++)
            colours[channels[channel]] = (byte)Math.Clamp(At(palette, channel), 0, 175);
        return [$"iconplt:{resref}:{Convert.ToHexString(colours)}"];
    }

    // -- tooltip details ------------------------------------------------------

    /// <summary>
    /// The tooltip details: base type and category, weapon damage, armour figures, property lines (identified items
    /// only), charges, weight (tenths of a pound), value, size and the slots it goes in.
    /// </summary>
    public static JsonObject Info(INwnBridgeData data, ItemAppearanceAndProps item)
    {
        int b = (int)item.BaseItemId;
        string type = Tlk(data, Int(data, "baseitems", b, "Name", -1));
        if (type.Length == 0) type = Cell(data, "baseitems", b, "label");
        string category = Category(data, b);
        int slots = EquipableSlots(data, b);
        string damage = "", damageType = "", critical = "";
        if (category is "weapon" or "ammo")
        {
            int dice = Int(data, "baseitems", b, "NumDice"), die = Int(data, "baseitems", b, "DieToRoll");
            if (dice > 0 && die > 0)
            {
                damage = $"{dice}d{die}";
                damageType = Int(data, "baseitems", b, "WeaponType") switch
                {
                    1 => "Piercing",
                    2 => "Bludgeoning",
                    3 => "Slashing",
                    4 => "Piercing / Slashing",
                    5 => "Bludgeoning / Piercing",
                    _ => "",
                };
                int threat = Math.Max(1, Int(data, "baseitems", b, "CritThreat", 1)), mult = Math.Max(2, Int(data, "baseitems", b, "CritHitMult", 2));
                critical = (threat > 1 ? $"{21 - threat}–20" : "20") + $" / ×{mult}";
            }
        }
        JsonObject? armor = null;
        int ac = -1;
        if (b == BaseArmor && item.ArmorValue is { } baseAc)
        {
            ac = baseAc;
            int maxDex = Int(data, "armor", baseAc, "DEXBONUS", 100);
            armor = new JsonObject
            {
                ["weight"] = baseAc == 0 ? "Clothing" : baseAc <= 3 ? "Light" : baseAc <= 5 ? "Medium" : "Heavy",
                ["maxDex"] = maxDex >= 100 ? -1 : maxDex,
                ["checkPenalty"] = Int(data, "armor", baseAc, "ACCHECK"),
                ["spellFailure"] = Int(data, "armor", baseAc, "ARCANEFAILURE%"),
            };
        }
        else if (category == "shield")
        {
            ac = Int(data, "baseitems", b, "BaseAC");
            armor = new JsonObject
            {
                ["weight"] = "Shield",
                ["maxDex"] = -1,
                ["checkPenalty"] = -Math.Abs(Int(data, "baseitems", b, "ArmorCheckPen")),
                ["spellFailure"] = Int(data, "baseitems", b, "ArcaneSpellFailure"),
            };
        }
        var properties = new JsonArray();
        var requirements = new JsonArray();
        bool usable = false;
        foreach (var p in item.Properties)
        {
            if (p.Name is PropCastSpell or PropHealersKit or PropThievesTools or PropTrap) usable = true;
            if (!item.Identified) continue;
            var text = PropertyText(data, p);
            if (text.Length == 0) continue;
            if (p.Name is >= PropUseLimitAlignmentGroup and <= PropUseLimitTerrain)
                requirements.Add(new JsonObject { ["text"] = text, ["met"] = true });
            else properties.Add(new JsonObject { ["text"] = text, ["bad"] = false, ["temporary"] = false });
        }
        int maxCharges = Int(data, "baseitems", b, "ChargesStarting");
        int charges = maxCharges > 0 && item.UsesLeft is { UseableProperties: > 0 } uses && uses.Uses.Count > 0 ? uses.Uses[0] ?? 0 : -1;
        return new JsonObject
        {
            ["type"] = type.TrimEnd('*', ' '),
            ["category"] = category,
            ["panel"] = StorePanel(data, b),
            ["inventorySound"] = InventorySound(data, b, ac),
            ["damage"] = damage,
            ["damageType"] = damageType,
            ["critical"] = critical,
            ["ac"] = ac,
            ["armor"] = armor,
            ["properties"] = properties,
            ["requirements"] = requirements,
            ["charges"] = charges,
            ["weight"] = Weight(data, item),
            ["value"] = item.Identified ? (int)item.Cost : -1,
            ["plot"] = false,
            ["stolen"] = item.Stolen,
            ["cursed"] = item.Cursed,
            ["usable"] = usable,
            ["maxStack"] = Math.Max(1, Int(data, "baseitems", b, "Stacking", 1)),
            ["width"] = Math.Max(1, Int(data, "baseitems", b, "InvSlotWidth", 1)),
            ["height"] = Math.Max(1, Int(data, "baseitems", b, "InvSlotHeight", 1)),
            ["slots"] = slots,
        };
    }

    /// <summary>The weight of the stack in tenths of a pound: armour by its base AC (armor.2da), the rest by baseitems.</summary>
    public static int Weight(INwnBridgeData data, ItemAppearanceAndProps item)
    {
        int b = (int)item.BaseItemId;
        int unit = b == BaseArmor && item.ArmorValue is { } ac ? Int(data, "armor", ac, "WEIGHT") : Int(data, "baseitems", b, "TenthLBS");
        return unit * (int)Math.Max(1, item.StackSize);
    }

    static string InventorySound(INwnBridgeData data, int baseItem, int ac)
    {
        if (baseItem == BaseArmor)
            return ac switch { <= 1 => "it_materialcloth", <= 3 => "it_armorleather", <= 5 => "it_armorchain", _ => "it_armorplate" };
        return Cell(data, "inventorysnds", Int(data, "baseitems", baseItem, "InvSoundType", -1), "InventorySound");
    }

    /// <summary>The tooltip category of a base item (the client's backpack filter tabs group them).</summary>
    public static string Category(INwnBridgeData data, int b)
    {
        if (b < 0 || b >= data.TwoDARowCount("baseitems")) return "misc";
        if (Int(data, "baseitems", b, "Container") == 1) return "container";
        if (b == BaseArmor) return "armor";
        int slots = EquipableSlots(data, b);
        if ((slots & (SlotNeck | SlotRings)) != 0) return "jewelry";
        if ((slots & (SlotHead | SlotArms | SlotCloak | SlotBelt | SlotBoots)) != 0) return "apparel";
        bool weapon = Int(data, "baseitems", b, "WeaponType") > 0 && (slots & SlotRightHand) != 0 && Int(data, "baseitems", b, "WeaponWield") != 9;
        return Int(data, "baseitems", b, "Category") switch
        {
            1 or 2 or 7 => "weapon",
            3 => "shield",
            6 => "ammo",
            8 => weapon ? "weapon" : "consumable",
            9 or 10 or 11 or 15 or 19 => "consumable",
            _ => "misc",
        };
    }

    /// <summary>
    /// An item property's text: its name (itempropdef GameStrRef), then the subtype, cost value and parameter value
    /// names from their tables.
    /// </summary>
    public static string PropertyText(INwnBridgeData data, ItemPropertyEntry p)
    {
        int type = p.Name;
        if (type >= data.TwoDARowCount("itempropdef")) return "";
        string Name(string table, int row) => table.Length > 0 ? Tlk(data, Int(data, table.ToLowerInvariant(), row, "Name", -1)) : "";
        int nameRef = Int(data, "itempropdef", type, "GameStrRef", -1);
        if (nameRef < 0) nameRef = Int(data, "itempropdef", type, "Name", -1);
        var parts = new List<string> { nameRef >= 0 ? Tlk(data, nameRef) : Cell(data, "itempropdef", type, "Label") };
        if (Cell(data, "itempropdef", type, "SubTypeResRef") is { Length: > 0 } sub) parts.Add(Name(sub, p.Subtype));
        int costTable = Int(data, "itempropdef", type, "CostTableResRef", -1);
        if (costTable > 0) parts.Add(Name(Cell(data, "iprp_costtable", costTable, "Name"), p.CostTableValue));
        int paramTable = Int(data, "itempropdef", type, "Param1ResRef", -1);
        if (paramTable >= 0 && p.Param1 != 255 && Cell(data, "iprp_paramtable", paramTable, "TableResRef") is { Length: > 0 } param)
            parts.Add(Name(param, p.Param1));
        return string.Join(" ", parts.Where(s => s.Length > 0));
    }
}
