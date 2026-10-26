// NWNX_Events item events: inventory add/remove, acquire, equip and its validation, unequip, gold, give, destroy,
// stack decrements, item use, item property effects (whitelisted), and skipping.
#include "pnwnxev_inc"

object C() { return GetObjectByTag("GOB_C"); }
object T() { return GetObjectByTag("GOB_T"); }

void Watch()
{
    EvWatch("NWNX_ON_INVENTORY_ADD_ITEM", "o:ITEM");
    EvWatch("NWNX_ON_INVENTORY_REMOVE_ITEM", "o:ITEM");
    EvWatch("NWNX_ON_ITEM_ACQUIRE", "o:ITEM,o:GIVER,RESULT");
    EvWatch("NWNX_ON_VALIDATE_ITEM_EQUIP", "o:ITEM_OBJECT_ID,SLOT,BEFORE_RESULT");
    EvWatch("NWNX_ON_ITEM_EQUIP", "o:ITEM,SLOT");
    EvWatch("NWNX_ON_ITEM_UNEQUIP", "o:ITEM");
    EvWatch("NWNX_ON_INVENTORY_ADD_GOLD", "GOLD");
    EvWatch("NWNX_ON_INVENTORY_REMOVE_GOLD", "GOLD");
    EvWatch("NWNX_ON_ITEM_DESTROY_OBJECT", "");
    EvWatch("NWNX_ON_ITEM_DECREMENT_STACKSIZE", "");
    EvWatch("NWNX_ON_VALIDATE_USE_ITEM", "o:ITEM_OBJECT_ID,BEFORE_RESULT");
    EvWatch("NWNX_ON_USE_ITEM", "o:ITEM_OBJECT_ID,o:TARGET_OBJECT_ID,ITEM_PROPERTY_INDEX,ITEM_SUB_PROPERTY_INDEX,TARGET_POSITION_X,TARGET_POSITION_Z,USE_CHARGES");
    EvWatch("NWNX_ON_ITEMPROPERTY_EFFECT_APPLIED", "o:CREATURE,LOADING_GAME,INVENTORY_SLOT,PROPERTY,x:ID,SUBTYPE,TAG,COST_TABLE,COST_TABLE_VALUE,PARAM1,PARAM1_VALUE");
    EvWatch("NWNX_ON_ITEMPROPERTY_EFFECT_REMOVED", "o:CREATURE,LOADING_GAME,INVENTORY_SLOT,PROPERTY,x:ID,SUBTYPE,TAG,COST_TABLE,COST_TABLE_VALUE,PARAM1,PARAM1_VALUE");
    EvWatch("NWNX_ON_ITEM_MERGE", "o:ITEM_TO_MERGE_INTO,o:ITEM_TO_MERGE");
    EvWatch("NWNX_ON_ITEM_SPLIT", "o:ITEM,NUMBER_SPLIT_OFF");
    EvWatch("NWNX_ON_CAST_SPELL", "SPELL_ID,o:TARGET_OBJECT_ID,o:ITEM_OBJECT_ID,MULTI_CLASS,SPELL_COUNTERED,COUNTERING_SPELL,PROJECTILE_PATH_TYPE,IS_INSTANT_SPELL");
    EvWatch("NWNX_ON_BROADCAST_CAST_SPELL", "SPELL_ID,MULTI_CLASS,FEAT,o:TARGET_OBJECT_ID,SPELL_DOMAIN,SPELL_SPONTANEOUS,SPELL_METAMAGIC,PROJECTILE_PATH_TYPE");
    EvWatch("NWNX_ON_EFFECT_APPLIED", "TYPE");
    EvWatch("NWNX_ON_HEAL", "o:TARGET_OBJECT_ID,x:HEAL_AMOUNT");
    EvWatch("NWNX_ON_DECREMENT_SPELL_COUNT", "SPELL_ID,CLASS,DOMAIN,METAMAGIC,CASTERLEVEL");
}

void Step1()
{
    EvLog("create");
    object oS = CreateItemOnObject("nw_wblcl001", C(), 1, "SWORD");
    CreateItemOnObject("nw_wswls001", C(), 1, "LONG");
    EvLog("chest");
    CreateItemOnObject("nw_it_mpotion001", GetObjectByTag("CHEST"), 1, "CPOT");
    EvLog("arrows");
    CreateItemOnObject("nw_wamar001", C(), 10, "ARR");
    CreateItemOnObject("nw_wamar001", C(), 5, "ARR");
    EvLog("equip");
    AssignCommand(C(), ActionEquipItem(oS, INVENTORY_SLOT_RIGHTHAND));
    EvLog("queued");
}

void Step2()
{
    EvFlush("s1");
    EvLog("unequip");
    AssignCommand(C(), ActionUnequipItem(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, C())));
}

void Step3()
{
    EvFlush("s2");
    EvLog("gold");
    GiveGoldToCreature(C(), 50);
    EvLog("take");
    TakeGoldFromCreature(20, C(), TRUE);
    EvLog("take2");
    AssignCommand(T(), TakeGoldFromCreature(10, C(), FALSE));
    EvLog("take3");
    TakeGoldFromCreature(3, C(), FALSE);
    EvLog("take4");
    AssignCommand(T(), TakeGoldFromCreature(4, C(), TRUE));
    EvLog("give");
    AssignCommand(C(), ActionGiveItem(GetItemPossessedBy(C(), "SWORD"), T()));
}

void Step4()
{
    EvFlush("s3");
    EvLog("copy");
    CopyItem(GetItemPossessedBy(T(), "SWORD"), C(), TRUE);
    EvLog("destroy");
    DestroyObject(GetItemPossessedBy(T(), "SWORD"));
    EvLog("stack");
    SetItemStackSize(GetItemPossessedBy(C(), "ARR"), 3);
    EvLog("bag");
    object oBag = CreateItemOnObject("nw_it_contain006", C(), 1, "BAG");
    CreateItemOnObject("nw_it_mpotion001", oBag, 1, "BAGPOT");
    EvLog("done");
}

void Step5()
{
    EvFlush("s4");
    // Item property effects: an AC bonus is whitelisted, the attack bonus is not.
    NWNX_Events_AddIDToWhitelist("NWNX_ON_ITEMPROPERTY_EFFECT", ITEM_PROPERTY_AC_BONUS);
    object oS = GetItemPossessedBy(C(), "SWORD");
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyACBonus(2), oS);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyAttackBonus(1), oS);
    EvLog("equip");
    AssignCommand(C(), ActionEquipItem(oS, INVENTORY_SLOT_RIGHTHAND));
}

void Step6()
{
    EvFlush("s5");
    PLogI("s5.ac", GetAC(C()));
    EvLog("unequip");
    AssignCommand(C(), ActionUnequipItem(GetItemPossessedBy(C(), "SWORD")));
}

void Step7()
{
    EvFlush("s6");
    PLogI("s6.ac", GetAC(C()));
    EvLog("potion");
    object oP = CreateItemOnObject("nw_it_mpotion001", C(), 2, "POT");
    object oQ = CreateItemOnObject("nw_it_mpotion001", T(), 1, "POTT");
    SetCurrentHitPoints(C(), 1);
    AssignCommand(C(), ActionUseItemOnObject(oP, GetFirstItemProperty(oP), C()));
    AssignCommand(T(), ActionUseItemOnObject(oQ, GetFirstItemProperty(oQ), T()));
}

void Step8()
{
    EvFlush("s7");
    PLogI("s7.hp", GetCurrentHitPoints(C()));
    PLogI("s7.stack", GetItemStackSize(GetItemPossessedBy(C(), "POT")));
    // Skips: the equip itself, then its validation with a result.
    EvSkip("NWNX_ON_ITEM_EQUIP_BEFORE");
    EvLog("equipskip");
    AssignCommand(C(), ActionEquipItem(GetItemPossessedBy(C(), "SWORD"), INVENTORY_SLOT_RIGHTHAND));
}

void Step9()
{
    EvSkip("NWNX_ON_ITEM_EQUIP_BEFORE", FALSE);
    EvFlush("s8");
    PLog("s8.right", EvObj(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, C())));
    EvSkip("NWNX_ON_VALIDATE_ITEM_EQUIP_BEFORE");
    EvResult("NWNX_ON_VALIDATE_ITEM_EQUIP_BEFORE", "0");
    EvLog("validate0");
    AssignCommand(C(), ActionEquipItem(GetItemPossessedBy(C(), "SWORD"), INVENTORY_SLOT_RIGHTHAND));
}

void Step10()
{
    EvFlush("s9");
    PLog("s9.right", EvObj(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, C())));
    // Validation forced to 1: a sword into the chest slot.
    EvResult("NWNX_ON_VALIDATE_ITEM_EQUIP_BEFORE", "1");
    EvLog("validate1");
    AssignCommand(C(), ActionEquipItem(GetItemPossessedBy(C(), "SWORD"), INVENTORY_SLOT_CHEST));
}

void Step11()
{
    EvSkip("NWNX_ON_VALIDATE_ITEM_EQUIP_BEFORE", FALSE);
    EvResult("NWNX_ON_VALIDATE_ITEM_EQUIP_BEFORE", "");
    EvFlush("s10");
    PLog("s10.chest", EvObj(GetItemInSlot(INVENTORY_SLOT_CHEST, C())));
    // Skipped gold and inventory additions.
    EvSkip("NWNX_ON_INVENTORY_ADD_GOLD_BEFORE");
    int g = GetGold(C());
    GiveGoldToCreature(C(), 7);
    PLogI("s11.gold", GetGold(C()) - g);
    EvSkip("NWNX_ON_INVENTORY_ADD_GOLD_BEFORE", FALSE);
    EvSkip("NWNX_ON_INVENTORY_ADD_ITEM_BEFORE");
    object oX = CreateItemOnObject("nw_it_mpotion001", T(), 1, "SKIPPOT");
    EvSkip("NWNX_ON_INVENTORY_ADD_ITEM_BEFORE", FALSE);
    PLog("s11.skippot", EvObj(GetItemPossessor(oX)));
    PLogI("s11.skippotvalid", GetIsObjectValid(oX));
    EvSkip("NWNX_ON_ITEM_ACQUIRE_BEFORE");
    object oY = CreateItemOnObject("nw_it_mpotion001", T(), 1, "ACQPOT");
    EvSkip("NWNX_ON_ITEM_ACQUIRE_BEFORE", FALSE);
    PLog("s11.acqpot", EvObj(GetItemPossessor(oY)));
    PLogI("s11.acqpotvalid", GetIsObjectValid(oY));
    EvSkip("NWNX_ON_INVENTORY_ADD_ITEM_BEFORE");
    object oZ = CreateItemOnObject("nw_it_mpotion001", GetObjectByTag("CHEST"), 1, "CHPOT");
    EvSkip("NWNX_ON_INVENTORY_ADD_ITEM_BEFORE", FALSE);
    PLog("s11.chpot", EvObj(GetItemPossessor(oZ)));
    PLogI("s11.chpotvalid", GetIsObjectValid(oZ));
    EvSkip("NWNX_ON_ITEM_DESTROY_OBJECT_BEFORE");
    DestroyObject(GetItemPossessedBy(C(), "LONG"));
    DelayCommand(0.2, EvSkip("NWNX_ON_ITEM_DESTROY_OBJECT_BEFORE", FALSE));
    DelayCommand(0.3, PLogI("s11.long", GetIsObjectValid(GetItemPossessedBy(C(), "LONG"))));
    EvFlush("s11");
}

void StepTake()
{
    EvFlush("s11b");
    EvLog("take");
    AssignCommand(T(), ActionTakeItem(GetItemPossessedBy(GetObjectByTag("CHEST"), "CPOT"), GetObjectByTag("CHEST")));
}

void main()
{
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(), FALSE, "GOB_C");
    object oT = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(1.5), FALSE, "GOB_T");
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(0.0, 3.0), FALSE, "CHEST");
    SetAILevel(oC, AI_LEVEL_NORMAL);
    SetAILevel(oT, AI_LEVEL_NORMAL);
    // Clear what the blueprint carries, before watching.
    object oI = GetFirstItemInInventory(oC);
    while (GetIsObjectValid(oI)) { DestroyObject(oI); oI = GetNextItemInInventory(oC); }
    int i;
    for (i = 0; i < NUM_INVENTORY_SLOTS; i++) DestroyObject(GetItemInSlot(i, oC));
    for (i = 0; i < NUM_INVENTORY_SLOTS; i++) DestroyObject(GetItemInSlot(i, oT));
    DelayCommand(0.5, Watch());
    DelayCommand(1.0, Step1());
    DelayCommand(3.0, Step2());
    DelayCommand(5.0, Step3());
    DelayCommand(8.0, Step4());
    DelayCommand(9.0, Step5());
    DelayCommand(11.0, Step6());
    DelayCommand(13.0, Step7());
    DelayCommand(17.0, Step8());
    DelayCommand(19.0, Step9());
    DelayCommand(21.0, Step10());
    DelayCommand(23.0, Step11());
    DelayCommand(24.0, StepTake());
    DelayCommand(29.0, EvFlush("s12"));
    DelayCommand(29.5, PDone());
}
