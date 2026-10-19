// Fire up close: a brazier, a candelabra and a standing torch on a dim castle floor, the camera low and near,
// to compare how bright and big the flames and their glow are.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    VPlaceable(oPC, "plc_brazier", -2.0, 2.0);
    VPlaceable(oPC, "plc_candelabra", 0.0, 2.5);
    VPlaceable(oPC, "plc_freetorch", 2.0, 2.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 5.0, 75.0));
    DelayCommand(5.0, VShot("close"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(9.0, VShot("far"));
    DelayCommand(11.0, VDone());
}
