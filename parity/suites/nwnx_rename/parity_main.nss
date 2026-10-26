// NWNX_Rename on a creature that isn't a player (nwserver has no players here).
#include "parity_inc"
#include "nwnx_rename"

void main()
{
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    PLog("get.none", NWNX_Rename_GetPCNameOverride(oC));
    NWNX_Rename_SetPCNameOverride(oC, "Bob", "<", ">");
    PLog("get.npc", NWNX_Rename_GetPCNameOverride(oC));
    PLog("name.npc", GetName(oC));
    NWNX_Rename_ClearPCNameOverride(oC);
    PLog("get.cleared", NWNX_Rename_GetPCNameOverride(oC));
    NWNX_Rename_ClearPCNameOverride(OBJECT_INVALID, OBJECT_INVALID, TRUE);
    PLog("get.invalid", NWNX_Rename_GetPCNameOverride(OBJECT_INVALID));
    PDone();
}
