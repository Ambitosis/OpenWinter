#include "visual_inc"
void RNote(string text){PrintString("VISUAL|note|"+text);}
void Start(object pc, string label){RNote(label+".start");AssignCommand(pc,ActionCastSpellAtObject(SPELL_MAGIC_MISSILE,pc,METAMAGIC_NONE,TRUE));}
void Cancel(object pc){RNote("clear.action_queue");AssignCommand(pc,ClearAllActions());}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
 DelayCommand(1.0,VCamera(pc,270.0,7.0,65.0));
 DelayCommand(3.0,Start(pc,"success"));
 DelayCommand(4.75,VShot("success_before"));
 DelayCommand(5.10,VShot("success_release"));
 DelayCommand(10.0,Start(pc,"cancel"));
 DelayCommand(11.75,Cancel(pc));
 DelayCommand(12.10,VShot("late_clear_release"));
 DelayCommand(13.0,VShot("cancel_clear"));
 DelayCommand(20.0,Start(pc,"early"));
 DelayCommand(21.0,Cancel(pc));
 DelayCommand(22.1,VShot("early_no_release"));
 DelayCommand(23.0,VShot("early_clear"));
 DelayCommand(24.0,VDone());
}
