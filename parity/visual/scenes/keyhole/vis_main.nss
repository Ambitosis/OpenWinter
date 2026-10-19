// Keyholing: the see-through hole the original cuts around the player when walls hide it. The castle
// (tic01) spot of castle/b_low, with a wall between camera and player, at several zooms and pitches.
// Run once with the original's keyholing on and once off (visual.py --keyhole enabled=false --tag _off)
// and diff the pairs to get the hole's mask.
#include "visual_inc"

void View(object oPC, float t, float fDir, float fDist, float fPitch, string sName)
{
    DelayCommand(t, VCamera(oPC, fDir, fDist, fPitch));
    DelayCommand(t + 1.6, VShot(sName));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    float t = 3.0;
    View(oPC, t, 45.0, 5.0, 72.0, "d05_p72");  t += 2.4;
    View(oPC, t, 45.0, 7.0, 72.0, "d07_p72");  t += 2.4;
    View(oPC, t, 45.0, 10.0, 72.0, "d10_p72"); t += 2.4;
    View(oPC, t, 45.0, 14.0, 72.0, "d14_p72"); t += 2.4;
    View(oPC, t, 45.0, 20.0, 72.0, "d20_p72"); t += 2.4;
    View(oPC, t, 45.0, 28.0, 72.0, "d28_p72"); t += 2.4;
    View(oPC, t, 45.0, 10.0, 50.0, "d10_p50"); t += 2.4;
    View(oPC, t, 45.0, 10.0, 60.0, "d10_p60"); t += 2.4;
    View(oPC, t, 45.0, 10.0, 80.0, "d10_p80"); t += 2.4;
    View(oPC, t, 90.0, 12.0, 55.0, "s12_p55"); t += 2.4;
    View(oPC, t, 90.0, 6.0, 55.0, "s06_p55");  t += 2.4;
    View(oPC, t, 90.0, 20.0, 55.0, "s20_p55"); t += 2.4;
    DelayCommand(t, VDone());
}
