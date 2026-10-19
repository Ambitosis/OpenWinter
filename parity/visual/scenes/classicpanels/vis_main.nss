// Authored coexistence/closure control; no assumed placement rules.
#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Talk(object npc, object pc) { AssignCommand(npc, ActionStartConversation(pc, "vis_panels", FALSE, FALSE)); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    object npc = VSpawn(pc, "nw_bandit001", 0.0, 3.0, 270.0);
    SetName(npc, "Panel Conversation Probe");
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(3.0, Tap(73));
    DelayCommand(4.0, VShot("01_inventory"));
    DelayCommand(5.0, Tap(67));
    DelayCommand(6.0, VShot("02_inventory_character"));
    DelayCommand(7.0, Tap(74));
    DelayCommand(8.0, VShot("03_three"));
    DelayCommand(9.0, Tap(67));
    DelayCommand(10.0, VShot("04_close_character"));
    DelayCommand(11.0, Tap(67));
    DelayCommand(12.0, VShot("05_reopen_character"));
    DelayCommand(13.0, Tap(73));
    DelayCommand(14.0, VShot("06_close_inventory"));
    DelayCommand(15.0, Tap(74));
    DelayCommand(16.0, VShot("07_character_only"));
    DelayCommand(17.0, Tap(67));
    DelayCommand(18.0, VShot("08_closed"));
    DelayCommand(19.0, Tap(66));
    DelayCommand(20.0, VShot("09_spellbook_pair"));
    DelayCommand(21.0, Tap(73));
    DelayCommand(22.0, VShot("10_book_inventory"));
    DelayCommand(23.0, Tap(74));
    DelayCommand(24.0, VShot("11_book_inventory_journal"));
    DelayCommand(25.0, Tap(66));
    DelayCommand(26.0, VShot("12_close_book"));
    DelayCommand(27.0, Tap(73));
    DelayCommand(28.0, VShot("13_journal_only"));
    DelayCommand(29.0, Tap(67));
    DelayCommand(30.0, VShot("14_journal_character"));
    DelayCommand(31.0, Tap(73));
    DelayCommand(32.0, VShot("15_journal_character_inventory"));
    DelayCommand(33.0, Tap(74));
    DelayCommand(34.0, VShot("16_close_oldest"));
    DelayCommand(35.0, Tap(74));
    DelayCommand(36.0, VShot("17_before_conversation"));
    DelayCommand(37.0, Talk(npc, pc));
    DelayCommand(39.0, VShot("18_conversation"));
    DelayCommand(40.0, Tap(49));
    DelayCommand(42.0, VShot("19_after_conversation"));
    DelayCommand(43.0, VDone());
}
