#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Talk(object npc, object pc) { AssignCommand(npc, ActionStartConversation(pc, "vis_panels", FALSE, FALSE)); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    object npc = VSpawn(pc, "nw_bandit001", 0.0, 3.0, 270.0);
    SetName(npc, "Panel Book Probe");
    DelayCommand(2.0, Tap(66));
    DelayCommand(4.0, VShot("01_book_pair"));
    DelayCommand(6.0, Talk(npc, pc));
    DelayCommand(8.0, VShot("02_conversation"));
    DelayCommand(10.0, Tap(49));
    DelayCommand(12.0, VShot("03_after_reply"));
    DelayCommand(13.0, VDone());
}
