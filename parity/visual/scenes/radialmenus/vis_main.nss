#include "visual_inc"
void Npc(object pc) {
 object npc=CreateObject(OBJECT_TYPE_CREATURE,"nw_dwarfmerc005",Location(GetArea(pc),GetPosition(pc)+Vector(3.0,0.0,0.0),180.0));
 ChangeFaction(npc,pc); SetName(npc,"Radial Reference"); SetTag(npc,"UI_RADIAL_NPC");
 SetCommandable(FALSE,npc);
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|key|105|tap"));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|key|97|tap"));
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(13.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(15.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(16.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(17.0,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(19.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(20.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(21.0,WriteTimestampedLogEntry("VISUAL|key|103|tap"));
 DelayCommand(23.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));
 DelayCommand(24.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(25.0,WriteTimestampedLogEntry("VISUAL|key|97|tap"));
 DelayCommand(27.0,WriteTimestampedLogEntry("VISUAL|key|27|tap"));

 DelayCommand(6.0,VShot("a_root_cast_hover"));
 DelayCommand(8.0,VShot("b_cast"));
 DelayCommand(10.0,VShot("c_wizard"));
 DelayCommand(14.0,VShot("d_combat"));
 DelayCommand(18.0,VShot("e_special"));
 DelayCommand(22.0,VShot("f_emotes"));
 DelayCommand(26.0,VShot("g_chat"));
 DelayCommand(29.0,VShot("h_npc"));
 DelayCommand(31.0,VShot("i_world_rightclick"));
 DelayCommand(28.0,Npc(pc));
 DelayCommand(30.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.5,0.49"));
 DelayCommand(32.0,VDone());
}
