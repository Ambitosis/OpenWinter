// Placeable light summing: three braziers side by side (and none on the right), to compare with placelight's
// single brazier: does the original add overlapping lights or cap them?
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    VPlaceable(oPC, "plc_brazier", -5.0, 0.0);
    VPlaceable(oPC, "plc_brazier", -5.0, 0.6);
    VPlaceable(oPC, "plc_brazier", -5.0, -0.6);
    DelayCommand(3.0, VCamera(oPC, 90.0, 18.0, 1.0));
    DelayCommand(5.0, VShot("top"));
    DelayCommand(7.0, VDone());
}
