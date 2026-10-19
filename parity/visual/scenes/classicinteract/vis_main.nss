#include "visual_inc"
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object item = GetFirstItemInInventory(pc);
 while(GetIsObjectValid(item)) { DestroyObject(item); item = GetNextItemInInventory(pc); }
 CreateItemOnObject("nw_aarcl001",pc); CreateItemOnObject("nw_aarcl004",pc); CreateItemOnObject("nw_aarcl009",pc);
 GiveGoldToCreature(pc,123);
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(6.0,VShot("a_armor"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|click|0.255,0.615"));
 DelayCommand(10.0,VShot("b_gold"));
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|click|0.55,0.608"));
 DelayCommand(13.0,WriteTimestampedLogEntry("VISUAL|click|0.025,0.615"));
 DelayCommand(15.0,VShot("c_examine"));
 DelayCommand(17.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(20.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(22.0,VShot("d_radial"));
 DelayCommand(24.0,WriteTimestampedLogEntry("VISUAL|key|104|tap"));
 DelayCommand(26.0,VShot("e_submenu"));
 DelayCommand(28.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(30.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(32.0,WriteTimestampedLogEntry("VISUAL|click|0.255,0.615"));
 DelayCommand(34.0,WriteTimestampedLogEntry("VISUAL|key|53|tap"));
 DelayCommand(35.0,WriteTimestampedLogEntry("VISUAL|key|48|tap"));
 DelayCommand(36.0,WriteTimestampedLogEntry("VISUAL|click|0.45,0.608"));
 DelayCommand(38.0,VShot("f_gold_accepted"));
 DelayCommand(40.0,WriteTimestampedLogEntry("VISUAL|click|0.6,0.6"));
 DelayCommand(43.0,VShot("g_gold_ground"));
 DelayCommand(45.0,VDone());
}
