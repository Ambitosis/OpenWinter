// Tile light calibration: a castle floor lit only by the tiles' main lights (lightcolor.2da rows in the
// variants), seen from above, to compare how bright the lights are and how far they reach.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    DelayCommand(3.0, VCamera(oPC, 90.0, 18.0, 1.0));
    DelayCommand(5.0, VShot("top"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(9.0, VShot("side"));
    DelayCommand(11.0, VDone());
}
