// Placeable light calibration: a dark castle floor (no tile lights, no ambient) lit only by a brazier and a
// lamp post, whose lights come from placeables.2da (LightColor and offset), seen from above and the side.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    VPlaceable(oPC, "plc_brazier", -5.0, 0.0);
    VPlaceable(oPC, "plc_lamppost", 6.0, 0.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 18.0, 1.0));
    DelayCommand(5.0, VShot("top"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(9.0, VShot("side"));
    DelayCommand(11.0, VDone());
}
