#include "visual_inc"
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|key|105|tap"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(7.0,VShot("a_spell_abilities"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|keyshot|104|b_craft|0.8"));
 DelayCommand(13.0,VShot("c_craft_later"));
 DelayCommand(14.0,VDone());
}
