#include "visual_inc"
void Note(object o,string phase) {
 WriteTimestampedLogEntry("VISUAL|note|"+phase+"|valid="+IntToString(GetIsObjectValid(o))+"|kind="+IntToString(GetObjectType(o))+"|resref="+GetResRef(o)+"|locked="+IntToString(GetLocked(o))+"|inventory="+IntToString(GetHasInventory(o))+"|usable="+IntToString(GetUseableFlag(o))+"|trapped="+IntToString(GetIsTrapped(o)));
}
void Setup(object pc) {
 object chest=VPlaceable(pc,"plc_chest1",3.0,0.0);SetTag(chest,"RO_CHEST");SetName(chest,"Radial Chest");SetLocked(chest,FALSE);
 CreateItemOnObject("nw_it_mpotion001",chest);Note(chest,"chest");
 object door=GetObjectByTag("RO_DOOR");SetName(door,"Radial Door");SetLocked(door,FALSE);Note(door,"door");
}
void Lock() { object o=GetObjectByTag("RO_CHEST");SetLocked(o,TRUE);Note(o,"locked"); }
void Trap(object pc) { object o=GetObjectByTag("RO_CHEST");CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE,o);SetTrapDetectedBy(o,pc,TRUE);Note(o,"trap"); }
void Item(object pc) { DestroyObject(GetObjectByTag("RO_CHEST"));object o=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),GetPosition(pc)+Vector(3.0,0.0,0.0),0.0));SetName(o,"Radial Sword");SetTag(o,"RO_SWORD");Note(o,"item"); }
void Table(object pc) { DestroyObject(GetObjectByTag("RO_SWORD"));object o=VPlaceable(pc,"plc_table",3.0,0.0);SetUseableFlag(o,TRUE);SetName(o,"Radial Table");Note(o,"table"); }
void Root(float t,string point,string name) {
 DelayCommand(t,WriteTimestampedLogEntry("VISUAL|hover|"+point));
 DelayCommand(t+0.3,WriteTimestampedLogEntry("VISUAL|mouse|"+point+"|down|right"));
 DelayCommand(t+1.7,WriteTimestampedLogEntry("VISUAL|mouse|"+point+"|up|right"));
 DelayCommand(t+1.3,VShot(name));
 DelayCommand(t+2.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(1.0,Setup(pc));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,VShot("a_world"));Root(4.0,"0.754,0.63","b_chest");
 DelayCommand(7.0,Lock());Root(8.0,"0.754,0.63","c_locked");
 DelayCommand(11.0,Trap(pc));Root(12.0,"0.754,0.63","d_trapped");
 DelayCommand(15.0,Item(pc));DelayCommand(16.0,VShot("e_itemworld"));Root(17.0,"0.754,0.66","f_item");
 Root(22.0,"0.246,0.50","g_door");
 DelayCommand(26.0,Table(pc));Root(28.0,"0.754,0.59","h_usable");
 DelayCommand(32.0,VDone());
}
