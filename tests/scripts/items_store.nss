// Store functions (OBJECT_SELF = a creature); results go to module locals for ItemsTests.
void main()
{
    object m = GetModule();
    object oStore = GetObjectByTag("test_store");
    SetLocalInt(m, "sgold", GetStoreGold(oStore));
    SetLocalInt(m, "smaxbuy", GetStoreMaxBuyPrice(oStore));
    SetLocalInt(m, "sident", GetStoreIdentifyCost(oStore));
    SetLocalInt(m, "snotstore", GetStoreGold(OBJECT_SELF));
    SetStoreGold(oStore, 1000);
    SetStoreMaxBuyPrice(oStore, 500);
    SetStoreIdentifyCost(oStore, 25);
    SetLocalInt(m, "sset", GetStoreGold(oStore) + GetStoreMaxBuyPrice(oStore) + GetStoreIdentifyCost(oStore));
    int n = 0;
    object it = GetFirstItemInInventory(oStore);
    while (GetIsObjectValid(it)) { n++; it = GetNextItemInInventory(oStore); }
    SetLocalInt(m, "sitems", n);
    OpenStore(oStore, OBJECT_SELF, 10, -5);
}
