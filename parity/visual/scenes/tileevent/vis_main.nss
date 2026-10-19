#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_TILE_ACTION,"ta_event");
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|mouse|0.70,0.50|down|right"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(5.0,VShot("submenu"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|key|104|tap"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|mouse|0.70,0.50|up|right"));
 DelayCommand(9.0,VDone());
}
