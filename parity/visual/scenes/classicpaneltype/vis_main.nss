#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Talk(object npc, object pc) { AssignCommand(npc, ActionStartConversation(pc, "vis_panels", FALSE, FALSE)); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    object npc = VSpawn(pc, "nw_bandit001", 0.0, 3.0, 270.0);
    SetName(npc, "Panel Type Probe");
    DelayCommand(2.0, Tap(74));
    DelayCommand(3.0, VShot("01_journal_left"));
    DelayCommand(5.0, Talk(npc, pc));
    DelayCommand(7.0, VShot("02_journal_dialog"));
    DelayCommand(9.0, Tap(49));
    DelayCommand(10.0, VShot("03_journal_after"));
    DelayCommand(12.0, Tap(73));
    DelayCommand(13.0, VShot("04_inventory_right"));
    DelayCommand(15.0, Talk(npc, pc));
    DelayCommand(17.0, VShot("05_inventory_dialog"));
    DelayCommand(19.0, Tap(49));
    DelayCommand(20.0, VShot("06_after_reply"));
    DelayCommand(21.0, VDone());
}
