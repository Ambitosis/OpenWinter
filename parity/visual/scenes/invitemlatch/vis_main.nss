#include "visual_inc"
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.1953,0.556"));
 DelayCommand(5.0,VShot("a_quick_release"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.1953,0.556"));
 DelayCommand(7.0,VShot("b_second_click"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|mouse|0.1953,0.556|down|right"));
 DelayCommand(9.6,VShot("c_held"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|mouse|0.1953,0.556|up|right"));
 DelayCommand(11.0,VShot("d_held_center_release"));
 DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
 DelayCommand(13.0,WriteTimestampedLogEntry("VISUAL|mouse|0.1953,0.556|down|right"));
 DelayCommand(13.1,WriteTimestampedLogEntry("VISUAL|mouse|0.1953,0.556|up|right"));
 DelayCommand(14.0,VShot("e_short_center_release"));
 DelayCommand(15.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.1953,0.556"));
 DelayCommand(16.0,VShot("f_short_second_click"));
 DelayCommand(18.0,VDone());
}
