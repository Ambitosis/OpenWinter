#include "visual_inc"
float Now(){return IntToFloat(GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;}
void Sample(object pc,object d,string label,int n=0){
 WriteTimestampedLogEntry("VISUAL|note|"+label+"|n="+IntToString(n)+"|t="+FloatToString(Now(),0,3)+"|open="+IntToString(GetIsOpen(d))+"|action="+IntToString(GetCurrentAction(pc))+"|distance="+FloatToString(GetDistanceBetween(pc,d),0,3));
 if(n<24)DelayCommand(0.1,Sample(pc,d,label,n+1));}
void Trial(object pc,object d,int open){AssignCommand(pc,ClearAllActions(TRUE));if(open)AssignCommand(pc,ActionOpenDoor(d));else AssignCommand(pc,ActionCloseDoor(d));Sample(pc,d,open?"opening":"closing");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;object d=GetObjectByTag("audio_door");SetLocked(d,FALSE);SetEventScript(d,EVENT_SCRIPT_DOOR_ON_OPEN,"dr_open");SetEventScript(d,EVENT_SCRIPT_DOOR_ON_CLOSE,"dr_close");
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));DelayCommand(4.0,Trial(pc,d,TRUE));DelayCommand(4.5,VShot("reach"));DelayCommand(5.3,VShot("opened"));DelayCommand(8.0,Trial(pc,d,FALSE));DelayCommand(12.0,Trial(pc,d,TRUE));DelayCommand(16.0,Trial(pc,d,TRUE));DelayCommand(20.0,VDone());}
