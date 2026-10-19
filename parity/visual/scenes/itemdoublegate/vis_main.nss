#include "visual_inc"
void Report(object pc,string phase){object i=GetLocalObject(pc,"probe");WriteTimestampedLogEntry("VISUAL|note|"+phase+"|valid="+IntToString(GetIsObjectValid(i))+"|right="+IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)==i)+"|owned="+IntToString(GetItemPossessor(i)==pc)+"|simple="+IntToString(GetHasFeat(FEAT_WEAPON_PROFICIENCY_SIMPLE,pc))+"|name="+GetName(pc));}
void EquipControl(object pc){AssignCommand(pc,ActionEquipItem(GetLocalObject(pc,"probe"),INVENTORY_SLOT_RIGHTHAND));}
void Create(object pc);
void Reset(object pc){AssignCommand(pc,ClearAllActions(TRUE));DestroyObject(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc));object i=GetFirstItemInInventory(pc);while(GetIsObjectValid(i)){DestroyObject(i);i=GetNextItemInInventory(pc);}DelayCommand(.2,Create(pc));}
void Create(object pc){object i=CreateItemOnObject("nw_wblcl001",pc,1,"click_probe");SetLocalObject(pc,"probe",i);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
 DelayCommand(.5,Reset(pc));DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(2.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(3.0,VShot("baseline"));
 DelayCommand(4.0,Report(pc,"fast100.before"));DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|click-pair|fast100|20,365|20,365|100"));DelayCommand(6.0,Report(pc,"fast100.after"));
 DelayCommand(6.2,VShot("fast100"));
 // Verify equip eligibility after the input probe so the control cannot relocate its backpack cell first.
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|click-pair-dblclk|explicit_dblclk|20,365|20,365|100"));
 DelayCommand(10.0,Report(pc,"explicit_dblclk.after"));DelayCommand(10.2,VShot("explicit_dblclk"));
 DelayCommand(12.0,EquipControl(pc));DelayCommand(14.0,Report(pc,"script_equip_positive"));
 DelayCommand(15.0,VDone());
}
