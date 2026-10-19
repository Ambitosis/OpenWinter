#include "visual_inc"
void main()
{
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 object target=VSpawn(pc,"nw_bandit001",0.0,1.0,270.0);
 DelayCommand(1.0,VCamera(pc,270.0,12.0,65.0));
 DelayCommand(2.0,ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectVisualEffect(VFX_DUR_SPELLTURNING),target,11.0));
 DelayCommand(2.5,VShot("small"));DelayCommand(4.5,VShot("large"));
 DelayCommand(6.5,VShot("small_again"));DelayCommand(8.5,VShot("red"));
 DelayCommand(10.5,VShot("off"));DelayCommand(14.0,VShot("removed"));
 DelayCommand(14.5,VDone());
}
