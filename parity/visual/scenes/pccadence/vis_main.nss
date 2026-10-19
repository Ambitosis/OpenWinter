#include "visual_inc"
#include "cadence_inc"
void Quiet(object o){ int i; for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);SetPlotFlag(o,TRUE);SetSoundset(o,65535);AssignCommand(o,ClearAllActions(TRUE)); }
void Ready(object actor,object target,string key){
 CNote("identity."+key,"valid."+IntToString(GetIsObjectValid(actor))+".pc."+IntToString(GetIsPC(actor))+".level."+IntToString(GetHitDice(actor))+".appearance."+IntToString(GetAppearanceType(actor))+".gender."+IntToString(GetGender(actor))+".weapon."+IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,actor)))+".seen."+IntToString(GetObjectSeen(target,actor))+".enemy."+IntToString(GetIsEnemy(target,actor)));
}
object Pair(object pc,object actor,string key,float x,float y){
 object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y+1.5,0.0),270.0));
 Quiet(actor);Quiet(target);SetCommandable(FALSE,target);
 SetLocalString(actor,"cadence_key",key);SetLocalObject(target,"cadence_attacker",actor);SetLocalObject(actor,"cadence_target",target);
 SetEventScript(actor,EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND,"cad_round");SetEventScript(target,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"cad_atk");
 SetIsTemporaryEnemy(actor,target);SetIsTemporaryEnemy(target,actor);
 object weapon=GetItemPossessedBy(actor,"NW_WSWDG001");AssignCommand(actor,ActionEquipItem(weapon,INVENTORY_SLOT_RIGHTHAND));
 DelayCommand(7.0,Ready(actor,target,key));return target;
}
void Start(object a,int count){SetBaseAttackBonus(count,a);CNote(GetLocalString(GetModule(),"cadence_phase")+"."+GetLocalString(a,"cadence_key")+".apr",IntToString(GetAttacksPerRound(a)));AssignCommand(a,ActionAttack(GetLocalObject(a,"cadence_target")));}
void Phase(object pc,object clone,int count){SetLocalString(GetModule(),"cadence_phase","n"+IntToString(count));Start(pc,count);Start(clone,count);}
void StopPair(object pc,object clone){AssignCommand(pc,ClearAllActions(TRUE));AssignCommand(clone,ClearAllActions(TRUE));}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"cadence_started"))return;SetLocalInt(GetModule(),"cadence_started",1);
 CClockStart();SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetPlotFlag(pc,TRUE);
 object clone=CopyObject(pc,Location(GetArea(pc),Vector(40.0,30.0,0.0),90.0),OBJECT_INVALID,"cadence_copy");
 CNote("copy.valid",IntToString(GetIsObjectValid(clone)));if(!GetIsObjectValid(clone)){VDone();return;}
 Pair(pc,pc,"pc",30.0,30.0);Pair(pc,clone,"copy",40.0,30.0);
 DelayCommand(1.0,VCamera(pc,90.0,20.0,75.0));
 DelayCommand(9.0,Phase(pc,clone,1));DelayCommand(45.0,StopPair(pc,clone));
 DelayCommand(53.0,Phase(pc,clone,2));DelayCommand(89.0,StopPair(pc,clone));
 DelayCommand(97.0,Phase(pc,clone,6));DelayCommand(133.0,StopPair(pc,clone));
 DelayCommand(10.0,VShot("one_attack"));DelayCommand(54.0,VShot("two_attacks"));DelayCommand(98.0,VShot("six_attacks"));DelayCommand(137.0,VDone());
}
