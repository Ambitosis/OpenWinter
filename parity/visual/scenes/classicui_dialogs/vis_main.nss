// The original interface's conversations, game menu and death panel, for the Classic UI mode: a base-game
// conversation (x1_harper) started by a creature, a reply chosen with its number key, the game menu (Esc;
// it pauses single player, so the harness taps the key and shoots on its own clock: VISUAL|keyshot) and the
// death panel. Not in cutscene mode, so the GUI shows.
#include "visual_inc"

void Tap(int nVk)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap");
}

void Talk(object oNpc, object oPC)
{
    AssignCommand(oNpc, ActionStartConversation(oPC, "x1_harper", FALSE, FALSE));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    object oNpc = VSpawn(oPC, "nw_bandit001", 0.0, 3.0, 270.0);
    DelayCommand(2.0, VCamera(oPC, 90.0, 8.0, 60.0));
    DelayCommand(4.0, Talk(oNpc, oPC));
    DelayCommand(7.0, VShot("a_dialog"));
    // 1: the first reply; the creature answers with its next line.
    DelayCommand(8.0, Tap(49));
    DelayCommand(10.0, VShot("b_dialog_reply"));
    // Esc ends the conversation.
    DelayCommand(11.0, Tap(27));
    DelayCommand(13.0, VShot("c_after"));
    // Esc again: the game menu (paused); the harness shoots it and closes it with Esc.
    DelayCommand(14.0, WriteTimestampedLogEntry("VISUAL|keyshot|27|d_menu|2.0|27"));
    DelayCommand(16.0, PopUpDeathGUIPanel(oPC, TRUE, TRUE, 0, ""));
    DelayCommand(18.0, VShot("e_death"));
    DelayCommand(20.0, VDone());
}
