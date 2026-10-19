#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(TRUE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void Camera(object pc,float distance){VCamera(pc,90.0,distance,65.0);Note("camera."+FloatToString(distance,0,0));}
void Start(object source,object target){Note("start.valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectBeam(VFX_BEAM_LIGHTNING,source,BODY_NODE_HAND),target,18.0);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 object source=VSpawn(pc,"nw_bandit001",-1.0,3.0,0.0);object target=VSpawn(pc,"nw_bandit001",1.0,20.0,180.0);Quiet(source);Quiet(target);
 DelayCommand(1.0,Camera(pc,6.0));DelayCommand(4.0,Start(source,target));DelayCommand(6.0,VShot("camera6"));
 DelayCommand(10.0,Camera(pc,14.0));DelayCommand(12.0,VShot("camera14"));DelayCommand(16.0,Camera(pc,24.0));DelayCommand(18.0,VShot("camera24"));DelayCommand(24.0,VDone());}
