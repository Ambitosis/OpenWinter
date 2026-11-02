// The camera's distance and pitch limits. SetCameraFacing from 0.5 to 40 m and at pitch 0 and 89, first under the
// client's own limits (no SetCameraLimits), then under wide script limits (0.1 to 60 m) as the reference, then after
// SetCameraLimits(-1, ...). Last, out of cutscene mode and under the client's own limits, the mouse wheel zoomed all
// the way in, then all the way out. Dogs 4 m around the player mark the floor.
#include "visual_inc"

void View(object oPC, float t, float fDist, float fPitch, string sName)
{
    DelayCommand(t, VCamera(oPC, 90.0, fDist, fPitch));
    DelayCommand(t + 1.5, VShot(sName));
}

void Limits(object oPC, float fMinPitch, float fMaxPitch, float fMinDist, float fMaxDist)
{
    SetCameraLimits(oPC, fMinPitch, fMaxPitch, fMinDist, fMaxDist);
}

void Wheel(int nClicks)
{
    WriteTimestampedLogEntry("VISUAL|wheel|0.5,0.45|" + IntToString(nClicks));
}

void Play(object oPC)
{
    SetCutsceneMode(oPC, FALSE);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCutsceneMode(oPC, TRUE, FALSE);
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    VSpawn(oPC, "nw_dog", 4.0, 0.0, 180.0);
    VSpawn(oPC, "nw_dog", -4.0, 0.0, 0.0);
    VSpawn(oPC, "nw_dog", 0.0, 4.0, 270.0);
    VSpawn(oPC, "nw_dog", 0.0, -4.0, 90.0);
    float t = 3.0;
    View(oPC, t, 10.0, 60.0, "d10");  t += 2.2;
    View(oPC, t, 0.5, 60.0, "d05");   t += 2.2;
    View(oPC, t, 1.5, 60.0, "d15");   t += 2.2;
    View(oPC, t, 25.0, 60.0, "d25");  t += 2.2;
    View(oPC, t, 40.0, 60.0, "d40");  t += 2.2;
    View(oPC, t, 10.0, 89.0, "dp89"); t += 2.2;
    View(oPC, t, 10.0, 0.0, "dp00");  t += 2.2;
    DelayCommand(t, Limits(oPC, 0.0, 89.9, 0.1, 60.0)); t += 0.5;
    View(oPC, t, 0.5, 60.0, "r05");   t += 2.2;
    View(oPC, t, 1.0, 60.0, "r10");   t += 2.2;
    View(oPC, t, 1.5, 60.0, "r15");   t += 2.2;
    View(oPC, t, 2.5, 60.0, "r25");   t += 2.2;
    View(oPC, t, 25.0, 60.0, "r250"); t += 2.2;
    View(oPC, t, 30.0, 60.0, "r300"); t += 2.2;
    View(oPC, t, 40.0, 60.0, "r400"); t += 2.2;
    View(oPC, t, 10.0, 89.0, "rp89"); t += 2.2;
    View(oPC, t, 10.0, 85.0, "rp85"); t += 2.2;
    View(oPC, t, 10.0, 1.0, "rp01");  t += 2.2;
    View(oPC, t, 10.0, 0.0, "rp00");  t += 2.2;
    DelayCommand(t, Limits(oPC, -1.0, -1.0, -1.0, -1.0)); t += 0.5;
    View(oPC, t, 0.5, 60.0, "x05");   t += 2.2;
    View(oPC, t, 40.0, 60.0, "x40");  t += 2.2;
    DelayCommand(t, Play(oPC)); t += 0.5;
    View(oPC, t, 10.0, 60.0, "k_start"); t += 2.2;
    DelayCommand(t, Wheel(60)); t += 5.0;
    DelayCommand(t, VShot("k_in")); t += 0.5;
    DelayCommand(t, Wheel(-90)); t += 6.0;
    DelayCommand(t, VShot("k_out")); t += 0.5;
    DelayCommand(t, VDone());
}
