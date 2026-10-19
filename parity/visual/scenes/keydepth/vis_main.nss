// Keyholing against occluder depth: the camera keeps its distance (16 m, then 22 m) while the player steps away
// from the wall that hides it (keyzoom's spot), so only the wall's depth between camera and player changes.
#include "visual_inc"

void Step(object oPC, float t, float fOff, float fDist, string sName)
{
    DelayCommand(t, VMovePC(oPC, 15.0 + fOff, 15.0 + fOff, 0.0, 45.0));
    DelayCommand(t + 0.8, VCamera(oPC, 45.0, fDist, 80.0));
    DelayCommand(t + 2.2, VShot(sName));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    Step(oPC, t, 0.0, 16.0, "a16_0"); t += 3.0;
    Step(oPC, t, 1.0, 16.0, "a16_1"); t += 3.0;
    Step(oPC, t, 2.0, 16.0, "a16_2"); t += 3.0;
    Step(oPC, t, 3.0, 16.0, "a16_3"); t += 3.0;
    Step(oPC, t, -1.0, 16.0, "a16_m1"); t += 3.0;
    Step(oPC, t, 0.0, 22.0, "a22_0"); t += 3.0;
    Step(oPC, t, 2.0, 22.0, "a22_2"); t += 3.0;
    Step(oPC, t, 4.0, 22.0, "a22_4"); t += 3.0;
    DelayCommand(t, VDone());
}
