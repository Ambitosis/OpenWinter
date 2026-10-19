#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Use(object pc,int equipped){object p=GetLocalObject(pc,"pot");Note("use."+IntToString(equipped)+".pc."+IntToString(GetIsPC(pc))+".stack."+IntToString(GetItemStackSize(p))+".weapon."+IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc))));AssignCommand(pc,ActionUseItemOnObject(p,GetFirstItemProperty(p),pc));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);object p=GetFirstItemInInventory(pc);while(GetIsObjectValid(p)&&(GetBaseItemType(p)!=49||GetItemPropertySubType(GetFirstItemProperty(p))!=66))p=GetNextItemInInventory(pc);SetItemStackSize(p,3);SetLocalObject(pc,"pot",p);Note("ready."+IntToString(GetBaseItemType(p))+".stack."+IntToString(GetItemStackSize(p)));DelayCommand(1.0,VCamera(pc,90.0,4.5,65.0));DelayCommand(3.0,AssignCommand(pc,PlayAnimation(ANIMATION_FIREFORGET_DRINK)));DelayCommand(7.0,Use(pc,0));
DelayCommand(3.15,VShot("gesture0"));
DelayCommand(3.65,VShot("gesture1"));
DelayCommand(4.10,VShot("gesture2"));
DelayCommand(4.35,VShot("gesture3"));
DelayCommand(4.65,VShot("gesture4"));
DelayCommand(4.90,VShot("gesture5"));
DelayCommand(5.30,VShot("gesture6"));
DelayCommand(5.70,VShot("gesture7"));
DelayCommand(7.15,VShot("bare0"));
DelayCommand(7.65,VShot("bare1"));
DelayCommand(8.10,VShot("bare2"));
DelayCommand(8.35,VShot("bare3"));
DelayCommand(8.65,VShot("bare4"));
DelayCommand(8.90,VShot("bare5"));
DelayCommand(9.30,VShot("bare6"));
DelayCommand(9.70,VShot("bare7"));
DelayCommand(12.0,VDone());}
