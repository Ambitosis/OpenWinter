#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Finished(string label) { Note(label + " finished"); }
void Cast(object pc, string label) {
    AssignCommand(pc, ClearAllActions());
    Note(label + " start");
    AssignCommand(pc, ActionCastSpellAtObject(SPELL_AID, pc, METAMAGIC_NONE, TRUE));
    AssignCommand(pc, ActionDoCommand(Finished(label)));
}
void Cancel(object pc) { Note("cancel now"); AssignCommand(pc, ClearAllActions()); }
void main() {
    object pc=GetEnteringObject(); if (!GetIsPC(pc)) return;
    VBegin(pc); SetPlotFlag(pc, TRUE);
    SetLocalString(GetModule(), "X2_S_UD_SPELLSCRIPT", "cs_hook");
    DelayCommand(1.0, VCamera(pc,90.0,7.0,65.0));
    DelayCommand(4.0, Cast(pc,"success"));
    DelayCommand(8.0, VShot("success_tail"));
    DelayCommand(15.0, Cast(pc,"cancel"));
    DelayCommand(15.8, Cancel(pc));
    DelayCommand(19.0, VShot("cancel_tail"));
    DelayCommand(25.0, VDone());
}
