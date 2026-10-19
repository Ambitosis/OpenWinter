#include "visual_inc"
void Note(string phase) {
 object pc=GetFirstPC(); object n=GetObjectByTag("probe_npc");
 WriteTimestampedLogEntry("VISUAL|note|loginconvo|"+phase+"|pcArea="+IntToString(GetIsObjectValid(GetArea(pc)))+"|npcTalking="+IntToString(IsInConversation(n))+"|pcTalking="+IntToString(IsInConversation(pc)));
 VShot(phase);
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 WriteTimestampedLogEntry("VISUAL|note|loginconvo|areaenter|npcTalking="+IntToString(IsInConversation(GetObjectByTag("probe_npc"))));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,50.0));DelayCommand(3.0,Note("a_after_load"));DelayCommand(8.0,Note("b_later"));
 // Control: the same dialog started after arrival, then the player jumps away, so the abort script hook is known to work.
 object n=GetObjectByTag("probe_npc");
 DelayCommand(9.0,AssignCommand(n,ActionStartConversation(pc,"lc_talk",FALSE,FALSE)));DelayCommand(11.0,Note("c_control_talking"));
 DelayCommand(12.0,AssignCommand(pc,JumpToLocation(Location(GetArea(pc),Vector(30.0,30.0,0.0),0.0))));DelayCommand(15.0,Note("d_control_jumped"));
 DelayCommand(17.0,VDone());
}
