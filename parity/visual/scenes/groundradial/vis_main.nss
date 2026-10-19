#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(2.0,VShot("zero_input_control"));
 DelayCommand(2.5,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(4.0,VShot("self_input_control"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(7.5,WriteTimestampedLogEntry("VISUAL|hover|0.70,0.50"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.70,0.50"));
 DelayCommand(9.0,VShot("a_ground_root"));
 DelayCommand(9.3,WriteTimestampedLogEntry("VISUAL|hover|0.779,0.50"));
 DelayCommand(11.0,VShot("b_ground_e_label"));
 DelayCommand(11.3,WriteTimestampedLogEntry("VISUAL|hover|0.755,0.598"));
 DelayCommand(13.0,VShot("c_ground_se_label"));
 DelayCommand(13.3,WriteTimestampedLogEntry("VISUAL|mouse|0.755,0.598|down"));
 DelayCommand(13.5,WriteTimestampedLogEntry("VISUAL|mouse|0.755,0.598|up"));
 DelayCommand(14.5,VShot("d_ground_se_menu"));
 DelayCommand(14.8,WriteTimestampedLogEntry("VISUAL|hover|0.70,0.50"));
 DelayCommand(15.2,WriteTimestampedLogEntry("VISUAL|rightclick|0.70,0.50"));
 DelayCommand(16.0,WriteTimestampedLogEntry("VISUAL|hover|0.622,0.50"));
 DelayCommand(18.0,VShot("e_ground_w_label"));
 DelayCommand(18.3,WriteTimestampedLogEntry("VISUAL|mouse|0.622,0.50|down"));
 DelayCommand(18.5,WriteTimestampedLogEntry("VISUAL|mouse|0.622,0.50|up"));
 DelayCommand(19.5,VShot("f_ground_w_menu"));
 DelayCommand(21.0,VDone());
}
