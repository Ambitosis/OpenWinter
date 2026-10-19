#include "visual_inc"
void Cast(object pc, int spell) {
 AssignCommand(pc,ActionCastSpellAtObject(spell,pc,METAMAGIC_NONE,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE));
}
void Report(object pc, string phase) {
 object item = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc);
 int props = 0; itemproperty ip = GetFirstItemProperty(item);
 while(GetIsItemPropertyValid(ip)) { props++; ip=GetNextItemProperty(item); }
 WriteTimestampedLogEntry("UISTATS|"+phase+"|ac="+IntToString(GetAC(pc))+"|weaponProps="+IntToString(props));
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 object weapon=CreateItemOnObject("nw_wswdg001",pc);
 AssignCommand(pc,ActionEquipItem(weapon,INVENTORY_SLOT_RIGHTHAND));
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|67|tap"));
 DelayCommand(6.0,VShot("a_before")); DelayCommand(6.0,Report(pc,"before"));
 DelayCommand(8.0,Cast(pc,SPELL_MAGIC_WEAPON));
 DelayCommand(13.0,VShot("b_magic_weapon")); DelayCommand(13.0,Report(pc,"magic_weapon"));
 DelayCommand(15.0,Cast(pc,SPELL_SHIELD_OF_FAITH));
 DelayCommand(20.0,VShot("c_shield_of_faith")); DelayCommand(20.0,Report(pc,"shield_of_faith"));
 DelayCommand(22.0,VDone());
}
