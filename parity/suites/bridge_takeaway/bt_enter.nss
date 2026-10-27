// Equips the entering player once: the experience for sixth level (15000), gold, a weapon, armour, a helmet, stacks,
// a wand, a ring, a bag with a potion inside, and a lasting effect to take away later.
void Give(object oPC, string sResRef, string sTag, int nStack = 1)
{
    CreateItemOnObject(sResRef, oPC, nStack, sTag);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC) || GetLocalInt(oPC, "BT_GIVEN")) return;
    SetLocalInt(oPC, "BT_GIVEN", TRUE);
    if (GetXP(oPC) < 15000) DelayCommand(3.0, SetXP(oPC, 15000));
    if (GetItemPossessedBy(oPC, "BT_SWORD") != OBJECT_INVALID) return;
    GiveGoldToCreature(oPC, 1000);
    object oSword = CreateItemOnObject("nw_wswls001", oPC, 1, "BT_SWORD");
    object oHelm = CreateItemOnObject("nw_arhe001", oPC, 1, "BT_HELM");
    Give(oPC, "nw_aarcl004", "BT_ARMOR");
    Give(oPC, "nw_it_mpotion001", "BT_POTION", 5);
    Give(oPC, "nw_wamar001", "BT_ARROW", 40);
    Give(oPC, "nw_wmgwn004", "BT_WAND");
    Give(oPC, "nw_it_mring001", "BT_RING");
    Give(oPC, "nw_wswdg001", "BT_DAGGER");
    object oBag = CreateItemOnObject("nw_it_contain001", oPC, 1, "BT_BAG");
    CreateItemOnObject("nw_it_mpotion002", oBag, 2, "BT_BAGPOTION");
    AssignCommand(oPC, ActionEquipItem(oSword, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(oPC, ActionEquipItem(oHelm, INVENTORY_SLOT_HEAD));
}
