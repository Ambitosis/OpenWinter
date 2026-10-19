#include "visual_inc"
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN); SetPlotFlag(pc,TRUE);
 object npc=VSpawn(pc,"nw_bandit001",0.0,2.0,270.0); SetCommandable(FALSE,npc); SetSoundset(npc,GetSoundset(pc));
 DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));
 DelayCommand(3.0,AssignCommand(pc,ActionAttack(npc))); DelayCommand(4.0,VShot("attack_start"));
 DelayCommand(6.0,VShot("attack_swing")); DelayCommand(8.0,AssignCommand(pc,ClearAllActions()));
 DelayCommand(9.0,SetPlotFlag(npc,FALSE)); DelayCommand(9.1,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),npc));
 DelayCommand(10.0,VShot("dead")); DelayCommand(11.0,VDone());
}
