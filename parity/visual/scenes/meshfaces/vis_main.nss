#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc); VHidePC(pc);
    object actor = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    object item = GetFirstItemInInventory(actor);
    while (GetIsObjectValid(item)) { DestroyObject(item); item = GetNextItemInInventory(actor); }
    SetCreatureAppearanceType(actor, 31);
    WriteTimestampedLogEntry("VISUAL|note|appearance." + IntToString(GetAppearanceType(actor)) + ".race." + Get2DAString("appearance", "RACE", 31));
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    DelayCommand(3.0, VShot("front_back"));
    DelayCommand(5.0, VDone());
}
