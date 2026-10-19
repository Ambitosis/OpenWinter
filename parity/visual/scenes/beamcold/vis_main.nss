#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void Beam(object source,object target,float duration,string tag){Note(tag+".valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectBeam(VFX_BEAM_COLD,source,BODY_NODE_HAND),target,duration);DelayCommand(.5,VShot(tag));}
void Cue(object target){Note("play.valid."+IntToString(GetIsObjectValid(target)));AssignCommand(target,PlaySound("sim_raycold"));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 object source=VSpawn(pc,"nw_bandit001",-1.0,3.0,0.0);object target=VSpawn(pc,"nw_bandit001",1.0,3.0,180.0);Quiet(source);Quiet(target);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(4.0,Beam(source,target,1.7,"short"));DelayCommand(10.0,Beam(source,target,8.0,"long"));DelayCommand(20.0,Cue(target));DelayCommand(30.0,VDone());}
