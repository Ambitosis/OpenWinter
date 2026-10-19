#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void State(object target,string tag){Note(tag+".valid."+IntToString(GetIsObjectValid(target))+".dead."+IntToString(GetIsDead(target)));}
void Kill(object target){SetPlotFlag(target,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),target);Note("kill");}
void Apply(object source,object target){Note("start.valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBeam(VFX_BEAM_LIGHTNING,source,BODY_NODE_HAND),target);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 object source=VSpawn(pc,"nw_bandit001",-2.0,3.0,0.0);object target=VSpawn(pc,"nw_bandit001",2.0,3.0,180.0);Quiet(source);Quiet(target);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(4.0,Apply(source,target));DelayCommand(4.5,VShot("before_death"));
 DelayCommand(8.0,Kill(target));DelayCommand(8.1,State(target,"after_kill"));
 DelayCommand(12.0,State(target,"four_after"));DelayCommand(12.9,State(target,"before_decay"));
 DelayCommand(13.5,State(target,"after_decay"));DelayCommand(14.0,VShot("after_decay"));
 DelayCommand(16.0,State(target,"eight_after"));DelayCommand(20.0,VDone());}
