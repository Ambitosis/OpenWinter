// What the original shows and plays when the player becomes able to level up: experience given with
// GiveXPToCreature (short of a level, then past it), SetXP further up, LevelUpHenchman tried on the player,
// and SetXP back down and up again. Phases are logged for tools/visual/combat_audio.py --sounds gui_.
#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Phase(string s) { WriteTimestampedLogEntry("VISUAL|note|levelup|phase|" + s); }
void State(object pc, string tag)
{
    WriteTimestampedLogEntry("VISUAL|note|state|" + tag + "|xp=" + IntToString(GetXP(pc)) + "|hd=" + IntToString(GetHitDice(pc))
        + "|class0=" + IntToString(GetClassByPosition(1, pc)));
}
void Give(object pc, int xp, string phase) { Phase(phase); GiveXPToCreature(pc, xp); State(pc, phase); }
void Set(object pc, int xp, string phase) { Phase(phase); SetXP(pc, xp); State(pc, phase); }
void Henchman(object pc)
{
    Phase("levelup_henchman");
    int r = LevelUpHenchman(pc, CLASS_TYPE_INVALID, TRUE);
    WriteTimestampedLogEntry("VISUAL|note|levelup_henchman|result=" + IntToString(r));
    State(pc, "levelup_henchman");
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(3.0, State(pc, "start"));
    DelayCommand(3.5, VShot("a_start"));
    DelayCommand(5.0, Give(pc, 500, "give500"));
    DelayCommand(5.4, VShot("b_give500_04"));
    DelayCommand(7.0, VShot("b_give500_2"));
    DelayCommand(9.0, Give(pc, 600, "give600_ready"));
    DelayCommand(9.4, VShot("c_ready_04"));
    DelayCommand(10.5, VShot("c_ready_15"));
    DelayCommand(12.0, VShot("c_ready_3"));
    DelayCommand(14.0, VShot("c_ready_5"));
    DelayCommand(16.0, Tap(67));
    DelayCommand(17.5, VShot("d_character"));
    DelayCommand(18.0, Tap(67));
    DelayCommand(19.0, Set(pc, 3500, "setxp3500"));
    DelayCommand(19.4, VShot("e_setxp3500"));
    DelayCommand(22.0, Henchman(pc));
    DelayCommand(22.4, VShot("f_levelup_04"));
    DelayCommand(23.5, VShot("f_levelup_15"));
    DelayCommand(25.0, VShot("f_levelup_3"));
    DelayCommand(28.0, Set(pc, 0, "setxp0"));
    DelayCommand(28.4, VShot("g_setxp0"));
    DelayCommand(31.0, Set(pc, 1000, "setxp1000_again"));
    DelayCommand(31.4, VShot("h_again_04"));
    DelayCommand(33.0, VShot("h_again_2"));
    DelayCommand(36.0, VDone());
}
