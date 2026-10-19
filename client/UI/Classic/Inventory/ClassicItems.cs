using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The classic item windows of one game and what they share: the player's items (kept by the modern
/// <see cref="InventoryPanel"/>, which stays hidden and listens to the server), the item actions (use, equip,
/// drop, split, examine) with the same requests as the modern windows, and the right-click options.
/// </summary>
public sealed class ClassicItems
{
    public required ClassicContext Ctx;
    public ClassicInventoryWindow Inventory = null!;
    public ClassicStoreWindow Store = null!;
    public ClassicBarterWindow Barter = null!;
    public ClassicExamineItem Examine = null!;
    public ClassicStackSplit Split = null!;
    public ClassicConfirm Confirm = null!;

    public ClientSession Session => Ctx.Session;
    /// <summary>The player's items, gold and weight (the hidden modern inventory keeps them current).</summary>
    public InventoryPanel Data => Ctx.Inventory;
    public ClassicAssets Assets => Ctx.Assets;

    public static bool Fits(ItemEntry item, int slot) => item.Info is { } i && (i.Slots & (1 << slot)) != 0;

    static string PrimaryVerb(ItemEntry item) =>
        item.IsBag ? "open" : item.Info?.Usable == true && item.Info.Slots == 0 ? "use" : item.Info?.Slots > 0 ? "equip" : "examine";

    /// <summary>Double-click on a carried item: open bags, use consumables, equip equipment, otherwise examine.</summary>
    public void Primary(ItemEntry item)
    {
        if (item.Slot >= 0) { Session.Request("unequip", new { item = item.Id }); return; }
        switch (PrimaryVerb(item))
        {
            case "open": Inventory.OpenBag(item); break;
            case "use": Session.Request("item-use", new { item = item.Id }); break;
            case "equip": Session.Request("equip", new { item = item.Id }); break;
            default: ExamineItem(item.Id); break;
        }
    }

    public void ExamineItem(uint id) => Ctx.Examine.Examine(id);

    /// <summary>Drops an item on the ground (valuable ones ask first; plot items can't be dropped).</summary>
    public void Drop(ItemEntry item) => Drop(item, new { item = item.Id });

    public void DropOnWorld(ItemEntry item)
    {
        if (Session.Area is not { } area || Ctx.Interaction.PickGround(Kit.ItemDragging.Pointer(Inventory)) is not { } point) return;
        Drop(item, new { item = item.Id, area = area.Info.AreaId, position = new[] { point.X, point.Y, point.Z } });
    }

    void Drop(ItemEntry item, object request)
    {
        if (item.Info?.Plot == true) return;
        void Go() => Session.Request("drop", request);
        if (item.Info?.Value >= 1000) Confirm.Ask($"Drop {item.Name} on the ground?", Go);
        else Go();
    }

    /// <summary>
    /// Right-click on an item in the player's inventory, equipment or an open bag: the item's radial menu, whatever
    /// else is open (a container, a store or a bag). The original has no drop-down lists. With a store open its ring has
    /// Sell at E in place of Equip, on every item but a bag (measured, `storeradial`); dragging onto the store still sells
    /// at once. Putting into a container stays a drag.
    /// </summary>
    public void ShowMenu(ItemEntry item, Vector2 at) =>
        Data.Radial?.OpenInventory(item, at, Data, () => Inventory.IsOpen, Drop, Split.Show, Inventory.OpenBag,
            Store is { IsOpen: true } ? i => Store.Sell(i) : null, Store is { IsOpen: true } ? Store.CanSell : null);

    /// <summary>
    /// Right-click on an item the player doesn't own (in a container or a store): a radial menu of
    /// <paramref name="entries"/> (direction, label, icon, action; a null action is greyed), open while
    /// <paramref name="valid"/> holds.
    /// </summary>
    public void ShowForeignMenu(ItemEntry item, Vector2 at, Func<bool> valid, params (int Direction, string Label, string Icon, Action? Act)[] entries)
    {
        if (Data.Radial is not { } radial) return;
        radial.OpenItemMenu(item, at, valid, () => RadialMenu.FixedRing(entries.Select(e => (e.Direction,
            e.Icon.Length > 0
                ? RadialMenu.Icon(new RadialEntry { Label = e.Label, Glyph = "bag", Disabled = e.Act == null, Act = e.Act }, e.Icon)
                : new RadialEntry { Label = e.Label, Glyph = e.Label == "Examine" ? "eye" : "bag", Disabled = e.Act == null, Act = e.Act })).ToArray()));
    }
}
