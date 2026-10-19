#include "visual_inc"
void Report(object pc,string phase){object i=GetLocalObject(pc,"item");vector p=GetPosition(pc);vector v=GetPosition(i);WriteTimestampedLogEntry("VISUAL|note|"+phase+"|owned="+IntToString(GetItemPossessor(i)==pc)+"|action="+IntToString(GetCurrentAction(pc))+"|pc="+FloatToString(p.x)+","+FloatToString(p.y)+"|item="+FloatToString(v.x)+","+FloatToString(v.y));}
void Setup(object pc){object i=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(23.0,15.0,0.0),0.0));SetLocalObject(pc,"item",i);SetName(i,"Ground pickup probe");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(.5,Setup(pc));DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,VShot("a_inventory"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|click|0.754,0.66"));
 DelayCommand(6.0,Report(pc,"carry"));DelayCommand(6.0,VShot("b_carry"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|click|0.03,0.34"));
 DelayCommand(8.4,VShot("c_pickup"));DelayCommand(10.0,Report(pc,"placed"));DelayCommand(10.0,VShot("d_placed"));DelayCommand(12.0,VDone());
}
