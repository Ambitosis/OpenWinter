#include "visual_inc"
void Quiet(object o) { int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,""); SetAILevel(o,AI_LEVEL_NORMAL); SetPlotFlag(o,TRUE); }
void Stop(object o) { AssignCommand(o,ClearAllActions(TRUE)); SetCommandable(FALSE,o); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN); SetPlotFlag(pc,TRUE);
 int i; for(i=0;i<1;i++) {
  float x=20.0+IntToFloat(i%4)*6.0; float y=24.0+IntToFloat(i/4)*6.0;
  object a=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y,0.0),90.0));
  object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y+1.5,0.0),270.0));
  Quiet(a); Quiet(b); SetCommandable(FALSE,b); SetSoundset(a,GetSoundset(pc)); SetSoundset(b,GetSoundset(pc));
  ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE); ChangeToStandardFaction(b,STANDARD_FACTION_COMMONER);
  DelayCommand(3.0,AssignCommand(a,ActionAttack(b))); DelayCommand(43.0,Stop(a));
 }
 DelayCommand(1.0,VCamera(pc,90.0,12.0,65.0));
 DelayCommand(4.0,VShot("attack_start")); DelayCommand(7.0,VShot("attack_swing")); DelayCommand(10.0,VShot("attack_late")); DelayCommand(25.0,VShot("attack_mid")); DelayCommand(40.0,VShot("attack_end"));
 DelayCommand(45.0,VDone());
}
