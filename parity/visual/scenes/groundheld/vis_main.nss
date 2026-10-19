#include "visual_inc"
void Observe(object pc,string key){vector v=GetPosition(pc);WriteTimestampedLogEntry("VISUAL|note|"+key+"|action="+IntToString(GetCurrentAction(pc))+"|x="+FloatToString(v.x)+"|y="+FloatToString(v.y));}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(2.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(3.0,VShot("self_control"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|mouse|0.70,0.50|down|right"));
 DelayCommand(5.05,VShot("held_5_05"));
 DelayCommand(5.2,VShot("held_5_2"));
 DelayCommand(5.5,VShot("held_5_5"));
 DelayCommand(6.0,VShot("held_6_0"));
 DelayCommand(6.5,VShot("held_6_5"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(8.0,VShot("se_while_held"));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|key|102|tap"));
 DelayCommand(10.1,Observe(pc,"after_e"));
 DelayCommand(10.3,Observe(pc,"move_10.3"));
 DelayCommand(10.8,Observe(pc,"move_10.8"));
 DelayCommand(11.3,Observe(pc,"move_11.3"));
 DelayCommand(11.8,Observe(pc,"move_11.8"));
 DelayCommand(11.0,VShot("e_while_held"));
 DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|mouse|0.70,0.50|up|right"));
 DelayCommand(14.0,VShot("after_release"));
 DelayCommand(16.0,VDone());
}
