// Keyholing over time: does the hole ease to its size? The keyzoom spot (a wall hides the player), the camera
// snapped from an unoccluded view to a distance and held, shots at several delays.
#include "visual_inc"

void Cam(object oPC, float t, float fDist)
{
    DelayCommand(t, VCamera(oPC, 45.0, fDist, 80.0));
}

void Shot(float t, string sName)
{
    DelayCommand(t, VShot(sName));
}

float Hold(object oPC, float t, float fDist, string sPrefix)
{
    Cam(oPC, t, 6.0);
    t += 6.0;
    Cam(oPC, t, fDist);
    Shot(t + 0.2, sPrefix + "_0_2");
    Shot(t + 0.7, sPrefix + "_0_7");
    Shot(t + 1.4, sPrefix + "_1_4");
    Shot(t + 2.5, sPrefix + "_2_5");
    Shot(t + 4.0, sPrefix + "_4_0");
    Shot(t + 7.0, sPrefix + "_7_0");
    return t + 8.0;
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    t = Hold(oPC, t, 16.0, "h16");
    t = Hold(oPC, t, 10.0, "h10");
    t = Hold(oPC, t, 25.0, "h25");
    DelayCommand(t, VDone());
}
