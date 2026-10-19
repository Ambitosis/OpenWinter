// Tile fade measurement (castle, generated) (player at points, camera setups per point).
#include "visual_inc"

void Spot(object oPC, float t, float x, float y, string sName)
{
    DelayCommand(t, VMovePC(oPC, x, y, 0.0, 90.0));
    DelayCommand(t + 0.4, VCamera(oPC, 90.0, 4.0, 40.0));
    DelayCommand(t + 2.4, VShot(sName + "_c0"));
    DelayCommand(t + 3.6, VCamera(oPC, 90.0, 8.0, 40.0));
    DelayCommand(t + 5.6, VShot(sName + "_c1"));
    DelayCommand(t + 6.8, VCamera(oPC, 90.0, 14.0, 40.0));
    DelayCommand(t + 8.8, VShot(sName + "_c2"));
    DelayCommand(t + 10.0, VCamera(oPC, 90.0, 4.0, 60.0));
    DelayCommand(t + 12.0, VShot(sName + "_c3"));
    DelayCommand(t + 13.2, VCamera(oPC, 90.0, 8.0, 60.0));
    DelayCommand(t + 15.2, VShot(sName + "_c4"));
    DelayCommand(t + 16.4, VCamera(oPC, 90.0, 14.0, 60.0));
    DelayCommand(t + 18.4, VShot(sName + "_c5"));
    DelayCommand(t + 19.6, VCamera(oPC, 90.0, 4.0, 75.0));
    DelayCommand(t + 21.6, VShot(sName + "_c6"));
    DelayCommand(t + 22.8, VCamera(oPC, 90.0, 8.0, 75.0));
    DelayCommand(t + 24.8, VShot(sName + "_c7"));
    DelayCommand(t + 26.0, VCamera(oPC, 90.0, 14.0, 75.0));
    DelayCommand(t + 28.0, VShot(sName + "_c8"));
    DelayCommand(t + 29.2, VCamera(oPC, 90.0, 4.0, 85.0));
    DelayCommand(t + 31.2, VShot(sName + "_c9"));
    DelayCommand(t + 32.4, VCamera(oPC, 90.0, 8.0, 85.0));
    DelayCommand(t + 34.4, VShot(sName + "_c10"));
    DelayCommand(t + 35.6, VCamera(oPC, 90.0, 14.0, 85.0));
    DelayCommand(t + 37.6, VShot(sName + "_c11"));
    DelayCommand(t + 38.8, VCamera(oPC, 180.0, 4.0, 40.0));
    DelayCommand(t + 40.8, VShot(sName + "_c12"));
    DelayCommand(t + 42.0, VCamera(oPC, 180.0, 8.0, 40.0));
    DelayCommand(t + 44.0, VShot(sName + "_c13"));
    DelayCommand(t + 45.2, VCamera(oPC, 180.0, 14.0, 40.0));
    DelayCommand(t + 47.2, VShot(sName + "_c14"));
    DelayCommand(t + 48.4, VCamera(oPC, 180.0, 4.0, 60.0));
    DelayCommand(t + 50.4, VShot(sName + "_c15"));
    DelayCommand(t + 51.6, VCamera(oPC, 180.0, 8.0, 60.0));
    DelayCommand(t + 53.6, VShot(sName + "_c16"));
    DelayCommand(t + 54.8, VCamera(oPC, 180.0, 14.0, 60.0));
    DelayCommand(t + 56.8, VShot(sName + "_c17"));
    DelayCommand(t + 58.0, VCamera(oPC, 180.0, 4.0, 75.0));
    DelayCommand(t + 60.0, VShot(sName + "_c18"));
    DelayCommand(t + 61.2, VCamera(oPC, 180.0, 8.0, 75.0));
    DelayCommand(t + 63.2, VShot(sName + "_c19"));
    DelayCommand(t + 64.4, VCamera(oPC, 180.0, 14.0, 75.0));
    DelayCommand(t + 66.4, VShot(sName + "_c20"));
    DelayCommand(t + 67.6, VCamera(oPC, 180.0, 4.0, 85.0));
    DelayCommand(t + 69.6, VShot(sName + "_c21"));
    DelayCommand(t + 70.8, VCamera(oPC, 180.0, 8.0, 85.0));
    DelayCommand(t + 72.8, VShot(sName + "_c22"));
    DelayCommand(t + 74.0, VCamera(oPC, 180.0, 14.0, 85.0));
    DelayCommand(t + 76.0, VShot(sName + "_c23"));
    DelayCommand(t + 77.2, VCamera(oPC, 270.0, 4.0, 40.0));
    DelayCommand(t + 79.2, VShot(sName + "_c24"));
    DelayCommand(t + 80.4, VCamera(oPC, 270.0, 8.0, 40.0));
    DelayCommand(t + 82.4, VShot(sName + "_c25"));
    DelayCommand(t + 83.6, VCamera(oPC, 270.0, 14.0, 40.0));
    DelayCommand(t + 85.6, VShot(sName + "_c26"));
    DelayCommand(t + 86.8, VCamera(oPC, 270.0, 4.0, 60.0));
    DelayCommand(t + 88.8, VShot(sName + "_c27"));
    DelayCommand(t + 90.0, VCamera(oPC, 270.0, 8.0, 60.0));
    DelayCommand(t + 92.0, VShot(sName + "_c28"));
    DelayCommand(t + 93.2, VCamera(oPC, 270.0, 14.0, 60.0));
    DelayCommand(t + 95.2, VShot(sName + "_c29"));
    DelayCommand(t + 96.4, VCamera(oPC, 270.0, 4.0, 75.0));
    DelayCommand(t + 98.4, VShot(sName + "_c30"));
    DelayCommand(t + 99.6, VCamera(oPC, 270.0, 8.0, 75.0));
    DelayCommand(t + 101.6, VShot(sName + "_c31"));
    DelayCommand(t + 102.8, VCamera(oPC, 270.0, 14.0, 75.0));
    DelayCommand(t + 104.8, VShot(sName + "_c32"));
    DelayCommand(t + 106.0, VCamera(oPC, 270.0, 4.0, 85.0));
    DelayCommand(t + 108.0, VShot(sName + "_c33"));
    DelayCommand(t + 109.2, VCamera(oPC, 270.0, 8.0, 85.0));
    DelayCommand(t + 111.2, VShot(sName + "_c34"));
    DelayCommand(t + 112.4, VCamera(oPC, 270.0, 14.0, 85.0));
    DelayCommand(t + 114.4, VShot(sName + "_c35"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    Spot(oPC, t, 16.0, 12.5, "p160_125"); t += 115.7;
    Spot(oPC, t, 13.5, 15.0, "p135_150"); t += 115.7;
    DelayCommand(t, VDone());
}
