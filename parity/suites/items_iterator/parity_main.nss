#include "parity_inc"
string Contents(object c) {
 string result="";object it=GetFirstItemInInventory(c);int limit=0;
 while(GetIsObjectValid(it)&&limit<40){result+=GetTag(it)+" ";it=GetNextItemInInventory(c);limit++;}
 return result;
}
void Clean(object c) {
 int slot;for(slot=0;slot<NUM_INVENTORY_SLOTS;slot++)DestroyObject(GetItemInSlot(slot,c));
 object it=GetFirstItemInInventory(c);while(GetIsObjectValid(it)){DestroyObject(it);it=GetNextItemInInventory(c);}
}
void Give(object c) {
 CreateItemOnObject("nw_cloth001",c,1,"ROBE");
 CreateItemOnObject("nw_wswdg001",c,1,"DAGGER");
 CreateItemOnObject("nw_it_mring001",c,1,"RING");
 CreateItemOnObject("nw_it_mpotion001",c,1,"POTION");
 object bag=CreateItemOnObject("nw_it_contain001",c,1,"BAG");
 CreateItemOnObject("nw_wamar001",bag,10,"ARROWS");
 PLog("before.order",Contents(c));
}
void Equip(object c,string tag,int slot){AssignCommand(c,ActionEquipItem(GetObjectByTag(tag),slot));}
void Report(object c) {
 PLog("equipped.chest",GetTag(GetItemInSlot(INVENTORY_SLOT_CHEST,c)));
 PLog("equipped.hand",GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c)));
 PLog("equipped.ring",GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTRING,c)));
 PLog("equipped.order",Contents(c));
 object it=GetFirstItemInInventory(c);string destroyed="";int limit=0;
 while(GetIsObjectValid(it)&&limit<40){destroyed+=GetTag(it)+" ";DestroyObject(it);it=GetNextItemInInventory(c);limit++;}
 PLog("destroyed.order",destroyed);
}
void Final(object c) {
 PLog("after.order",Contents(c));
 PLog("after.chest",GetTag(GetItemInSlot(INVENTORY_SLOT_CHEST,c)));
 PLog("after.hand",GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c)));
 PLog("after.ring",GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTRING,c)));
 PDone();
}
void main() {
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_hen_sha_09",PCentre(),FALSE,"ITERATOR");
 SetAILevel(c,AI_LEVEL_NORMAL);AssignCommand(c,ClearAllActions());
 DelayCommand(0.3,Clean(c));DelayCommand(0.6,Give(c));
 DelayCommand(0.9,Equip(c,"ROBE",INVENTORY_SLOT_CHEST));
 DelayCommand(1.3,Equip(c,"DAGGER",INVENTORY_SLOT_RIGHTHAND));
 DelayCommand(1.7,Equip(c,"RING",INVENTORY_SLOT_RIGHTRING));
 DelayCommand(3.0,Report(c));DelayCommand(3.4,Final(c));
}
