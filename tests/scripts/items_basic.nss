// Inventory conformance (run with OBJECT_SELF = a creature); results go to module locals for ItemsTests.
void main()
{
    object m = GetModule();
    object oPC = OBJECT_SELF;
    object oChest = GetObjectByTag("test_chest");

    // Creating and stacking.
    object a1 = CreateItemOnObject("nw_wamar001", oPC, 10);
    object a2 = CreateItemOnObject("nw_wamar001", oPC, 5);
    SetLocalInt(m, "merged", a1 == a2);
    SetLocalInt(m, "stack", GetItemStackSize(a1));
    SetLocalInt(m, "possessor", GetItemPossessor(a1) == oPC);
    SetLocalObject(m, "arrows", a1);

    object sw = CreateItemOnObject("nw_wswls001", oPC, 1, "my_sword");
    SetLocalInt(m, "marked_set", 1);
    SetLocalInt(sw, "marked", 7);
    SetLocalObject(m, "sword", sw);
    SetLocalInt(m, "tag", GetTag(sw) == "my_sword");
    SetLocalInt(m, "bytag", GetItemPossessedBy(oPC, "MY_SWORD") == sw);
    SetItemStackSize(sw, 5);
    SetLocalInt(m, "swstack", GetItemStackSize(sw));
    SetItemStackSize(a1, 500);
    SetLocalInt(m, "arrowmax", GetItemStackSize(a1));
    SetLocalInt(m, "value", GetGoldPieceValue(sw));
    SetLocalInt(m, "base", GetBaseItemType(sw));
    SetLocalInt(m, "baseinvalid", GetBaseItemType(OBJECT_INVALID));
    SetLocalInt(m, "ranged", GetWeaponRanged(sw) * 10 + GetWeaponRanged(CreateItemOnObject("nw_wbwln001", oPC)));

    // Iteration includes container contents after the container.
    object bag = CreateItemOnObject("nw_it_contain001", oPC);
    object gem = CreateItemOnObject("nw_it_mpotion001", bag);
    SetLocalInt(m, "gem_in_bag", GetItemPossessor(gem) == oPC);
    SetLocalInt(m, "gem_bag", GetItemPossessor(gem, TRUE) == bag);
    SetLocalInt(m, "hasinv", GetHasInventory(bag) * 100 + GetHasInventory(oPC) * 10 + GetHasInventory(sw));
    int n = 0;
    object it = GetFirstItemInInventory(oPC);
    while (GetIsObjectValid(it)) { n++; it = GetNextItemInInventory(oPC); }
    SetLocalInt(m, "count", n);
    SetLocalInt(m, "bagcount", GetIsObjectValid(GetFirstItemInInventory(bag)) + GetIsObjectValid(GetNextItemInInventory(bag)));
    SetLocalInt(m, "bag_in_bag", GetIsObjectValid(CreateItemOnObject("nw_it_contain001", bag)));

    // Gold.
    GiveGoldToCreature(oPC, 100);
    TakeGoldFromCreature(30, oPC, TRUE);
    SetLocalInt(m, "gold", GetGold(oPC));

    // Flags.
    SetIdentified(sw, FALSE);
    SetStolenFlag(sw, TRUE);
    SetDroppableFlag(sw, FALSE);
    SetItemCursedFlag(sw, TRUE);
    SetInfiniteFlag(sw, TRUE);
    SetLocalInt(m, "flags", GetIdentified(sw) + GetStolenFlag(sw) * 10 + GetDroppableFlag(sw) * 100 + GetItemCursedFlag(sw) * 1000 + GetInfiniteFlag(sw) * 10000);
    SetIdentified(sw, TRUE);
    SetStolenFlag(sw, FALSE);
    SetItemCursedFlag(sw, FALSE);
    SetInfiniteFlag(sw, FALSE);

    // Copies: into the chest, with and without locals.
    object cp = CopyItem(sw, oChest, TRUE);
    SetLocalInt(m, "copy_in_chest", GetItemPossessor(cp) == oChest);
    SetLocalInt(m, "copy_var", GetLocalInt(cp, "marked"));
    object cp2 = CopyItem(sw, oChest, FALSE);
    SetLocalInt(m, "copy2_var", GetLocalInt(cp2, "marked"));
    SetLocalInt(m, "chestcount", GetIsObjectValid(GetFirstItemInInventory(oChest)) + GetIsObjectValid(GetNextItemInInventory(oChest)));

    // Appearance.
    object recol = CopyItemAndModify(sw, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_TOP, 3);
    SetLocalInt(m, "recolor", GetItemAppearance(recol, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_TOP));
    SetLocalInt(m, "recolor_model", GetItemAppearance(recol, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP) == GetItemAppearance(sw, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP));
    object armored = CopyItemAndModify(sw, ITEM_APPR_TYPE_ARMOR_MODEL, 0, 1);
    SetLocalInt(m, "badmodify", GetIsObjectValid(armored));
    DestroyObject(recol);
    DestroyObject(armored);

    // Equipping is an action: queued now, done on the next tick.
    ActionEquipItem(sw, INVENTORY_SLOT_RIGHTHAND);
    ActionEquipItem(a1, INVENTORY_SLOT_HEAD); // not allowed in that slot
    ActionGiveItem(a1, oChest);
}
