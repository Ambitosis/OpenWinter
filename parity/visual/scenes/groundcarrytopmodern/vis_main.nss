// groundcarrytop for the modern interface, whose inventory window covers the middle and right of the screen:
// the item lies north-west of the player, left of the window and above the chat; the carried icon is shot over the world, then over the
// inventory window and over its backpack grid (docs/GROUND_ITEMS.md).
#include "visual_inc"
void Setup(object pc){object i=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(13.5,18.5,0.0),0.0));SetLocalObject(pc,"item",i);SetName(i,"Ground pickup probe");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(.5,Setup(pc));DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,VShot("a_inventory"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|click|0.11,0.66||-6.5,3.5"));
 DelayCommand(6.0,VShot("b_carry_world"));
 DelayCommand(6.5,WriteTimestampedLogEntry("VISUAL|hover|0.35,0.45"));
 DelayCommand(7.5,VShot("c_carry_paperdoll"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|hover|0.70,0.50"));
 DelayCommand(9.0,VShot("d_carry_backpack"));
 DelayCommand(9.5,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(11.0,VDone());
}
