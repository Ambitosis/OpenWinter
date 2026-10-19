#include "visual_inc"
void Cast(object who){AssignCommand(who,ActionCastSpellAtObject(3,who,METAMAGIC_NONE,TRUE));}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 object control=VSpawn(pc,"nw_hen_sha_11",-2.0,1.0,270.0);
 object release=VSpawn(pc,"nw_hen_sha_11",0.0,1.0,270.0);
 object recovery=VSpawn(pc,"nw_hen_sha_11",2.0,1.0,270.0);
 SetEventScript(control,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 SetEventScript(release,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 SetEventScript(recovery,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 ReplaceObjectAnimation(release,"castself","sitcross");
 ReplaceObjectAnimation(recovery,"castselflp","worship");
 DelayCommand(1.0,VCamera(pc,90.0,9.0,65.0));
 DelayCommand(3.0,Cast(control));DelayCommand(3.0,Cast(release));DelayCommand(3.0,Cast(recovery));
 DelayCommand(3.75,VShot("conjure"));
 DelayCommand(4.75,VShot("release"));
 DelayCommand(5.10,VShot("recovery_start"));
 DelayCommand(5.60,VShot("recovery_mid"));
 DelayCommand(5.85,VShot("recovery_end"));
 DelayCommand(6.5,VShot("finished"));
 DelayCommand(8.0,VDone());
}
