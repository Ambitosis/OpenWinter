#include "visual_inc"
void Hit(object c){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_IMP_SUNSTRIKE),c);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 object a=VSpawn(pc,"nw_skeleton",-2.0,1.0,270.0);object b=VSpawn(pc,"nw_skeleton",2.0,1.0,270.0);
 int i;for(i=5000;i<=5012;i++){SetEventScript(a,i,"");SetEventScript(b,i,"");}SetPlotFlag(a,FALSE);SetPlotFlag(b,FALSE);
 DelayCommand(1.0,VCamera(pc,90.0,9.0,65.0));DelayCommand(3.0,VShot("alive"));
 DelayCommand(4.4,VShot("flash"));DelayCommand(4.2,Hit(a));DelayCommand(4.2,Hit(b));
 DelayCommand(4.3,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(TRUE),b));DelayCommand(9.0,VDone());}
