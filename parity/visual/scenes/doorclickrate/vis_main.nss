#include "visual_inc"
void Click() {
 WriteTimestampedLogEntry("VISUAL|mouse|0.441406,0.333333|down");
 DelayCommand(0.07,WriteTimestampedLogEntry("VISUAL|mouse|0.441406,0.333333|up"));
}
void Burst(int n) {Click(); if(n<59)DelayCommand(0.2,Burst(n+1));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object door=GetObjectByTag("audio_door");SetLocked(door,FALSE);SetPlotFlag(door,TRUE);
 SetEventScript(door,EVENT_SCRIPT_DOOR_ON_OPEN,"dr_open");SetEventScript(door,EVENT_SCRIPT_DOOR_ON_CLOSE,"dr_close");
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|mouse|0.441406,0.333333|up"));
 DelayCommand(4.0,Click());DelayCommand(6.0,VShot("a_open"));
 DelayCommand(7.0,Click());DelayCommand(9.0,VShot("b_closed"));
 DelayCommand(10.0,Burst(0));DelayCommand(25.0,VShot("c_after_burst"));
 DelayCommand(28.0,VDone());}
