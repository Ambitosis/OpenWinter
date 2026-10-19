#include "visual_inc"
void Gate(object c,object t){WriteTimestampedLogEntry("VISUAL|note|turn_gate|seen="+IntToString(GetObjectSeen(t,c))+"|friend="+IntToString(GetIsFriend(t,c))+"|cleric="+IntToString(GetLevelByClass(CLASS_TYPE_CLERIC,c))+"|race="+IntToString(GetRacialType(t))+"|hd="+IntToString(GetHitDice(t)));}
void State(object t){WriteTimestampedLogEntry("VISUAL|note|turn_result|dead="+IntToString(GetIsDead(t))+"|valid="+IntToString(GetIsObjectValid(t)));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 object caster=CreateObject(OBJECT_TYPE_CREATURE,"tu_cleric",Location(GetArea(pc),Vector(38.0,41.0,0.0),0.0));
 object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_skeleton",Location(GetArea(pc),Vector(41.0,41.0,0.0),180.0));
 int i;for(i=5000;i<=5012;i++){SetEventScript(caster,i,"");SetEventScript(target,i,"");}
 ChangeToStandardFaction(caster,STANDARD_FACTION_DEFENDER);ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);
 SetIsTemporaryEnemy(target,caster);SetIsTemporaryEnemy(caster,target);
 SetAILevel(caster,AI_LEVEL_NORMAL);SetAILevel(target,AI_LEVEL_NORMAL);SetPlotFlag(caster,TRUE);
 DelayCommand(1.0,VCamera(pc,90.0,10.0,65.0));DelayCommand(3.0,VShot("alive"));
 DelayCommand(10.0,Gate(caster,target));
 DelayCommand(11.0,AssignCommand(caster,ActionUseFeat(FEAT_TURN_UNDEAD,caster)));
 DelayCommand(12.5,WriteTimestampedLogEntry("VISUAL|burst|impact|3|10"));
 DelayCommand(16.0,VShot("corpse"));DelayCommand(16.0,State(target));DelayCommand(21.0,VShot("remains"));DelayCommand(23.0,VDone());}
