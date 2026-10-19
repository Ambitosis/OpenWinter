#include "visual_inc"
void Cast(object pc) {
 WriteTimestampedLogEntry("VISUAL|note|start.6");
 AssignCommand(pc,ActionCastSpellAtObject(SPELL_BLESS,pc,METAMAGIC_ANY,TRUE));
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetPlotFlag(pc,TRUE); SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));
 DelayCommand(4.0,Cast(pc));
 DelayCommand(5.0,VShot("conjure")); DelayCommand(8.0,VShot("impact"));
 DelayCommand(12.0,VDone());
}
