#include "visual_inc"
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(2.5,AssignCommand(pc,PlaySound("gui_radial_open")));
 DelayCommand(2.5,WriteTimestampedLogEntry("VISUAL|note|radial_open_control"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(5.5,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(6.6,WriteTimestampedLogEntry("VISUAL|hover|0.5,0.49"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.5,0.49"));
 DelayCommand(7.8,VShot("a_root"));
 DelayCommand(8.5,WriteTimestampedLogEntry("VISUAL|key|105|tap"));
 DelayCommand(9.5,VShot("b_submenu"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(11.0,VDone());
}
