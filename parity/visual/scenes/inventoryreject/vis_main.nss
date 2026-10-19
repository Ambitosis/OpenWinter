#include "visual_inc"
void Spawn(object pc) {
 object armor=CreateItemOnObject("nw_aarcl007",pc);
 SetLocalObject(pc,"reject_armor",armor);
}
void State(object pc,string phase) {
 object armor=GetLocalObject(pc,"reject_armor");
 WriteTimestampedLogEntry("VISUAL|note|reject|"+phase+"|owned="+IntToString(GetItemPossessor(armor)==pc)
  +"|equipped="+IntToString(GetItemInSlot(INVENTORY_SLOT_CHEST,pc)==armor)
  +"|area="+IntToString(GetIsObjectValid(GetArea(armor)))+"|chest="+GetResRef(GetItemInSlot(INVENTORY_SLOT_CHEST,pc))
  +"|heavy="+IntToString(GetHasFeat(FEAT_ARMOR_PROFICIENCY_HEAVY,pc)));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object it=GetFirstItemInInventory(pc);
 while(GetIsObjectValid(it)) {DestroyObject(it);it=GetNextItemInInventory(pc);}
 DelayCommand(0.2,Spawn(pc));
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,VShot("a_ready"));
 DelayCommand(4.6,WriteTimestampedLogEntry("VISUAL|hover|0.025,0.535"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|click|0.025,0.535"));
 DelayCommand(6.6,WriteTimestampedLogEntry("VISUAL|hover|0.095,0.105"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|click|0.095,0.105"));
 DelayCommand(9.0,State(pc,"after_equip"));
 DelayCommand(9.0,VShot("a_rejected"));
 DelayCommand(9.6,WriteTimestampedLogEntry("VISUAL|hover|0.420,0.480"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|click|0.420,0.480"));
 DelayCommand(14.0,State(pc,"after_terrain"));
 DelayCommand(14.0,VShot("b_terrain"));
 DelayCommand(15.0,VDone());
}
