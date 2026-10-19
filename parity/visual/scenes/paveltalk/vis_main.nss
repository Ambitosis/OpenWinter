// Pavel (Prelude, senior barracks, holding a torch) in a conversation the player starts: he turns to the player,
// lets the torch down while talking, and plays the talk animations of the lines that ask for them (his own
// dialogue's lines all use the default, 0, which plays nothing). The conversation is the scene's own.
#include "visual_inc"

void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }

void Shots(string s, float t)
{
    DelayCommand(t + 0.4, VShot(s + "_0"));
    DelayCommand(t + 1.2, VShot(s + "_1"));
    DelayCommand(t + 2.2, VShot(s + "_2"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    object oPavel = GetObjectByTag("M1Q0APavel");
    // The player 3.5 m to Pavel's west, facing away from him.
    AssignCommand(oPC, ClearAllActions(TRUE));
    AssignCommand(oPC, JumpToLocation(Location(GetArea(oPC), GetPosition(oPavel) + Vector(-3.5, 0.0, 0.0), 180.0)));
    DelayCommand(3.0, VCamera(oPC, 20.0, 5.5, 70.0));
    DelayCommand(5.0, VShot("a_before"));
    DelayCommand(6.0, AssignCommand(oPC, ActionStartConversation(oPavel, "vis_pt")));
    Shots("b_line0", 7.0);
    DelayCommand(10.0, Tap(49)); Shots("c_line38", 10.0);
    DelayCommand(14.0, Tap(49)); Shots("d_line40", 14.0);
    DelayCommand(18.0, Tap(49));
    DelayCommand(19.5, VShot("e_after"));
    DelayCommand(21.0, VDone());
}
