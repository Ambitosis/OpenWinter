// Lighting calibration: the bare ground (no grass, player hidden) under known sun ambient and
// diffuse colours (see the variants), from straight above and from the side.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    DelayCommand(3.0, VCamera(oPC, 90.0, 8.0, 1.0));
    DelayCommand(5.0, VShot("top"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(9.0, VShot("side"));
    DelayCommand(11.0, VDone());
}
