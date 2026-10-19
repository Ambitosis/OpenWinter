#include "visual_inc"
void Equip(object c,string res) { object i=CreateItemOnObject(res,c); AssignCommand(c,ActionEquipItem(i,INVENTORY_SLOT_HEAD)); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 object a=VSpawn(pc,"nw_bandit001",-1.0,1.0,270.0);
 object b=VSpawn(pc,"nw_bandit001",1.0,1.0,270.0);
 SetCreatureAppearanceType(a,APPEARANCE_TYPE_HUMAN);SetCreatureAppearanceType(b,APPEARANCE_TYPE_HUMAN);
 DelayCommand(1.0,VCamera(pc,90.0,5.0,80.0));
 DelayCommand(3.0,VShot("bare"));
 DelayCommand(5.0,Equip(a,"nw_arhe001"));DelayCommand(5.0,Equip(b,"nw_arhe002"));
 DelayCommand(8.0,VShot("front"));
 DelayCommand(10.0,VCamera(pc,180.0,5.0,80.0));
 DelayCommand(12.0,VShot("side"));DelayCommand(14.0,VDone());
}
