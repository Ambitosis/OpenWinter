// The game's own MTRs: ice tiles (tti01: TTI01_IceFloor, TTI01_IceClear) and the old man's and old woman's heads
// (appearances 239 and 240: oldm_head, oldf_head), from above, at an angle and up close.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object oMan = VSpawn(oPC, "nw_bandit001", -0.5, 1.2, 90.0);
    object oWoman = VSpawn(oPC, "nw_bandit001", 0.5, 1.2, 90.0);
    SetCreatureAppearanceType(oMan, 239);
    SetCreatureAppearanceType(oWoman, 240);
    DelayCommand(3.0, VCamera(oPC, 90.0, 14.0, 1.0));
    DelayCommand(5.0, VShot("ice_above"));
    DelayCommand(6.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(8.0, VShot("ice_side"));
    DelayCommand(9.0, VCamera(oPC, 270.0, 12.0, 70.0));
    DelayCommand(11.0, VShot("ice_back"));
    DelayCommand(12.0, VCamera(oPC, 270.0, 3.0, 80.0));
    DelayCommand(14.0, VShot("heads"));
    DelayCommand(16.0, VDone());
}
