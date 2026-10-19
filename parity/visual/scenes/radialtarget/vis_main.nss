#include "visual_inc"
void Setup(object pc) {
 object npc=CreateObject(OBJECT_TYPE_CREATURE,"nw_dwarfmerc005",Location(GetArea(pc),GetPosition(pc)+Vector(3.0,0.0,0.0),180.0));
 ChangeFaction(npc,pc); SetName(npc,"Radial Reference"); SetTag(npc,"UI_RADIAL_NPC");SetCommandable(FALSE,npc);
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(1.0,Setup(pc)); DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,VShot("a_world"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.754,0.525"));
 DelayCommand(5.0,VShot("b_target_root"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|key|105|tap"));
 DelayCommand(7.0,VShot("c_target_cast"));
 DelayCommand(8.0,VDone());
}
