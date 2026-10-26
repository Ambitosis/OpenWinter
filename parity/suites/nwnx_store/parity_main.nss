// NWNX_Store: mark up, mark down, black market, restrictions and customers, and how they change prices.
#include "parity_inc"
#include "nwnx_store"

void main()
{
    object oStore = CreateObject(OBJECT_TYPE_STORE, "nw_storemagic001", PCentre(), FALSE, "ST_STORE");
    object oWp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "ST_WP");
    PLog("start", IntToString(NWNX_Store_GetMarkUp(oStore)) + "/" + IntToString(NWNX_Store_GetMarkDown(oStore)) + "/"
        + IntToString(NWNX_Store_GetBlackMarket(oStore)) + "/" + IntToString(NWNX_Store_GetBlackMarketMarkDown(oStore)));
    NWNX_Store_SetMarkUp(oStore, 120);
    NWNX_Store_SetMarkDown(oStore, 60);
    NWNX_Store_SetBlackMarket(oStore, TRUE);
    NWNX_Store_SetBlackMarketMarkDown(oStore, 15);
    PLog("set", IntToString(NWNX_Store_GetMarkUp(oStore)) + "/" + IntToString(NWNX_Store_GetMarkDown(oStore)) + "/"
        + IntToString(NWNX_Store_GetBlackMarket(oStore)) + "/" + IntToString(NWNX_Store_GetBlackMarketMarkDown(oStore)));
    NWNX_Store_SetBlackMarket(oStore, 5);
    PLogI("blackmarket.five", NWNX_Store_GetBlackMarket(oStore));
    PLog("invalid", IntToString(NWNX_Store_GetMarkUp(oWp)) + "/" + IntToString(NWNX_Store_GetMarkDown(oWp)) + "/"
        + IntToString(NWNX_Store_GetBlackMarket(oWp)) + "/" + IntToString(NWNX_Store_GetBlackMarketMarkDown(oWp)) + "/"
        + IntToString(NWNX_Store_GetCurrentCustomersCount(oWp)) + "/" + IntToString(NWNX_Store_GetIsRestrictedBuyItem(oWp, BASE_ITEM_LONGSWORD)));
    PLogI("customers", NWNX_Store_GetCurrentCustomersCount(oStore));
    PLog("restricted", IntToString(NWNX_Store_GetIsRestrictedBuyItem(oStore, BASE_ITEM_LONGSWORD)) + "/" + IntToString(NWNX_Store_GetIsRestrictedBuyItem(oStore, BASE_ITEM_POTIONS)));
    PLogI("gold", GetStoreGold(oStore));
    PDone();
}
