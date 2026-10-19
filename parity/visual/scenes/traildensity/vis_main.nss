#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
 DelayCommand(.5,VCamera(pc,90.0,13.0,55.0));
 DelayCommand(1.0,VShot("baseline"));
 DelayCommand(2.0,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_IMP_AC_BONUS),pc));
 DelayCommand(2.5,VShot("moving050"));
 DelayCommand(3.5,VShot("moving150"));
 DelayCommand(4.5,VShot("moving250"));
 DelayCommand(5.25,VShot("stopped025"));
 DelayCommand(5.75,VShot("stopped075"));
 DelayCommand(6.5,VShot("draining"));
 DelayCommand(8.0,VShot("clear"));
 DelayCommand(8.5,VDone());
}
