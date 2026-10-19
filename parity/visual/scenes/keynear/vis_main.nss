// Keyholing near the player: the player close to walls of the keycurve spot (12.5,11 and 8.4,8.7: points the
// original accepts; it moves the player off unwalkable ones, OpenWinter does not). From the south-west walls hide the
// player; from the sides they stand beside the player, nearer the camera but not in the way.
#include "visual_inc"

void Spot(object oPC, float t, float fX, float fY, string sName)
{
    DelayCommand(t, VMovePC(oPC, fX, fY, 0.0, 45.0));
    DelayCommand(t + 0.8, VCamera(oPC, 45.0, 10.0, 70.0));
    DelayCommand(t + 2.2, VShot(sName + "_behind"));
    DelayCommand(t + 3.4, VCamera(oPC, 100.0, 10.0, 70.0));
    DelayCommand(t + 4.8, VShot(sName + "_side"));
    DelayCommand(t + 6.0, VCamera(oPC, 350.0, 10.0, 70.0));
    DelayCommand(t + 7.4, VShot(sName + "_other"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    Spot(oPC, t, 12.5, 11.0, "n35"); t += 9.0;
    Spot(oPC, t, 8.4, 8.7, "n11"); t += 9.0;
    DelayCommand(t, VDone());
}
