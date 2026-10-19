// Camera calibration: creatures on a grid around the player, seen from a few camera settings.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VSpawn(oPC, "nw_dog", 4.0, 0.0, 180.0);
    VSpawn(oPC, "nw_dog", -4.0, 0.0, 0.0);
    VSpawn(oPC, "nw_dog", 0.0, 4.0, 270.0);
    VSpawn(oPC, "nw_dog", 0.0, -4.0, 90.0);
    VSpawn(oPC, "nw_dog", 8.0, 8.0, 225.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 10.0, 60.0));
    DelayCommand(6.0, VShot("d10_p60"));
    DelayCommand(8.0, VCamera(oPC, 90.0, 5.0, 75.0));
    DelayCommand(10.0, VShot("d5_p75"));
    DelayCommand(12.0, VCamera(oPC, 90.0, 20.0, 30.0));
    DelayCommand(14.0, VShot("d20_p30"));
    DelayCommand(16.0, VDone());
}
