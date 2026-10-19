#include "parity_inc"
void State(object c,string key) {
 object right=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c);
 PLogI(key+".size",GetCreatureSize(c));
 PLogI(key+".equipped",GetIsObjectValid(right));
 PLog(key+".weapon",GetResRef(right));
 int swords=0;object it=GetFirstItemInInventory(c);
 while(GetIsObjectValid(it)) {if(GetBaseItemType(it)==BASE_ITEM_LONGSWORD)swords++;it=GetNextItemInInventory(c);}
 PLogI(key+".swords",swords);
 PLogI(key+".martial",GetHasFeat(FEAT_WEAPON_PROFICIENCY_MARTIAL,c));
 string slots="";int i;for(i=0;i<NUM_INVENTORY_SLOTS;i++){it=GetItemInSlot(i,c);if(GetIsObjectValid(it))slots+=IntToString(i)+":"+GetResRef(it)+" ";}
 PLog(key+".slots",slots);
 int carried=0;it=GetFirstItemInInventory(c);while(GetIsObjectValid(it)){carried++;it=GetNextItemInInventory(c);}
 PLogI(key+".carried",carried);
}
int CountTag(string tag) {int n=0;while(GetIsObjectValid(GetObjectByTag(tag,n)))n++;return n;}
void Probe(string name,float dx) {
 string itemtag="NW_WSWLS001";
 if(name=="eq_conflict")PLogI("conflict.items_before",CountTag(itemtag));
 object c=CreateObject(OBJECT_TYPE_CREATURE,name,PCentre(dx));
 SetLocalObject(GetModule(),name,c);
 if(name=="eq_conflict") {
  PLog("conflict.itemtag",itemtag);PLogI("conflict.items_after",CountTag(itemtag));
  object lost=GetObjectByTag(itemtag,CountTag(itemtag)-1);
  PLogI("conflict.displaced_owned",GetItemPossessor(lost)==c);
  PLogI("conflict.displaced_owner_valid",GetIsObjectValid(GetItemPossessor(lost)));
  PLogI("conflict.displaced_area_valid",GetIsObjectValid(GetArea(lost)));
 }
 State(c,name+".immediate");
 DelayCommand(0.1,State(c,name+".tick"));
 DelayCommand(2.0,State(c,name+".later"));
}
void main() {
 State(GetObjectByTag("placed_no"),"placed.untrained");
 State(GetObjectByTag("placed_yes"),"placed.trained");
 Probe("eq_conflict",1.0);
 Probe("eq_untrained",-3.0);Probe("eq_trained",3.0);
 Probe("eq_heavy_no",5.0);
 Probe("eq_heavy_yes",7.0);
 Probe("eq_cloth",9.0);
 Probe("eq_shield_no",11.0);
 Probe("eq_shield_yes",13.0);
 Probe("eq_badslot",15.0);
 Probe("eq_twohand",17.0);
 Probe("eq_both",19.0);
 Probe("eq_small",0.0);
 DelayCommand(3.0,PDone());
}
