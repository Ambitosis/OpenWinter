#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(TRUE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void State(object pc,object target,int d){Note("distance."+IntToString(d)+".valid."+IntToString(GetIsObjectValid(target))+".actual."+FloatToString(GetDistanceBetween(pc,target),0,3));}
void Move(object pc,object target,int d){vector p=GetPosition(pc);p.y+=IntToFloat(d);AssignCommand(target,JumpToLocation(Location(GetArea(pc),p,180.0)));DelayCommand(.5,State(pc,target,d));DelayCommand(1.0,VShot("distance_"+IntToString(d)));}
void Start(object source,object target){Note("start.valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectBeam(VFX_BEAM_LIGHTNING,source,BODY_NODE_HAND),target);}
void Remove(object target){Note("remove.valid."+IntToString(GetIsObjectValid(target)));SetPlotFlag(target,FALSE);SetIsDestroyable(TRUE,TRUE,TRUE,target);DestroyObject(target);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 object source=VSpawn(pc,"nw_bandit001",-2.0,3.0,0.0);object target=VSpawn(pc,"nw_bandit001",0.0,1.0,180.0);Quiet(source);Quiet(target);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));DelayCommand(4.0,Start(source,target));DelayCommand(4.5,State(pc,target,1));DelayCommand(5.0,VShot("distance_1"));
 DelayCommand(9.0,Move(pc,target,3));DelayCommand(14.0,Move(pc,target,6));DelayCommand(19.0,Move(pc,target,10));DelayCommand(24.0,Move(pc,target,20));DelayCommand(29.0,Move(pc,target,30));DelayCommand(34.0,Move(pc,target,40));DelayCommand(39.0,Move(pc,target,50));
 DelayCommand(44.0,Remove(target));DelayCommand(48.0,VDone());}
