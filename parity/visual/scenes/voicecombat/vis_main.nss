#include "visual_inc"
void Hit(object o) { ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(1,DAMAGE_TYPE_MAGICAL),o); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN); SetPlotFlag(pc,TRUE);
 object npc=VSpawn(pc,"nw_bandit001",0.0,2.0,270.0); SetCommandable(FALSE,npc); SetSoundset(npc,GetSoundset(pc));
 DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));
 DelayCommand(3.0,AssignCommand(pc,ActionAttack(npc))); DelayCommand(4.0,VShot("attack_start"));
 DelayCommand(6.0,VShot("attack_swing")); DelayCommand(8.0,AssignCommand(pc,ClearAllActions()));
 DelayCommand(10.0,PlayVoiceChat(VOICE_CHAT_ATTACK,pc)); DelayCommand(11.0,VShot("attack_reference"));
 DelayCommand(14.0,PlayVoiceChat(VOICE_CHAT_BATTLECRY1,pc)); DelayCommand(15.0,VShot("battlecry_reference"));
 DelayCommand(18.0,SetPlotFlag(npc,FALSE)); DelayCommand(18.1,Hit(npc)); DelayCommand(19.0,VShot("hurt"));
 DelayCommand(22.0,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),npc)); DelayCommand(23.0,VShot("dead"));
 DelayCommand(25.0,VDone());
}
