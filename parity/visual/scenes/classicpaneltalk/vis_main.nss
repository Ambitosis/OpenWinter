#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Talk(object npc, object pc) { AssignCommand(npc, ActionStartConversation(pc, "vis_panels", FALSE, FALSE)); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    object npc = VSpawn(pc, "nw_bandit001", 0.0, 3.0, 270.0);
    SetName(npc, "Panel Conversation Probe");
    DelayCommand(2.0, Tap(73));
    DelayCommand(4.0, Tap(67));
    DelayCommand(6.0, Tap(74));
    DelayCommand(8.0, VShot("01_three_panels"));
    DelayCommand(10.0, Talk(npc, pc));
    DelayCommand(12.0, VShot("02_conversation"));
    DelayCommand(14.0, Tap(49));
    DelayCommand(16.0, VShot("03_after_reply"));
    DelayCommand(17.0, VDone());
}
