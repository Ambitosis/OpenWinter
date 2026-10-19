// Highlighting and walls: goblins all round the player among the walled corner tiles, every object lit by
// holding Tab. Walls in front of a creature hide it and its glow in the original.
#include "visual_inc"

void Key(int nVk, string sState)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|" + sState);
}

void Goblin(object oPC, float fDx, float fDy)
{
    object o = VSpawn(oPC, "nw_goblina", fDx, fDy, 270.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), o);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    // Not cutscene mode: the original ignores Tab there.
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    VHidePC(oPC);
    Goblin(oPC, 0.0, 6.0);
    Goblin(oPC, 0.0, 11.0);
    Goblin(oPC, 6.0, 6.0);
    Goblin(oPC, -6.0, 6.0);
    Goblin(oPC, 6.0, 0.0);
    Goblin(oPC, -6.0, 0.0);
    Goblin(oPC, 3.0, 9.0);
    Goblin(oPC, -3.0, 9.0);
    DelayCommand(3.0, VCamera(oPC, 270.0, 12.0, 55.0));
    DelayCommand(6.0, VShot("a_none"));
    DelayCommand(7.0, Key(9, "down"));
    DelayCommand(9.0, VShot("b_tab"));
    DelayCommand(10.0, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(13.0, VShot("c_tab_other_side"));
    DelayCommand(14.0, Key(9, "up"));
    DelayCommand(16.0, VDone());
}
