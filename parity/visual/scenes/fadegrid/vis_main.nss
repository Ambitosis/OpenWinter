// Tile fade measurement (castle, generated) (player at points, camera setups per point).
#include "visual_inc"

void Spot(object oPC, float t, float x, float y, string sName)
{
    DelayCommand(t, VMovePC(oPC, x, y, 0.0, 90.0));
    DelayCommand(t + 0.4, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(t + 2.4, VShot(sName + "_c0"));
    DelayCommand(t + 3.6, VCamera(oPC, 45.0, 7.0, 72.0));
    DelayCommand(t + 5.6, VShot(sName + "_c1"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    Spot(oPC, t, 4.5, 4.0, "p045_040"); t += 6.9;
    Spot(oPC, t, 7.5, 4.0, "p075_040"); t += 6.9;
    Spot(oPC, t, 13.5, 4.0, "p135_040"); t += 6.9;
    Spot(oPC, t, 16.0, 4.0, "p160_040"); t += 6.9;
    Spot(oPC, t, 18.5, 4.0, "p185_040"); t += 6.9;
    Spot(oPC, t, 22.0, 4.0, "p220_040"); t += 6.9;
    Spot(oPC, t, 26.0, 4.0, "p260_040"); t += 6.9;
    Spot(oPC, t, 7.5, 7.5, "p075_075"); t += 6.9;
    Spot(oPC, t, 13.5, 7.5, "p135_075"); t += 6.9;
    Spot(oPC, t, 16.0, 7.5, "p160_075"); t += 6.9;
    Spot(oPC, t, 18.5, 7.5, "p185_075"); t += 6.9;
    Spot(oPC, t, 22.0, 7.5, "p220_075"); t += 6.9;
    Spot(oPC, t, 26.0, 7.5, "p260_075"); t += 6.9;
    Spot(oPC, t, 4.5, 12.5, "p045_125"); t += 6.9;
    Spot(oPC, t, 7.5, 12.5, "p075_125"); t += 6.9;
    Spot(oPC, t, 13.5, 12.5, "p135_125"); t += 6.9;
    Spot(oPC, t, 16.0, 12.5, "p160_125"); t += 6.9;
    Spot(oPC, t, 18.5, 12.5, "p185_125"); t += 6.9;
    Spot(oPC, t, 22.0, 12.5, "p220_125"); t += 6.9;
    Spot(oPC, t, 26.0, 12.5, "p260_125"); t += 6.9;
    Spot(oPC, t, 7.5, 15.0, "p075_150"); t += 6.9;
    Spot(oPC, t, 13.5, 15.0, "p135_150"); t += 6.9;
    Spot(oPC, t, 22.0, 15.0, "p220_150"); t += 6.9;
    Spot(oPC, t, 26.0, 15.0, "p260_150"); t += 6.9;
    Spot(oPC, t, 7.5, 17.5, "p075_175"); t += 6.9;
    Spot(oPC, t, 13.5, 17.5, "p135_175"); t += 6.9;
    Spot(oPC, t, 16.0, 17.5, "p160_175"); t += 6.9;
    Spot(oPC, t, 22.0, 17.5, "p220_175"); t += 6.9;
    Spot(oPC, t, 26.0, 17.5, "p260_175"); t += 6.9;
    Spot(oPC, t, 4.5, 22.5, "p045_225"); t += 6.9;
    Spot(oPC, t, 7.5, 22.5, "p075_225"); t += 6.9;
    Spot(oPC, t, 13.5, 22.5, "p135_225"); t += 6.9;
    Spot(oPC, t, 16.0, 22.5, "p160_225"); t += 6.9;
    Spot(oPC, t, 18.5, 22.5, "p185_225"); t += 6.9;
    Spot(oPC, t, 22.0, 22.5, "p220_225"); t += 6.9;
    Spot(oPC, t, 26.0, 22.5, "p260_225"); t += 6.9;
    Spot(oPC, t, 7.5, 25.5, "p075_255"); t += 6.9;
    Spot(oPC, t, 13.5, 25.5, "p135_255"); t += 6.9;
    Spot(oPC, t, 18.5, 25.5, "p185_255"); t += 6.9;
    Spot(oPC, t, 22.0, 25.5, "p220_255"); t += 6.9;
    Spot(oPC, t, 26.0, 25.5, "p260_255"); t += 6.9;
    DelayCommand(t, VDone());
}
