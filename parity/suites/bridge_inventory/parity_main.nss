// A test bed for joining over the NWN bridge, not a comparison: a chest with loot, a store opened by using a
// barrel, and a body bag. br_enter equips the player with a spread of items. It reports done at once.
#include "parity_inc"

void main()
{
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(3.0, 2.0), FALSE, "BR_CHEST");
    CreateItemOnObject("nw_wswls001", oChest);
    CreateItemOnObject("nw_wamar001", oChest, 25);
    CreateItemOnObject("nw_it_mpotion001", oChest, 3);
    CreateItemOnObject("nw_it_gold001", oChest, 120);
    object oRing = CreateItemOnObject("nw_it_mring020", oChest);
    SetIdentified(oRing, FALSE);

    object oStore = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", PCentre(-3.0, 2.0), FALSE, "BR_STORE");
    SetStoreGold(oStore, 5000);

    // A goblin dies with loot: its remains are a body bag.
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(0.0, -4.0), FALSE, "BR_GOBLIN");
    object oLoot = CreateItemOnObject("nw_wswdg001", oGob);
    SetDroppableFlag(oLoot, TRUE);
    SetLootable(oGob, FALSE);
    AssignCommand(oGob, SetIsDestroyable(TRUE, FALSE, FALSE));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), oGob));
    PDone();
}
