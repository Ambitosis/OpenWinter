#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","gr_cast");
 SetMemorizedSpell(pc,CLASS_TYPE_WIZARD,1,0,SPELL_GREASE,TRUE);
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(23.0,VDone());
}
