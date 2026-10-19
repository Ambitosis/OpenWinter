#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
 DelayCommand(.5,VCamera(pc,90.0,15.0,55.0));
 DelayCommand(1.0,VShot("baseline"));
 DelayCommand(2.0,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_IMP_AC_BONUS),pc));
 DelayCommand(2.5,VShot("turn_050"));
 DelayCommand(3.0,VShot("turn_100"));
 DelayCommand(4.0,VShot("turn_200"));
 DelayCommand(5.0,VShot("turn_300"));
 DelayCommand(6.0,VShot("turn_400"));
 DelayCommand(7.5,VShot("draining"));
 DelayCommand(10.0,VShot("clear"));
 DelayCommand(10.5,VDone());
}
