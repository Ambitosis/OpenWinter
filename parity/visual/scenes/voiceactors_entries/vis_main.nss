#include "visual_inc"
void Quiet(object o) { int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,""); SetAILevel(o,AI_LEVEL_NORMAL); SetPlotFlag(o,TRUE); }
void Start(object actor,object target) {
 WriteTimestampedLogEntry("VISUAL|voice_actor|"+IntToString(GetLocalInt(actor,"actor"))+"|start");
 WriteTimestampedLogEntry("VISUAL|voice_seen|"+IntToString(GetLocalInt(actor,"actor"))+"|"+IntToString(GetObjectSeen(target,actor)));
 AssignCommand(actor,ActionAttack(target));
}
void Stop(object actor) { AssignCommand(actor,ClearAllActions(TRUE)); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetPlotFlag(pc,TRUE); SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 int i;for(i=0;i<20;i++) {
  float x=24.0+IntToFloat(i%4)*4.0;float y=26.0+IntToFloat(i/4)*4.0;
  object a=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y,0.0),90.0));
  object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y+1.5,0.0),270.0));
  Quiet(a);Quiet(b);SetCommandable(FALSE,b);SetSoundset(a,5100+i);SetSoundset(b,5099);
  ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(b,STANDARD_FACTION_COMMONER);
  SetLocalInt(a,"actor",i);SetLocalInt(b,"actor",i);SetEventScript(b,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"vis_atk");
  float start=9.0+IntToFloat(i)*6.0;
  DelayCommand(start,Start(a,b));DelayCommand(start+2.5,Stop(a));
 }
 DelayCommand(1.0,VCamera(pc,90.0,20.0,75.0));
 DelayCommand(12.0,VShot("early"));DelayCommand(72.0,VShot("middle"));DelayCommand(126.0,VShot("late"));
 DelayCommand(128.0,VDone());
}
