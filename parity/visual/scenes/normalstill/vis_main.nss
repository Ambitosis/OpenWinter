#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
 DelayCommand(.5,VCamera(pc,90.0,15.0,55.0));
 DelayCommand(1.0,VShot("baseline"));
 DelayCommand(2.0,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_IMP_AC_BONUS),pc));
 DelayCommand(2.5,VShot("early"));
 DelayCommand(3.5,VShot("steady"));
 DelayCommand(4.5,VShot("late"));
 DelayCommand(7.0,VShot("clear"));
 DelayCommand(7.5,VDone());
}
