#include "pinterarea_inc"
// Logs the first block; opens the door when the walker has OPEN_ON_BLOCK (as the default nw_c2_defaulte does).
void main()
{
    // The pursuit's fight is not compared (OpenWinter's attacker reacts sooner: docs/INTER_AREA_MOVEMENT.md).
    if (GetLocalInt(OBJECT_SELF, "BT_FIGHT")) return;
    object d = GetBlockingDoor();
    IAFirst(OBJECT_SELF, "blocked", GetTag(d) + " action " + IntToString(GetCurrentAction()));
    if (GetLocalInt(OBJECT_SELF, "OPEN_ON_BLOCK") && GetIsDoorActionPossible(d, DOOR_ACTION_OPEN)) DoDoorAction(d, DOOR_ACTION_OPEN);
}
