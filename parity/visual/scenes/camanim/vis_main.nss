// Camera and custom animation probe: every camera script function with known values, animation replacements and
// the looping custom animations. Each step is announced by the player in chat ("<step> <what>") and in the log, so a
// recording reads step by step; bursts sample camera transitions and shots the animations.
#include "visual_inc"

void Mark(string s)
{
    AssignCommand(GetFirstPC(), SpeakString(s));
    WriteTimestampedLogEntry("VISUAL|note|" + s);
}

void Burst(string sName, float fSeconds) { WriteTimestampedLogEntry("VISUAL|burst|" + sName + "|" + FloatToString(fSeconds, 0, 1) + "|8"); }

void Facing(string sName, float fDirection, float fDistance, float fPitch, int nTransition)
{
    Mark("C " + sName + " " + FloatToString(fDirection, 0, 1) + " " + FloatToString(fDistance, 0, 1) + " " + FloatToString(fPitch, 0, 1) + " " + IntToString(nTransition));
    AssignCommand(GetFirstPC(), SetCameraFacing(fDirection, fDistance, fPitch, nTransition));
}

void Height(float fHeight)
{
    Mark("H " + FloatToString(fHeight, 0, 2));
    SetCameraHeight(GetFirstPC(), fHeight);
}

void Store() { Mark("S store"); AssignCommand(GetFirstPC(), StoreCameraFacing()); }
void Restore() { Mark("S restore"); AssignCommand(GetFirstPC(), RestoreCameraFacing()); }
void Mode(int n) { Mark("M " + IntToString(n)); SetCameraMode(GetFirstPC(), n); }

void Lock(string sWhich, int bLock)
{
    Mark("L " + sWhich + " " + IntToString(bLock));
    object oPC = GetFirstPC();
    if (sWhich == "pitch") LockCameraPitch(oPC, bLock);
    else if (sWhich == "distance") LockCameraDistance(oPC, bLock);
    else LockCameraDirection(oPC, bLock);
}

void Limits(float a, float b, float c, float d)
{
    Mark("X " + FloatToString(a, 0, 1) + " " + FloatToString(b, 0, 1) + " " + FloatToString(c, 0, 1) + " " + FloatToString(d, 0, 1));
    SetCameraLimits(GetFirstPC(), a, b, c, d);
}

void Flags(int n) { Mark("F " + IntToString(n)); SetCameraFlags(GetFirstPC(), n); }
void Attach(object o, int bClear) { Mark("T " + GetName(o) + " " + IntToString(bClear)); AttachCamera(GetFirstPC(), o, bClear); }
void Cutscene(int bOn) { Mark("U " + IntToString(bOn)); SetCutsceneMode(GetFirstPC(), bOn); }
void Rate(float f) { Mark("R " + FloatToString(f, 0, 2)); SetCutsceneCameraMoveRate(GetFirstPC(), f); }

void Replace(object o, string sOld, string sNew)
{
    Mark("A " + sOld + " " + sNew);
    ReplaceObjectAnimation(o, sOld, sNew);
}

void Anim(object o, string sName, int n, float fDuration)
{
    Mark("P " + sName + " " + IntToString(n) + " " + FloatToString(fDuration, 0, 1));
    AssignCommand(o, ClearAllActions());
    AssignCommand(o, PlayAnimation(n, 1.0, fDuration));
}

void Shots(string sName, float t)
{
    DelayCommand(t + 0.4, VShot(sName + "_0"));
    DelayCommand(t + 2.0, VShot(sName + "_1"));
    DelayCommand(t + 4.5, VShot(sName + "_2"));
    DelayCommand(t + 7.5, VShot(sName + "_3"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    SetTime(12, 0, 0, 0);
    object oA = VSpawn(oPC, "nw_bandit001", 2.0, 0.0, 180.0);
    SetName(oA, "Actor");
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(oA, i, "");
    AssignCommand(oA, ClearAllActions(TRUE));

    float t = 3.0;
    // SetCameraFacing: all fields, then each alone.
    DelayCommand(t, Facing("all", 90.0, 10.0, 45.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("c_all")); t += 3.0;
    DelayCommand(t, Facing("dir", 0.0, -1.0, -1.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("c_dir")); t += 3.0;
    DelayCommand(t, Facing("dist", -1.0, 6.0, -1.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("c_dist")); t += 3.0;
    DelayCommand(t, Facing("pitch", -1.0, -1.0, 70.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("c_pitch")); t += 3.0;
    DelayCommand(t, Facing("snap", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    // Transitions: each turns the camera half round, sampled from the moment it is asked.
    DelayCommand(t, Facing("medium", 270.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_MEDIUM)); DelayCommand(t, Burst("t_medium", 4.0)); t += 6.0;
    DelayCommand(t, Facing("fast", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_FAST)); DelayCommand(t, Burst("t_fast", 3.0)); t += 5.0;
    DelayCommand(t, Facing("slow", 270.0, 6.0, 80.0, CAMERA_TRANSITION_TYPE_SLOW)); DelayCommand(t, Burst("t_slow", 5.0)); t += 7.0;
    DelayCommand(t, Facing("crawl", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_CRAWL)); DelayCommand(t, Burst("t_crawl", 5.0)); t += 7.0;
    DelayCommand(t, Facing("snap2", 90.0, 12.0, 50.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 2.0;
    // SetCameraHeight.
    DelayCommand(t, Height(3.0)); DelayCommand(t + 1.5, VShot("h_3")); t += 3.0;
    DelayCommand(t, Height(0.0)); DelayCommand(t + 1.5, VShot("h_0")); t += 3.0;
    DelayCommand(t, Height(-1.0)); DelayCommand(t + 1.5, VShot("h_m1")); t += 3.0;
    // StoreCameraFacing / RestoreCameraFacing.
    DelayCommand(t, Store()); t += 1.0;
    DelayCommand(t, Facing("away", 200.0, 20.0, 30.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("s_away")); t += 3.0;
    DelayCommand(t, Restore()); DelayCommand(t + 1.5, VShot("s_restored")); t += 3.0;
    // Modes, locks, limits, flags, attach, move rate.
    DelayCommand(t, Mode(CAMERA_MODE_CHASE_CAMERA)); t += 1.0;
    DelayCommand(t, Mode(CAMERA_MODE_STIFF_CHASE_CAMERA)); t += 1.0;
    DelayCommand(t, Mode(CAMERA_MODE_TOP_DOWN)); t += 1.0;
    DelayCommand(t, Lock("pitch", TRUE)); DelayCommand(t + 0.5, Lock("pitch", FALSE)); t += 1.0;
    DelayCommand(t, Lock("distance", TRUE)); DelayCommand(t + 0.5, Lock("distance", FALSE)); t += 1.0;
    DelayCommand(t, Lock("direction", TRUE)); DelayCommand(t + 0.5, Lock("direction", FALSE)); t += 1.0;
    DelayCommand(t, Limits(20.0, 60.0, 4.0, 15.0)); t += 1.0;
    DelayCommand(t, Limits(-1.0, -1.0, -1.0, -1.0)); t += 1.0;
    DelayCommand(t, Flags(CAMERA_FLAG_DISABLE_ZOOM | CAMERA_FLAG_DISABLE_TURN)); DelayCommand(t + 0.5, Flags(0)); t += 1.0;
    DelayCommand(t, Attach(oA, TRUE)); DelayCommand(t + 1.5, VShot("a_attached")); DelayCommand(t + 2.5, Attach(OBJECT_INVALID, FALSE)); t += 4.0;
    DelayCommand(t, Rate(1.5)); t += 1.0;
    // ReplaceObjectAnimation: one, a second, the first undone, an identity.
    DelayCommand(t, Replace(oA, "pause1", "custom3lp")); DelayCommand(t + 2.0, VShot("r_idle")); t += 3.0;
    DelayCommand(t, Replace(oA, "walk", "run")); t += 1.0;
    DelayCommand(t, Replace(oA, "pause1", "")); DelayCommand(t + 2.0, VShot("r_undone")); t += 3.0;
    DelayCommand(t, Replace(oA, "walk", "walk")); t += 1.0;
    DelayCommand(t, Replace(oA, "walk", "")); t += 1.0;
    // The looping custom animations: CUSTOM3 (custom3start/lp/end in the stock model), CUSTOM6 (none there), CUSTOM1.
    DelayCommand(t, Facing("anims", 90.0, 5.0, 75.0, CAMERA_TRANSITION_TYPE_SNAP)); t += 1.0;
    DelayCommand(t, Anim(oA, "custom3", ANIMATION_LOOPING_CUSTOM3, 6.0)); Shots("p_custom3", t); t += 10.0;
    DelayCommand(t, Anim(oA, "custom6", ANIMATION_LOOPING_CUSTOM6, 6.0)); Shots("p_custom6", t); t += 10.0;
    DelayCommand(t, Anim(oA, "custom4", ANIMATION_LOOPING_CUSTOM4, 6.0)); Shots("p_custom4", t); t += 10.0;
    DelayCommand(t, Anim(oA, "custom1", ANIMATION_LOOPING_CUSTOM1, 6.0)); Shots("p_custom1", t); t += 10.0;
    // Cutscene mode on and off around a camera change.
    DelayCommand(t, Cutscene(TRUE)); t += 1.0;
    DelayCommand(t, Facing("cut", 180.0, 8.0, 30.0, CAMERA_TRANSITION_TYPE_SNAP)); DelayCommand(t + 1.5, VShot("u_on")); t += 3.0;
    DelayCommand(t, Cutscene(FALSE)); DelayCommand(t + 1.5, VShot("u_off")); t += 3.0;
    DelayCommand(t, VDone());
}
