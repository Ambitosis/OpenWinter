#include "visual_inc"
void Report(object pc,string phase){object i=GetLocalObject(pc,"item");vector p=GetPosition(pc);vector v=GetPosition(i);WriteTimestampedLogEntry("VISUAL|note|"+phase+"|owned="+IntToString(GetItemPossessor(i)==pc)+"|action="+IntToString(GetCurrentAction(pc))+"|pc="+FloatToString(p.x)+","+FloatToString(p.y)+"|item="+FloatToString(v.x)+","+FloatToString(v.y));}
void Setup(object pc){object i=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(23.0,15.0,0.0),0.0));SetLocalObject(pc,"item",i);SetName(i,"Ground pickup probe");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(.5,Setup(pc));DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,VShot("a_ground"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|click|0.754,0.66"));
 DelayCommand(4.7,Report(pc,"closed_click_during"));DelayCommand(4.7,VShot("b_pickup_pose"));
 DelayCommand(7.0,Report(pc,"closed_click_after"));DelayCommand(7.0,VShot("c_acquired"));
 DelayCommand(9.0,AssignCommand(pc,ActionPutDownItem(GetLocalObject(pc,"item"))));
 DelayCommand(9.5,VShot("d_drop_pose"));DelayCommand(11.0,Report(pc,"drop_after"));DelayCommand(11.0,VShot("e_dropped"));
 DelayCommand(13.0,VDone());
}
