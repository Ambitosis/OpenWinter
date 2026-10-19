#include "visual_inc"
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void Destroy(object o){SetPlotFlag(o,FALSE);SetIsDestroyable(TRUE,TRUE,TRUE,o);DestroyObject(o);}
void Apply(int n,object source,object target){
 WriteTimestampedLogEntry("VISUAL|note|anchor."+IntToString(n)+".valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));
 ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectBeam(VFX_BEAM_LIGHTNING,source,BODY_NODE_HAND),target,2.0);
 DelayCommand(0.5,VShot("anchor_"+IntToString(n)));
 DelayCommand(3.0,Destroy(source));DelayCommand(3.0,Destroy(target));
}
void Case(object pc,int n,float sourceY,float targetY){
 object source=VSpawn(pc,"nw_bandit001",-1.0,sourceY,0.0);object target=VSpawn(pc,"nw_bandit001",1.0,targetY,180.0);Quiet(source);Quiet(target);
 DelayCommand(1.0,Apply(n,source,target));
}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(3.0,Case(pc,0,3.0,20.0));
 DelayCommand(9.0,Case(pc,1,20.0,3.0));
 DelayCommand(15.0,Case(pc,2,3.0,3.0));
 DelayCommand(21.0,VDone());}
