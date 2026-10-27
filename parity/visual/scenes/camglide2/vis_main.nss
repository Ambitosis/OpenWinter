// Pitch and distance transitions: whether their time depends on how far they go, and how it scales with the
// transition speed (camglide measured the direction's).
#include "visual_inc"

void Burst(string sName, float fSeconds) { WriteTimestampedLogEntry("VISUAL|burst|" + sName + "|" + FloatToString(fSeconds, 0, 1) + "|10"); }

void Facing(float fDirection, float fDistance, float fPitch, int nTransition)
{
    AssignCommand(GetFirstPC(), SetCameraFacing(fDirection, fDistance, fPitch, nTransition));
}

float Step(float t, string sName, float fFromDist, float fFromPitch, float fDist, float fPitch, int nTransition, float fSeconds)
{
    DelayCommand(t, Facing(90.0, fFromDist, fFromPitch, CAMERA_TRANSITION_TYPE_SNAP));
    DelayCommand(t + 1.5, Facing(-1.0, fDist, fPitch, nTransition));
    DelayCommand(t + 1.5, Burst(sName, fSeconds));
    return t + 1.5 + fSeconds + 1.0;
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    SetTime(12, 0, 0, 0);
    float t = 3.0;
    t = Step(t, "pitch_fast20", 12.0, 40.0, -1.0, 60.0, CAMERA_TRANSITION_TYPE_FAST, 3.0);
    t = Step(t, "pitch_medium60", 12.0, 20.0, -1.0, 80.0, CAMERA_TRANSITION_TYPE_MEDIUM, 5.0);
    t = Step(t, "pitch_vfast60", 12.0, 20.0, -1.0, 80.0, CAMERA_TRANSITION_TYPE_VERY_FAST, 3.0);
    t = Step(t, "pitch_slow20", 12.0, 40.0, -1.0, 60.0, CAMERA_TRANSITION_TYPE_SLOW, 5.0);
    t = Step(t, "dist_fast5", 8.0, 50.0, 13.0, -1.0, CAMERA_TRANSITION_TYPE_FAST, 3.0);
    t = Step(t, "dist_medium15", 5.0, 50.0, 20.0, -1.0, CAMERA_TRANSITION_TYPE_MEDIUM, 5.0);
    DelayCommand(t, VShot("end"));
    DelayCommand(t + 2.0, VDone());
}
