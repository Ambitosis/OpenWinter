using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Items: possession, inventory, equipment, gold, stacks, charges, flags, copying and item actions.</summary>
static class ItemFunctions
{
    const int BaseItemInvalid = 256;

    static InventorySystem Inv(Call c)
    {
        var inv = c.World.Inventory;
        inv.EnsureLoaded();
        return inv;
    }

    static uint IdOf(GameObject? o) => o?.Id ?? GameObject.InvalidId;

    static void Enqueue(Call c, GameAction a)
    {
        if (c.Self is { Commandable: true } o) o.Actions.Add(a);
    }

    // ----- possession and iteration -----

    [NwFunc("GetItemPossessor")]
    static object? Possessor(Call c) =>
        c.Obj<Item>(0) is { } item ? IdOf(Inv(c).Possessor(item, c.I(1) != 0)) : GameObject.InvalidId;

    [NwFunc("GetItemPossessedBy")]
    static object? PossessedBy(Call c) =>
        c.Obj(0) is { } o && InventorySystem.InventoryList(o) != null ? IdOf(Inv(c).FindByTag(o, c.S(1))) : GameObject.InvalidId;

    [NwFunc("GetFirstItemInInventory")]
    static object? FirstItem(Call c) =>
        c.Obj(0) is { } o && InventorySystem.InventoryList(o) != null ? IdOf(Inv(c).FirstItem(o)) : GameObject.InvalidId;

    [NwFunc("GetNextItemInInventory")]
    static object? NextItem(Call c) =>
        c.Obj(0) is { } o && InventorySystem.InventoryList(o) != null ? IdOf(Inv(c).NextItem(o)) : GameObject.InvalidId;

    [NwFunc("GetHasInventory")] static object? HasInventory(Call c) => Inv(c).HasInventory(c.Obj(0)) ? 1 : 0;

    [NwFunc("GetItemInSlot")]
    static object? InSlot(Call c)
    {
        int slot = c.I(0);
        if (c.Obj(1) is not Creature cr || slot is < 0 or > 17) return GameObject.InvalidId;
        Inv(c).Load(cr);
        return cr.Equipment.TryGetValue(EquipSlot.Bit(slot), out var item) && !item.IsDestroyed ? item.Id : GameObject.InvalidId;
    }

    [NwFunc("CreateItemOnObject")]
    static object? CreateOn(Call c)
    {
        var target = c.Obj(1);
        if (target == null) return GameObject.InvalidId;
        return IdOf(Inv(c).CreateOn(c.S(0), target, c.I(2, 1), c.S(3)));
    }

    [NwFunc("GetBaseItemFitsInInventory")]
    static object? Fits(Call c)
    {
        int baseItem = c.I(0);
        var target = c.Obj(1);
        var inv = Inv(c);
        if (target == null || baseItem < 0 || !inv.HasInventory(target)) return 0;
        inv.Load(target);
        return inv.BaseItemFits(target, baseItem) ? 1 : 0;
    }

    // ----- item actions -----

    [NwFunc("ActionEquipItem")]
    static object? ActionEquip(Call c)
    {
        Enqueue(c, new EquipItemAction { ItemId = c.O(0), Slot = c.I(1) });
        return null;
    }
    [NwFunc("ActionUnequipItem")] static object? ActionUnequip(Call c) { Enqueue(c, new UnequipItemAction { ItemId = c.O(0) }); return null; }
    [NwFunc("ActionPickUpItem")] static object? ActionPickUp(Call c) { Enqueue(c, new PickUpItemAction { ItemId = c.O(0) }); return null; }
    [NwFunc("ActionPutDownItem")] static object? ActionPutDown(Call c) { Enqueue(c, new PutDownItemAction { ItemId = c.O(0) }); return null; }
    [NwFunc("ActionGiveItem")]
    static object? ActionGive(Call c)
    {
        if (c.Obj<Item>(0) != null && c.Obj(1) != null) Enqueue(c, new TransferItemAction { ItemId = c.O(0), Other = c.O(1) });
        return null;
    }
    [NwFunc("ActionTakeItem")]
    static object? ActionTake(Call c)
    {
        if (c.Obj<Item>(0) != null && c.Obj(1) != null) Enqueue(c, new TransferItemAction { ItemId = c.O(0), Other = c.O(1), Take = true });
        return null;
    }

    [NwFunc("ActionUseItemOnObject")]
    static object? UseOnObject(Call c)
    {
        Enqueue(c, new UseItemAction { ItemId = c.O(0), Property = c.IP(1), Target = c.O(2), SubPropertyIndex = c.I(3), DecrementCharges = c.I(4, 1) != 0 });
        return null;
    }
    [NwFunc("ActionUseItemAtLocation")]
    static object? UseAtLocation(Call c)
    {
        Enqueue(c, new UseItemAction { ItemId = c.O(0), Property = c.IP(1), TargetLocation = c.L(2), SubPropertyIndex = c.I(3), DecrementCharges = c.I(4, 1) != 0 });
        return null;
    }

    static IEnumerable<Item> Carried(Call c, Creature cr) => Inv(c).AllItems(cr).Where(i => !i.IsDestroyed);

    /// <summary>Queues the equips <see cref="MostDamagingWeapon"/> chooses (parity suite equip_mostdamaging).</summary>
    static void EquipBest(Call c, List<(Item Item, int Slot)> choice)
    {
        foreach (var (item, slot) in choice) Enqueue(c, new EquipItemAction { ItemId = item.Id, Slot = slot });
    }

    [NwFunc("ActionEquipMostDamagingMelee")]
    static object? EquipMelee(Call c)
    {
        if (c.Self is Creature cr) EquipBest(c, MostDamagingWeapon.Melee(c.World, cr, c.Obj(0), c.I(1) != 0));
        return null;
    }
    [NwFunc("ActionEquipMostDamagingRanged")]
    static object? EquipRanged(Call c)
    {
        if (c.Self is Creature cr) EquipBest(c, MostDamagingWeapon.Ranged(c.World, cr, c.Obj(0)));
        return null;
    }
    [NwFunc("ActionEquipMostEffectiveArmor")]
    static object? EquipArmor(Call c)
    {
        if (c.Self is not Creature cr) return null;
        var rules = Inv(c).Rules;
        var best = Carried(c, cr).Where(i => i.BaseItem == ItemRules.BaseItemArmor && i.Identified).OrderByDescending(rules.ACValue).FirstOrDefault();
        if (best == null) return null;
        cr.Equipment.TryGetValue(EquipSlot.Chest, out var current);
        if (current != best && (current == null || rules.ACValue(best) > rules.ACValue(current)))
            Enqueue(c, new EquipItemAction { ItemId = best.Id, Slot = 1 });
        return null;
    }

    // ----- equip event data -----

    static uint EvObj(Call c, string key) => c.World.Get(c.Event.Obj(key)) != null ? c.Event.Obj(key) : GameObject.InvalidId;
    [NwFunc("GetPCItemLastEquipped")] static object? LastEquipped(Call c) => EvObj(c, "EquippedItem");
    [NwFunc("GetPCItemLastEquippedBy")] static object? LastEquippedBy(Call c) => EvObj(c, "EquippedBy");
    [NwFunc("GetPCItemLastUnequipped")] static object? LastUnequipped(Call c) => EvObj(c, "UnequippedItem");
    [NwFunc("GetPCItemLastUnequippedBy")] static object? LastUnequippedBy(Call c) => EvObj(c, "UnequippedBy");
    [NwFunc("GetPCItemLastEquippedSlot")]
    static object? LastEquippedSlot(Call c) =>
        c.Event.Values.TryGetValue("EquippedSlot", out var v) && v is int i ? i : -1;
    [NwFunc("GetPCItemLastUnequippedSlot")]
    static object? LastUnequippedSlot(Call c) =>
        c.Event.Values.TryGetValue("UnequippedSlot", out var v) && v is int i ? i : -1;
    // Set by the spell system; inside DelayCommand the event is gone, so fall back to the caster's spell context.
    [NwFunc("GetSpellCastItem")]
    static object? SpellCastItem(Call c) =>
        EvObj(c, "SpellCastItem") is var id && id != GameObject.InvalidId ? id
        : c.Self?.LastSpell?.CastItem is { } item && c.World.Get(item) != null ? item : GameObject.InvalidId;

    // ----- gold -----

    [NwFunc("GetGold")] static object? Gold(Call c) => c.Obj(0) is Creature cr ? cr.Gold : 0;

    [NwFunc("GiveGoldToCreature")]
    static object? GiveGold(Call c)
    {
        if (c.Obj<Creature>(0) is { } cr && c.I(1) > 0)
        {
            bool added = Inv(c).ChangeGold(cr, c.I(1));
            // "Acquired 925GP" (dialog.tlk 62489; dying scene chat log).
            if (added && cr.IsPC) c.World.Output?.SendMessage(cr, c.World.TlkStringOverridden(62489).Replace("<CUSTOM0>", c.I(1).ToString()));
        }
        return null;
    }

    [NwFunc("TakeGoldFromCreature")]
    static object? TakeGold(Call c)
    {
        int amount = c.I(0);
        if (c.Obj<Creature>(1) is not { } from || amount <= 0) return null;
        int taken = Math.Min(amount, from.Gold);
        var inv = Inv(c);
        // Taken by a creature, the gold raises NWNX_ON_INVENTORY_REMOVE_GOLD (and ADD_GOLD on the taker); taken by
        // anything else, neither (nwnx_events_items). A skip keeps the gold where it was.
        if (c.Self is not Creature) inv.AddGold(from, -taken);
        else if (!inv.ChangeGold(from, -taken)) return null;
        // "Lost 100GP" (dialog.tlk 62490): the stock respawn's 10% penalty (dyingfx chat log).
        if (taken > 0 && from.IsPC) c.World.Output?.SendMessage(from, c.World.TlkStringOverridden(62490).Replace("<CUSTOM0>", taken.ToString()));
        if (c.I(2) == 0 && c.Self is Creature taker && taker != from) inv.ChangeGold(taker, taken);
        return null;
    }

    // ----- value and rules data -----

    [NwFunc("GetGoldPieceValue")] static object? Value(Call c) => c.Obj<Item>(0) is { } item ? Inv(c).Rules.GoldValue(item) : 0;
    [NwFunc("GetBaseItemType")] static object? BaseType(Call c) => c.Obj<Item>(0)?.BaseItem ?? BaseItemInvalid;
    [NwFunc("GetItemACValue")] static object? AC(Call c) => c.Obj<Item>(0) is { } item ? Inv(c).Rules.ACValue(item) : 0;
    [NwFunc("GetWeaponRanged")] static object? Ranged(Call c) => c.Obj<Item>(0) is { } item && Inv(c).Rules.IsRangedWeapon(item.BaseItem) ? 1 : 0;

    [NwFunc("GetIsWeaponEffective")]
    static object? WeaponEffective(Call c)
    {
        if (c.Obj(0) is not { } versus) return 1;
        var rules = Inv(c).Rules;
        int enhancement = 0;
        if (c.Self is Creature cr && cr.Equipment.TryGetValue(c.I(1) != 0 ? EquipSlot.LeftHand : EquipSlot.RightHand, out var weapon))
            enhancement = rules.Enhancement(weapon);
        // DAMAGE_POWER_* puts ENERGY (6) between +5 and +6.
        int power = enhancement <= 5 ? enhancement : enhancement + 1;
        int required = 0;
        foreach (var e in versus.Effects)
            if (e.Type == 7) required = Math.Max(required, e.Ints[1]); // EFFECT_TYPE_DAMAGE_REDUCTION(amount, power)
        if (versus is Creature vc)
            foreach (var item in vc.Equipment.Values)
                foreach (var p in item.Properties)
                    if (p.Type == 22) required = Math.Max(required, p.SubType < 5 ? p.SubType + 1 : p.SubType + 2); // DAMAGE_REDUCTION: subtype 0 = +1
        // TODO(combat): racial/feat damage reduction and damage immunities.
        return required == 0 || power >= required ? 1 : 0;
    }

    // ----- stacks and charges -----

    [NwFunc("GetNumStackedItems")] static object? NumStacked(Call c) => c.Obj<Item>(0)?.StackSize ?? 0;
    [NwFunc("GetItemStackSize")] static object? StackSize(Call c) => c.Obj<Item>(0)?.StackSize ?? 0;
    [NwFunc("SetItemStackSize")]
    static object? SetStackSize(Call c)
    {
        if (c.Obj<Item>(0) is { } item)
        {
            item.StackSize = Math.Clamp(c.I(1), 1, Inv(c).Rules.MaxStack(item.BaseItem));
            Inv(c).Changed(item);
        }
        return null;
    }
    [NwFunc("GetItemCharges")] static object? Charges(Call c) => c.Obj<Item>(0)?.Charges ?? 0;
    [NwFunc("SetItemCharges")]
    static object? SetCharges(Call c)
    {
        if (c.Obj<Item>(0) is { } item) Inv(c).SetCharges(item, c.I(1));
        return null;
    }

    [NwFunc("SetItemPropertyUsesPerDayRemaining")]
    static object? SetUses(Call c)
    {
        // The item's own property (or the variable that was added), capped at the cost table's uses; negative
        // values wrap to a byte as in the original.
        if (c.Obj<Item>(0) is not { } item) return null;
        var ip = c.IP(1);
        var prop = item.Properties.FirstOrDefault(p => p.Id == ip.Id);
        if (prop == null) return null;
        int max = InventorySystem.MaxUsesPerDay(prop);
        if (max > 0)
        {
            prop.UsesPerDay = c.I(2) < 0 ? c.I(2) & 0xFF : Math.Min(c.I(2), max);
            Inv(c).Changed(item);
        }
        return null;
    }

    // ----- flags -----

    [NwFunc("GetIdentified")] static object? Identified(Call c) => c.Obj<Item>(0)?.Identified == true ? 1 : 0;
    [NwFunc("SetIdentified")]
    static object? SetIdentified(Call c)
    {
        if (c.Obj<Item>(0) is { } i) { i.Identified = c.I(1) != 0; i.Reprice(); }
        return null;
    }
    [NwFunc("GetDroppableFlag")] static object? Droppable(Call c) => c.Obj<Item>(0)?.Droppable == true ? 1 : 0;
    [NwFunc("SetDroppableFlag")] static object? SetDroppable(Call c) { if (c.Obj<Item>(0) is { } i) i.Droppable = c.I(1) != 0; return null; }
    [NwFunc("GetStolenFlag")] static object? Stolen(Call c) => c.Obj<Item>(0)?.Stolen == true ? 1 : 0;
    [NwFunc("SetStolenFlag")] static object? SetStolen(Call c) { if (c.Obj<Item>(0) is { } i) i.Stolen = c.I(1) != 0; return null; }
    [NwFunc("GetItemCursedFlag")] static object? Cursed(Call c) => c.Obj<Item>(0)?.Cursed == true ? 1 : 0;
    [NwFunc("SetItemCursedFlag")] static object? SetCursed(Call c) { if (c.Obj<Item>(0) is { } i) i.Cursed = c.I(1) != 0; return null; }
    [NwFunc("GetPickpocketableFlag")] static object? Pickpocketable(Call c) => c.Obj<Item>(0)?.Pickpocketable == true ? 1 : 0;
    [NwFunc("SetPickpocketableFlag")] static object? SetPickpocketable(Call c) { if (c.Obj<Item>(0) is { } i) i.Pickpocketable = c.I(1) != 0; return null; }
    [NwFunc("GetInfiniteFlag")] static object? Infinite(Call c) => c.Obj<Item>(0)?.Infinite == true ? 1 : 0;
    [NwFunc("SetInfiniteFlag")] static object? SetInfinite(Call c) { if (c.Obj<Item>(0) is { } i) i.Infinite = c.I(1) != 0; return null; }

    [NwFunc("GetHiddenWhenEquipped")] static object? HiddenWhenEquipped(Call c) => c.Obj<Item>(0)?.HiddenWhenEquipped == true ? 1 : 0;
    [NwFunc("SetHiddenWhenEquipped")]
    static object? SetHiddenWhenEquipped(Call c)
    {
        if (c.Obj<Item>(0) is not { } item) return null;
        item.HiddenWhenEquipped = c.I(1) != 0;
        var inv = Inv(c);
        if (inv.Holder(item) is Creature cr && InventorySystem.SlotOf(cr, item) is var slot and >= 0) inv.EquipmentChanged(cr, item, slot);
        return null;
    }

    [NwFunc("GetUseableFlag")]
    static object? Useable(Call c) => c.Obj(0) switch
    {
        Placeable p => p.Useable ? 1 : 0,
        Item i => i.UseableOnGround ? 1 : 0,
        Creature or Door => Inv(c).UseableOverrides.TryGetValue(c.Obj(0)!.Id, out var u) ? (u ? 1 : 0) : 1,
        _ => 0,
    };
    [NwFunc("SetUseableFlag")]
    static object? SetUseable(Call c)
    {
        bool v = c.I(1) != 0;
        switch (c.Obj(0))
        {
            case Placeable { Static: false } p: p.Useable = v; c.World.MarkChanged(p); break;
            case Item i: i.UseableOnGround = v; break;
            case Creature or Door: Inv(c).UseableOverrides[c.O(0)] = v; c.World.MarkChanged(c.Obj(0)!); break;
        }
        return null;
    }

    // ----- copying and appearance -----

    [NwFunc("CopyItem")]
    static object? CopyItem(Call c) =>
        c.Obj<Item>(0) is { } item ? IdOf(Inv(c).CopyItem(item, c.Obj(1), c.I(2) != 0)) : GameObject.InvalidId;

    [NwFunc("CopyItemAndModify")]
    static object? CopyAndModify(Call c) =>
        c.Obj<Item>(0) is { } item ? IdOf(Inv(c).CopyAndModify(item, c.I(1), c.I(2), c.I(3), c.I(4) != 0)) : GameObject.InvalidId;

    [NwFunc("GetItemAppearance")] static object? Appearance(Call c) => c.Obj<Item>(0) is { } item ? Inv(c).GetAppearance(item, c.I(1), c.I(2)) : -1;

    [NwFunc("CopyObject")]
    static object? CopyObject(Call c) =>
        c.Obj(0) is { } src and not Areas.Area and not Module ? IdOf(Inv(c).CopyObject(src, c.L(1), c.Obj(2), ObjectFunctions.CleanTag(c.S(3)), c.I(4) != 0)) : GameObject.InvalidId;

    // ----- player UI -----

    [NwFunc("OpenInventory")]
    static object? OpenInventory(Call c)
    {
        if (c.Obj<Creature>(0) is not { } target || c.Obj<Creature>(1) is not { IsPC: true } pc) return null;
        // TODO(player): permission rules (DMs any creature; players themselves and their associates).
        c.World.Output?.Notify("inventory-open", target, Inv(c).InventoryView(target), pc);
        return null;
    }

    [NwFunc("StoreCameraFacing")]
    static object? StoreCamera(Call c)
    {
        if (c.Self is Creature { IsPC: true } pc) c.World.Output?.Notify("camera-store", pc, new { }, pc);
        return null;
    }
}
