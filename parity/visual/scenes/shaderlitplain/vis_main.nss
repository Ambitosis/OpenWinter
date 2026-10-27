// The area of scene "shaderlit" without its shader: the ground with its plain material.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 1.0));
    DelayCommand(5.0, VShot("std"));
    DelayCommand(6.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(8.0, VShot("std_side"));
    DelayCommand(10.0, VDone());
}
