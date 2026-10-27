// Camera edge cases: the player turning the camera (left arrow held) during a scripted transition, a negative
// direction, and SetCameraFacing outside SetCameraLimits. Reference views are snapped to for comparison. Not in
// cutscene mode, so the player's camera keys work. Each step is announced in chat and the log.
#include "visual_inc"

void Mark(string s)
{
    AssignCommand(GetFirstPC(), SpeakString(s));
    WriteTimestampedLogEntry("VISUAL|note|" + s);
}

void Facing(string sName, float fDirection, float fDistance, float fPitch, int nTransition)
{
    Mark("C " + sName + " " + FloatToString(fDirection, 0, 1) + " " + FloatToString(fDistance, 0, 1) + " " + FloatToString(fPitch, 0, 1) + " " + IntToString(nTransition));
    AssignCommand(GetFirstPC(), SetCameraFacing(fDirection, fDistance, fPitch, nTransition));
}

void Limits(float a, float b, float c, float d)
{
    Mark("X " + FloatToString(a, 0, 1) + " " + FloatToString(b, 0, 1) + " " + FloatToString(c, 0, 1) + " " + FloatToString(d, 0, 1));
    SetCameraLimits(GetFirstPC(), a, b, c, d);
}

void Key(int nVk, string sState) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|" + sState); }

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    SetTime(12, 0, 0, 0);
    object oA = VSpawn(oPC, "nw_bandit001", 2.0, 0.0, 180.0);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(oA, i, "");
    AssignCommand(oA, ClearAllActions(TRUE));
    float t = 3.0;
    // References.
    DelayCommand(t, Facing("ref90", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("ref90")); t += 3.0;
    DelayCommand(t, Facing("ref270", 270.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("ref270")); t += 3.0;
    // A negative direction.
    DelayCommand(t, Facing("snap90", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing("neg90", -90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("neg90")); t += 3.0;
    DelayCommand(t, Facing("neg1", -1.0, 6.0, -1.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("neg1")); t += 3.0;
    // The left arrow held for 0.6 s, 1.5 s into a SLOW half turn (9 s): does the turn go on to 270?
    DelayCommand(t, Facing("snap90b", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing("slow", 270.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SLOW));
    DelayCommand(t + 1.0, VShot("k_before"));
    DelayCommand(t + 1.5, Key(37, "down")); DelayCommand(t + 2.1, Key(37, "up"));
    DelayCommand(t + 3.0, VShot("k_after"));
    DelayCommand(t + 6.0, VShot("k_mid"));
    DelayCommand(t + 11.0, VShot("k_end"));
    t += 13.0;
    // The same without a key, for comparison.
    DelayCommand(t, Facing("snap90c", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing("slow2", 270.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SLOW));
    DelayCommand(t + 3.0, VShot("n_after"));
    DelayCommand(t + 6.0, VShot("n_mid"));
    t += 11.0;
    // SetCameraFacing outside the limits.
    DelayCommand(t, Facing("ref_p60", 90.0, 12.0, 60.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("ref_p60")); t += 3.0;
    DelayCommand(t, Facing("ref_p30", 90.0, 12.0, 30.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("ref_p30")); t += 3.0;
    DelayCommand(t, Facing("ref_d20", 90.0, 20.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("ref_d20")); t += 3.0;
    DelayCommand(t, Facing("ref_d8", 90.0, 8.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("ref_d8")); t += 3.0;
    DelayCommand(t, Limits(30.0, 60.0, 8.0, 20.0)); t += 1.0;
    DelayCommand(t, Facing("lim_p85", 90.0, 12.0, 85.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("lim_p85")); t += 3.0;
    DelayCommand(t, Facing("lim_p10", 90.0, 12.0, 10.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("lim_p10")); t += 3.0;
    DelayCommand(t, Facing("lim_d30", 90.0, 30.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("lim_d30")); t += 3.0;
    DelayCommand(t, Facing("lim_d3", 90.0, 3.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("lim_d3")); t += 3.0;
    DelayCommand(t, Limits(-1.0, -1.0, -1.0, -1.0)); t += 1.0;
    DelayCommand(t, VDone());
}
