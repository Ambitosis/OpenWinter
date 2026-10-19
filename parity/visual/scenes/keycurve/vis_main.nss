// Keyholing size against camera distance, finely: the player near the wall of the keyzoom spot, so it hides
// the player from 5 m out; the camera nearly level (pitch 80) from the south-west.
#include "visual_inc"

void View(object oPC, float t, float fDist, string sName)
{
    DelayCommand(t, VCamera(oPC, 45.0, fDist, 80.0));
    DelayCommand(t + 1.4, VShot(sName));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    View(oPC, t, 5.0, "c05"); t += 2.0;
    View(oPC, t, 6.0, "c06"); t += 2.0;
    View(oPC, t, 7.0, "c07"); t += 2.0;
    View(oPC, t, 8.0, "c08"); t += 2.0;
    View(oPC, t, 9.0, "c09"); t += 2.0;
    View(oPC, t, 10.0, "c10"); t += 2.0;
    View(oPC, t, 11.0, "c11"); t += 2.0;
    View(oPC, t, 12.0, "c12"); t += 2.0;
    View(oPC, t, 14.0, "c14"); t += 2.0;
    View(oPC, t, 16.0, "c16"); t += 2.0;
    View(oPC, t, 19.0, "c19"); t += 2.0;
    View(oPC, t, 23.0, "c23"); t += 2.0;
    View(oPC, t, 28.0, "c28"); t += 2.0;
    View(oPC, t, 35.0, "c35"); t += 2.0;
    DelayCommand(t, VDone());
}
