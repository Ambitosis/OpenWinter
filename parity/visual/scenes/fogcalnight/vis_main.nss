// Fog calibration: red fog of each amount (see the variants), the ground seen from straight above at
// growing distances, and a low view towards the horizon.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    DelayCommand(3.0, VCamera(oPC, 90.0, 5.0, 1.0));
    DelayCommand(5.0, VShot("d05"));
    DelayCommand(6.0, VCamera(oPC, 90.0, 10.0, 1.0));
    DelayCommand(8.0, VShot("d10"));
    DelayCommand(9.0, VCamera(oPC, 90.0, 20.0, 1.0));
    DelayCommand(11.0, VShot("d20"));
    DelayCommand(12.0, VCamera(oPC, 90.0, 35.0, 1.0));
    DelayCommand(14.0, VShot("d35"));
    DelayCommand(15.0, VCamera(oPC, 90.0, 10.0, 80.0));
    DelayCommand(17.0, VShot("low"));
    DelayCommand(19.0, VDone());
}
