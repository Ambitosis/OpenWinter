// Camera transitions one field at a time: SetCameraFacing changes only the direction, the pitch or the distance
// from a snapped start, sampled by bursts from the moment it is asked; the camera stops when a burst's frames stop
// changing (the scene holds still otherwise).
#include "visual_inc"

void Burst(string sName, float fSeconds) { WriteTimestampedLogEntry("VISUAL|burst|" + sName + "|" + FloatToString(fSeconds, 0, 1) + "|10"); }

void Facing(float fDirection, float fDistance, float fPitch, int nTransition)
{
    WriteTimestampedLogEntry("VISUAL|note|facing " + FloatToString(fDirection, 0, 1) + " " + FloatToString(fDistance, 0, 1) + " " + FloatToString(fPitch, 0, 1) + " " + IntToString(nTransition));
    AssignCommand(GetFirstPC(), SetCameraFacing(fDirection, fDistance, fPitch, nTransition));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    SetTime(12, 0, 0, 0);
    float t = 3.0;
    // Direction alone: 90 degrees at SLOW (20) and at VERY_FAST (100), 45 at VERY_SLOW (5).
    DelayCommand(t, Facing(90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing(180.0, -1.0, -1.0, CAMERA_TRANSITION_TYPE_SLOW)); DelayCommand(t, Burst("yaw_slow90", 5.0)); t += 7.0;
    DelayCommand(t, Facing(90.0, -1.0, -1.0, CAMERA_TRANSITION_TYPE_VERY_FAST)); DelayCommand(t, Burst("yaw_vfast90", 2.0)); t += 4.0;
    DelayCommand(t, Facing(110.0, -1.0, -1.0, CAMERA_TRANSITION_TYPE_VERY_SLOW)); DelayCommand(t, Burst("yaw_vslow20", 5.0)); t += 7.0;
    // Pitch alone: 20 to 80 at SLOW, 80 to 20 at FAST.
    DelayCommand(t, Facing(90.0, 12.0, 20.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing(-1.0, -1.0, 80.0, CAMERA_TRANSITION_TYPE_SLOW)); DelayCommand(t, Burst("pitch_slow60", 5.0)); t += 7.0;
    DelayCommand(t, Facing(-1.0, -1.0, 20.0, CAMERA_TRANSITION_TYPE_FAST)); DelayCommand(t, Burst("pitch_fast60", 3.0)); t += 5.0;
    // Distance alone: 5 to 20 at SLOW, 20 to 5 at FAST.
    DelayCommand(t, Facing(90.0, 5.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing(-1.0, 20.0, -1.0, CAMERA_TRANSITION_TYPE_SLOW)); DelayCommand(t, Burst("dist_slow15", 5.0)); t += 7.0;
    DelayCommand(t, Facing(-1.0, 5.0, -1.0, CAMERA_TRANSITION_TYPE_FAST)); DelayCommand(t, Burst("dist_fast15", 3.0)); t += 5.0;
    // All three at MEDIUM: 90 degrees, 30 degrees of pitch, 10 m.
    DelayCommand(t, Facing(90.0, 8.0, 40.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    DelayCommand(t, Facing(180.0, 18.0, 70.0, CAMERA_TRANSITION_TYPE_MEDIUM)); DelayCommand(t, Burst("all_medium", 4.0)); t += 6.0;
    DelayCommand(t, VShot("end"));
    DelayCommand(t + 2.0, VDone());
}
