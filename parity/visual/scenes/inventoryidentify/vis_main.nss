#include "visual_inc"
void Spawn(object pc){CreateItemOnObject("nw_aarcl002",pc);}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object it=GetFirstItemInInventory(pc);
 while(GetIsObjectValid(it)) {DestroyObject(it);it=GetNextItemInInventory(pc);}
 DelayCommand(0.2,Spawn(pc));
 DelayCommand(2.0,AssignCommand(pc,PlaySound("it_genericmedium")));
 DelayCommand(2.0,WriteTimestampedLogEntry("VISUAL|note|control"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|click|0.025,0.535"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|note|pickup_bottom"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|click|0.120,0.535"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|note|placement"));
 DelayCommand(9.0,ExportSingleCharacter(pc));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|click|0.120,0.535"));
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|click|0.420,0.480"));
 DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|note|possessor="+IntToString(GetItemPossessor(GetFirstItemInInventory(pc))==pc)));
 DelayCommand(13.0,VShot("final"));
 DelayCommand(14.0,VDone());
}
