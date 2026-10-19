#include "visual_inc"
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
DelayCommand(4.0,AssignCommand(pc,SummonFamiliar()));
DelayCommand(4.2,VShot("a_builtin"));
DelayCommand(8.0,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(36),pc));
DelayCommand(8.2,VShot("b_row36"));
DelayCommand(12.0,VDone());}
