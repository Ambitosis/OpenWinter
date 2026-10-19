#include "visual_inc"
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|mouse|0.445,0.599|down"));
 DelayCommand(6.0,VShot("a_southwest_down"));
 DelayCommand(6.3,WriteTimestampedLogEntry("VISUAL|mouse|0.445,0.599|up"));
 DelayCommand(7.0,VShot("b_southwest"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|mouse|0.445,0.401|down"));
 DelayCommand(10.0,VShot("c_northwest_down"));
 DelayCommand(10.3,WriteTimestampedLogEntry("VISUAL|mouse|0.445,0.401|up"));
 DelayCommand(11.0,VShot("d_northwest"));
 DelayCommand(12.0,VDone());
}
