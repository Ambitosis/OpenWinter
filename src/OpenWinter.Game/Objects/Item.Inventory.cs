namespace OpenWinter.Game.Objects;

/// <summary>Item state used by the inventory and store systems (flags that aren't in Objects.cs).</summary>
public sealed partial class Item
{
    /// <summary>Store items that stay in stock after being bought (UTM/GIT "Infinite").</summary>
    public bool Infinite;
    /// <summary>Ammunition the engine gave a spawned NPC for its launcher; it goes when the launcher does.</summary>
    public bool DefaultAmmo;
    /// <summary>SetHiddenWhenEquipped: the item's mesh is not drawn while equipped (helmets, cloaks...).</summary>
    public bool HiddenWhenEquipped;
    /// <summary>SetUseableFlag on items: whether it can be interacted with while on the ground.</summary>
    public bool UseableOnGround = true;
    /// <summary>Store category (UTM StoreList struct id: 0 armour, 1 misc, 2 potions, 3 rings, 4 weapons), -1 if not in a store.</summary>
    public int StorePanel = -1;
    /// <summary>
    /// Per-part armour colour overrides (CopyItemAndModify with nIndex past ITEM_APPR_ARMOR_NUM_COLORS),
    /// keyed by part * 6 + colour channel.
    /// </summary>
    public readonly Dictionary<int, int> PartColorOverrides = new();
    /// <summary>
    /// Charges when the value was last worked out. The original works the value out lazily: creating, copying,
    /// adding or removing properties and identifying only mark it stale, and the next read (GetGoldPieceValue, a
    /// store's price) fixes it with the charges of that moment. SetItemCharges doesn't mark it stale, so charges
    /// set after a read keep the old value (items_value_charges). Null: stale, the next read uses the current charges.
    /// </summary>
    public int? PricedCharges;

    /// <summary>Marks the value stale: the next read prices charge-based spells with the charges at that moment.</summary>
    public void Reprice() { PricedCharges = null; HeldValue = null; }

    /// <summary>
    /// RemoveItemProperty reprices only after the running script: until <see cref="HeldAt"/> (world time) passes,
    /// GetGoldPieceValue still reports the value from before the removal.
    /// </summary>
    public int? HeldValue;
    public double HeldAt;
    /// <summary>Properties removed at <see cref="HeldAt"/>; they still count when stacks are compared.</summary>
    public List<Scripting.ItemProperty>? RemovedProperties;
}

public sealed partial class Placeable
{
    /// <summary>The template's ItemList has been turned into items.</summary>
    public bool InventoryLoaded;
}

public sealed partial class Store
{
    /// <summary>Pricing and the categorised inventory have been read from the template.</summary>
    public bool InventoryLoaded;
    /// <summary>Native category page counts retain their high-water mark as stock is removed.</summary>
    public readonly int[] CategoryPages = [1, 1, 1, 1, 1];
    public bool BlackMarket;
    /// <summary>Mark down for stolen goods at a black market.</summary>
    public int BlackMarketMarkDown = 25;
}
