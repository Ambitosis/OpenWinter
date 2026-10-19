#include "visual_inc"
void Quiet(object o) { int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,""); SetAILevel(o,AI_LEVEL_NORMAL); SetPlotFlag(o,TRUE); }
void Stop(object o) { AssignCommand(o,ClearAllActions(TRUE)); WriteTimestampedLogEntry("VISUAL|voice_stop|"+IntToString(GetLocalInt(o,"actor"))); }
void State(object o) { WriteTimestampedLogEntry("VISUAL|voice_state|"+IntToString(GetLocalInt(o,"actor"))+"|"+IntToString(GetCurrentAction(o))+"|"+IntToString(GetIsInCombat(o))); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN); SetPlotFlag(pc,TRUE);
 int i; for(i=0;i<1;i++) {
  float x=30.0; float y=32.0;
  object a=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y,0.0),90.0));
  object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y+6.0,0.0),270.0));
  Quiet(a); Quiet(b); SetCommandable(FALSE,b); SetSoundset(a,5100+i); SetSoundset(b,5099); SetName(a,"Voice "+IntToString(i));
  SetBaseAttackBonus(6,a); SetEventScript(a,EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND,"vis_round"); SetLocalInt(a,"actor",i); SetLocalInt(b,"actor",i); SetEventScript(b,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"vis_atk");
  ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE); ChangeToStandardFaction(b,STANDARD_FACTION_COMMONER);
  object bow=CreateItemOnObject("nw_wbwln001",a);
  object ammo=CreateItemOnObject("nw_wamar001",a,99);
  AssignCommand(a,ActionEquipItem(bow,INVENTORY_SLOT_RIGHTHAND));
  AssignCommand(a,ActionEquipItem(ammo,INVENTORY_SLOT_ARROWS));
  DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|voice_equipped|"+IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a)))+"|"+IntToString(GetItemStackSize(GetItemInSlot(INVENTORY_SLOT_ARROWS,a)))));
  DelayCommand(101.0,WriteTimestampedLogEntry("VISUAL|voice_ammo_remaining|"+IntToString(GetItemStackSize(GetItemInSlot(INVENTORY_SLOT_ARROWS,a)))));
  float start=9.0+IntToFloat(i)*0.2;
  DelayCommand(start,WriteTimestampedLogEntry("VISUAL|voice_actor|"+IntToString(i)+"|start"));
  DelayCommand(start,WriteTimestampedLogEntry("VISUAL|voice_seen|"+IntToString(i)+"|"+IntToString(GetObjectSeen(b,a))));
  DelayCommand(start,AssignCommand(a,ActionAttack(b))); DelayCommand(start+90.0,Stop(a));
  DelayCommand(start+0.1,State(a)); DelayCommand(start+89.5,State(a)); DelayCommand(start+90.5,State(a)); DelayCommand(start+91.0,State(a));
 }
 DelayCommand(1.0,VCamera(pc,90.0,20.0,75.0));
 DelayCommand(4.0,VShot("attack_start")); DelayCommand(7.0,VShot("attack_swing")); DelayCommand(10.0,VShot("attack_late")); DelayCommand(25.0,VShot("attack_mid")); DelayCommand(40.0,VShot("attack_end"));
 DelayCommand(102.0,VDone());
}
