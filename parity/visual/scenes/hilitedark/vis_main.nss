// Identical geometry with and without ambient/diffuse light. The highlight must cut through blackness.
#include "visual_inc"

void Key(string state)
{
    WriteTimestampedLogEntry("VISUAL|key|9|" + state);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    VHidePC(pc);
    object npc = VSpawn(pc, "nw_bandit001", 0.9, 2.0, 270.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), npc);
    VPlaceable(pc, "plc_chest1", -1.3, 2.0, 0.0);
    DelayCommand(3.0, VCamera(pc, 90.0, 7.0, 60.0));
    DelayCommand(5.0, VShot("a_none"));
    DelayCommand(6.0, Key("down"));
    DelayCommand(8.0, VShot("b_tab"));
    DelayCommand(9.0, Key("up"));
    DelayCommand(11.0, VShot("c_released"));
    DelayCommand(13.0, VDone());
}
