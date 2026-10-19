using System.Text.Json;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// A QBList entry. ItemTemplate refers to the embedded item in this same BIC. Label and Command are a text macro's
/// (QBObjectType 18: QBCommandLabel and QBCommandLine).
/// </summary>
public sealed record BicQuickbarEntry(int Type, int Parameter, int Class, int Meta,
    GffStruct? ItemTemplate, int Property, bool UnsupportedItemOptions, QuickbarSlot? SavedAction,
    GffStruct? SecondaryItemTemplate = null, bool Paired = false, string Label = "", string Command = "");

/// <summary>A live item's location and property index in the BIC being written.</summary>
public sealed record BicQuickbarItem(uint EquipmentSlot, int X, int Y, int Property, string Uuid, bool InContainer = false);

/// <summary>Reads and writes the 36 positional quickbar entries in a character file.</summary>
public static class BicQuickbar
{
    public const int Slots = 36;

    /// <summary>Null means no saved QBList; an empty list is an intentionally empty bar.</summary>
    public static BicQuickbarEntry[]? Read(GffStruct character)
    {
        if (!character.Has("QBList")) return null;
        var classes = character.GetList("ClassList");
        return character.GetList("QBList").Take(Slots).Select(s =>
        {
            int cls = (int)s.GetInt("QBMultiClass", -1);
            int prop = (int)s.GetInt("QBCastPropIndex", 255);
            bool paired = s.Has("QBSecItemInvSlot");
            bool measuredPair = paired && s.GetInt("QBItemInvSlot") is 16 or uint.MaxValue
                && s.GetInt("QBSecItemInvSlot") is 32 or uint.MaxValue && prop == 255;
            // QBMultiClass indexes ClassList; it is not a classes.2da row.
            return new BicQuickbarEntry((int)s.GetInt("QBObjectType"), (int)s.GetInt("QBINTParam1", -1),
                cls >= 0 && cls < classes.Count ? (int)classes[cls].GetInt("Class", -1) : -1,
                (int)s.GetInt("QBMetaType", 255), FindItem(character, s), prop == 255 ? -1 : prop,
                paired && !measuredPair || s.GetInt("QBCastSubPropIdx", 255) != 255
                    || s.GetInt("QBContReposX", 255) != 255 || s.GetInt("QBContReposY", 255) != 255, SavedAction(s),
                FindSecondaryItem(character, s, measuredPair), paired || SavedAction(s)?.Paired == true,
                s.GetString("QBCommandLabel"), s.GetString("QBCommandLine"));
        }).ToArray();
    }

    static GffStruct? FindSecondaryItem(GffStruct character, GffStruct slot, bool measuredPair)
    {
        if (slot.GetString("ElySecItemUuid") is { Length: > 0 } uuid)
            return Items(character).FirstOrDefault(s => s.GetString("UUID") == uuid);
        if (!measuredPair) return null;
        return slot.GetInt("QBSecItemInvSlot") == uint.MaxValue
            ? At(character.GetList("ItemList"), (int)slot.GetInt("QBSecItemReposX", -1), (int)slot.GetInt("QBSecItemReposY", -1))
            : character.GetList("Equip_ItemList").FirstOrDefault(s => s.StructType == 32);
    }

    static GffStruct? FindItem(GffStruct character, GffStruct slot)
    {
        if (slot.GetString("ElyItemUuid") is { Length: > 0 } uuid)
            return Items(character).FirstOrDefault(s => s.GetString("UUID") == uuid);
        if (slot.GetInt("QBObjectType") != 1) return null;
        long equip = slot.GetInt("QBItemInvSlot", uint.MaxValue);
        if (equip != uint.MaxValue)
            return character.GetList("Equip_ItemList").FirstOrDefault(s => s.StructType == equip);
        return At(character.GetList("ItemList"), (int)slot.GetInt("QBItemReposX", -1), (int)slot.GetInt("QBItemReposY", -1));
    }

    static GffStruct? At(List<GffStruct> items, int x, int y) => x < 0 || y < 0 ? null
        : items.FirstOrDefault(s => s.GetInt("Repos_PosX", -1) == x && s.GetInt("Repos_Posy", -1) == y);

    static IEnumerable<GffStruct> Items(GffStruct owner)
    {
        foreach (var item in owner.GetList("Equip_ItemList").Concat(owner.GetList("ItemList")))
        {
            yield return item;
            foreach (var inner in Items(item)) yield return inner;
        }
    }

    static QuickbarSlot? SavedAction(GffStruct entry)
    {
        if (entry.GetString("ElySlot") is not { Length: > 0 } json) return null;
        try { return JsonSerializer.Deserialize<QuickbarSlot>(json); }
        catch (JsonException) { return null; }
    }

    public static bool HasSavedEdits(GffStruct character) => character.GetInt("ElyQBSaved") != 0;
    public static void MarkSaved(GffStruct character) => character.SetByte("ElyQBSaved", 1);

    /// <summary>The original's text macro entry type (quickbargaps: QBCommandLabel and QBCommandLine, CExoStrings).</summary>
    public const int TextMacro = 18;

    /// <summary>Leave an unchanged native shortcut and its unmodelled fields intact on export.</summary>
    public static bool MatchesItem(GffStruct entry, BicQuickbarItem item, BicQuickbarItem? secondary = null) => !item.InContainer
        && (secondary == null ? !entry.Has("QBSecItemInvSlot") : !secondary.InContainer
            && entry.GetInt("QBSecItemInvSlot", -1) == secondary.EquipmentSlot
            && (secondary.EquipmentSlot != uint.MaxValue || entry.GetInt("QBSecItemReposX", -1) == secondary.X
                && entry.GetInt("QBSecItemReposY", -1) == secondary.Y))
        && entry.GetInt("QBObjectType") == 1
        && entry.GetInt("QBItemInvSlot", uint.MaxValue) == item.EquipmentSlot
        && (item.EquipmentSlot != uint.MaxValue || entry.GetInt("QBItemReposX", -1) == item.X
            && entry.GetInt("QBItemReposY", -1) == item.Y)
        && entry.GetInt("QBCastPropIndex", 255) == (item.Property < 0 ? 255 : item.Property);

    /// <summary>Updates one entry without discarding the other (possibly unsupported) original entries.</summary>
    public static void Set(GffStruct character, int index, GffStruct entry)
    {
        if (index is < 0 or >= Slots) throw new ArgumentOutOfRangeException(nameof(index));
        var list = character.GetList("QBList");
        while (list.Count < Slots) list.Add(new GffStruct(0).SetByte("QBObjectType", 0));
        list[index] = entry;
        character.SetList("QBList", list);
    }

    /// <summary>
    /// Writes measured native forms. ElySlot retains OpenWinter's richer actions/selection semantics;
    /// actions without a measured native form appear empty in the original game.
    /// </summary>
    public static GffStruct Write(QuickbarSlot? slot, int classIndex = -1, BicQuickbarItem? item = null, BicQuickbarItem? secondary = null)
    {
        var s = new GffStruct(0).SetByte("QBObjectType", 0);
        if (slot == null || slot.Kind == "item" && item == null && !slot.Paired && (!slot.ItemHasPower || slot.ItemKey.Length == 0)) return s;
        var saved = slot.Copy();
        saved.Item = 0; // runtime IDs must never be stored in a BIC
        saved.SecondaryItem = 0;
        s.SetString("ElySlot", JsonSerializer.Serialize(saved));
        if (slot.Paired)
        {
            if (item != null) s.SetString("ElyItemUuid", item.Uuid);
            if (secondary != null) s.SetString("ElySecItemUuid", secondary.Uuid);
            // quickbarpairbind exports both members in the backpack, using independent repository coordinates.
            if (item is { InContainer: false, X: >= 0 and < 255, Y: >= 0 and < 255 }
                && secondary is { InContainer: false, X: >= 0 and < 255, Y: >= 0 and < 255 }
                && item.EquipmentSlot is 16 or uint.MaxValue && secondary.EquipmentSlot is 32 or uint.MaxValue)
                s.SetByte("QBObjectType", 1).SetDWord("QBItemInvSlot", item.EquipmentSlot).SetDWord("QBSecItemInvSlot", secondary.EquipmentSlot)
                    .SetByte("QBItemReposX", item.X).SetByte("QBItemReposY", item.Y).SetByte("QBSecItemReposX", secondary.X).SetByte("QBSecItemReposY", secondary.Y)
                    .SetByte("QBContReposX", 255).SetByte("QBContReposY", 255)
                    .SetByte("QBCastPropIndex", 255).SetByte("QBCastSubPropIdx", 255);
            return s;
        }
        switch (slot.Kind)
        {
            case "item" when item != null:
                s.SetString("ElyItemUuid", item.Uuid);
                if (!item.InContainer && item.X is >= 0 and < 255 && item.Y is >= 0 and < 255)
                    s.SetByte("QBObjectType", 1).SetDWord("QBItemInvSlot", item.EquipmentSlot)
                        .SetByte("QBItemReposX", item.X).SetByte("QBItemReposY", item.Y)
                        .SetByte("QBContReposX", 255).SetByte("QBContReposY", 255)
                        .SetByte("QBCastPropIndex", item.Property < 0 ? 255 : item.Property).SetByte("QBCastSubPropIdx", 255);
                // Item properties are sorted on export; the extension uses that same new index.
                saved.Prop = item.Property;
                s.SetString("ElySlot", JsonSerializer.Serialize(saved));
                break;
            case "spell" when classIndex >= 0:
                s.SetByte("QBObjectType", 2).SetInt("QBINTParam1", slot.Id).SetByte("QBMultiClass", classIndex)
                    .SetByte("QBMetaType", slot.Meta == 255 ? 0 : slot.Meta).SetByte("QBDomainLevel", 0);
                break;
            case "skill" when slot.Sub == 0:
                s.SetByte("QBObjectType", 3).SetInt("QBINTParam1", slot.Id);
                break;
            case "feat" when slot.Sub == 0:
                s.SetByte("QBObjectType", 4).SetInt("QBINTParam1", slot.Id);
                break;
            case "mode" when slot.Id is 0 or 1:
                s.SetByte("QBObjectType", 10).SetInt("QBINTParam1", slot.Id);
                break;
            case "mode" when slot.Id == 2: // Parry is saved as skill 10 in the inspected BICs.
                s.SetByte("QBObjectType", 3).SetInt("QBINTParam1", 10);
                break;
            case "emote" when Emotes.Find(slot.Arg) is { Id: >= 0 } emote:
                s.SetByte("QBObjectType", Emotes.QuickbarType).SetInt("QBINTParam1", emote.Id);
                break;
            case "macro":
                s.SetByte("QBObjectType", TextMacro).SetString("QBCommandLabel", slot.Name).SetString("QBCommandLine", slot.Text);
                break;
            case "command" when slot.Arg is "talk" or "attack" or "examine":
                s.SetByte("QBObjectType", slot.Arg == "talk" ? 6 : slot.Arg == "attack" ? 7 : 40);
                break;
        }
        return s;
    }
}
