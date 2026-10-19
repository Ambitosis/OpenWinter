// Keyholing against zoom: the castle (tic01) spot of the keyhole scene, camera nearly level so one flat wall
// about 2.6 m in front of the player hides it, from 4 to 25 m; first with the harness's wide camera limits
// (1-40 m), then with the game's default limits (1-25 m), to see whether the hole's size follows the limits.
#include "visual_inc"

void View(object oPC, float t, float fDist, string sName)
{
    DelayCommand(t, VCamera(oPC, 45.0, fDist, 80.0));
    DelayCommand(t + 1.6, VShot(sName));
}

void Limits(object oPC, float fMax)
{
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, fMax);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    View(oPC, t, 4.0, "w04");  t += 2.4;
    View(oPC, t, 6.0, "w06");  t += 2.4;
    View(oPC, t, 8.0, "w08");  t += 2.4;
    View(oPC, t, 10.0, "w10"); t += 2.4;
    View(oPC, t, 13.0, "w13"); t += 2.4;
    View(oPC, t, 16.0, "w16"); t += 2.4;
    View(oPC, t, 20.0, "w20"); t += 2.4;
    View(oPC, t, 25.0, "w25"); t += 2.4;
    View(oPC, t, 32.0, "w32"); t += 2.4;
    DelayCommand(t, Limits(oPC, 25.0)); t += 0.5;
    View(oPC, t, 4.0, "l04");  t += 2.4;
    View(oPC, t, 8.0, "l08");  t += 2.4;
    View(oPC, t, 13.0, "l13"); t += 2.4;
    View(oPC, t, 20.0, "l20"); t += 2.4;
    View(oPC, t, 25.0, "l25"); t += 2.4;
    DelayCommand(t, VDone());
}
