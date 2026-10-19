#include "visual_inc"
// A store opened by an answer stays open beside the live conversation (convoend3 store_tail_on). Then the character sheet, journal, spellbook and map open one after another: which
// panel the fourth and fifth evict, and whether the conversation lasts.
void Tap(int vk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|tap"); }
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s + "|talking=" + IntToString(IsInConversation(GetFirstPC()))); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
    DelayCommand(3.0, AssignCommand(GetObjectByTag("probe_npc"), ActionStartConversation(pc, "scp_convo", FALSE, FALSE)));
    DelayCommand(5.0, Tap(49));
    DelayCommand(7.0, Note("a_store_convo")); DelayCommand(7.0, VShot("a_store_convo"));
    DelayCommand(8.0, Tap(67)); DelayCommand(10.0, Note("b_character")); DelayCommand(10.0, VShot("b_character"));
    DelayCommand(12.0, Tap(74)); DelayCommand(14.0, Note("c_journal")); DelayCommand(14.0, VShot("c_journal"));
    DelayCommand(16.0, Tap(66)); DelayCommand(18.0, Note("d_spellbook")); DelayCommand(18.0, VShot("d_spellbook"));
    DelayCommand(20.0, Tap(77)); DelayCommand(22.0, Note("e_map")); DelayCommand(22.0, VShot("e_map"));
    DelayCommand(24.0, Tap(73)); DelayCommand(26.0, Note("f_inventory")); DelayCommand(26.0, VShot("f_inventory"));
    DelayCommand(29.0, VDone());
}
