using System.Globalization;
using System.Text;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Item (parity suite nwnx_item).</summary>
public static class ItemPlugin
{
    const string P = "Item";

    /// <summary>The weight of one item of the stack, in tenths of a pound, until the item is saved and loaded.</summary>
    [NwnxFunc(P, "SetWeight")]
    static void SetWeight(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        int weight = n.PopInt();
        if (item == null) return;
        item.WeightOverride = Math.Max(0, weight);
        n.World.Inventory.Changed(item);
    }

    /// <summary>The value of one item before additional value; it is worked out again when the item is identified or its properties change.</summary>
    [NwnxFunc(P, "SetBaseGoldPieceValue")]
    static void SetBaseValue(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        int gold = n.PopInt();
        if (item != null) item.BaseValueOverride = Math.Max(0, gold);
    }

    [NwnxFunc(P, "GetBaseGoldPieceValue")]
    static void GetBaseValue(NwnxCall n) =>
        n.Push(n.PopObject<Item>() is { } item ? item.BaseValueOverride ?? (int)n.World.Inventory.Rules.UnitValue(item, item.Identified) : 0);

    [NwnxFunc(P, "SetAddGoldPieceValue")]
    static void SetAddValue(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        int gold = n.PopInt();
        if (item != null) item.AddCost = gold;
    }

    [NwnxFunc(P, "GetAddGoldPieceValue")] static void GetAddValue(NwnxCall n) => n.Push(n.PopObject<Item>()?.AddCost ?? 0);

    [NwnxFunc(P, "SetBaseItemType")]
    static void SetBaseItem(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        int type = n.PopInt();
        if (item == null) return;
        item.BaseItem = type;
        item.Reprice();
        n.World.MarkChanged(item);
    }

    /// <summary>Base armour class: armour by its torso, shields by baseitems.2da; -1 for no item.</summary>
    [NwnxFunc(P, "GetBaseArmorClass")]
    static void GetBaseAC(NwnxCall n) => n.Push(n.PopObject<Item>() is { } item ? n.World.Inventory.Rules.BaseAC(item) : -1);

    // ---------------------------------------------------------------- appearance

    const int Colors = 6, Parts = 19, ModelParts = 3;

    /// <summary>
    /// The whole appearance as hex, as the original writes it: six colours (two digits each), the three model parts
    /// and the nineteen armour parts (four digits each), then the per-part colours, channel by channel, part by part
    /// (two digits, FF for none). "" for no item.
    /// </summary>
    public static string EntireAppearance(Item item)
    {
        var t = item.Template;
        var sb = new StringBuilder(328);
        foreach (var field in InventorySystem.ArmorColorFields) sb.Append(((int)t.GetInt(field) & 0xFF).ToString("X2", CultureInfo.InvariantCulture));
        for (int i = 1; i <= ModelParts; i++) sb.Append(((int)CreatureFactory.Wide(t, "ModelPart" + i) & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture));
        foreach (var field in InventorySystem.ArmorModelFields) sb.Append(((int)CreatureFactory.Wide(t, field) & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture));
        for (int c = 0; c < Colors; c++)
            for (int p = 0; p < Parts; p++)
                sb.Append((item.PartColorOverrides.TryGetValue(p * Colors + c, out int v) ? v & 0xFF : 0xFF).ToString("X2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    [NwnxFunc(P, "GetEntireItemAppearance")]
    static void GetEntire(NwnxCall n) => n.Push(n.PopObject<Item>() is { } item ? EntireAppearance(item) : "");

    /// <summary>Sets every part from a string GetEntireItemAppearance made; anything else leaves the item as it was.</summary>
    [NwnxFunc(P, "RestoreItemAppearance")]
    static void Restore(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        var text = n.PopString();
        if (item == null || text.Length != Colors * 2 + (ModelParts + Parts) * 4 + Colors * Parts * 2) return;
        if (!text.All(Uri.IsHexDigit)) return;
        var t = item.Template;
        int at = 0;
        int Next(int digits) { int v = int.Parse(text.AsSpan(at, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture); at += digits; return v; }
        foreach (var field in InventorySystem.ArmorColorFields) t.SetByte(field, (byte)Next(2));
        for (int i = 1; i <= ModelParts; i++) SetWide(t, "ModelPart" + i, Next(4));
        foreach (var field in InventorySystem.ArmorModelFields) SetWide(t, field, Next(4));
        item.PartColorOverrides.Clear();
        for (int c = 0; c < Colors; c++)
            for (int p = 0; p < Parts; p++)
                if (Next(2) is var v and not 0xFF) item.PartColorOverrides[p * Colors + c] = v;
        n.World.MarkChanged(item);
    }

    static void SetWide(Aurora.Formats.GffStruct t, string field, int value)
    {
        t.SetByte(field, (byte)Math.Min(value, 255));
        if (value > 255 || t.Has("x" + field)) t.SetWord("x" + field, value);
    }

    /// <summary>One change, as CopyItemAndModify makes it, to the item itself; optionally shown on its wearer at once.</summary>
    [NwnxFunc(P, "SetItemAppearance")]
    static void SetAppearance(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        int type = n.PopInt(), index = n.PopInt(), value = n.PopInt();
        bool update = n.PopInt() != 0;
        if (item == null || !n.World.Inventory.SetAppearance(item, type, index, value)) return;
        n.World.MarkChanged(item);
        if (update && n.World.Inventory.Owner(item) is Creature c && c.Equipment.ContainsValue(item)) n.World.MarkChanged(c);
    }

    // ---------------------------------------------------------------- level to equip

    const string LevelOverride = "NWNX_Item!MINIMUM_EQUIP_LEVEL_OVERRIDE", LevelModifier = "NWNX_Item!MINIMUM_EQUIP_LEVEL_MODIFIER";

    static int Setting(World w, Item item, string key) =>
        w.Systems.OfType<NwnxRegistry>().FirstOrDefault() is { } r && NwnxPersistence.Get(r, item.Id, key) is { Value: int v } ? v : 0;

    /// <summary>
    /// The level an item needs (item level restriction): its override if set, else the first itemvalue.2da row
    /// whose MAXSINGLEITEMVALUE covers its value, plus its modifier, kept to a byte as the original does.
    /// </summary>
    public static int MinEquipLevel(World w, Item item)
    {
        int level = Setting(w, item, LevelOverride);
        if (level <= 0)
        {
            var table = w.Resources.TwoDA("itemvalue");
            int value = w.Inventory.Rules.GoldValue(item);
            level = Math.Max(1, table.RowCount);
            for (int row = 0; row < table.RowCount; row++)
                if (table.GetInt(row, "MAXSINGLEITEMVALUE", int.MaxValue) >= value) { level = row + 1; break; }
        }
        return (level + Setting(w, item, LevelModifier)) & 0xFF;
    }

    [NwnxFunc(P, "GetMinEquipLevel")] static void GetMinLevel(NwnxCall n) => n.Push(n.PopObject<Item>() is { } item ? MinEquipLevel(n.World, item) : 0);
    [NwnxFunc(P, "GetMinEquipLevelModifier")] static void GetModifier(NwnxCall n) => n.Push(n.PopObject<Item>() is { } item ? Setting(n.World, item, LevelModifier) : 0);
    [NwnxFunc(P, "GetMinEquipLevelOverride")] static void GetOverride(NwnxCall n) => n.Push(n.PopObject<Item>() is { } item ? Setting(n.World, item, LevelOverride) : 0);
    [NwnxFunc(P, "SetMinEquipLevelModifier")] static void SetModifier(NwnxCall n) => SetLevel(n, LevelModifier);
    [NwnxFunc(P, "SetMinEquipLevelOverride")] static void SetOverride(NwnxCall n) => SetLevel(n, LevelOverride);

    static void SetLevel(NwnxCall n, string key)
    {
        var item = n.PopObject<Item>();
        int value = n.PopInt();
        bool persist = n.PopInt() != 0;
        if (item != null) NwnxPersistence.Set(n.Registry, item.Id, key, NwnxValue.Int(value), persist);
    }

    // ---------------------------------------------------------------- moving

    /// <summary>Moves an item into a creature, placeable, store or container (not a container into another, nor into itself).</summary>
    [NwnxFunc(P, "MoveTo")]
    static void MoveTo(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        var target = n.PopObject();
        n.PopInt(); // bHideAllFeedback: no feedback is sent for these moves
        if (item == null || target == null || target == item || !n.World.Inventory.HasInventory(target)) { n.Push(0); return; }
        if (target is Item && n.World.Inventory.HasInventory(item)) { n.Push(0); return; }
        n.World.LiftFromArea(item);
        n.Push(n.World.Inventory.MoveTo(item, target, merge: false, force: true) != null);
    }
}
