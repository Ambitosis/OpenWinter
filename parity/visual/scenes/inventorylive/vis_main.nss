#include "visual_inc"
void Observe(object pc, object potion, object arrows, string phase) {
 WriteTimestampedLogEntry("VISUAL|note|inventory|"+phase+"|potion="+IntToString(GetItemPossessor(potion)==pc)+"|arrows="+IntToString(GetItemPossessor(arrows)==pc));
}
void Setup(object pc) {
 object chest=VPlaceable(pc,"plc_chest1",1.5,0.0);
 SetTag(chest,"live_chest");SetLocked(chest,FALSE);
 object p=CreateItemOnObject("nw_it_mpotion001",chest,1,"live_potion");SetName(p,"Live potion");
 object a=CreateItemOnObject("nw_wamar001",chest,12,"live_arrows");SetName(a,"Live arrows");
 DelayCommand(2.0,AssignCommand(pc,ActionInteractObject(chest)));
 DelayCommand(5.0,VShot("a_container"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|mouse|0.016,0.235|down"));
 DelayCommand(6.4,WriteTimestampedLogEntry("VISUAL|mouse|0.016,0.235|up"));
 DelayCommand(7.0,VShot("a2_click_potion"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|mouse|0.042,0.235|down"));
 DelayCommand(8.2,WriteTimestampedLogEntry("VISUAL|hover|0.197,0.51"));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|mouse|0.197,0.51|up"));
 DelayCommand(10.0,VShot("a3_drag_arrows"));
 DelayCommand(12.0,Observe(pc,p,a,"after_inputs"));
 DelayCommand(20.0,DestroyObject(p));
 DelayCommand(21.0,VShot("b_deleted_potion"));
 DelayCommand(28.0,DestroyObject(a));
 DelayCommand(29.0,VShot("c_deleted_arrows"));
 DelayCommand(32.0,VDone());
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object i=GetFirstItemInInventory(pc);
 while(GetIsObjectValid(i)){DestroyObject(i);i=GetNextItemInInventory(pc);}
 DelayCommand(0.5,Setup(pc));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
}
