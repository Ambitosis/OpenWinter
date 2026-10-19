#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Equip(object pc){object w=GetItemPossessedBy(pc,"NW_WSWDG001");Note("dagger."+IntToString(GetIsObjectValid(w)));AssignCommand(pc,ActionEquipItem(w,INVENTORY_SLOT_RIGHTHAND));}
void Use(object pc){object p=GetLocalObject(pc,"pot");Note("use.pc."+IntToString(GetIsPC(pc))+".stack."+IntToString(GetItemStackSize(p))+".weapon."+IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc))));AssignCommand(pc,ActionUseItemOnObject(p,GetFirstItemProperty(p),pc));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);object p=GetFirstItemInInventory(pc);while(GetIsObjectValid(p)&&(GetBaseItemType(p)!=49||GetItemPropertySubType(GetFirstItemProperty(p))!=66))p=GetNextItemInInventory(pc);SetItemStackSize(p,3);SetLocalObject(pc,"pot",p);DelayCommand(.5,Equip(pc));DelayCommand(1.0,VCamera(pc,90.0,4.5,65.0));DelayCommand(2.8,VShot("equipped"));DelayCommand(4.0,Use(pc));DelayCommand(9.0,AssignCommand(pc,PlayAnimation(ANIMATION_FIREFORGET_DRINK)));
DelayCommand(4.15,VShot("drink0"));
DelayCommand(4.65,VShot("drink1"));
DelayCommand(5.10,VShot("drink2"));
DelayCommand(5.35,VShot("drink3"));
DelayCommand(5.65,VShot("drink4"));
DelayCommand(5.90,VShot("drink5"));
DelayCommand(6.30,VShot("drink6"));
DelayCommand(6.70,VShot("drink7"));
DelayCommand(9.7,VShot("gesture"));DelayCommand(11.0,VShot("restored"));DelayCommand(12.0,VDone());}
