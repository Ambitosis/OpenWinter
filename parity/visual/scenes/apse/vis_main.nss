// The Chapter 1 Apse (tic01; a big black tile cap once hid the player on entry) (player at points, camera setups per point).
#include "visual_inc"

void Spot(object oPC, float t, float x, float y, string sName)
{
    DelayCommand(t, VMovePC(oPC, x, y, 0.0, 90.0));
    DelayCommand(t + 0.4, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(t + 2.4, VShot(sName + "_c0"));
    DelayCommand(t + 3.6, VCamera(oPC, 90.0, 10.0, 62.0));
    DelayCommand(t + 5.6, VShot(sName + "_c1"));
    DelayCommand(t + 6.8, VCamera(oPC, 45.0, 7.0, 72.0));
    DelayCommand(t + 8.8, VShot(sName + "_c2"));
    DelayCommand(t + 10.0, VCamera(oPC, 120.0, 10.0, 60.0));
    DelayCommand(t + 12.0, VShot(sName + "_c3"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    Spot(oPC, t, 15.0, 5.5, "p150_055"); t += 13.3;
    Spot(oPC, t, 15.0, 8.5, "p150_085"); t += 13.3;
    Spot(oPC, t, 15.0, 12.5, "p150_125"); t += 13.3;
    Spot(oPC, t, 15.0, 16.0, "p150_160"); t += 13.3;
    Spot(oPC, t, 8.0, 15.0, "p080_150"); t += 13.3;
    Spot(oPC, t, 22.0, 18.0, "p220_180"); t += 13.3;
    DelayCommand(t, VDone());
}
