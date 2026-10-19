#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Positive(object o,int slot){Note("positive.slot."+IntToString(slot));PlayVoiceChat(slot,o);}
void Attack(object a,object t,int n){
 Note("fatal.context."+IntToString(n)+".valid."+IntToString(GetIsObjectValid(t))+".dead."+IntToString(GetIsDead(t))+".seen."+IntToString(GetObjectSeen(t,a))+".enemy."+IntToString(GetIsEnemy(t,a))+".distance."+FloatToString(GetDistanceBetween(a,t),0,2)+".commandable."+IntToString(GetCommandable(t)));
 if(!GetIsObjectValid(t)||GetIsDead(t)||!GetObjectSeen(t,a)||!GetIsEnemy(t,a)||GetDistanceBetween(a,t)>2.0){Note("INVALID_FATAL_CONTEXT.trial."+IntToString(n));VDone();return;}
 SetCommandable(FALSE,t);Note("fatal.attack."+IntToString(n)+".hp."+IntToString(GetCurrentHitPoints(t))+".seen.1.enemy.1");AssignCommand(a,ActionAttack(t));
}
void main(){object mod=GetModule();int n=GetLocalInt(mod,"next");object pc=GetLocalObject(mod,"pc");
 if(n>=20){Note("fatal.completed."+IntToString(GetLocalInt(mod,"deaths")));SetSoundset(pc,5100);DelayCommand(.5,Positive(pc,14));DelayCommand(.9,Positive(pc,15));DelayCommand(1.3,Positive(pc,16));DelayCommand(1.7,Positive(pc,18));DelayCommand(2.2,VShot("finished"));DelayCommand(3.0,VDone());return;}
 object a=GetLocalObject(mod,"attacker");object t=GetLocalObject(mod,"target_"+IntToString(n));SetLocalInt(mod,"next",n+1);AssignCommand(a,ClearAllActions(TRUE));SetCommandable(TRUE,t);AssignCommand(t,JumpToLocation(Location(GetArea(pc),Vector(30.0,32.0,0.0),90.0)));DelayCommand(.7,Attack(a,t,n));
}
