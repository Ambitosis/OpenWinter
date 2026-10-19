namespace OpenWinter.Client.Gameplay.Logic;

/// <summary>A command an item's radial menu can hold.</summary>
public enum ItemRingCommand { None, Examine, CastSpell, Open, Equip, Sell, Use, Drop, Unequip, EquipOffhand, Split, Buy }

/// <summary>What the item ring needs to know about one of the player's items.</summary>
/// <param name="Bag">A container item (it opens).</param>
/// <param name="Equipped">Worn or wielded (in an equipment slot).</param>
/// <param name="Equippable">Fits some equipment slot.</param>
/// <param name="Offhand">May also go in the left hand (the server's equipOffhand fact).</param>
/// <param name="Stack">Stack size.</param>
/// <param name="HasPowers">Has item powers (Use).</param>
/// <param name="StoreOpen">A store is open.</param>
public readonly record struct ItemRingFacts(bool Bag, bool Equipped, bool Equippable, bool Offhand, int Stack,
    bool HasPowers, bool StoreOpen);

/// <summary>
/// Where the original puts each command of an item's radial menu (slot 0 north, clockwise to 7 north-west), measured
/// on nwmain (visual scenes invitemring and storeradial). The player's items: Examine N,
/// Cast Spell NE, Use SE (its powers), Drop S always; E is Open for a bag, otherwise Sell while a store is open, otherwise
/// Equip for an unworn equippable item; W is Unequip on a worn item, Split on a stack, otherwise Equip offhand (never
/// with a store open). A store's ware: Examine N and Buy E only.
/// </summary>
public static class ItemRingLayout
{
    public static ItemRingCommand[] Owned(ItemRingFacts f)
    {
        var ring = new ItemRingCommand[8];
        ring[0] = ItemRingCommand.Examine;
        ring[1] = ItemRingCommand.CastSpell;
        ring[4] = ItemRingCommand.Drop;
        if (f.HasPowers) ring[3] = ItemRingCommand.Use;
        if (f.Bag) ring[2] = ItemRingCommand.Open;
        else if (f.StoreOpen) ring[2] = ItemRingCommand.Sell;
        else if (!f.Equipped && f.Equippable) ring[2] = ItemRingCommand.Equip;
        if (f.Equipped) ring[6] = ItemRingCommand.Unequip;
        else if (f.Stack > 1) ring[6] = ItemRingCommand.Split;
        else if (!f.StoreOpen && f.Equippable && f.Offhand) ring[6] = ItemRingCommand.EquipOffhand;
        return ring;
    }

    public static ItemRingCommand[] Ware()
    {
        var ring = new ItemRingCommand[8];
        ring[0] = ItemRingCommand.Examine;
        ring[2] = ItemRingCommand.Buy;
        return ring;
    }
}
