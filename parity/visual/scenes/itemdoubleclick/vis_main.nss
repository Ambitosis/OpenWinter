#include "visual_inc"
void Report(object pc,string phase){object i=GetLocalObject(pc,"probe");WriteTimestampedLogEntry("VISUAL|note|"+phase+"|valid="+IntToString(GetIsObjectValid(i))+"|right="+IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)==i)+"|owned="+IntToString(GetItemPossessor(i)==pc));}
void Create(object pc);
void Reset(object pc){AssignCommand(pc,ClearAllActions(TRUE));DestroyObject(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc));object i=GetFirstItemInInventory(pc);while(GetIsObjectValid(i)){DestroyObject(i);i=GetNextItemInInventory(pc);}DelayCommand(.2,Create(pc));}
void Create(object pc){object i=CreateItemOnObject("nw_wblcl001",pc,1,"click_probe");SetLocalObject(pc,"probe",i);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(.5,Reset(pc));DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(2.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(3.0,VShot("baseline"));
 DelayCommand(4.0,Report(pc,"fast100.before"));DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|click-pair|fast100|20,365|20,365|100"));DelayCommand(6.0,Report(pc,"fast100.after"));
 DelayCommand(6.2,VShot("fast100"));
 DelayCommand(7.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(7.7,Reset(pc));DelayCommand(8.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(9.0,Report(pc,"time250.before"));DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|click-pair|time250|20,365|20,365|250"));DelayCommand(11.0,Report(pc,"time250.after"));
 DelayCommand(12.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(12.7,Reset(pc));DelayCommand(13.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(14.0,Report(pc,"time400.before"));DelayCommand(14.0,WriteTimestampedLogEntry("VISUAL|click-pair|time400|20,365|20,365|400"));DelayCommand(16.0,Report(pc,"time400.after"));
 DelayCommand(17.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(17.7,Reset(pc));DelayCommand(18.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(19.0,Report(pc,"time500.before"));DelayCommand(19.0,WriteTimestampedLogEntry("VISUAL|click-pair|time500|20,365|20,365|500"));DelayCommand(21.0,Report(pc,"time500.after"));
 DelayCommand(22.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(22.7,Reset(pc));DelayCommand(23.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(24.0,Report(pc,"slow650.before"));DelayCommand(24.0,WriteTimestampedLogEntry("VISUAL|click-pair|slow650|20,365|20,365|650"));DelayCommand(26.0,Report(pc,"slow650.after"));
 DelayCommand(26.2,VShot("slow650"));
 DelayCommand(27.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(27.7,Reset(pc));DelayCommand(28.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(29.0,Report(pc,"move8.before"));DelayCommand(29.0,WriteTimestampedLogEntry("VISUAL|click-pair|move8|20,365|20,373|150"));DelayCommand(31.0,Report(pc,"move8.after"));
 DelayCommand(32.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(32.7,Reset(pc));DelayCommand(33.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(34.0,Report(pc,"move16.before"));DelayCommand(34.0,WriteTimestampedLogEntry("VISUAL|click-pair|move16|20,365|20,381|150"));DelayCommand(36.0,Report(pc,"move16.after"));
 DelayCommand(37.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(37.7,Reset(pc));DelayCommand(38.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(39.0,Report(pc,"move32.before"));DelayCommand(39.0,WriteTimestampedLogEntry("VISUAL|click-pair|move32|20,365|20,397|150"));DelayCommand(41.0,Report(pc,"move32.after"));
 DelayCommand(42.5,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(42.7,Reset(pc));DelayCommand(43.4,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(44.0,Report(pc,"awayback.before"));DelayCommand(44.0,WriteTimestampedLogEntry("VISUAL|click-pair|awayback|20,365|20,365|250|120,365"));DelayCommand(46.0,Report(pc,"awayback.after"));
 DelayCommand(46.2,VShot("awayback"));
 DelayCommand(50.0,VDone());
}
