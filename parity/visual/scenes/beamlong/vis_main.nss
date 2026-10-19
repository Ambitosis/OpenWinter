#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(TRUE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void State(object pc,object target,string tag){vector p=GetPosition(target);Note(tag+".valid."+IntToString(GetIsObjectValid(target))+".x."+FloatToString(p.x,0,3)+".y."+FloatToString(p.y,0,3)+".distance."+FloatToString(GetDistanceBetween(pc,target),0,3));}
void Move(object pc,object target,float y,string tag){vector p=GetPosition(pc);p.x+=1.0;p.y+=y;AssignCommand(target,JumpToLocation(Location(GetArea(pc),p,180.0)));DelayCommand(0.5,State(pc,target,tag));DelayCommand(1.0,VShot(tag));}
void Start(object source,object target){Note("start.valid."+IntToString(GetIsObjectValid(source))+IntToString(GetIsObjectValid(target)));ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectBeam(VFX_BEAM_LIGHTNING,source,BODY_NODE_HAND),target);}
void Remove(object target){Note("remove.valid."+IntToString(GetIsObjectValid(target)));SetPlotFlag(target,FALSE);SetIsDestroyable(TRUE,TRUE,TRUE,target);DestroyObject(target);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 object source=VSpawn(pc,"nw_bandit001",-1.0,3.0,0.0);object target=VSpawn(pc,"nw_bandit001",1.0,3.0,180.0);Quiet(source);Quiet(target);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(4.0,Start(source,target));DelayCommand(5.0,State(pc,target,"near_start"));DelayCommand(6.0,VShot("near_start"));
 DelayCommand(18.0,Move(pc,target,20.0,"far"));
 DelayCommand(34.0,Move(pc,target,3.0,"near_return"));
 DelayCommand(53.0,State(pc,target,"before_remove"));DelayCommand(53.0,VShot("before_remove"));
 DelayCommand(54.0,Remove(target));DelayCommand(55.0,State(pc,target,"after_remove"));DelayCommand(56.0,VShot("after_remove"));DelayCommand(58.0,VDone());}
