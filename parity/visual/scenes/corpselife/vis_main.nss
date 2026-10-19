#include "visual_inc"
object Actor(object pc,string res,float x,int loot){object c=VSpawn(pc,res,x,2.0,270.0);int i;for(i=5000;i<=5012;i++)SetEventScript(c,i,"");SetPlotFlag(c,FALSE);SetLootable(c,loot);object p=CreateItemOnObject("nw_it_mpotion001",c);SetDroppableFlag(p,TRUE);return c;}
void Kill(object c,int spectacular){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(spectacular),c);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 object a=Actor(pc,"nw_bandit001",-4.0,FALSE);object b=Actor(pc,"nw_bandit001",-2.0,TRUE);
 object c=Actor(pc,"nw_skeleton",0.0,FALSE);object d=Actor(pc,"nw_skeleton",2.0,FALSE);
 DelayCommand(1.0,VCamera(pc,90.0,12.0,55.0));DelayCommand(3.0,VShot("alive"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|burst|death|7|6"));
 DelayCommand(4.2,Kill(a,FALSE));DelayCommand(4.2,Kill(b,FALSE));DelayCommand(4.2,Kill(c,FALSE));DelayCommand(4.2,Kill(d,TRUE));
 DelayCommand(9.0,VShot("age4_8"));DelayCommand(9.5,VShot("age5_3"));DelayCommand(10.0,VShot("age5_8"));DelayCommand(10.5,VShot("age6_3"));DelayCommand(11.0,VShot("age6_8"));DelayCommand(12.0,VShot("settled"));DelayCommand(14.0,VDone());}
