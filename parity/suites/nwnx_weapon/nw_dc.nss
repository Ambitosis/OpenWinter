// Devastating critical event script: what it is told, and bypassing for one group.
#include "pcombat_inc"
#include "nwnx_weapon"

void main()
{
    struct NWNX_Weapon_DevastatingCriticalEvent_Data d = NWNX_Weapon_GetDevastatingCriticalEventData();
    string g = GetLocalString(OBJECT_SELF, "G");
    PAdd(g + ".dcev");
    if (d.oWeapon == GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, OBJECT_SELF)) PAdd(g + ".dcweapon");
    if (d.oTarget == GetLocalObject(OBJECT_SELF, "T")) PAdd(g + ".dctarget");
    if (d.nDamage > 0) PAdd(g + ".dcdamage");
    if (d.nDamage == GetLocalInt(d.oTarget, "LASTBASE")) PAdd(g + ".dcbase");
    if (g == "dcbypass") NWNX_Weapon_BypassDevastatingCritical();
}
