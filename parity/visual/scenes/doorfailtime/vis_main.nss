#include "visual_inc"
float Now(){return IntToFloat(GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;}
void Trial(object pc,object d,int key){
 SetLockKeyRequired(d,key);SetLocked(d,TRUE);AssignCommand(pc,ClearAllActions(TRUE));
 WriteTimestampedLogEntry("VISUAL|note|attempt|key="+IntToString(key)+"|t="+FloatToString(Now(),0,3)+"|distance="+FloatToString(GetDistanceBetween(pc,d),0,3));
 AssignCommand(pc,ActionOpenDoor(d));
}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;object d=GetObjectByTag("audio_door");
 SetEventScript(d,EVENT_SCRIPT_DOOR_ON_FAIL_TO_OPEN,"dt_fail");
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));DelayCommand(4.0,Trial(pc,d,FALSE));
 DelayCommand(4.4,VShot("plain_locked"));DelayCommand(8.0,Trial(pc,d,TRUE));
 DelayCommand(8.4,VShot("key_required"));DelayCommand(11.0,VDone());}
