using OpenWinter.Aurora.Formats;
using OpenWinter.Content;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Persistence;

/// <summary>Live quickbar item bindings and their character-file representation.</summary>
public static class QuickbarPersistence
{
    public static void BindItems(Creature character)
    {
        if (BicQuickbar.Read(character.Template) is not { } entries) return;
        var items = Items(character.Equipment.Values.Concat(character.Inventory)).ToList();
        for (int i = 0; i < entries.Length; i++)
        {
            if (items.FirstOrDefault(item => ReferenceEquals(item.Template, entries[i].ItemTemplate)) is { } item)
                character.QuickbarItems[i] = item;
            if (items.FirstOrDefault(item => ReferenceEquals(item.Template, entries[i].SecondaryItemTemplate)) is { } secondary)
                character.QuickbarSecondaryItems[i] = secondary;
        }
    }

    static IEnumerable<Item> Items(IEnumerable<Item> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var inner in Items(item.Inventory)) yield return inner;
        }
    }

    /// <summary>Called before serializing inventories, so referenced items already have their saved UUIDs.</summary>
    public static void Write(World world, Creature character, GffStruct bic)
    {
        var slots = Requests.QuickbarRequests.Read(world, character, _ => { });
        if (slots == null) return;
        var original = BicQuickbar.Read(character.Template) ?? [];
        if (character.QuickbarEdits.Count > 0) BicQuickbar.MarkSaved(bic);
        for (int i = 0; i < slots.Length; i++)
        {
            bool edited = character.QuickbarEdits.ContainsKey(i);
            var slot = slots[i];
            // Unchanged unknown entries remain byte-for-byte GFF fields from the original template.
            bool originalItem = i < original.Length && (original[i].SavedAction?.Kind == "item"
                || original[i] is { Type: 1, UnsupportedItemOptions: false });
            if (!edited && !originalItem) continue;
            if (!edited && !character.QuickbarItems.ContainsKey(i) && !character.QuickbarSecondaryItems.ContainsKey(i)) continue; // originally unresolved, not a removed item
            var location = ItemLocation(character, i, slot);
            var secondary = slot?.Paired == true ? ItemLocation(character, i, slot, secondary: true) : null;
            if (!edited && original[i].SavedAction == null && location != null
                && BicQuickbar.MatchesItem(character.Template.GetList("QBList")[i], location, secondary)) continue;
            if (location != null)
                location = location with { Uuid = world.System<PersistenceSystem>().GetUuid(character.QuickbarItems[i]) };
            if (secondary != null)
                secondary = secondary with { Uuid = world.System<PersistenceSystem>().GetUuid(character.QuickbarSecondaryItems[i]) };
            int cls = slot == null ? -1 : character.Classes.FindIndex(c => c.Class == slot.Class);
            if (slot is { Kind: "spell", Class: -1 })
                cls = character.Classes.FindIndex(c => c.KnownSpells.Any(l => l.Contains(slot.Id))
                    || c.Memorized.Any(l => l.Any(s => s.Spell == slot.Id)));
            BicQuickbar.Set(bic, i, BicQuickbar.Write(slot, cls, location, secondary));
        }
    }

    static BicQuickbarItem? ItemLocation(Creature character, int index, QuickbarSlot? slot, bool secondary = false)
    {
        var bindings = secondary ? character.QuickbarSecondaryItems : character.QuickbarItems;
        if (slot?.Kind != "item" || !bindings.TryGetValue(index, out var item) || item.IsDestroyed) return null;
        var equipment = Rules.EffectSystem.OwnEquipment(character);
        if (!equipment.Any(kv => kv.Value == item) && !Items(character.Inventory).Contains(item)) return null;
        uint equip = equipment.FirstOrDefault(kv => kv.Value == item).Key is > 0 and var bit ? (uint)bit : uint.MaxValue;
        int prop = secondary ? -1 : slot.Prop;
        if (prop >= 0)
        {
            if (prop >= item.Properties.Count) return null;
            var selected = item.Properties[prop];
            prop = item.Properties.Where(p => p.Type == 15).Concat(item.Properties.Where(p => p.Type != 15)).ToList().IndexOf(selected);
        }
        return new(equip, Math.Max(0, item.InvX), Math.Max(0, item.InvY), prop,
            "", equip == uint.MaxValue && !character.Inventory.Contains(item));
    }
}
