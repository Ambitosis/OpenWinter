// A ground item picked up with the inventory open rides the pointer above every window: the carried icon is
// shot over the world, then over the inventory window and over the chat window (docs/GROUND_ITEMS.md).
#include "visual_inc"
void Setup(object pc){object i=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(23.0,15.0,0.0),0.0));SetLocalObject(pc,"item",i);SetName(i,"Ground pickup probe");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(.5,Setup(pc));DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,VShot("a_inventory"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|click|0.754,0.66"));
 DelayCommand(6.0,VShot("b_carry_world"));
 DelayCommand(6.5,WriteTimestampedLogEntry("VISUAL|hover|0.13,0.45"));
 DelayCommand(7.5,VShot("c_carry_inventory"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|hover|0.30,0.88"));
 DelayCommand(9.0,VShot("d_carry_chat"));
 DelayCommand(9.5,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(11.0,VDone());
}
