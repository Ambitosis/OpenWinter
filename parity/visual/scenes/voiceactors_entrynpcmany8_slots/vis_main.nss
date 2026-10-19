#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);AssignCommand(o,ClearAllActions(TRUE));}
void Positive(object actor,int n,int slot){Note("positive.actor."+IntToString(n)+".slot."+IntToString(slot));PlayVoiceChat(slot,actor);}
void Start(object actor,object target,int n){
 int ready=GetIsObjectValid(actor)&&GetIsObjectValid(target)&&!GetIsPC(actor)&&!GetIsDead(actor)&&!GetPlotFlag(actor)&&GetCommandable(actor)&&GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,actor))==BASE_ITEM_DAGGER&&GetDistanceBetween(actor,target)<=2.0&&GetSoundset(actor)==5100+n&&GetObjectSeen(target,actor)&&GetIsEnemy(target,actor)&&!GetIsInCombat(actor)&&GetLocalInt(target,"attacks")==0;
 Note("entry.start."+IntToString(n)+".ready."+IntToString(ready)+".pc."+IntToString(GetIsPC(actor))+".apr."+IntToString(GetAttacksPerRound(actor))+".commandable."+IntToString(GetCommandable(actor))+".distance."+FloatToString(GetDistanceBetween(actor,target),0,2)+".soundset."+IntToString(GetSoundset(actor)));
 if(!ready){Note("INVALID_PC_CONTEXT");VDone();return;}AssignCommand(actor,ActionAttack(target));
}
void State(object actor,object target,int n){Note("entry.state."+IntToString(n)+".action."+IntToString(GetCurrentAction(actor))+".target."+IntToString(GetAttackTarget(actor)==target)+".combat."+IntToString(GetIsInCombat(actor))+".distance."+FloatToString(GetDistanceBetween(actor,target),0,2));}
void Stop(object actor,object target,int n){
 AssignCommand(actor,ClearAllActions(TRUE));int count=GetLocalInt(target,"attacks");Note("entry.stop."+IntToString(n)+".attacks."+IntToString(count)+".dead."+IntToString(GetIsDead(actor)));
 SetIsDestroyable(TRUE,FALSE,FALSE,target);SetPlotFlag(target,FALSE);DestroyObject(target);
 if(count<1)Note("INVALID_ENTRY_ATTACK");
}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;Quiet(pc);SetPlotFlag(pc,TRUE);SetSoundset(pc,5099);SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 int i;for(i=0;i<20;i++){
  float x=24.0+IntToFloat(i%4)*4.0;float y=26.0+IntToFloat(i/4)*4.0;
  object actor=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y,0.0),90.0));
  object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y+1.5,0.0),270.0));
  Quiet(actor);Quiet(target);SetPlotFlag(actor,FALSE);SetPlotFlag(target,TRUE);SetCommandable(FALSE,target);SetSoundset(actor,5100+i);SetSoundset(target,5099);
  ChangeToStandardFaction(actor,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(target,STANDARD_FACTION_COMMONER);
  object dagger=CreateItemOnObject("nw_wswdg001",actor);AssignCommand(actor,ActionEquipItem(dagger,INVENTORY_SLOT_RIGHTHAND));
  SetLocalInt(target,"trial",i);SetEventScript(target,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"vis_atk");
  SetLocalObject(target,"expectedattacker",actor);
  float start=9.0+IntToFloat(i)*1.5;DelayCommand(start,Start(actor,target,i));DelayCommand(start+1.0,State(actor,target,i));DelayCommand(start+6.0,State(actor,target,i));DelayCommand(start+8.0,Stop(actor,target,i));
  float positive=50.0+IntToFloat(i)*1.7;int slot;for(slot=0;slot<4;slot++)DelayCommand(positive+IntToFloat(slot)*0.4,Positive(actor,i,slot));
 }
 DelayCommand(1.0,VCamera(pc,90.0,20.0,75.0));DelayCommand(3.0,VShot("ready"));DelayCommand(47.0,Note("entry.completed.20"));DelayCommand(48.0,VShot("after_entries"));DelayCommand(85.0,VShot("finished"));DelayCommand(86.0,VDone());}
