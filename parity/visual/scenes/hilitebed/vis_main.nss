// The chest beside the Prelude bedroom's black border, plus an NPC for the same halo.
// Tab reaches the original without moving the real mouse. Pair highlighted and unhighlighted views.
#include "visual_inc"

void Key(string state)
{
    WriteTimestampedLogEntry("VISUAL|key|9|" + state);
}

void View(object pc, float t, float dir, float dist, float pitch, string name)
{
    DelayCommand(t, Key("up"));
    DelayCommand(t, VCamera(pc, dir, dist, pitch));
    DelayCommand(t + 2.0, VShot(name + "_none"));
    DelayCommand(t + 3.0, Key("down"));
    DelayCommand(t + 5.0, VShot(name + "_tab"));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    // Cutscene mode ignores Tab in the original.
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    VHidePC(pc);
    object npc = VSpawn(pc, "nw_bandit001", 0.0, 2.0, 270.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), npc);
    View(pc, 3.0, 330.0, 9.0, 60.0, "a_border");
    View(pc, 11.0, 35.0, 9.0, 60.0, "b_reverse");
    View(pc, 19.0, 330.0, 6.0, 65.0, "c_close");
    View(pc, 27.0, 330.0, 14.0, 50.0, "d_far");
    DelayCommand(34.0, Key("up"));
    DelayCommand(36.0, VShot("e_released"));
    DelayCommand(38.0, VDone());
}
