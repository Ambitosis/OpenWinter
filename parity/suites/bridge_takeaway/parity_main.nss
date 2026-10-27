// A test bed for what a server takes away, over the NWN bridge, not a comparison: bt_enter equips the entering
// player, and bt_chat takes things away (experience, abilities, feats, spells, items, effects, associates, windows)
// on chat commands. A chest, a store, a barrel, a talking commoner and a map pin are placed here. It reports done at once.
#include "parity_inc"

void main()
{
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(3.0, 2.0), FALSE, "BT_CHEST");
    CreateItemOnObject("nw_wswls001", oChest, 1, "BT_CHESTSWORD");
    CreateItemOnObject("nw_it_mpotion001", oChest, 3, "BT_CHESTPOTION");
    object oStore = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", PCentre(-3.0, 2.0), FALSE, "BT_STORE");
    SetStoreGold(oStore, 5000);
    CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", PCentre(0.0, 3.0), FALSE, "BT_TALKER");
    SetUseableFlag(CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", PCentre(-3.0, -2.0), FALSE, "BT_BARREL"), TRUE);
    PDone();
}
