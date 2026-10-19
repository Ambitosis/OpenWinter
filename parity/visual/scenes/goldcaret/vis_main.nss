#include "visual_inc"
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;GiveGoldToCreature(pc,123);
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|click|0.255,0.615"));
 DelayCommand(5.0,VShot("a_empty"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|key|50|tap"));
 DelayCommand(7.0,VShot("b_typed"));
 DelayCommand(8.0,VShot("c_typed"));
 DelayCommand(10.0,VDone());
}
