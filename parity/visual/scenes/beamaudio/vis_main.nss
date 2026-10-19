#include "visual_inc"
void Note(string s) {WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o) {int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void Destroy(object o) {if(GetIsObjectValid(o)){SetPlotFlag(o,FALSE);SetIsDestroyable(TRUE,TRUE,TRUE,o);DestroyObject(o);}}
void Remove(object o) {effect e=GetFirstEffect(o);while(GetIsEffectValid(e)){if(GetEffectTag(e)=="beam_audio")RemoveEffect(o,e);e=GetNextEffect(o);}}
void State(int n,object source,object target) {Note("state."+IntToString(n)+".source."+IntToString(GetIsObjectValid(source))+".target."+IntToString(GetIsObjectValid(target))+".sourceDead."+IntToString(GetIsDead(source))+".targetDead."+IntToString(GetIsDead(target)));}
void Change(int n,object source,object target) {
 Note("change."+IntToString(n));
 if(n==2)Remove(target);
 if(n==3)Destroy(source);
 if(n==4)Destroy(target);
 if(n==5){SetPlotFlag(source,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),source);}
 if(n==6){SetPlotFlag(target,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),target);}
 DelayCommand(0.2,State(n,source,target));
}
void Apply(int n,object pc,object source,object target) {
 Note("start."+IntToString(n)+".valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));
 effect e=TagEffect(EffectBeam(VFX_BEAM_LIGHTNING,source,BODY_NODE_HAND),"beam_audio");
 if(n==7)ApplyEffectAtLocation(DURATION_TYPE_INSTANT,e,GetLocation(target));
 else {int duration=DURATION_TYPE_PERMANENT;if(n==0)duration=DURATION_TYPE_INSTANT;if(n==1)duration=DURATION_TYPE_TEMPORARY;ApplyEffectToObject(duration,e,target,2.0);}
 DelayCommand(0.5,VShot("beam_"+IntToString(n)));
 DelayCommand(4.0,Change(n,source,target));
 DelayCommand(8.0,State(n,source,target));
 DelayCommand(10.0,Destroy(source));DelayCommand(10.0,Destroy(target));
 DelayCommand(10.2,Note("end."+IntToString(n)));
}
void Case(int n,object pc) {
 object source=VSpawn(pc,"nw_bandit001",-2.0,3.0,0.0);object target=VSpawn(pc,"nw_bandit001",2.0,3.0,180.0);
 Quiet(source);Quiet(target);DelayCommand(1.0,Apply(n,pc,source,target));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(3.0,Case(0,pc));
 DelayCommand(15.0,Case(1,pc));
 DelayCommand(27.0,Case(2,pc));
 DelayCommand(39.0,Case(3,pc));
 DelayCommand(51.0,Case(4,pc));
 DelayCommand(63.0,Case(5,pc));
 DelayCommand(75.0,Case(6,pc));
 DelayCommand(87.0,Case(7,pc));
 DelayCommand(101.0,VDone());
}
